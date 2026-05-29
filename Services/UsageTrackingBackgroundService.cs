using Microsoft.EntityFrameworkCore;
using TTLockManager.Data;
using TTLockManager.Models;

namespace TTLockManager.Services
{
    /// <summary>
    /// Background service chạy mỗi 5 phút để:
    ///  1. Polling lock records từ TTLock API
    ///  2. Đếm số lần giặt mới
    ///  3. Tự động xóa credential khi hết hạn mức (UC-15)
    ///  4. Reset + khôi phục credential đầu tháng mới (UC-16)
    /// </summary>
    public class UsageTrackingBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITTLockApiClient _ttlock;
        private readonly IConfiguration _config;
        private readonly ILogger<UsageTrackingBackgroundService> _logger;

        // Lưu timestamp poll lần cuối để tránh đếm trùng
        private long _lastPollTimestamp;
        private int _lastResetMonth = -1;

        public UsageTrackingBackgroundService(
            IServiceScopeFactory scopeFactory,
            ITTLockApiClient ttlock,
            IConfiguration config,
            ILogger<UsageTrackingBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _ttlock = ttlock;
            _config = config;
            _logger = logger;
            _lastPollTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeMilliseconds();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("UsageTrackingService: Khởi động");

            // Đăng nhập TTLock lần đầu
            await _ttlock.AuthenticateAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;

                    // UC-16: Reset đầu tháng mới
                    if (now.Day == 1 && now.Month != _lastResetMonth)
                    {
                        await ResetMonthlyQuotaAsync();
                        _lastResetMonth = now.Month;
                    }

                    // UC-14: Poll records mới
                    await PollNewRecordsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "UsageTrackingService: Lỗi trong vòng lặp");
                }

                // Chờ 5 phút rồi poll tiếp
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        // ── UC-14: Polling & đếm lần giặt ───────────────────────────────

        private async Task PollNewRecordsAsync()
        {
            var lockId = long.Parse(_config["TTLock:LockId"]!);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var result = await _ttlock.GetLockRecordsAsync(lockId, _lastPollTimestamp, now);
            if (result.errcode != 0)
            {
                _logger.LogWarning("TTLock GetRecords lỗi: {Msg}", result.errmsg);
                return;
            }

            if (result.list.Count == 0)
            {
                _lastPollTimestamp = now;
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Chỉ xử lý: 4=PIN, 7=Thẻ, 8=Vân tay
            var validTypes = new[] { 4, 7, 8 };

            foreach (var record in result.list.Where(r => validTypes.Contains(r.recordType)))
            {
                // Tránh đếm trùng
                if (await db.WashRecords.AnyAsync(r => r.TTLockRecordId == record.recordId))
                    continue;

                // Tìm khách qua credential mapping
                var credential = await db.CustomerCredentials
                    .Include(c => c.Customer)
                    .FirstOrDefaultAsync(c => c.CredentialId == record.keyboardPwd && c.IsActiveOnLock);

                if (credential == null)
                {
                    _logger.LogWarning("Không tìm thấy khách cho credential: {Pwd}", record.keyboardPwd);
                    continue;
                }

                var openedAt = DateTimeOffset.FromUnixTimeMilliseconds(record.lockDate).LocalDateTime;

                // Lưu WashRecord
                db.WashRecords.Add(new WashRecord
                {
                    CustomerId = credential.CustomerId,
                    RecordType = record.recordType,
                    KeyboardPwd = record.keyboardPwd,
                    OpenedAt = openedAt,
                    Month = openedAt.Month,
                    Year = openedAt.Year,
                    TTLockRecordId = record.recordId
                });

                // Cập nhật MonthlyUsage
                var usage = await GetOrCreateUsageAsync(db, credential.CustomerId, openedAt.Month, openedAt.Year);
                usage.UsedCount++;

                await db.SaveChangesAsync();
                _logger.LogInformation("Ghi nhận lần giặt: Khách #{CId} – Lần {Used}/{Quota}",
                    credential.CustomerId, usage.UsedCount, usage.Quota);

                // UC-15: Kiểm tra và tự động xóa credential nếu hết lần
                if (usage.IsExhausted)
                    await FreezeCustomerAsync(db, credential.Customer, lockId);
            }

            _lastPollTimestamp = now;
        }

        // ── UC-15: Freeze (xóa credential) khi hết hạn mức ────────────

        private async Task FreezeCustomerAsync(AppDbContext db, Customer customer, long lockId)
        {
            _logger.LogInformation("Freeze khách #{Id} ({Name}) – hết hạn mức", customer.Id, customer.Name);

            var credentials = await db.CustomerCredentials
                .Where(c => c.CustomerId == customer.Id && c.IsActiveOnLock)
                .ToListAsync();

            foreach (var cred in credentials)
            {
                TTLockBaseResponse resp;
                switch (cred.Type)
                {
                    case Models.CredentialType.Card:
                        resp = await _ttlock.DeleteCardAsync(lockId, cred.CredentialId);
                        break;
                    case Models.CredentialType.Fingerprint:
                        resp = await _ttlock.DeleteFingerprintAsync(lockId, cred.CredentialId);
                        break;
                    default: // Pin
                        resp = await _ttlock.DeletePasscodeAsync(lockId, cred.CredentialId);
                        break;
                }

                if (resp.errcode == 0)
                {
                    cred.IsActiveOnLock = false;
                    _logger.LogInformation("Đã xóa {Type} của khách #{Id}", cred.Type, customer.Id);
                }
                else
                {
                    _logger.LogError("Xóa {Type} thất bại: {Msg}", cred.Type, resp.errmsg);
                }
            }

            customer.Status = CustomerStatus.Suspended;
            await db.SaveChangesAsync();
        }

        // ── UC-16: Reset đầu tháng ────────────────────────────────────────

        private async Task ResetMonthlyQuotaAsync()
        {
            _logger.LogInformation("UsageTrackingService: Reset hạn mức tháng mới");

            var lockId = long.Parse(_config["TTLock:LockId"]!);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.Now;
            var customers = await db.Customers
                .Include(c => c.Credentials)
                .Where(c => c.Status != CustomerStatus.Revoked)
                .ToListAsync();

            foreach (var customer in customers)
            {
                // Tạo MonthlyUsage mới cho tháng này
                var existingUsage = await db.MonthlyUsages
                    .FirstOrDefaultAsync(m => m.CustomerId == customer.Id && m.Month == now.Month && m.Year == now.Year);

                if (existingUsage == null)
                {
                    db.MonthlyUsages.Add(new MonthlyUsage
                    {
                        CustomerId = customer.Id,
                        Month = now.Month,
                        Year = now.Year,
                        Quota = customer.QuotaPerMonth,
                        UsedCount = 0
                    });
                }

                // Khôi phục credential cho khách đang bị Suspended
                if (customer.Status == CustomerStatus.Suspended)
                {
                    foreach (var cred in customer.Credentials.Where(c => !c.IsActiveOnLock))
                    {
                        var endDate = new DateTimeOffset(now.Year, now.Month,
                            DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59, TimeSpan.Zero)
                            .ToUnixTimeMilliseconds();
                        var startDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        bool success = false;
                        switch (cred.Type)
                        {
                            case Models.CredentialType.Card:
                                var cardResp = await _ttlock.AddCardAsync(lockId, $"Card_{customer.Name}", startDate, endDate);
                                if (cardResp.errcode == 0) { cred.CredentialId = cardResp.cardId; success = true; }
                                break;
                            case Models.CredentialType.Fingerprint:
                                var fpResp = await _ttlock.AddFingerprintAsync(lockId, customer.Name, startDate, endDate);
                                if (fpResp.errcode == 0) { cred.CredentialId = fpResp.fingerprintId; success = true; }
                                break;
                            case Models.CredentialType.Pin:
                                var pinResp = await _ttlock.AddPasscodeAsync(lockId, cred.CredentialId, customer.Name, startDate, endDate);
                                if (pinResp.errcode == 0) success = true;
                                break;
                        }

                        if (success) cred.IsActiveOnLock = true;
                    }

                    customer.Status = CustomerStatus.Active;
                    _logger.LogInformation("Khôi phục khách #{Id} ({Name}) cho tháng mới", customer.Id, customer.Name);
                }
            }

            await db.SaveChangesAsync();
        }

        // ── Helper ────────────────────────────────────────────────────────

        private static async Task<MonthlyUsage> GetOrCreateUsageAsync(AppDbContext db, int customerId, int month, int year)
        {
            var usage = await db.MonthlyUsages
                .FirstOrDefaultAsync(m => m.CustomerId == customerId && m.Month == month && m.Year == year);

            if (usage != null) return usage;

            var customer = await db.Customers.FindAsync(customerId);
            usage = new MonthlyUsage
            {
                CustomerId = customerId,
                Month = month,
                Year = year,
                Quota = customer?.QuotaPerMonth ?? 10,
                UsedCount = 0
            };
            db.MonthlyUsages.Add(usage);
            return usage;
        }
    }
}
