using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TTLockManager.Data;
using TTLockManager.Models;

namespace TTLockManager.Pages.Customers
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public IList<Customer> Customers { get; set; } = default!;

        public async Task OnGetAsync()
        {
            ViewData["ActivePage"] = "Customers";
            Customers = await _db.Customers
                .Include(c => c.Credentials)
                .Where(c => c.Status != CustomerStatus.Revoked)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
    }
}
