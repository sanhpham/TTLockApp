using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TTLockManager.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Room { get; set; } = string.Empty;

        /// <summary>Số lần giặt tối đa mỗi tháng</summary>
        public int QuotaPerMonth { get; set; } = 10;

        public CustomerStatus Status { get; set; } = CustomerStatus.Active;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(AppUser))]
        public int AppUserId { get; set; }
        public AppUser? AppUser { get; set; }

        public long? LockId { get; set; } // ID ổ khóa đang sử dụng

        // Navigation
        public ICollection<CustomerCredential> Credentials { get; set; } = new List<CustomerCredential>();
        public ICollection<WashRecord> WashRecords { get; set; } = new List<WashRecord>();
        public ICollection<MonthlyUsage> MonthlyUsages { get; set; } = new List<MonthlyUsage>();
    }

    public enum CustomerStatus
    {
        Active = 1,
        Suspended = 2,
        Revoked = 3
    }
}
