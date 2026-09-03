using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ozakboy.Gmail.OAuth
{
    /// <summary>
    /// Google OAuth 2.0 端點的用戶端契約:組授權網址、換權杖、更新權杖、撤銷權杖。
    /// The Google OAuth 2.0 client contract: build the authorization URL, exchange a code, refresh and revoke tokens.
    /// </summary>
    /// <remarks>
    /// 與 <see cref="IGmailClient"/> 一樣,介面的存在是為了 DI 註冊與測試替換。
    /// Like <see cref="IGmailClient"/>, the interface exists for DI registration and test doubles.
    /// </remarks>
    public interface IGoogleOAuthClient
    {
        /// <summary>
        /// 組出使用者同意畫面的網址。純函式,不會連網。
        /// Builds the URL of the user consent screen. A pure function with no network access.
        /// </summary>
        /// <param name="redirectUri">授權完成後要導回的網址,必須與後續換權杖時完全一致。The redirect URL, which has to match the one used when the code is exchanged.</param>
        /// <param name="scopes">要求的授權範圍,會以空白串接。The requested scopes, joined with spaces.</param>
        /// <param name="state">回呼時要驗證的狀態值,防 CSRF 由呼叫端負責。The state value to verify on the callback; CSRF protection stays the caller's job.</param>
        /// <param name="options">存取類型、同意提示等可選參數,null 表示採用預設值。Optional parameters such as the access type and prompt; null means the defaults.</param>
        /// <returns>授權網址。The authorization URL.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="scopes"/> 為 null 時擲出。Thrown when <paramref name="scopes"/> is null.</exception>
        /// <exception cref="System.ArgumentException"><paramref name="redirectUri"/>、<paramref name="state"/> 為 null 或空白,或 <paramref name="scopes"/> 為空序列時擲出。Thrown when <paramref name="redirectUri"/> or <paramref name="state"/> is null or blank, or <paramref name="scopes"/> is empty.</exception>
        string BuildAuthorizationUrl(string redirectUri, IEnumerable<string> scopes, string state, GoogleAuthorizationUrlOptions? options = null);

        /// <summary>
        /// 用授權碼換取存取權杖與更新權杖。
        /// Exchanges an authorization code for an access token and a refresh token.
        /// </summary>
        /// <param name="code">回呼帶回的授權碼,null 或空白時擲出例外。The authorization code from the callback; null or blank throws.</param>
        /// <param name="redirectUri">與授權網址完全相同的導回網址,null 或空白時擲出例外。The exact redirect URL used in the authorization URL; null or blank throws.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>權杖回應。The token response.</returns>
        /// <exception cref="System.ArgumentException">任一必填參數為 null 或空白時擲出。Thrown when a required argument is null or blank.</exception>
        /// <exception cref="GmailApiException">Google 回傳非 2xx 時擲出。Thrown when Google answers with a non-2xx status.</exception>
        Task<GoogleTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default);

        /// <summary>
        /// 用更新權杖換取新的存取權杖。回應中的 RefreshToken 會是 null,原本那把繼續用。
        /// Exchanges a refresh token for a new access token. RefreshToken in the response is null; keep the one you already have.
        /// </summary>
        /// <param name="refreshToken">更新權杖,null 或空白時擲出例外。The refresh token; null or blank throws.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>權杖回應。The token response.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="refreshToken"/> 為 null 或空白時擲出。Thrown when <paramref name="refreshToken"/> is null or blank.</exception>
        /// <exception cref="GmailApiException">Google 回傳非 2xx 時擲出;更新權杖已失效時 <see cref="GmailApiException.IsUnauthorized"/> 為 true。Thrown on a non-2xx status; <see cref="GmailApiException.IsUnauthorized"/> is true when the refresh token is dead.</exception>
        Task<GoogleTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// 撤銷權杖。存取權杖與更新權杖都能傳,撤銷任一把都會讓整份授權失效。
        /// Revokes a token. Either an access or a refresh token works, and revoking either invalidates the whole grant.
        /// </summary>
        /// <param name="token">要撤銷的權杖,null 或空白時擲出例外。The token to revoke; null or blank throws.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>代表非同步作業的 <see cref="Task"/>。A <see cref="Task"/> representing the operation.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="token"/> 為 null 或空白時擲出。Thrown when <paramref name="token"/> is null or blank.</exception>
        /// <exception cref="GmailApiException">Google 回傳非 2xx 時擲出;權杖早已失效時 Google 會回 400 invalid_token。Thrown on a non-2xx status; Google answers 400 invalid_token when the token was already invalid.</exception>
        Task RevokeAsync(string token, CancellationToken cancellationToken = default);
    }
}
