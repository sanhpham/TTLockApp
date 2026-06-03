using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TTLockManager.Data;
using TTLockManager.Models;

namespace TTLockManager.Services
{
    /// <summary>
    /// Background service chạy mỗi 5 phút để:
    ///  1. Tự động kiểm tra và refresh token cho các AppUser
    ///  2. Polling lock records từ TTLock API sử dụng token của từng user
    ///  3. Đếm số lần giặt mới và cập nhật hạn mức
    ///  4. Tự động xóa credential khi hết hạn mức (UC-15)
    ///  5. Reset + khôi phục credential đầu tháng mới (UC-16)
    /// </summary>
    public class UsageTrackingBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITTLockApiClient _ttlock;
        private readonly ILogger<UsageTrackingBackgroundService> _logger;

        // Lưu timestamp poll lần cuối cho từng ổ khóa của mỗi user
        private readonly Dictionary<(int userId, long lockId), long> _lastPolls = new();
        private int _lastResetMonth = -1;

        public UsageTrackingBackgroundService(
            IServiceScopeFactory scopeFactory,
            ITTLockApiClient ttlock,
            ILogger<UsageTrackingBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _ttlock = ttlock;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("UsageTrackingService: Khởi động");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var users = await db.AppUsers.ToListAsync(stoppingToken);
                    foreach (var user in users)
                    {
                        var token = await EnsureUserTokenAsync(db, user);
                        if (string.IsNullOrEmpty(token)) continue;

                        var userLocks = await db.DeviceLocks
                            .Where(l => l.AppUserId == user.Id)
                            .ToListAsync(stoppingToken);

                        foreach (var lockItem in userLocks)
                        {
                            // UC-16: Reset đầu tháng mới
                            if (now.Day == 1 && now.Month != _lastResetMonth)
                            {
                                await ResetUserMonthlyQuotaAsync(db, user, lockItem.LockId, token);
                            }

                            // UC-14: Poll records mới
                            await PollUserLockRecordsAsync(db, user, lockItem.LockId, token);
                        }
                    }

                    if (now.Day == 1 && now.Month != _lastResetMonth)
                    {
                        _lastResetMonth = now.Month;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "UsageTrackingService: Lỗi trong vòng lặp");
                }

                // Chờ 5 phút rồi poll tiếp
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        private async Task<string?> EnsureUserTokenAsync(AppDbContext db, AppUser user)
        {
            if (user.TokenExpiresAt > DateTime.Now.AddMinutes(10))
            {
                return user.AccessToken;
            }

            if (string.IsNullOrEmpty(user.RefreshToken))
            {
                _logger.LogWarning("User {Username} không có RefreshToken", user.TTLockUsername);
                return null;
            }

            _logger.LogInformation("Refreshing token cho user {Username}", user.TTLockUsername);
            var refreshResult = await _ttlock.RefreshUserTokenAsync(user.RefreshToken);
            if (refreshResult != null && refreshResult.errcode == 0)
            {
                user.AccessToken = refreshResult.access_token;
                user.RefreshToken = refreshResult.refresh_token;
                user.TokenExpiresAt = DateTime.Now.AddSeconds(refreshResult.expires_in);
                
                db.AppUsers.Update(user);
                await db.SaveChangesAsync();
                return user.AccessToken;
            }

            _logger.LogWarning("Refresh token cho user {Username} thất bại: {Msg}", user.TTLockUsername, refreshResult?.errmsg);
            return null;
        }

        // ── UC-14: Polling & đếm lần giặt ───────────────────────────────

        private async Task PollUserLockRecordsAsync(AppDbContext db, AppUser user, long lockId, string accessToken)
        {
            var key = (user.Id, lockId);
            if (!_lastPolls.TryGetValue(key, out var lastPoll))
            {
                lastPoll = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();
            }
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var result = await _ttlock.GetLockRecordsAsync(lockId, lastPoll, now, accessToken: accessToken);
            if (result.errcode != 0)
            {
                _logger.LogWarning("TTLock GetRecords lỗi cho lock {LockId}: {Msg}", lockId, result.errmsg);
                return;
            }

            if (result.list.Count == 0)
            {
                _lastPolls[key] = now;
                return;
            }

            // Chỉ xử lý: 4=PIN, 7=Thẻ, 8=Vân tay
            var validTypes = new[] { 4, 7, 8 };

            foreach (var record in result.list.Where(r => validTypes.Contains(r.recordType)))
            {
                // Tránh đếm trùng
                if (await db.WashRecords.AnyAsync(r => r.TTLockRecordId == record.recordId))
                    continue;

                // Tìm khách của chính user này qua credential mapping và khớp lockId
                var credential = await db.CustomerCredentials
                    .Include(c => c.Customer)
                    .FirstOrDefaultAsync(c => c.CredentialId == record.keyboardPwd && c.IsActiveOnLock && c.Customer.AppUserId == user.Id && c.Customer.LockId == lockId);

                if (credential == null)
                {
                    _logger.LogWarning("Không tìm thấy khách của user {UserId} trên lock {LockId} cho credential: {Pwd}", user.Id, lockId, record.keyboardPwd);
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
                    await FreezeCustomerAsync(db, credential.Customer, lockId, accessToken);
            }

            _lastPolls[key] = now;
        }

        // ── UC-15: Freeze (xóa credential) khi hết hạn mức ────────────

        private async Task FreezeCustomerAsync(AppDbContext db, Customer customer, long lockId, string accessToken)
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
                        resp = await _ttlock.DeleteCardAsync(lockId, cred.CredentialId, accessToken);
                        break;
                    case Models.CredentialType.Fingerprint:
                        resp = await _ttlock.DeleteFingerprintAsync(lockId, cred.CredentialId, accessToken);
                        break;
                    default: // Pin
                        resp = await _ttlock.DeletePasscodeAsync(lockId, cred.CredentialId, accessToken);
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

        private async Task ResetUserMonthlyQuotaAsync(AppDbContext db, AppUser user, long lockId, string accessToken)
        {
            _logger.LogInformation("UsageTrackingService: Reset hạn mức tháng mới cho user {Username} trên lock {LockId}", user.TTLockUsername, lockId);

            var now = DateTime.Now;
            var customers = await db.Customers
                .Include(c => c.Credentials)
                .Where(c => c.Status != CustomerStatus.Revoked && c.AppUserId == user.Id && c.LockId == lockId)
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
                                var cardResp = await _ttlock.AddCardAsync(lockId, $"Card_{customer.Name}", startDate, endDate, accessToken);
                                if (cardResp.errcode == 0) { cred.CredentialId = cardResp.cardId; success = true; }
                                break;
                            case Models.CredentialType.Fingerprint:
                                var fpResp = await _ttlock.AddFingerprintAsync(lockId, customer.Name, startDate, endDate, accessToken);
                                if (fpResp.errcode == 0) { cred.CredentialId = fpResp.fingerprintId; success = true; }
                                break;
                            case Models.CredentialType.Pin:
                                var pinResp = await _ttlock.AddPasscodeAsync(lockId, cred.CredentialId, customer.Name, startDate, endDate, accessToken);
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
