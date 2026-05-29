using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TTLockManager.Models
{
    /// <summary>
    /// Lưu thông tin thẻ từ / vân tay / mã PIN của từng khách,
    /// bao gồm credentialId để có thể thêm lại khi gia hạn.
    /// </summary>
    public class CustomerCredential
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        public CredentialType Type { get; set; }

        /// <summary>
        /// cardId (thẻ từ) | fingerprintId (vân tay) | passcode (PIN)
        /// — lưu lại để khôi phục khi gia hạn
        /// </summary>
        [MaxLength(200)]
        public string CredentialId { get; set; } = string.Empty;

        /// <summary>Tên hiển thị (VD: "Thẻ chính", "Ngón trỏ")</summary>
        [MaxLength(100)]
        public string Label { get; set; } = string.Empty;

        /// <summary>true = đang được đăng ký trong lock vật lý</summary>
        public bool IsActiveOnLock { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public enum CredentialType
    {
        Card = 1,        // Thẻ từ IC/RFID
        Fingerprint = 2, // Vân tay
        Pin = 3          // Mã số PIN
    }
}
