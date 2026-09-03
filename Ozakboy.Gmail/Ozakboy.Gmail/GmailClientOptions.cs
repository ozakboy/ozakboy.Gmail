using System;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// <see cref="GmailClient"/> 的行為設定:重試次數、退避基準與上限、批次大小與信箱識別。
    /// Behaviour settings for <see cref="GmailClient"/>: retry count, backoff base and cap, batch size and mailbox identity.
    /// </summary>
    /// <remarks>
    /// 建構 client 時會把這些值複製一份保存,之後再改這個物件不會影響已建立的 client。
    /// The values are copied when a client is constructed, so changing this object afterwards does not affect an existing client.
    /// </remarks>
    public class GmailClientOptions
    {
        /// <summary>
        /// 遇到可重試的失敗時最多重送幾次。3 表示總共最多嘗試四次,0 表示不重試;負值會在建構 client 時擲出 <see cref="ArgumentOutOfRangeException"/>。
        /// How many times a retryable failure is re-sent. 3 means up to four attempts in total and 0 disables retries; a negative value throws <see cref="ArgumentOutOfRangeException"/> when the client is constructed.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// 第一次重試前的延遲,之後每次加倍(預設 1 秒 → 2 秒 → 4 秒)。回應帶 Retry-After 時以該值為準。<see cref="TimeSpan.Zero"/> 可讓測試不必真的等待。
        /// The delay before the first retry, doubled on every subsequent retry (1s → 2s → 4s by default). A Retry-After header wins for that attempt. <see cref="TimeSpan.Zero"/> lets tests run without waiting.
        /// </summary>
        public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// 單次重試等待的上限,預設 60 秒。回應建議的等待時間(Retry-After)或指數退避算出的時間只要超過這個值,
        /// 就不等待也不重試,直接擲出 <see cref="GmailApiException"/> 並在 <see cref="GmailApiException.RetryAfter"/> 帶回建議值,讓呼叫端在工作層級退避。
        /// null 表示不設上限。負值會在建構 client 時擲出 <see cref="ArgumentOutOfRangeException"/>。
        /// The cap on a single retry wait; 60 seconds by default. When the wait suggested by Retry-After, or computed by the exponential backoff, is longer than this,
        /// the request is neither delayed nor retried: a <see cref="GmailApiException"/> is thrown right away carrying the suggested value in <see cref="GmailApiException.RetryAfter"/> so the caller can back off at the job level.
        /// null removes the cap. A negative value throws <see cref="ArgumentOutOfRangeException"/> when the client is constructed.
        /// </summary>
        public TimeSpan? MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// 是否把傳輸層失敗(<see cref="System.Net.Http.HttpRequestException"/>,例如 DNS 解析失敗、連線被中斷)也納入重試,預設 false。
        /// 啟用時套用與 HTTP 錯誤相同的指數退避(沒有 Retry-After 可參考);重試用盡後原樣擲出最後一個 <see cref="System.Net.Http.HttpRequestException"/>,不會包成 <see cref="GmailApiException"/>。
        /// 取消一律不重試。
        /// Whether transport failures (<see cref="System.Net.Http.HttpRequestException"/>: a DNS failure, a dropped connection…) are retried too; false by default.
        /// When enabled they use the same exponential backoff as HTTP errors, with no Retry-After to honour, and the last <see cref="System.Net.Http.HttpRequestException"/> is rethrown as-is once the retries are exhausted rather than wrapped in a <see cref="GmailApiException"/>.
        /// Cancellation is never retried.
        /// </summary>
        public bool RetryOnNetworkErrors { get; set; }

        /// <summary>
        /// 批次取信時每個 HTTP 請求打包幾封郵件,預設 50(Gmail 建議值),允許 1 到 100。
        /// 超出範圍會在建構 client 時擲出 <see cref="ArgumentOutOfRangeException"/>。
        /// How many messages one HTTP request carries during a batch get; 50 by default (Gmail's own recommendation), and 1 to 100 is accepted.
        /// A value outside that range throws <see cref="ArgumentOutOfRangeException"/> when the client is constructed.
        /// </summary>
        public int BatchSize { get; set; } = 50;

        /// <summary>
        /// 網址中的 {userId} 路徑片段。除非是網域委派的服務帳號,否則維持 "me"。
        /// The {userId} path segment. Leave it as "me" unless you are a domain-wide-delegated service account.
        /// </summary>
        public string UserId { get; set; } = "me";
    }
}
