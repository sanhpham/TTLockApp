using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace TTLockManager.Services
{
    public class TTLockApiClient : ITTLockApiClient
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<TTLockApiClient> _logger;

        // Base URL TTLock EU
        private const string BaseUrl = "https://euapi.ttlock.com";

        public TTLockApiClient(HttpClient http, IConfiguration config, ILogger<TTLockApiClient> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static string Md5(string input)
        {
            var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLower();
        }

        private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private async Task<T> PostAsync<T>(string endpoint, Dictionary<string, string> form)
        {
            var content = new FormUrlEncodedContent(form);
            var response = await _http.PostAsync($"{BaseUrl}{endpoint}", content);
            var json = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("TTLock {Endpoint}: {Json}", endpoint, json);
            return JsonConvert.DeserializeObject<T>(json)!;
        }

        // ── Multi-user Auth ────────────────────────────────────────────────

        public async Task<TTLockTokenResponse> AuthenticateUserAsync(string username, string password)
        {
            var clientId = _config["TTLock:ClientId"]!;
            var clientSecret = _config["TTLock:ClientSecret"]!;
            var hashedPassword = Md5(password);

            return await PostAsync<TTLockTokenResponse>("/oauth2/token", new()
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["username"] = username,
                ["password"] = hashedPassword
            });
        }

        public async Task<TTLockTokenResponse> RefreshUserTokenAsync(string refreshToken)
        {
            var clientId = _config["TTLock:ClientId"]!;
            var clientSecret = _config["TTLock:ClientSecret"]!;

            return await PostAsync<TTLockTokenResponse>("/oauth2/token", new()
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            });
        }

        // ── Lock list ──────────────────────────────────────────────────────

        public async Task<TTLockLockListResponse> GetLockListAsync(string accessToken, int pageNo = 1, int pageSize = 100)
        {
            return await PostAsync<TTLockLockListResponse>("/v3/lock/list", new()
            {
                ["clientId"] = _config["TTLock:ClientId"]!,
                ["accessToken"] = accessToken,
                ["pageNo"] = pageNo.ToString(),
                ["pageSize"] = pageSize.ToString(),
                ["date"] = NowMs().ToString()
            });
        }

        // ── IC Card ────────────────────────────────────────────────────────

        public async Task<TTLockAddCardResponse> AddCardAsync(long lockId, string cardName, long startDate, long endDate, string accessToken)
        {
            var token = accessToken;
            return await PostAsync<TTLockAddCardResponse>("/v3/iccard/add", new()
            {
                ["clientId"] = _config["TTLock:ClientId"]!,
                ["accessToken"] = token,
                ["lockId"] = lockId.ToString(),
                ["cardName"] = cardName,
                ["startDate"] = startDate.ToString(),
                ["endDate"] = endDate.ToString(),
                ["date"] = NowMs().ToString()
            });
        }

        public async Task<TTLockBaseResponse> DeleteCardAsync(long lockId, string cardId, string accessToken)
        {
            var token = accessToken;
            return await PostAsync<TTLockBaseResponse>("/v3/iccard/delete", new()
            {
                ["clientId"] = _config["TTLock:ClientId"]!,
                ["accessToken"] = token,
                ["lockId"] = lockId.ToString(),
                ["cardId"] = cardId,
                ["date"] = NowMs().ToString()
            });
        }
      
        // ── Fingerprint ────────────────────────────────────────────────────

        public async Task<TTLockAddFingerprintResponse> AddFingerprintAsync(long lockId, string name, long startDate, long endDate, string accessToken)
        {
            var token = accessToken;
            return await PostAsync<TTLockAddFingerprintResponse>("/v3/fingerprint/add", new()
            {
                ["clientId"] = _config["TTLock:ClientId"]!,
                ["accessToken"] = token,
                ["lockId"] = lockId.ToString(),
                ["fingerprintName"] = name,
                ["startDate"] = startDate.ToString(),
                ["endDate"] = endDate.ToString(),
                ["date"] = NowMs().ToString()
            });
        }

        public async Task<TTLockBaseResponse> DeleteFingerprintAsync(long lockId, string fingerprintId, string accessToken)
        {
            var token = accessToken;
            return await PostAsync<TTLockBaseResponse>("/v3/fingerprint/delete", new()
            {
                ["clientId"] = _config["TTLock:ClientId"]!,
                ["accessToken"] = token,
                ["lockId"] = lockId.ToString(),
                ["fingerprintId"] = fingerprintId,
                ["date"] = NowMs().ToString()
            });
        }

        // ── Passcode (PIN) ─────────────────────────────────────────────────
        public async Task<TTLockAddPasscodeResponse> AddPasscodeAsync(long lockId, string passcode, string name, long startDate, long endDate, string accessToken)
        {
            var token = accessToken;
            // TTLock sử dụng endpoint /v3/keyboardPwd/add cho việc tạo Passcode (PIN)
            // Thêm tham số addType = 2 để gửi lệnh qua Gateway (remote)
            return await PostAsync<TTLockAddPasscodeResponse>("/v3/keyboardPwd/add", new()
            {
                ["clientId"]        = _config["TTLock:ClientId"]!,
                ["accessToken"]     = token,
                ["lockId"]          = lockId.ToString(),
                ["keyboardPwd"]     = passcode,
                ["keyboardPwdName"] = name,
                ["startDate"]       = startDate.ToString(),
                ["endDate"]         = endDate.ToString(),
                ["addType"]         = "2",
                ["date"]            = NowMs().ToString()
            });
        }

        public async Task<TTLockBaseResponse> DeletePasscodeAsync(long lockId, string keyboardPwdId, string accessToken)
        {
            var token = accessToken;
            return await PostAsync<TTLockBaseResponse>("/v3/passcode/delete", new()
            {
                ["clientId"] = _config["TTLock:ClientId"]!,
                ["accessToken"] = token,
                ["lockId"] = lockId.ToString(),
                ["keyboardPwdId"] = keyboardPwdId,
                ["date"] = NowMs().ToString()
            });
        }

        // ── Records ────────────────────────────────────────────────────────

        public async Task<TTLockRecordListResponse> GetLockRecordsAsync(long lockId, long startDate, long endDate, int pageNo = 1, int pageSize = 100, string accessToken = "")
        {
            var token = accessToken;
            return await PostAsync<TTLockRecordListResponse>("/v3/lockRecord/list", new()
            {
                ["clientId"] = _config["TTLock:ClientId"]!,
                ["accessToken"] = token,
                ["lockId"] = lockId.ToString(),
                ["startDate"] = startDate.ToString(),
                ["endDate"] = endDate.ToString(),
                ["pageNo"] = pageNo.ToString(),
                ["pageSize"] = pageSize.ToString(),
                ["date"] = NowMs().ToString()
            });
        }
    }
}
