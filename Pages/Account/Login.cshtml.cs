using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using TTLockManager.Data;
using TTLockManager.Models;
using TTLockManager.Services;

namespace TTLockManager.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly ITTLockApiClient _ttlock;
        private readonly AppDbContext _db;

        public LoginModel(ITTLockApiClient ttlock, AppDbContext db)
        {
            _ttlock = ttlock;
            _db = db;
        }

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập Email hoặc Số điện thoại.")]
        [Display(Name = "Tài khoản TTLock")]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Gọi TTLock API để xác thực
            var authResult = await _ttlock.AuthenticateUserAsync(Username, Password);
            if (authResult == null || authResult.errcode != 0)
            {
                ModelState.AddModelError(string.Empty, authResult?.errmsg ?? "Lỗi kết nối tới hệ thống TTLock.");
                return Page();
            }

            // Tìm hoặc tạo mới AppUser trong database
            var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.TTLockUsername.ToLower() == Username.ToLower());
            if (user == null)
            {
                user = new AppUser
                {
                    TTLockUsername = Username,
                    DisplayName = Username.Split('@')[0], // Gợi ý DisplayName từ email
                    AccessToken = authResult.access_token,
                    RefreshToken = authResult.refresh_token,
                    TokenExpiresAt = DateTime.Now.AddSeconds(authResult.expires_in),
                    LastLoginAt = DateTime.Now
                };
                _db.AppUsers.Add(user);
            }
            else
            {
                user.AccessToken = authResult.access_token;
                user.RefreshToken = authResult.refresh_token;
                user.TokenExpiresAt = DateTime.Now.AddSeconds(authResult.expires_in);
                user.LastLoginAt = DateTime.Now;
                _db.AppUsers.Update(user);
            }

            await _db.SaveChangesAsync();

            // Đăng nhập Cookie
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.TTLockUsername),
                new Claim(ClaimTypes.GivenName, user.DisplayName)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            return LocalRedirect(ReturnUrl);
        }
    }
}
