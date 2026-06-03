using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TTLockManager.Models
{
    public class DeviceLock
    {
        public int Id { get; set; }
        
        public long LockId { get; set; } // ID khóa từ TTLock API
        
        [MaxLength(100)]
        public string LockName { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string LockAlias { get; set; } = string.Empty;
        
        [ForeignKey(nameof(AppUser))]
        public int AppUserId { get; set; }
        public AppUser AppUser { get; set; } = null!;
        
        public DateTime SyncedAt { get; set; } = DateTime.Now;
    }
}
