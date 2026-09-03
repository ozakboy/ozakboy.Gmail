using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ozakboy.Gmail.OAuth
{
    /// <summary>
    /// 現成的存取權杖提供者:快取權杖、提前續期、把並發的續期收斂成一次,並在每次續期後通知呼叫端保存。
    /// A ready-made access token provider: it caches the token, refreshes it ahead of expiry, collapses concurrent refreshes into one, and reports every refresh so the caller can persist it.
    /// </summary>
    /// <remarks>
    /// 用法是把 <see cref="GetAccessTokenAsync"/> 直接交給 <see cref="GmailClient"/>:
    /// <c>new GmailClient(httpClient, provider.GetAccessTokenAsync)</c>。
    /// 這個型別**不會**把任何東西寫到磁碟,更新權杖只存在記憶體欄位;要持久化請用
    /// <see cref="GoogleAccessTokenProviderOptions.OnRefreshed"/> 自己寫進安全的儲存體。
    /// 建構後可安全共用,<see cref="GetAccessTokenAsync"/> 是執行緒安全的。
    /// Hand <see cref="GetAccessTokenAsync"/> straight to <see cref="GmailClient"/>:
    /// <c>new GmailClient(httpClient, provider.GetAccessTokenAsync)</c>.
    /// The type writes **nothing** to disk and keeps the refresh token in an in-memory field only; persist what you need
    /// from <see cref="GoogleAccessTokenProviderOptions.OnRefreshed"/> into a store of your choosing.
    /// An instance is safe to share, and <see cref="GetAccessTokenAsync"/> is thread safe.
    /// </remarks>
    public class GoogleAccessTokenProvider
    {
        /// <summary>
        /// 負責呼叫 Google 權杖端點的用戶端。
        /// The client that calls Google's token endpoint.
        /// </summary>
        private readonly IGoogleOAuthClient _oauthClient;

        /// <summary>
        /// 更新權杖。只存在記憶體中,絕不寫出。
        /// The refresh token. It lives in memory only and is never written out.
        /// </summary>
        private readonly string _refreshToken;

        /// <summary>
        /// 提前續期的緩衝時間。
        /// How early the token is refreshed.
        /// </summary>
        private readonly TimeSpan _refreshSkew;

        /// <summary>
        /// 續期成功後的回呼,可為 null。
        /// The callback invoked after a successful refresh; may be null.
        /// </summary>
        private readonly Func<GoogleTokenResponse, CancellationToken, Task>? _onRefreshed;

        /// <summary>
        /// 續期用的鎖,確保並發呼叫只會真的續期一次。
        /// The refresh lock, so concurrent callers trigger only one real refresh.
        /// </summary>
        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 目前快取的權杖狀態,沒有快取時為 null。整份一起換掉,讀取端才不會看到半新半舊的組合。
        /// The cached token state; null when there is none. It is swapped as a whole so readers never see a half-updated pair.
        /// </summary>
        private volatile TokenState? _state;

        /// <summary>
        /// 建立存取權杖提供者。
        /// Creates the access token provider.
        /// </summary>
        /// <param name="oauthClient">用來續期的 OAuth 用戶端,null 時擲出例外。The OAuth client used to refresh; null throws.</param>
        /// <param name="refreshToken">更新權杖,null 或空白時擲出例外。The refresh token; null or blank throws.</param>
        /// <param name="options">快取與續期設定,null 視同預設值;內容會在此複製一份。Cache and refresh settings; null means the defaults, and the values are copied here.</param>
        /// <exception cref="ArgumentNullException"><paramref name="oauthClient"/> 為 null 時擲出。Thrown when <paramref name="oauthClient"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="refreshToken"/> 為 null 或空白時擲出。Thrown when <paramref name="refreshToken"/> is null or blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> 的 RefreshSkew 為負值時擲出。Thrown when RefreshSkew on <paramref name="options"/> is negative.</exception>
        public GoogleAccessTokenProvider(IGoogleOAuthClient oauthClient, string refreshToken, GoogleAccessTokenProviderOptions? options = null)
        {
            if (oauthClient == null)
                throw new ArgumentNullException(nameof(oauthClient));

            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("更新權杖不可為 null 或空白。The refresh token cannot be null or blank.", nameof(refreshToken));

            var refreshSkew = options == null ? new GoogleAccessTokenProviderOptions().RefreshSkew : options.RefreshSkew;
            if (refreshSkew < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(options), refreshSkew, "RefreshSkew 不可為負值。RefreshSkew cannot be negative.");

            _oauthClient = oauthClient;
            _refreshToken = refreshToken;
            _refreshSkew = refreshSkew;
            _onRefreshed = options?.OnRefreshed;

            // 兩個初始值要同時具備才算得上一份可用的快取,只給一個一律當作沒有
            // Both initial values are needed for a usable cache; supplying just one counts as supplying none.
            if (options != null && !string.IsNullOrEmpty(options.InitialAccessToken) && options.InitialExpiresAt.HasValue)
                _state = new TokenState(options.InitialAccessToken!, options.InitialExpiresAt.Value);
        }

        /// <summary>
        /// 目前快取的存取權杖,沒有快取時為 null。這個值不會觸發續期,純粹用來觀察狀態。
        /// The cached access token, or null when there is none. Reading it never triggers a refresh; it is for inspection only.
        /// </summary>
        public string? CurrentAccessToken
        {
            get
            {
                var state = _state;
                return state?.Token;
            }
        }

        /// <summary>
        /// 快取權杖的到期時間,沒有快取時為 null。
        /// When the cached token expires; null when there is no cache.
        /// </summary>
        public DateTimeOffset? ExpiresAt
        {
            get
            {
                var state = _state;
                return state?.ExpiresAt;
            }
        }

        /// <summary>
        /// 取得可用的存取權杖。快取還沒進入緩衝區間就直接回傳,否則取鎖續期(並發呼叫只會續期一次)。
        /// Gets a usable access token. A cache that has not yet entered the skew window is returned as-is; otherwise the lock is taken and the token refreshed, once for all concurrent callers.
        /// </summary>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>存取權杖,永不為 null 或空字串。The access token; never null or empty.</returns>
        /// <exception cref="OperationCanceledException">等待續期鎖或續期途中被取消時擲出。Thrown when the wait for the refresh lock, or the refresh itself, is cancelled.</exception>
        /// <exception cref="InvalidOperationException">Google 的回應沒有帶存取權杖時擲出。Thrown when Google's response carries no access token.</exception>
        /// <exception cref="GmailApiException">續期失敗時原樣上拋;更新權杖已失效時 <see cref="GmailApiException.IsUnauthorized"/> 為 true,此時快取不會被更動。Rethrown as-is when the refresh fails; <see cref="GmailApiException.IsUnauthorized"/> is true when the refresh token is dead, and the cache is left untouched.</exception>
        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            var state = _state;
            if (IsFresh(state))
                return state!.Token;

            await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // 取到鎖時可能已經有別人續期完了,再檢查一次才不會白打一次權杖端點
                // Someone else may have refreshed while this caller waited for the lock; re-checking avoids a pointless token call.
                state = _state;
                if (IsFresh(state))
                    return state!.Token;

                var response = await _oauthClient.RefreshAsync(_refreshToken, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(response.AccessToken))
                {
                    throw new InvalidOperationException(
                        "Google 的權杖回應沒有帶 access_token。Google's token response carried no access_token.");
                }

                _state = new TokenState(response.AccessToken!, response.ExpiresAt);

                // 先更新快取再通知:回呼失敗屬於呼叫端的持久化問題,不該讓已經到手的權杖跟著作廢
                // The cache is updated before the notification: a failing callback is the caller's persistence problem and should not discard a token that is already in hand.
                if (_onRefreshed != null)
                    await _onRefreshed(response, cancellationToken).ConfigureAwait(false);

                return response.AccessToken!;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        /// <summary>
        /// 清掉快取,讓下一次 <see cref="GetAccessTokenAsync"/> 強制續期。Google 回 401 時可以呼叫。
        /// Clears the cache so the next <see cref="GetAccessTokenAsync"/> refreshes unconditionally. Call it when Google answers 401.
        /// </summary>
        public void Invalidate()
        {
            _state = null;
        }

        /// <summary>
        /// 判斷快取是否還在可用區間(扣掉提前續期的緩衝時間之後仍未到期)。
        /// Determines whether the cache is still usable, meaning it has not expired once the refresh skew is subtracted.
        /// </summary>
        /// <param name="state">快取狀態,可為 null。The cached state; may be null.</param>
        /// <returns>true 表示可以直接使用。true when it can be used as-is.</returns>
        private bool IsFresh(TokenState? state)
        {
            return state != null && state.ExpiresAt - _refreshSkew > DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// 一份快取的權杖與它的到期時間。兩個欄位一起換掉,避免讀到不成對的組合。
        /// One cached token and its expiry. The pair is swapped together so readers never see a mismatched combination.
        /// </summary>
        private sealed class TokenState
        {
            /// <summary>
            /// 建立快取狀態。
            /// Creates the cached state.
            /// </summary>
            /// <param name="token">存取權杖。The access token.</param>
            /// <param name="expiresAt">到期時間。When it expires.</param>
            internal TokenState(string token, DateTimeOffset expiresAt)
            {
                Token = token;
                ExpiresAt = expiresAt;
            }

            /// <summary>
            /// 存取權杖。
            /// The access token.
            /// </summary>
            internal string Token { get; }

            /// <summary>
            /// 到期時間。
            /// When the token expires.
            /// </summary>
            internal DateTimeOffset ExpiresAt { get; }
        }
    }
}
