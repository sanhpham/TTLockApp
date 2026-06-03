using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TTLockManager.Data;
using TTLockManager.Services;

namespace TTLockManager.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserSessionService _userSession;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(AppDbContext db, UserSessionService userSession, ILogger<IndexModel> logger)
        {
            _db = db;
            _userSession = userSession;
            _logger = logger;
        }

        public int TotalCustomers { get; set; }
        public int WashesToday { get; set; }
        public int WashesThisMonth { get; set; }
        public int ExhaustedCustomers { get; set; }

        public async Task OnGetAsync()
        {
            ViewData["ActivePage"] = "Dashboard";

            var user = await _userSession.GetCurrentUserAsync();
            if (user == null) return;

            var today = DateTime.Today;
            var month = today.Month;
            var year = today.Year;

            TotalCustomers = await _db.Customers
                .CountAsync(c => c.Status != Models.CustomerStatus.Revoked && c.AppUserId == user.Id);
            
            WashesToday = await _db.WashRecords
                .CountAsync(w => w.OpenedAt >= today && w.OpenedAt < today.AddDays(1) && w.Customer.AppUserId == user.Id);
                
            WashesThisMonth = await _db.WashRecords
                .CountAsync(w => w.Month == month && w.Year == year && w.Customer.AppUserId == user.Id);

            ExhaustedCustomers = await _db.Customers
                .CountAsync(c => c.Status == Models.CustomerStatus.Suspended && c.AppUserId == user.Id);
        }
    }
}
