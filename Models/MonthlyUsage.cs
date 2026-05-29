using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TTLockManager.Models
{
    /// <summary>Theo dõi hạn mức sử dụng theo từng tháng/năm</summary>
    public class MonthlyUsage
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        public int Month { get; set; }
        public int Year { get; set; }

        /// <summary>Hạn mức của tháng này (có thể khác QuotaPerMonth nếu được thay đổi)</summary>
        public int Quota { get; set; }

        /// <summary>Số lần đã dùng trong tháng</summary>
        public int UsedCount { get; set; } = 0;

        /// <summary>Còn lại bao nhiêu lần</summary>
        [NotMapped]
        public int Remaining => Math.Max(0, Quota - UsedCount);

        /// <summary>Đã hết lượt chưa</summary>
        [NotMapped]
        public bool IsExhausted => UsedCount >= Quota;
    }
}
