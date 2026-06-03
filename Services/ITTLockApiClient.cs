namespace TTLockManager.Services
{
    // DTO phản hồi từ TTLock API
    public class TTLockTokenResponse
    {
        public string access_token { get; set; } = string.Empty;
        public string refresh_token { get; set; } = string.Empty;
        public int expires_in { get; set; }
        public int uid { get; set; }
        public int errcode { get; set; }
        public string errmsg { get; set; } = string.Empty;
    }

    public class TTLockLockRecord
    {
        public long recordId { get; set; }
        public int recordType { get; set; }        // 4=PIN, 7=Thẻ, 8=Vân tay
        public string keyboardPwd { get; set; } = string.Empty;
        public long lockDate { get; set; }          // Unix ms
        public string username { get; set; } = string.Empty;
    }

    public class TTLockBaseResponse
    {
        public int errcode { get; set; }
        public string errmsg { get; set; } = string.Empty;
    }

    public class TTLockAddCardResponse : TTLockBaseResponse
    {
        public string cardId { get; set; } = string.Empty;
    }

    public class TTLockAddFingerprintResponse : TTLockBaseResponse
    {
        public string fingerprintId { get; set; } = string.Empty;
    }

    public class TTLockAddPasscodeResponse : TTLockBaseResponse
    {
        public string keyboardPwdId { get; set; } = string.Empty;
    }

    public class TTLockLockListResponse
    {
        public int errcode { get; set; }
        public string errmsg { get; set; } = string.Empty;
        public List<TTLockLockItem> list { get; set; } = new();
        public int pageNo { get; set; }
        public int pageSize { get; set; }
        public int total { get; set; }
    }

    public class TTLockLockItem
    {
        public long lockId { get; set; }
        public string lockName { get; set; } = string.Empty;
        public string lockAlias { get; set; } = string.Empty;
    }

    public class TTLockRecordListResponse
    {
        public int errcode { get; set; }
        public string errmsg { get; set; } = string.Empty;
        public List<TTLockLockRecord> list { get; set; } = new();
        public int pageNo { get; set; }
        public int pageSize { get; set; }
        public int pages { get; set; }
        public int total { get; set; }
    }

    // ===== Interface =====
    public interface ITTLockApiClient
    {
        // Auth
        Task<bool> AuthenticateAsync();
        Task<bool> RefreshTokenAsync();

        // Multi-user Auth
        Task<TTLockTokenResponse> AuthenticateUserAsync(string username, string password);
        Task<TTLockTokenResponse> RefreshUserTokenAsync(string refreshToken);

        // Lock list
        Task<TTLockLockListResponse> GetLockListAsync(string accessToken, int pageNo = 1, int pageSize = 100);

        // IC Card
        Task<TTLockAddCardResponse> AddCardAsync(long lockId, string cardName, long startDate, long endDate, string? accessToken = null);
        Task<TTLockBaseResponse> DeleteCardAsync(long lockId, string cardId, string? accessToken = null);

        // Fingerprint
        Task<TTLockAddFingerprintResponse> AddFingerprintAsync(long lockId, string name, long startDate, long endDate, string? accessToken = null);
        Task<TTLockBaseResponse> DeleteFingerprintAsync(long lockId, string fingerprintId, string? accessToken = null);

        // Passcode (PIN)
        Task<TTLockAddPasscodeResponse> AddPasscodeAsync(long lockId, string passcode, string name, long startDate, long endDate, string? accessToken = null);
        Task<TTLockBaseResponse> DeletePasscodeAsync(long lockId, string keyboardPwdId, string? accessToken = null);

        // Records
        Task<TTLockRecordListResponse> GetLockRecordsAsync(long lockId, long startDate, long endDate, int pageNo = 1, int pageSize = 100, string? accessToken = null);
    }
}
