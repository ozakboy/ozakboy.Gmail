using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// HTTP 傳輸核心。負責掛上 Bearer 權杖、送出請求、依重試策略重送,並把非 2xx 回應轉成 <see cref="GmailApiException"/>。
    /// The HTTP transport core: attaches the bearer token, sends the request, re-sends it according to the retry policy, and turns every non-2xx response into a <see cref="GmailApiException"/>.
    /// </summary>
    /// <remarks>
    /// Gmail 端與 OAuth 端共用同一份實作,差別只在 OAuth 端不需要存取權杖。
    /// The Gmail and OAuth clients share this implementation; the only difference is that OAuth calls carry no access token.
    /// </remarks>
    internal sealed class GmailHttp
    {
        /// <summary>
        /// 送出請求用的 HttpClient,由呼叫端提供,本型別不負責釋放。
        /// The HttpClient used for every call; supplied by the caller and never disposed here.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 存取權杖提供者,為 null 時不掛 Authorization 標頭(OAuth 端點適用)。
        /// The access token provider; null means no Authorization header is attached (as used by the OAuth endpoints).
        /// </summary>
        private readonly Func<CancellationToken, Task<string>>? _accessTokenProvider;

        /// <summary>
        /// 重試策略。
        /// The retry policy.
        /// </summary>
        private readonly RetryPolicy _retryPolicy;

        /// <summary>
        /// 建立 HTTP 傳輸核心。
        /// Creates the HTTP transport core.
        /// </summary>
        /// <param name="httpClient">送出請求用的 HttpClient。The HttpClient used for every call.</param>
        /// <param name="accessTokenProvider">存取權杖提供者,可為 null。The access token provider; may be null.</param>
        /// <param name="retryPolicy">重試策略。The retry policy.</param>
        internal GmailHttp(HttpClient httpClient, Func<CancellationToken, Task<string>>? accessTokenProvider, RetryPolicy retryPolicy)
        {
            _httpClient = httpClient;
            _accessTokenProvider = accessTokenProvider;
            _retryPolicy = retryPolicy;
        }

        /// <summary>
        /// 送出請求並將回應主體反序列化為指定型別。回應為空時回傳該型別的預設實體。
        /// Sends the request and deserializes the response body into the given type. An empty body yields a default instance of that type.
        /// </summary>
        /// <typeparam name="T">回應型別。The response type.</typeparam>
        /// <param name="requestFactory">請求工廠,每次嘗試都會呼叫一次以建立全新的請求。The request factory, invoked once per attempt to build a fresh request.</param>
        /// <param name="isHistoryRequest">是否為 history.list 請求,用來判定 404 是否代表歷程過期。Whether this is a history.list call, which decides whether a 404 means expired history.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>反序列化後的回應。The deserialized response.</returns>
        /// <exception cref="GmailApiException">重試用盡後仍收到非 2xx 回應時擲出。Thrown when a non-2xx response remains after retries are exhausted.</exception>
        /// <exception cref="InvalidOperationException">存取權杖提供者回傳 null 或空字串時擲出。Thrown when the access token provider returns null or an empty string.</exception>
        internal async Task<T> SendForJsonAsync<T>(Func<HttpRequestMessage> requestFactory, bool isHistoryRequest, CancellationToken cancellationToken)
            where T : class, new()
        {
            var body = await SendCoreAsync(requestFactory, isHistoryRequest, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
                return new T();

            return GmailJson.Deserialize<T>(body) ?? new T();
        }

        /// <summary>
        /// 送出請求並忽略回應主體,用於 Gmail 回 204 的端點。
        /// Sends the request and ignores the response body, for the endpoints where Gmail answers 204.
        /// </summary>
        /// <param name="requestFactory">請求工廠,每次嘗試都會呼叫一次以建立全新的請求。The request factory, invoked once per attempt to build a fresh request.</param>
        /// <param name="isHistoryRequest">是否為 history.list 請求。Whether this is a history.list call.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>代表非同步作業的 <see cref="Task"/>。A <see cref="Task"/> representing the operation.</returns>
        /// <exception cref="GmailApiException">重試用盡後仍收到非 2xx 回應時擲出。Thrown when a non-2xx response remains after retries are exhausted.</exception>
        /// <exception cref="InvalidOperationException">存取權杖提供者回傳 null 或空字串時擲出。Thrown when the access token provider returns null or an empty string.</exception>
        internal async Task SendAsync(Func<HttpRequestMessage> requestFactory, bool isHistoryRequest, CancellationToken cancellationToken)
        {
            await SendCoreAsync(requestFactory, isHistoryRequest, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 實際送出請求的迴圈:取一次權杖、依需要重送、成功回傳回應主體字串。
        /// The actual send loop: obtains the token once, re-sends when the policy allows, and returns the response body on success.
        /// </summary>
        /// <param name="requestFactory">請求工廠。The request factory.</param>
        /// <param name="isHistoryRequest">是否為 history.list 請求。Whether this is a history.list call.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>回應主體字串,可能為空字串。The response body; possibly an empty string.</returns>
        /// <exception cref="GmailApiException">重試用盡後仍收到非 2xx 回應時擲出。Thrown when a non-2xx response remains after retries are exhausted.</exception>
        /// <exception cref="InvalidOperationException">存取權杖提供者回傳 null 或空字串時擲出。Thrown when the access token provider returns null or an empty string.</exception>
        private async Task<string> SendCoreAsync(Func<HttpRequestMessage> requestFactory, bool isHistoryRequest, CancellationToken cancellationToken)
        {
            // 權杖只在整個呼叫的最前面取一次,重試沿用同一個權杖
            // The token is obtained once for the whole call; retries reuse the very same token.
            string? accessToken = null;
            if (_accessTokenProvider != null)
            {
                accessToken = await _accessTokenProvider(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new InvalidOperationException(
                        "存取權杖提供者回傳 null 或空字串,無法呼叫 Google API。The access token provider returned null or an empty string.");
                }
            }

            var attempt = 0;
            while (true)
            {
                using (var request = requestFactory())
                {
                    if (accessToken != null)
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                            return await ReadContentAsync(response, cancellationToken).ConfigureAwait(false);

                        var responseBody = await TryReadContentAsync(response, cancellationToken).ConfigureAwait(false);
                        var error = GoogleErrorBody.Parse(responseBody);
                        var statusCode = (int)response.StatusCode;

                        if (attempt < _retryPolicy.MaxRetries && RetryPolicy.IsRetryable(statusCode, error.Reason))
                        {
                            attempt++;
                            var delay = _retryPolicy.GetDelay(attempt, GetRetryAfter(response));

                            // 即使延遲為零也走 Task.Delay,取消才會在等待階段被觀察到
                            // Task.Delay is awaited even for a zero delay so cancellation is still observed while waiting.
                            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        throw CreateException(request, statusCode, error, responseBody, isHistoryRequest);
                    }
                }
            }
        }

        /// <summary>
        /// 由請求與錯誤資訊組出 <see cref="GmailApiException"/>。
        /// Builds the <see cref="GmailApiException"/> from the request and the parsed error.
        /// </summary>
        /// <param name="request">送出的請求。The request that was sent.</param>
        /// <param name="statusCode">HTTP 狀態碼。The HTTP status code.</param>
        /// <param name="error">解析後的錯誤內容。The parsed error body.</param>
        /// <param name="responseBody">原始回應主體,可為 null。The raw response body; may be null.</param>
        /// <param name="isHistoryRequest">是否為 history.list 請求。Whether this is a history.list call.</param>
        /// <returns>組裝完成的例外。The assembled exception.</returns>
        private static GmailApiException CreateException(
            HttpRequestMessage request,
            int statusCode,
            GoogleErrorBody error,
            string? responseBody,
            bool isHistoryRequest)
        {
            var method = request.Method.Method;
            var path = request.RequestUri == null ? string.Empty : request.RequestUri.PathAndQuery;

            return new GmailApiException(
                statusCode,
                error.Reason,
                error.ErrorMessage,
                string.IsNullOrEmpty(responseBody) ? null : responseBody,
                method,
                path,
                isHistoryRequest);
        }

        /// <summary>
        /// 取出回應的 Retry-After,支援秒數與 HTTP 日期兩種格式。
        /// Reads the response's Retry-After header, accepting both the seconds and the HTTP-date form.
        /// </summary>
        /// <param name="response">HTTP 回應。The HTTP response.</param>
        /// <returns>建議等待時間,沒有標頭時為 null。The suggested delay, or null when the header is absent.</returns>
        private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
        {
            var retryAfter = response.Headers.RetryAfter;
            if (retryAfter == null)
                return null;

            if (retryAfter.Delta.HasValue)
                return retryAfter.Delta.Value;

            if (retryAfter.Date.HasValue)
            {
                var remaining = retryAfter.Date.Value - DateTimeOffset.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }

            return null;
        }

        /// <summary>
        /// 讀取回應主體字串。
        /// Reads the response body as a string.
        /// </summary>
        /// <param name="response">HTTP 回應。The HTTP response.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>回應主體。The response body.</returns>
        private static async Task<string> ReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
#if NETSTANDARD2_0 || NETSTANDARD2_1
            // netstandard 的 HttpContent 沒有吃 CancellationToken 的多載
            // The netstandard HttpContent has no overload taking a CancellationToken.
            _ = cancellationToken;
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif
        }

        /// <summary>
        /// 盡力讀取回應主體字串,讀取失敗時回傳 null(錯誤路徑不因為讀不到主體而失去原本的狀態碼資訊)。
        /// Reads the response body on a best-effort basis, returning null on failure so the error path keeps its status information.
        /// </summary>
        /// <param name="response">HTTP 回應。The HTTP response.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>回應主體,讀取失敗時為 null。The response body, or null when it could not be read.</returns>
        private static async Task<string?> TryReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                return await ReadContentAsync(response, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }
    }
}
