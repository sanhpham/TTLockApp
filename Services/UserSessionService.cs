using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using TTLockManager.Data;
using TTLockManager.Models;

namespace TTLockManager.Services
{
    public class UserSessionService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _db;
        private readonly ITTLockApiClient _ttlock;

        public UserSessionService(IHttpContextAccessor httpContextAccessor, AppDbContext db, ITTLockApiClient ttlock)
        {
            _httpContextAccessor = httpContextAccessor;
            _db = db;
            _ttlock = ttlock;
        }

        public async Task<AppUser?> GetCurrentUserAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context?.User.Identity?.IsAuthenticated != true)
                return null;

            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return null;

            return await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<string?> GetAccessTokenAsync()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return null;

            // Nếu token còn hạn nhiều hơn 5 phút thì trả về luôn
            if (user.TokenExpiresAt > DateTime.Now.AddMinutes(5))
            {
                return user.AccessToken;
            }

            // Ngược lại, thực hiện refresh token
            if (string.IsNullOrEmpty(user.RefreshToken))
                return null;

            var refreshResult = await _ttlock.RefreshUserTokenAsync(user.RefreshToken);
            if (refreshResult != null && refreshResult.errcode == 0)
            {
                user.AccessToken = refreshResult.access_token;
                user.RefreshToken = refreshResult.refresh_token;
                user.TokenExpiresAt = DateTime.Now.AddSeconds(refreshResult.expires_in);
                
                await _db.SaveChangesAsync();
                return user.AccessToken;
            }

            return null; // Không refresh được (cần login lại)
        }
    }
}
