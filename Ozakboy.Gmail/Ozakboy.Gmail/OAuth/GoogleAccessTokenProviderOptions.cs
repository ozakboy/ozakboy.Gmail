using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ozakboy.Gmail.OAuth
{
    /// <summary>
    /// <see cref="GoogleAccessTokenProvider"/> 的行為設定:初始快取、提前續期的緩衝時間與續期後的回呼。
    /// Behaviour settings for <see cref="GoogleAccessTokenProvider"/>: the initial cache, how early to refresh, and the post-refresh callback.
    /// </summary>
    /// <remarks>
    /// 建構 provider 時會把這些值複製一份保存,之後再改這個物件不會影響已建立的 provider。
    /// The values are copied when a provider is constructed, so changing this object afterwards does not affect an existing provider.
    /// </remarks>
    public class GoogleAccessTokenProviderOptions
    {
        /// <summary>
        /// 已經持有的存取權杖,可省下第一次續期;必須與 <see cref="InitialExpiresAt"/> 同時提供才會被採用。
        /// An access token you already hold, which saves the first refresh; it is only used when <see cref="InitialExpiresAt"/> is supplied as well.
        /// </summary>
        public string? InitialAccessToken { get; set; }

        /// <summary>
        /// <see cref="InitialAccessToken"/> 的到期時間;必須與 <see cref="InitialAccessToken"/> 同時提供才會被採用。
        /// When <see cref="InitialAccessToken"/> expires; it is only used when <see cref="InitialAccessToken"/> is supplied as well.
        /// </summary>
        public DateTimeOffset? InitialExpiresAt { get; set; }

        /// <summary>
        /// 提前多久就續期,預設 2 分鐘,避免權杖在請求送到 Google 的路上剛好過期。
        /// 負值會在建構 provider 時擲出 <see cref="ArgumentOutOfRangeException"/>。
        /// How early the token is refreshed; two minutes by default, so it cannot expire while a request is still in flight.
        /// A negative value throws <see cref="ArgumentOutOfRangeException"/> when the provider is constructed.
        /// </summary>
        public TimeSpan RefreshSkew { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// 每次成功續期後會被 await 的回呼,用來把新權杖寫進自己的儲存體;null 表示不做任何事。
        /// 回呼擲出的例外會原樣上拋,但此時快取已經更新完成。
        /// A callback awaited after every successful refresh, for persisting the new token in your own store; null does nothing.
        /// An exception from the callback propagates as-is, by which point the cache has already been updated.
        /// </summary>
        public Func<GoogleTokenResponse, CancellationToken, Task>? OnRefreshed { get; set; }
    }
}
