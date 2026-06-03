using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TTLockManager.Data;
using TTLockManager.Models;
using TTLockManager.Services;

namespace TTLockManager.Pages.Customers
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

        public IList<Customer> Customers { get; set; } = default!;
        public List<DeviceLock> DeviceLocks { get; set; } = new();

        public async Task OnGetAsync()
        {
            ViewData["ActivePage"] = "Customers";
            var user = await _userSession.GetCurrentUserAsync();
            if (user != null)
            {
                DeviceLocks = await _db.DeviceLocks.Where(l => l.AppUserId == user.Id).ToListAsync();
                Customers = await _db.Customers
                    .Include(c => c.Credentials)
                    .Where(c => c.Status != CustomerStatus.Revoked && c.AppUserId == user.Id)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();
            }
            else
            {
                Customers = new List<Customer>();
            }
        }
    }
}
