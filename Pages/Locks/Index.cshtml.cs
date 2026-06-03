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

namespace TTLockManager.Pages.Locks
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UserSessionService _userSession;
        private readonly ITTLockApiClient _ttlock;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(AppDbContext db, UserSessionService userSession, ITTLockApiClient ttlock, ILogger<IndexModel> logger)
        {
            _db = db;
            _userSession = userSession;
            _ttlock = ttlock;
            _logger = logger;
        }

        public List<DeviceLock> DeviceLocks { get; set; } = new();
        
        [TempData]
        public string? SuccessMessage { get; set; }
        
        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            ViewData["ActivePage"] = "Locks";

            var user = await _userSession.GetCurrentUserAsync();
            if (user != null)
            {
                DeviceLocks = await _db.DeviceLocks
                    .Where(l => l.AppUserId == user.Id)
                    .ToListAsync();
            }
        }

        public async Task<IActionResult> OnPostSyncAsync()
        {
            var user = await _userSession.GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var token = await _userSession.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                ErrorMessage = "Không thể lấy mã thông tin xác thực TTLock. Vui lòng đăng nhập lại.";
                return RedirectToPage();
            }

            try
            {
                var response = await _ttlock.GetLockListAsync(token, pageNo: 1, pageSize: 100);
                if (response == null || response.errcode != 0)
                {
                    ErrorMessage = $"Lỗi từ TTLock API: {response?.errmsg ?? "Không có phản hồi"}";
                    return RedirectToPage();
                }

                // Lấy danh sách khóa hiện tại trong DB của user
                var existingLocks = await _db.DeviceLocks
                    .Where(l => l.AppUserId == user.Id)
                    .ToListAsync();

                var apiLockIds = response.list.Select(l => l.lockId).ToList();

                // 1. Thêm mới hoặc cập nhật từ API
                foreach (var apiLock in response.list)
                {
                    var dbLock = existingLocks.FirstOrDefault(l => l.LockId == apiLock.lockId);
                    if (dbLock == null)
                    {
                        dbLock = new DeviceLock
                        {
                            LockId = apiLock.lockId,
                            LockName = apiLock.lockName,
                            LockAlias = apiLock.lockAlias,
                            AppUserId = user.Id,
                            SyncedAt = DateTime.Now
                        };
                        _db.DeviceLocks.Add(dbLock);
                    }
                    else
                    {
                        dbLock.LockName = apiLock.lockName;
                        dbLock.LockAlias = apiLock.lockAlias;
                        dbLock.SyncedAt = DateTime.Now;
                        _db.DeviceLocks.Update(dbLock);
                    }
                }

                // 2. Xóa các khóa không còn tồn tại trên tài khoản TTLock
                var locksToRemove = existingLocks.Where(l => !apiLockIds.Contains(l.LockId)).ToList();
                if (locksToRemove.Count > 0)
                {
                    _db.DeviceLocks.RemoveRange(locksToRemove);
                }

                await _db.SaveChangesAsync();
                SuccessMessage = $"Đồng bộ thành công! Đã tìm thấy {response.list.Count} ổ khóa.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đồng bộ ổ khóa cho user {UserId}", user.Id);
                ErrorMessage = $"Đã xảy ra lỗi hệ thống: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}
