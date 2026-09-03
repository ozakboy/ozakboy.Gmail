using System;
using System.Text.Json.Serialization;

namespace Ozakboy.Gmail.OAuth
{
    /// <summary>
    /// Google 權杖端點的回應。
    /// The response from Google's token endpoint.
    /// </summary>
    /// <remarks>
    /// 要持久化的是 <see cref="ExpiresAt"/>,而且建議提早一兩分鐘就換新,不要卡在到期的那一秒。
    /// <see cref="ExpiresAt"/> is what you persist, and refreshing a minute or two early beats refreshing on the dot.
    /// </remarks>
    public class GoogleTokenResponse
    {
        /// <summary>
        /// 存取權杖。
        /// The access token.
        /// </summary>
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        /// <summary>
        /// 更新權杖。呼叫 RefreshAsync 時為 null(Google 不會在更新時輪替),交換授權碼但 Google 沒發時也是 null。
        /// The refresh token. It is null on RefreshAsync because Google does not rotate it, and also null when Google issued none during a code exchange.
        /// </summary>
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        /// <summary>
        /// 存取權杖的有效秒數。
        /// The access token's lifetime in seconds.
        /// </summary>
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        /// <summary>
        /// 實際授予的範圍,以空白分隔;可能比要求的少。
        /// The granted scopes, space separated; there may be fewer than were requested.
        /// </summary>
        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        /// <summary>
        /// 權杖類型,通常是 "Bearer"。
        /// The token type, normally "Bearer".
        /// </summary>
        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        /// <summary>
        /// OpenID Connect 的身分權杖,只有要求 "openid" 範圍時才有。
        /// The OpenID Connect identity token, present only when the "openid" scope was requested.
        /// </summary>
        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }

        /// <summary>
        /// 收到回應的時間(UTC),由用戶端在反序列化後填入。
        /// The moment the response was received (UTC); the client fills it in after deserialization.
        /// </summary>
        [JsonIgnore]
        public DateTimeOffset IssuedAt { get; set; }

        /// <summary>
        /// 存取權杖的到期時間,等於 <see cref="IssuedAt"/> 加上 <see cref="ExpiresIn"/> 秒。
        /// When the access token expires: <see cref="IssuedAt"/> plus <see cref="ExpiresIn"/> seconds.
        /// </summary>
        [JsonIgnore]
        public DateTimeOffset ExpiresAt
        {
            get { return IssuedAt.AddSeconds(ExpiresIn); }
        }

        /// <summary>
        /// 將 <see cref="Scope"/> 以空白切開。
        /// Splits <see cref="Scope"/> on spaces.
        /// </summary>
        /// <returns>範圍陣列;<see cref="Scope"/> 為 null 或空白時回傳空陣列。The scopes, or an empty array when <see cref="Scope"/> is null or blank.</returns>
        public string[] GetScopes()
        {
            if (string.IsNullOrWhiteSpace(Scope))
                return Array.Empty<string>();

            return Scope!.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
