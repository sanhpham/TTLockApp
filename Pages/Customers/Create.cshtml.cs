using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TTLockManager.Data;
using TTLockManager.Models;
using TTLockManager.Services;

namespace TTLockManager.Pages.Customers
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITTLockApiClient _ttlock;
        private readonly UserSessionService _userSession;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(AppDbContext db, ITTLockApiClient ttlock, UserSessionService userSession, ILogger<CreateModel> logger)
        {
            _db = db;
            _ttlock = ttlock;
            _userSession = userSession;
            _logger = logger;
        }

        [BindProperty]
        public Customer Customer { get; set; } = new();

        [BindProperty]
        public string CredentialType { get; set; } = "None";

        [BindProperty]
        public string PinCode { get; set; } = "";

        [BindProperty]
        public long SelectedLockId { get; set; }

        public List<DeviceLock> UserLocks { get; set; } = new();

        private async Task LoadLocksAsync(AppUser user)
        {
            UserLocks = await _db.DeviceLocks.Where(l => l.AppUserId == user.Id).ToListAsync();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            ViewData["ActivePage"] = "Customers";
            
            var user = await _userSession.GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            await LoadLocksAsync(user);
            Customer.QuotaPerMonth = 10;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userSession.GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            if (!ModelState.IsValid)
            {
                await LoadLocksAsync(user);
                ViewData["ActivePage"] = "Customers";
                return Page();
            }

            // Kiểm tra ổ khóa đã chọn
            var userLock = await _db.DeviceLocks.FirstOrDefaultAsync(l => l.LockId == SelectedLockId && l.AppUserId == user.Id);
            if (userLock == null)
            {
                ModelState.AddModelError("", "Vui lòng chọn ổ khóa hợp lệ.");
                await LoadLocksAsync(user);
                ViewData["ActivePage"] = "Customers";
                return Page();
            }

            var lockId = userLock.LockId;
            var token = await _userSession.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                ModelState.AddModelError("", "Mã xác thực TTLock đã hết hạn, vui lòng đăng nhập lại.");
                await LoadLocksAsync(user);
                ViewData["ActivePage"] = "Customers";
                return Page();
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var nextYear = DateTimeOffset.UtcNow.AddYears(10).ToUnixTimeMilliseconds(); // Permanent

            // Lưu Customer trước (đã gán AppUserId & LockId)
            Customer.AppUserId = user.Id;
            Customer.LockId = lockId;
            _db.Customers.Add(Customer);
            await _db.SaveChangesAsync();

            // Cấp credential nếu chọn
            bool credSuccess = false;
            string errMsg = "";

            if (CredentialType == "Card")
            {
                var resp = await _ttlock.AddCardAsync(lockId, $"Card_{Customer.Name}", now, nextYear, token);
                if (resp.errcode == 0)
                {
                    _db.CustomerCredentials.Add(new CustomerCredential
                    {
                        CustomerId = Customer.Id,
                        Type = Models.CredentialType.Card,
                        CredentialId = resp.cardId,
                        Label = "Thẻ chính"
                    });
                    credSuccess = true;
                }
                else errMsg = resp.errmsg;
            }
            else if (CredentialType == "Fingerprint")
            {
                var resp = await _ttlock.AddFingerprintAsync(lockId, Customer.Name, now, nextYear, token);
                if (resp.errcode == 0)
                {
                    _db.CustomerCredentials.Add(new CustomerCredential
                    {
                        CustomerId = Customer.Id,
                        Type = Models.CredentialType.Fingerprint,
                        CredentialId = resp.fingerprintId,
                        Label = "Vân tay"
                    });
                    credSuccess = true;
                }
                else errMsg = resp.errmsg;
            }
            else if (CredentialType == "Pin")
            {
                if (string.IsNullOrEmpty(PinCode) || PinCode.Length < 4)
                {
                    ModelState.AddModelError("", "Mã PIN phải từ 4 số.");
                    await LoadLocksAsync(user);
                    ViewData["ActivePage"] = "Customers";
                    return Page();
                }

                var resp = await _ttlock.AddPasscodeAsync(lockId, PinCode, Customer.Name, now, nextYear, token);
                if (resp.errcode == 0)
                {
                    _db.CustomerCredentials.Add(new CustomerCredential
                    {
                        CustomerId = Customer.Id,
                        Type = Models.CredentialType.Pin,
                        CredentialId = resp.keyboardPwdId,
                        Label = "Mã PIN"
                    });
                    credSuccess = true;
                }
                else errMsg = resp.errmsg;
            }
            else
            {
                // None
                credSuccess = true;
            }

            if (credSuccess)
            {
                await _db.SaveChangesAsync();
                return RedirectToPage("./Index");
            }

            // Fallback nếu add TTLock lỗi
            // Xóa customer vừa tạo để tránh rác DB
            _db.Customers.Remove(Customer);
            await _db.SaveChangesAsync();

            ModelState.AddModelError("", $"Lỗi TTLock API: {errMsg}");
            await LoadLocksAsync(user);
            ViewData["ActivePage"] = "Customers";
            return Page();
        }
    }
}
