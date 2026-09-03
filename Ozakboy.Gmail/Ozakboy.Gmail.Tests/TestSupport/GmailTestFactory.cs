using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ozakboy.Gmail.Tests.TestSupport
{
    /// <summary>
    /// 測試共用的用戶端建立方法,統一使用假的存取權杖與零延遲重試設定。
    /// </summary>
    public static class GmailTestFactory
    {
        /// <summary>測試用的假存取權杖。</summary>
        public const string AccessToken = "test-access-token";

        /// <summary>建立指向假 handler 的 GmailClient。</summary>
        public static GmailClient CreateClient(
            RecordingHandler handler,
            GmailClientOptions options = null,
            Func<CancellationToken, Task<string>> accessTokenProvider = null)
        {
            return new GmailClient(
                handler.CreateClient(),
                accessTokenProvider ?? (_ => Task.FromResult(AccessToken)),
                options ?? NoDelayOptions());
        }

        /// <summary>重試不等待的設定,避免測試變慢。</summary>
        public static GmailClientOptions NoDelayOptions(int maxRetries = 3, string userId = "me")
        {
            return new GmailClientOptions
            {
                MaxRetries = maxRetries,
                RetryBaseDelay = TimeSpan.Zero,
                UserId = userId,
            };
        }
    }
}
