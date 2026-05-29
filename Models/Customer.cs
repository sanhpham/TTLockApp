using System.ComponentModel.DataAnnotations;

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
