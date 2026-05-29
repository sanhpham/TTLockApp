using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TTLockManager.Data;
using TTLockManager.Models;

namespace TTLockManager.Pages.History
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public IList<WashRecord> Records { get; set; } = default!;
        public int CurrentMonth { get; set; }
        public int CurrentYear { get; set; }

        public async Task OnGetAsync(int? month, int? year)
        {
            ViewData["ActivePage"] = "History";
            
            CurrentMonth = month ?? DateTime.Now.Month;
            CurrentYear = year ?? DateTime.Now.Year;

            Records = await _db.WashRecords
                .Include(w => w.Customer)
                .Where(w => w.Month == CurrentMonth && w.Year == CurrentYear)
                .OrderByDescending(w => w.OpenedAt)
                .ToListAsync();
        }
    }
}
