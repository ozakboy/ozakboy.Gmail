using System;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// <see cref="GmailClient"/> 的行為設定:重試次數、退避基準與信箱識別。
    /// Behaviour settings for <see cref="GmailClient"/>: retry count, backoff base and mailbox identity.
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
        /// 網址中的 {userId} 路徑片段。除非是網域委派的服務帳號,否則維持 "me"。
        /// The {userId} path segment. Leave it as "me" unless you are a domain-wide-delegated service account.
        /// </summary>
        public string UserId { get; set; } = "me";
    }
}
