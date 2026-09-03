using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ozakboy.Gmail.Core;

namespace Ozakboy.Gmail.OAuth
{
    /// <summary>
    /// Google OAuth 2.0 用戶端。只負責組網址與呼叫權杖端點,不保存任何權杖。
    /// The Google OAuth 2.0 client. It builds URLs and calls the token endpoints; it never stores a token.
    /// </summary>
    /// <remarks>
    /// 權杖端點的非 2xx 一樣會轉成 <see cref="GmailApiException"/>,並套用與 Gmail 端相同的重試策略。
    /// A non-2xx from the token endpoints becomes a <see cref="GmailApiException"/> too, under the same retry policy as the Gmail calls.
    /// </remarks>
    public class GoogleOAuthClient : IGoogleOAuthClient
    {
        /// <summary>
        /// 使用者同意畫面的端點。
        /// The user consent endpoint.
        /// </summary>
        private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";

        /// <summary>
        /// 權杖端點。
        /// The token endpoint.
        /// </summary>
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

        /// <summary>
        /// 權杖撤銷端點。
        /// The token revocation endpoint.
        /// </summary>
        private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";

        /// <summary>
        /// HTTP 傳輸核心。OAuth 端點不需要 Bearer 權杖,因此不掛權杖提供者。
        /// The HTTP transport core. The OAuth endpoints need no bearer token, so no token provider is attached.
        /// </summary>
        private readonly GmailHttp _http;

        /// <summary>
        /// OAuth 用戶端識別碼。
        /// The OAuth client id.
        /// </summary>
        private readonly string _clientId;

        /// <summary>
        /// OAuth 用戶端密鑰。
        /// The OAuth client secret.
        /// </summary>
        private readonly string _clientSecret;

        /// <summary>
        /// 以預設重試設定建立用戶端。
        /// Creates the client with the default retry settings.
        /// </summary>
        /// <param name="httpClient">送出請求用的 HttpClient,本套件不負責釋放。The HttpClient used for every call; this library never disposes it.</param>
        /// <param name="options">OAuth 用戶端憑證。The OAuth client credentials.</param>
        /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> 或 <paramref name="options"/> 為 null 時擲出。Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">ClientId 或 ClientSecret 為空白時擲出。Thrown when ClientId or ClientSecret is blank.</exception>
        public GoogleOAuthClient(HttpClient httpClient, GoogleOAuthOptions options)
            : this(httpClient, options, null)
        {
        }

        /// <summary>
        /// 以指定重試設定建立用戶端。<paramref name="clientOptions"/> 只會取用重試相關設定。
        /// Creates the client with the given retry settings; only the retry-related values of <paramref name="clientOptions"/> are used.
        /// </summary>
        /// <param name="httpClient">送出請求用的 HttpClient,本套件不負責釋放。The HttpClient used for every call; this library never disposes it.</param>
        /// <param name="options">OAuth 用戶端憑證。The OAuth client credentials.</param>
        /// <param name="clientOptions">重試設定,null 視同預設值。The retry settings; null means the defaults.</param>
        /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> 或 <paramref name="options"/> 為 null 時擲出。Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">ClientId 或 ClientSecret 為空白時擲出。Thrown when ClientId or ClientSecret is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="clientOptions"/> 的 MaxRetries 為負值時擲出。Thrown when MaxRetries on <paramref name="clientOptions"/> is negative.</exception>
        public GoogleOAuthClient(HttpClient httpClient, GoogleOAuthOptions options, GmailClientOptions? clientOptions)
        {
            if (httpClient == null)
                throw new ArgumentNullException(nameof(httpClient));

            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (string.IsNullOrWhiteSpace(options.ClientId))
                throw new ArgumentException("ClientId 不可為空白。ClientId cannot be blank.", nameof(options));

            if (string.IsNullOrWhiteSpace(options.ClientSecret))
                throw new ArgumentException("ClientSecret 不可為空白。ClientSecret cannot be blank.", nameof(options));

            var maxRetries = clientOptions == null ? new GmailClientOptions().MaxRetries : clientOptions.MaxRetries;
            if (maxRetries < 0)
                throw new ArgumentOutOfRangeException(nameof(clientOptions), maxRetries, "MaxRetries 不可為負值。MaxRetries cannot be negative.");

            var retryBaseDelay = clientOptions == null ? new GmailClientOptions().RetryBaseDelay : clientOptions.RetryBaseDelay;

            _clientId = options.ClientId;
            _clientSecret = options.ClientSecret;
            _http = new GmailHttp(httpClient, null, new RetryPolicy(maxRetries, retryBaseDelay));
        }

