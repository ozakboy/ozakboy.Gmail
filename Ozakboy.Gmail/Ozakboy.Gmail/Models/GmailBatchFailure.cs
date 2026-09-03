using System;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// 批次取信中單筆失敗的子回應。整批不會因為其中一筆失敗而中止,失敗的都收在這裡。
    /// One failed sub-response of a batch get. A single failure never aborts the whole batch; every failure is collected here instead.
    /// </summary>
    /// <remarks>
    /// 最常見的兩種:郵件已被永久刪除(<see cref="IsNotFound"/>)、單筆被限流(<see cref="IsRateLimited"/>)。
    /// 限流的部分本套件不會自動重試,要不要重排由呼叫端決定。
    /// The two usual cases are a permanently deleted message (<see cref="IsNotFound"/>) and a per-item rate limit (<see cref="IsRateLimited"/>).
    /// Rate-limited items are not retried automatically; requeueing them stays the caller's decision.
    /// </remarks>
    public class GmailBatchFailure
    {
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
        /// 這筆子請求所要求的郵件識別碼;無法對回請求時為 null。
        /// The message id this sub-request asked for; null when it could not be matched back to a request.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// 該筆子回應的 HTTP 狀態碼。
        /// The HTTP status code of that sub-response.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 錯誤原因代碼,取 Google 的 <c>error.errors[0].reason</c>(例如 "notFound");無法取得時為 null。
        /// The error reason code from Google's <c>error.errors[0].reason</c> ("notFound"…); null when unavailable.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// 錯誤描述,取 Google 的 <c>error.message</c>;無法取得時為 null。
        /// The error description from Google's <c>error.message</c>; null when unavailable.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 是否為找不到郵件(404),通常代表該封已被永久刪除,直接略過即可。
        /// Whether the message is gone (404), which usually means it was permanently deleted and can simply be skipped.
        /// </summary>
        public bool IsNotFound
        {
            get { return StatusCode == 404; }
        }

        /// <summary>
        /// 是否被限流:429,或 403 且原因為 rateLimitExceeded / userRateLimitExceeded(與 <see cref="GmailApiException.IsRateLimited"/> 同一套規則)。
        /// Whether the item was rate limited: a 429, or a 403 with reason rateLimitExceeded / userRateLimitExceeded — the same rule as <see cref="GmailApiException.IsRateLimited"/>.
        /// </summary>
        public bool IsRateLimited
        {
            get
            {
                return StatusCode == 429
                    || (StatusCode == 403
                        && (string.Equals(Reason, RateLimitExceeded, StringComparison.Ordinal)
                            || string.Equals(Reason, UserRateLimitExceeded, StringComparison.Ordinal)));
            }
        }
    }
}
