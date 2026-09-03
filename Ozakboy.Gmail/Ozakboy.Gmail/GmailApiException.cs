using System;
using System.Globalization;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// Gmail 或 Google OAuth 端點回傳非 2xx(且重試用盡)時擲出的例外。
    /// Thrown when a Gmail or Google OAuth endpoint answers with a non-2xx status and the retries are exhausted.
    /// </summary>
    /// <remarks>
    /// 幾個布林旗標把常見的處置方式直接標示出來:<see cref="IsUnauthorized"/> 要重新授權、
    /// <see cref="IsHistoryExpired"/> 要改用時間區間重掃、<see cref="IsRateLimited"/> 要在工作層級退避、
    /// <see cref="IsNotFound"/> 通常直接略過該筆資料。
    /// The boolean flags name the usual remedy: <see cref="IsUnauthorized"/> means re-authorize,
    /// <see cref="IsHistoryExpired"/> means rescan by time window, <see cref="IsRateLimited"/> means back off at the job level,
    /// and <see cref="IsNotFound"/> usually means skip that item.
    /// </remarks>
    public class GmailApiException : Exception
    {
        /// <summary>
        /// OAuth 用來表示授權(refresh token)已失效的原因代碼。
        /// The OAuth reason code for an authorization grant (refresh token) that is no longer valid.
        /// </summary>
        private const string InvalidGrant = "invalid_grant";

        /// <summary>
        /// OAuth 用來表示權杖無效的原因代碼。
        /// The OAuth reason code for an invalid token.
        /// </summary>
        private const string InvalidToken = "invalid_token";

        /// <summary>
        /// Gmail 用來表達每使用者配額超限的 403 原因代碼。
        /// The 403 reason code Gmail uses for a per-user quota overrun.
        /// </summary>
        private const string RateLimitExceeded = "rateLimitExceeded";

        /// <summary>
        /// Gmail 用來表達使用者層級速率超限的 403 原因代碼。
        /// The 403 reason code Gmail uses for a user-level rate overrun.
        /// </summary>
        private const string UserRateLimitExceeded = "userRateLimitExceeded";

        /// <summary>
        /// 以預設訊息建立例外。
        /// Creates the exception with a default message.
        /// </summary>
        public GmailApiException()
            : base("Gmail API 呼叫失敗。The Gmail API call failed.")
        {
        }

        /// <summary>
        /// 以指定訊息建立例外。
        /// Creates the exception with the given message.
        /// </summary>
        /// <param name="message">例外訊息。The exception message.</param>
        public GmailApiException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 以指定訊息與內部例外建立例外。
        /// Creates the exception with the given message and inner exception.
        /// </summary>
        /// <param name="message">例外訊息。The exception message.</param>
        /// <param name="innerException">內部例外。The inner exception.</param>
        public GmailApiException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// 由 HTTP 回應資訊建立例外,並依狀態碼與錯誤原因算出各個旗標。
        /// Creates the exception from the HTTP response details, deriving every flag from the status code and the error reason.
        /// </summary>
        /// <param name="statusCode">HTTP 狀態碼。The HTTP status code.</param>
        /// <param name="reason">Google 回傳的錯誤原因代碼,可為 null。The error reason code from Google; may be null.</param>
        /// <param name="errorMessage">Google 回傳的錯誤描述,可為 null。The error description from Google; may be null.</param>
        /// <param name="responseBody">原始回應主體,可為 null。The raw response body; may be null.</param>
        /// <param name="requestMethod">HTTP 方法。The HTTP method.</param>
        /// <param name="requestPath">路徑加查詢字串,不含主機。The path and query, without the host.</param>
        /// <param name="isHistoryRequest">是否為 history.list 請求,決定 404 是否代表歷程過期。Whether this was a history.list call, which decides whether a 404 means expired history.</param>
        internal GmailApiException(
            int statusCode,
            string? reason,
            string? errorMessage,
            string? responseBody,
            string? requestMethod,
            string? requestPath,
            bool isHistoryRequest)
            : base(BuildMessage(statusCode, reason, errorMessage, requestMethod, requestPath))
        {
            StatusCode = statusCode;
            Reason = reason;
            ErrorMessage = errorMessage;
            ResponseBody = responseBody;
            RequestMethod = requestMethod;
            RequestPath = requestPath;

            IsUnauthorized = statusCode == 401
                || string.Equals(reason, InvalidGrant, StringComparison.Ordinal)
                || string.Equals(reason, InvalidToken, StringComparison.Ordinal);

            IsRateLimited = statusCode == 429
                || (statusCode == 403
                    && (string.Equals(reason, RateLimitExceeded, StringComparison.Ordinal)
                        || string.Equals(reason, UserRateLimitExceeded, StringComparison.Ordinal)));

            IsHistoryExpired = statusCode == 404 && isHistoryRequest;
            IsNotFound = statusCode == 404 && !IsHistoryExpired;
        }

        /// <summary>
        /// HTTP 狀態碼;不是由 HTTP 回應建立時為 0。
        /// The HTTP status code; 0 when the exception was not created from an HTTP response.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// 錯誤原因代碼。Gmail 取 error.errors[0].reason(例如 "notFound"),OAuth 取 error(例如 "invalid_grant");無法取得時為 null。
        /// The error reason: error.errors[0].reason for Gmail ("notFound"…), error for OAuth ("invalid_grant"…); null when unavailable.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// 錯誤描述。Gmail 取 error.message,OAuth 取 error_description;無法取得時為 null。
        /// The error description: error.message for Gmail, error_description for OAuth; null when unavailable.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// 原始回應主體,供記錄用;內容為空或讀取失敗時為 null。
        /// The raw response body for logging; null when it was empty or could not be read.
        /// </summary>
        public string? ResponseBody { get; }

        /// <summary>
        /// 送出的 HTTP 方法("GET"、"POST"…)。
        /// The HTTP method that was used ("GET", "POST"…).
        /// </summary>
        public string? RequestMethod { get; }

        /// <summary>
        /// 請求的路徑加查詢字串,不含主機。
        /// The request path plus query string, without the host.
        /// </summary>
        public string? RequestPath { get; }

        /// <summary>
        /// 是否需要重新授權:401,或 OAuth 的 invalid_grant / invalid_token。
        /// Whether re-authorization is needed: a 401, or the OAuth invalid_grant / invalid_token reasons.
        /// </summary>
        public bool IsUnauthorized { get; }

        /// <summary>
        /// 是否為歷程過期:history.list 回 404,代表 startHistoryId 太舊,要改用時間區間重掃。
        /// Whether the history expired: a 404 from history.list means the startHistoryId is too old and a time-window rescan is needed.
        /// </summary>
        public bool IsHistoryExpired { get; }

        /// <summary>
        /// 是否被限流:429,或 403 且原因為 rateLimitExceeded / userRateLimitExceeded。
        /// Whether the call was rate limited: a 429, or a 403 with reason rateLimitExceeded / userRateLimitExceeded.
        /// </summary>
        public bool IsRateLimited { get; }

        /// <summary>
        /// 是否為找不到資源:不屬於歷程過期的 404(郵件已刪、標籤不存在…)。
        /// Whether the resource is gone: a 404 that is not an expired history (message deleted, label removed…).
        /// </summary>
        public bool IsNotFound { get; }

        /// <summary>
        /// 組出人類可讀的例外訊息(不是解析用的契約格式)。
        /// Builds the human-readable exception message; it is not a parsing contract.
        /// </summary>
        /// <param name="statusCode">HTTP 狀態碼。The HTTP status code.</param>
        /// <param name="reason">錯誤原因代碼,可為 null。The error reason code; may be null.</param>
        /// <param name="errorMessage">錯誤描述,可為 null。The error description; may be null.</param>
        /// <param name="requestMethod">HTTP 方法。The HTTP method.</param>
        /// <param name="requestPath">路徑加查詢字串。The path and query.</param>
        /// <returns>例外訊息。The exception message.</returns>
        private static string BuildMessage(int statusCode, string? reason, string? errorMessage, string? requestMethod, string? requestPath)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Gmail API {0} {1} failed with {2} ({3}): {4}",
                requestMethod ?? "-",
                requestPath ?? "-",
                statusCode,
                string.IsNullOrEmpty(reason) ? "-" : reason,
                string.IsNullOrEmpty(errorMessage) ? "-" : errorMessage);
        }
    }
}