        /// <inheritdoc />
        public string BuildAuthorizationUrl(string redirectUri, IEnumerable<string> scopes, string state, GoogleAuthorizationUrlOptions? options = null)
        {
            RequireText(redirectUri, nameof(redirectUri));
            RequireText(state, nameof(state));

            if (scopes == null)
                throw new ArgumentNullException(nameof(scopes));

            var scopeList = new List<string>(scopes);
            if (scopeList.Count == 0)
                throw new ArgumentException("至少要指定一個授權範圍。At least one scope has to be supplied.", nameof(scopes));

            var parameters = new StringBuilder();
            AppendParameter(parameters, "client_id", _clientId);
            AppendParameter(parameters, "redirect_uri", redirectUri);
            AppendParameter(parameters, "response_type", "code");
            AppendParameter(parameters, "scope", string.Join(" ", scopeList));
            AppendParameter(parameters, "state", state);

            var urlOptions = options ?? new GoogleAuthorizationUrlOptions();
            AppendParameter(parameters, "access_type", urlOptions.AccessType);
            AppendParameter(parameters, "prompt", urlOptions.Prompt);

            // 只有要沿用既有授權範圍時才送這個參數,false 就等於 Google 預設
            // The parameter is only sent when previously granted scopes should carry over; false is Google's own default.
            if (urlOptions.IncludeGrantedScopes)
                AppendParameter(parameters, "include_granted_scopes", "true");

            AppendParameter(parameters, "login_hint", urlOptions.LoginHint);

            return AuthorizationEndpoint + parameters.ToString();
        }

        /// <inheritdoc />
        public Task<GoogleTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
        {
            RequireText(code, nameof(code));
            RequireText(redirectUri, nameof(redirectUri));

            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "client_id", _clientId },
                { "client_secret", _clientSecret },
                { "code", code },
                { "grant_type", "authorization_code" },
                { "redirect_uri", redirectUri },
            };

            return SendTokenRequestAsync(form, cancellationToken);
        }

        /// <inheritdoc />
        public Task<GoogleTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            RequireText(refreshToken, nameof(refreshToken));

            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "client_id", _clientId },
                { "client_secret", _clientSecret },
                { "refresh_token", refreshToken },
                { "grant_type", "refresh_token" },
            };

            return SendTokenRequestAsync(form, cancellationToken);
        }

        /// <inheritdoc />
        public Task RevokeAsync(string token, CancellationToken cancellationToken = default)
        {
            RequireText(token, nameof(token));

            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "token", token },
            };

            return _http.SendAsync(() => CreateFormRequest(RevokeEndpoint, form), false, cancellationToken);
        }

        /// <summary>
        /// 送出權杖請求並補上收到回應的時間。
        /// Sends a token request and stamps the moment the response was received.
        /// </summary>
        /// <param name="form">表單內容。The form fields.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>權杖回應。The token response.</returns>
        private async Task<GoogleTokenResponse> SendTokenRequestAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
        {
            var response = await _http
                .SendForJsonAsync<GoogleTokenResponse>(() => CreateFormRequest(TokenEndpoint, form), false, cancellationToken)
                .ConfigureAwait(false);

            response.IssuedAt = DateTimeOffset.UtcNow;
            return response;
        }

        /// <summary>
        /// 建立帶表單內容的 POST 請求。每次重試都會重建一份新的內容。
        /// Builds a POST request carrying form content; a fresh content instance is created for every attempt.
        /// </summary>
        /// <param name="url">端點網址。The endpoint URL.</param>
        /// <param name="form">表單內容。The form fields.</param>
        /// <returns>可送出的請求。The request, ready to be sent.</returns>
        private static HttpRequestMessage CreateFormRequest(string url, Dictionary<string, string> form)
        {
            return new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(form),
            };
        }

        /// <summary>
        /// 附加一個查詢參數,值為 null 或空字串時不附加。
        /// Appends one query parameter, skipping null or empty values.
        /// </summary>
        /// <param name="parameters">查詢字串緩衝區。The query string buffer.</param>
        /// <param name="name">參數名稱。The parameter name.</param>
        /// <param name="value">參數值,可為 null。The value; may be null.</param>
        private static void AppendParameter(StringBuilder parameters, string name, string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            parameters.Append(parameters.Length == 0 ? '?' : '&');
            parameters.Append(name);
            parameters.Append('=');
            parameters.Append(Uri.EscapeDataString(value));
        }

        /// <summary>
        /// 檢查必填字串參數。
        /// Validates a required string argument.
        /// </summary>
        /// <param name="value">參數值。The argument value.</param>
        /// <param name="parameterName">參數名稱。The parameter name.</param>
        /// <exception cref="ArgumentException">值為 null 或空白時擲出。Thrown when the value is null or blank.</exception>
        private static void RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("必填參數不可為 null 或空白。The required argument cannot be null or blank.", parameterName);
        }
    }
}
