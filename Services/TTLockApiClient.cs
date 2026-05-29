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

        private string _accessToken = string.Empty;
        private string _refreshToken = string.Empty;
        private DateTime _tokenExpiry = DateTime.MinValue;

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

        private async Task EnsureTokenAsync()
        {
            if (DateTime.Now < _tokenExpiry.AddMinutes(-5)) return;
            if (!string.IsNullOrEmpty(_refreshToken))
                await RefreshTokenAsync();
            else
                await AuthenticateAsync();
        }

        private async Task<T> PostAsync<T>(string endpoint, Dictionary<string, string> form)
        {
            var content = new FormUrlEncodedContent(form);
            var response = await _http.PostAsync($"{BaseUrl}{endpoint}", content);
            var json = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("TTLock {Endpoint}: {Json}", endpoint, json);
            return JsonConvert.DeserializeObject<T>(json)!;
        }

        // ── Auth ───────────────────────────────────────────────────────────

        public async Task<bool> AuthenticateAsync()
        {
            var clientId = _config["TTLock:ClientId"]!;
            var clientSecret = _config["TTLock:ClientSecret"]!;
            var username = _config["TTLock:Username"]!;
            var password = Md5(_config["TTLock:Password"]!);

            var result = await PostAsync<TTLockTokenResponse>("/oauth2/token", new()
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["username"] = username,
                ["password"] = password
            });

            if (result.errcode == 0)
            {
                _accessToken = result.access_token;
                _refreshToken = result.refresh_token;
                _tokenExpiry = DateTime.Now.AddSeconds(result.expires_in);
                _logger.LogInformation("TTLock: Đăng nhập thành công");
                return true;
            }

            _logger.LogError("TTLock: Đăng nhập thất bại – {Msg}", result.errmsg);
            return false;
        }

        public async Task<bool> RefreshTokenAsync()
        {
            var clientId = _config["TTLock:ClientId"]!;
            var clientSecret = _config["TTLock:ClientSecret"]!;

            var result = await PostAsync<TTLockTokenResponse>("/oauth2/token", new()
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = _refreshToken
            });

            if (result.errcode == 0)
            {
                _accessToken = result.access_token;
                _refreshToken = result.refresh_token;
                _tokenExpiry = DateTime.Now.AddSeconds(result.expires_in);
                return true;
            }

            // Refresh thất bại → re-login
            return await AuthenticateAsync();
        }

        // ── IC Card ────────────────────────────────────────────────────────

        public async Task<TTLockAddCardResponse> AddCardAsync(long lockId, string cardName, long startDate, long endDate)
        {
            await EnsureTokenAsync();
            return await PostAsync<TTLockAddCardResponse>("/v3/iccard/add", new()
            {
                ["clientId"] = _config["TTLock:ClientId"]!,
                ["accessToken"] = _accessToken,
                ["lockId"] = lockId.ToString(),
                ["cardName"] = cardName,
                ["startDate"] = startDate.ToString(),
                ["endDate"] = endDate.ToString(),
                ["date"] = NowMs().ToString()
            });
        }

        public async Task<TTLockBaseResponse> DeleteCardAsync(long lockId, string cardId)
        {
            await EnsureTokenAsync();
            return await PostAsync<TTLockBaseResponse>("/v3/iccard/delete", new()
            {
                ["clientId"] = _config["TTLock:ClientId"]!,
                ["accessToken"] = _accessToken,
                ["lockId"] = lockId.ToString(),
                ["cardId"] = cardId,
                ["date"] = NowMs().ToString()
            });
        }
      
        // ── Fingerprint ────────────────────────────────────────────────────

        public async Task<TTLockAddFingerprintResponse> AddFingerprintAsync(long lockId, string name, long startDate, long endDate)
        {
            await EnsureTokenAsync();
            return await PostAsync<TTLockAddFingerprintResponse>("/v3/fingerprint/add", new()
            {
                ["clientId"] = _config["TTLock:ClientId"]!,
                ["accessToken"] = _accessToken,
                ["lockId"] = lockId.ToString(),
                ["fingerprintName"] = name,
                ["startDate"] = startDate.ToString(),
                ["endDate"] = endDate.ToString(),
                ["date"] = NowMs().ToString()
            });
        }

        public async Task<TTLockBaseResponse> DeleteFingerprintAsync(long lockId, string fingerprintId)
        {
            await EnsureTokenAsync();
            return await PostAsync<TTLockBaseResponse>("/v3/fingerprint/delete", new()
            {
                ["clientId"] = _config["TTLock:ClientId"]!,
                ["accessToken"] = _accessToken,
                ["lockId"] = lockId.ToString(),
                ["fingerprintId"] = fingerprintId,
                ["date"] = NowMs().ToString()
            });
        }

        // ── Passcode (PIN) ─────────────────────────────────────────────────

        // ── Passcode (PIN) ─────────────────────────────────────────────────
        public async Task<TTLockAddPasscodeResponse> AddPasscodeAsync(long lockId, string passcode, string name, long startDate, long endDate)
        {
            await EnsureTokenAsync();
            // TTLock sử dụng endpoint /v3/keyboardPwd/add cho việc tạo Passcode (PIN)
            // Thêm tham số addType = 2 để gửi lệnh qua Gateway (remote)
            return await PostAsync<TTLockAddPasscodeResponse>("/v3/keyboardPwd/add", new()
            {
                ["clientId"]        = _config["TTLock:ClientId"]!,
                ["accessToken"]     = _accessToken,
                ["lockId"]          = lockId.ToString(),
                ["keyboardPwd"]     = passcode,
                ["keyboardPwdName"] = name,
                ["startDate"]       = startDate.ToString(),
                ["endDate"]         = endDate.ToString(),
                ["addType"]         = "2",
                ["date"]            = NowMs().ToString()
            });
        }

        public async Task<TTLockBaseResponse> DeletePasscodeAsync(long lockId, string keyboardPwdId)
        {
            await EnsureTokenAsync();
            return await PostAsync<TTLockBaseResponse>("/v3/passcode/delete", new()
            {
                ["clientId"] = _config["TTLock:ClientId"]!,
                ["accessToken"] = _accessToken,
                ["lockId"] = lockId.ToString(),
                ["keyboardPwdId"] = keyboardPwdId,
                ["date"] = NowMs().ToString()
            });
        }

        // ── Records ────────────────────────────────────────────────────────

        public async Task<TTLockRecordListResponse> GetLockRecordsAsync(long lockId, long startDate, long endDate, int pageNo = 1, int pageSize = 100)
        {
            await EnsureTokenAsync();
            return await PostAsync<TTLockRecordListResponse>("/v3/lockRecord/list", new()
            {
                ["clientId"] = _config["TTLock:ClientId"]!,
                ["accessToken"] = _accessToken,
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
