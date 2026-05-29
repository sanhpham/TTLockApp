using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TTLockManager.Data;

namespace TTLockManager.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(AppDbContext db, ILogger<IndexModel> logger)
        {
            _db = db;
            _logger = logger;
        }

        public int TotalCustomers { get; set; }
        public int WashesToday { get; set; }
        public int WashesThisMonth { get; set; }
        public int ExhaustedCustomers { get; set; }

        public async Task OnGetAsync()
        {
            ViewData["ActivePage"] = "Dashboard";

            var today = DateTime.Today;
            var month = today.Month;
            var year = today.Year;

            TotalCustomers = await _db.Customers.CountAsync(c => c.Status != Models.CustomerStatus.Revoked);
            
            WashesToday = await _db.WashRecords
                .CountAsync(w => w.OpenedAt >= today && w.OpenedAt < today.AddDays(1));
                
            WashesThisMonth = await _db.WashRecords
                .CountAsync(w => w.Month == month && w.Year == year);

            ExhaustedCustomers = await _db.Customers
                .CountAsync(c => c.Status == Models.CustomerStatus.Suspended);
        }
    }
}
