using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TTLockManager.Data;
using TTLockManager.Models;
using TTLockManager.Services;

namespace TTLockManager.Pages.History
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserSessionService _userSession;

        public IndexModel(AppDbContext db, UserSessionService userSession)
        {
            _db = db;
            _userSession = userSession;
        }

        public IList<WashRecord> Records { get; set; } = default!;
        public int CurrentMonth { get; set; }
        public int CurrentYear { get; set; }

        public async Task OnGetAsync(int? month, int? year)
        {
            ViewData["ActivePage"] = "History";
            
            CurrentMonth = month ?? DateTime.Now.Month;
            CurrentYear = year ?? DateTime.Now.Year;

            var user = await _userSession.GetCurrentUserAsync();
            if (user != null)
            {
                Records = await _db.WashRecords
                    .Include(w => w.Customer)
                    .Where(w => w.Month == CurrentMonth && w.Year == CurrentYear && w.Customer.AppUserId == user.Id)
                    .OrderByDescending(w => w.OpenedAt)
                    .ToListAsync();
            }
            else
            {
                Records = new List<WashRecord>();
            }
        }
    }
}
