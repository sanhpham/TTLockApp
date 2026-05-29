using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TTLockManager.Models
{
    /// <summary>Mỗi lần mở khóa = 1 WashRecord</summary>
    public class WashRecord
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        /// <summary>Loại phương thức mở khóa (4=PIN, 7=Thẻ, 8=Vân tay)</summary>
        public int RecordType { get; set; }

        /// <summary>keyboardPwd từ TTLock API (số thẻ / fingerprintId / PIN)</summary>
        [MaxLength(200)]
        public string KeyboardPwd { get; set; } = string.Empty;

        /// <summary>Timestamp mở khóa (từ TTLock)</summary>
        public DateTime OpenedAt { get; set; }

        public int Month { get; set; }
        public int Year { get; set; }

        /// <summary>ID bản ghi gốc từ TTLock (tránh đếm trùng)</summary>
        public long TTLockRecordId { get; set; }
    }
}
