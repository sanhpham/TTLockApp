using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TTLockManager.Data;
using TTLockManager.Models;
using TTLockManager.Services;

namespace TTLockManager.Pages.Quota
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITTLockApiClient _ttlock;
        private readonly UserSessionService _userSession;

        public IndexModel(AppDbContext db, ITTLockApiClient ttlock, UserSessionService userSession)
        {
            _db = db;
            _ttlock = ttlock;
            _userSession = userSession;
        }

        public class QuotaItem
        {
            public Customer Customer { get; set; } = null!;
            public MonthlyUsage Usage { get; set; } = null!;
        }

        public List<QuotaItem> Items { get; set; } = new();

        public async Task OnGetAsync()
        {
            ViewData["ActivePage"] = "Quota";

            var user = await _userSession.GetCurrentUserAsync();
            if (user == null) return;

            var month = DateTime.Now.Month;
            var year = DateTime.Now.Year;

            var customers = await _db.Customers
                .Include(c => c.MonthlyUsages.Where(m => m.Month == month && m.Year == year))
                .Where(c => c.Status != CustomerStatus.Revoked && c.AppUserId == user.Id)
                .ToListAsync();

            foreach (var c in customers)
            {
                var usage = c.MonthlyUsages.FirstOrDefault() ?? new MonthlyUsage 
                { 
                    Quota = c.QuotaPerMonth, 
                    UsedCount = 0 
                };

                Items.Add(new QuotaItem { Customer = c, Usage = usage });
            }
        }

        public async Task<IActionResult> OnPostExtendAsync(int customerId, int addAmount)
        {
            var user = await _userSession.GetCurrentUserAsync();
            if (user == null) return RedirectToPage("/Account/Login");

            if (addAmount <= 0) return RedirectToPage();

            var customer = await _db.Customers
                .Include(c => c.Credentials)
                .FirstOrDefaultAsync(c => c.Id == customerId && c.AppUserId == user.Id);
                
            if (customer == null) return RedirectToPage();

            var month = DateTime.Now.Month;
            var year = DateTime.Now.Year;

            var usage = await _db.MonthlyUsages
                .FirstOrDefaultAsync(m => m.CustomerId == customerId && m.Month == month && m.Year == year);

            if (usage != null)
            {
                usage.Quota += addAmount;
            }

            // Nếu khách đang bị khóa -> khôi phục credential (UC-06)
            if (customer.Status == CustomerStatus.Suspended)
            {
                var userLock = await _db.DeviceLocks.FirstOrDefaultAsync(l => l.AppUserId == user.Id);
                if (userLock == null) return RedirectToPage();

                var lockId = userLock.LockId;
                var token = await _userSession.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token)) return RedirectToPage();

                var now = DateTime.Now;
                var startDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var endDate = new DateTimeOffset(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59, TimeSpan.Zero).ToUnixTimeMilliseconds();

                foreach (var cred in customer.Credentials.Where(c => !c.IsActiveOnLock))
                {
                    bool success = false;
                    if (cred.Type == Models.CredentialType.Card)
                    {
                        var resp = await _ttlock.AddCardAsync(lockId, $"Card_{customer.Name}", startDate, endDate, token);
                        if (resp.errcode == 0) { cred.CredentialId = resp.cardId; success = true; }
                    }
                    else if (cred.Type == Models.CredentialType.Fingerprint)
                    {
                        var resp = await _ttlock.AddFingerprintAsync(lockId, customer.Name, startDate, endDate, token);
                        if (resp.errcode == 0) { cred.CredentialId = resp.fingerprintId; success = true; }
                    }
                    else if (cred.Type == Models.CredentialType.Pin)
                    {
                        var resp = await _ttlock.AddPasscodeAsync(lockId, cred.CredentialId, customer.Name, startDate, endDate, token);
                        if (resp.errcode == 0) success = true;
                    }

                    if (success) cred.IsActiveOnLock = true;
                }

                customer.Status = CustomerStatus.Active;
            }

            await _db.SaveChangesAsync();
            return RedirectToPage();
        }
    }
}
