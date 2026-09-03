using System;

namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// 重試策略:判斷哪些回應可以重送,以及每次重送前要等多久。
    /// The retry policy: decides which responses may be re-sent and how long to wait before each attempt.
    /// </summary>
    /// <remarks>
    /// 抽成獨立型別是為了能單獨做單元測試,不必真的送出 HTTP 請求。
    /// Kept as a separate type so it can be unit tested without issuing real HTTP requests.
    /// </remarks>
    internal sealed class RetryPolicy
    {
        /// <summary>
        /// 指數退避的最大次方,避免延遲時間溢位。
        /// The largest backoff exponent, guarding against overflow of the computed delay.
        /// </summary>
        private const int MaxExponent = 16;

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
        /// 建立重試策略。
        /// Creates a retry policy.
        /// </summary>
        /// <param name="maxRetries">失敗後最多重送幾次,0 表示不重試。How many times a failed request is re-sent; 0 disables retries.</param>
        /// <param name="baseDelay">第一次重試前的延遲,之後每次加倍。The delay before the first retry; doubled on every subsequent retry.</param>
        /// <param name="maxRetryDelay">單次等待的上限,null 表示不設限。The cap on a single wait; null means no cap.</param>
        /// <param name="retryOnNetworkErrors">是否把傳輸層失敗也視為可重試。Whether transport level failures count as retryable too.</param>
        internal RetryPolicy(int maxRetries, TimeSpan baseDelay, TimeSpan? maxRetryDelay = null, bool retryOnNetworkErrors = false)
        {
            MaxRetries = maxRetries;
            BaseDelay = baseDelay;
            MaxRetryDelay = maxRetryDelay;
            RetryOnNetworkErrors = retryOnNetworkErrors;
        }

        /// <summary>
        /// 失敗後最多重送幾次。
        /// How many times a failed request is re-sent.
        /// </summary>
        internal int MaxRetries { get; }

        /// <summary>
        /// 第一次重試前的延遲。
        /// The delay before the first retry.
        /// </summary>
        internal TimeSpan BaseDelay { get; }

        /// <summary>
        /// 單次等待的上限,null 表示不設限。
        /// The cap on a single wait; null means no cap.
        /// </summary>
        internal TimeSpan? MaxRetryDelay { get; }

        /// <summary>
        /// 是否把傳輸層失敗(<see cref="System.Net.Http.HttpRequestException"/>)也視為可重試。
        /// Whether transport level failures (<see cref="System.Net.Http.HttpRequestException"/>) count as retryable too.
        /// </summary>
        internal bool RetryOnNetworkErrors { get; }

        /// <summary>
        /// 判斷等待時間是否超過 <see cref="MaxRetryDelay"/>。超過就不該再等,改讓呼叫端在工作層級退避。
        /// Determines whether a delay exceeds <see cref="MaxRetryDelay"/>; when it does, waiting is pointless and the caller should back off at the job level.
        /// </summary>
        /// <param name="delay">要等待的時間。The delay that would be waited.</param>
        /// <returns>true 表示超過上限;<see cref="MaxRetryDelay"/> 為 null 時永遠是 false。true when the cap is exceeded; always false when <see cref="MaxRetryDelay"/> is null.</returns>
        internal bool ExceedsMaxDelay(TimeSpan delay)
        {
            return MaxRetryDelay.HasValue && delay > MaxRetryDelay.Value;
        }

        /// <summary>
        /// 判斷該狀態碼與錯誤原因是否值得重試:429、任何 5xx,或 403 且原因為 Gmail 的配額代碼。
        /// Determines whether a status code and reason are worth retrying: 429, any 5xx, or 403 with one of Gmail's quota reasons.
        /// </summary>
        /// <param name="statusCode">HTTP 狀態碼。The HTTP status code.</param>
        /// <param name="reason">Google 回應中的錯誤原因,可為 null。The error reason from the Google response; may be null.</param>
        /// <returns>true 表示可以重試。true when the request may be retried.</returns>
        internal static bool IsRetryable(int statusCode, string? reason)
        {
            if (statusCode == 429)
                return true;

            if (statusCode >= 500 && statusCode <= 599)
                return true;

            if (statusCode == 403)
            {
                return string.Equals(reason, RateLimitExceeded, StringComparison.Ordinal)
                    || string.Equals(reason, UserRateLimitExceeded, StringComparison.Ordinal);
            }

            return false;
        }

        /// <summary>
        /// 計算第 <paramref name="attempt"/> 次重試前要等的時間。回應帶 Retry-After 時直接採用該值。
        /// Computes the delay before retry number <paramref name="attempt"/>. A Retry-After value from the response wins when present.
        /// </summary>
        /// <param name="attempt">重試序號,從 1 起算。The 1-based retry number.</param>
        /// <param name="retryAfter">回應中的 Retry-After 值,沒有則為 null。The Retry-After value from the response, or null.</param>
        /// <returns>要等待的時間,永不為負值。The delay to wait; never negative.</returns>
        internal TimeSpan GetDelay(int attempt, TimeSpan? retryAfter)
        {
            if (retryAfter.HasValue)
                return retryAfter.Value < TimeSpan.Zero ? TimeSpan.Zero : retryAfter.Value;

            if (BaseDelay <= TimeSpan.Zero)
                return TimeSpan.Zero;

            var exponent = attempt <= 1 ? 0 : Math.Min(attempt - 1, MaxExponent);
            var ticks = BaseDelay.Ticks * Math.Pow(2, exponent);

            return ticks >= long.MaxValue ? TimeSpan.MaxValue : TimeSpan.FromTicks((long)ticks);
        }
    }
}
