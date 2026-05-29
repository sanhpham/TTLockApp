using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TTLockManager.Data;
using TTLockManager.Models;
using TTLockManager.Services;

namespace TTLockManager.Pages.Customers
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITTLockApiClient _ttlock;
        private readonly IConfiguration _config;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(AppDbContext db, ITTLockApiClient ttlock, IConfiguration config, ILogger<CreateModel> logger)
        {
            _db = db;
            _ttlock = ttlock;
            _config = config;
            _logger = logger;
        }

        [BindProperty]
        public Customer Customer { get; set; } = new();

        [BindProperty]
        public string CredentialType { get; set; } = "None";

        [BindProperty]
        public string PinCode { get; set; } = "";

        public void OnGet()
        {
            ViewData["ActivePage"] = "Customers";
            Customer.QuotaPerMonth = 10;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ViewData["ActivePage"] = "Customers";
                return Page();
            }

            var lockId = long.Parse(_config["TTLock:LockId"]!);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var nextYear = DateTimeOffset.UtcNow.AddYears(10).ToUnixTimeMilliseconds(); // Permanent

            // Lưu Customer trước
            _db.Customers.Add(Customer);
            await _db.SaveChangesAsync();

            // Cấp credential nếu chọn
            bool credSuccess = false;
            string errMsg = "";

            if (CredentialType == "Card")
            {
                var resp = await _ttlock.AddCardAsync(lockId, $"Card_{Customer.Name}", now, nextYear);
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
                var resp = await _ttlock.AddFingerprintAsync(lockId, Customer.Name, now, nextYear);
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
                    return Page();
                }

                var resp = await _ttlock.AddPasscodeAsync(lockId, PinCode, Customer.Name, now, nextYear);
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
            ModelState.AddModelError("", $"Lỗi TTLock API: {errMsg}");
            ViewData["ActivePage"] = "Customers";
            return Page();
        }
    }
}
