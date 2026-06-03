using System;
using System.Collections.Generic;

namespace TTLockManager.Models
{
    public class AppUser
    {
        public int Id { get; set; }
        public string TTLockUsername { get; set; } = string.Empty; // email/phone đăng nhập TTLock
        public string DisplayName { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;     // lưu token TTLock
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime TokenExpiresAt { get; set; }
        public DateTime LastLoginAt { get; set; }
        
        public ICollection<DeviceLock> DeviceLocks { get; set; } = new List<DeviceLock>();
        public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    }
}
