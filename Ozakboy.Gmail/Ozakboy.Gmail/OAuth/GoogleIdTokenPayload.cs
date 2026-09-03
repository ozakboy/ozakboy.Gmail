using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Ozakboy.Gmail.Core;

namespace Ozakboy.Gmail.OAuth
{
    /// <summary>
    /// Google id_token(JWT)酬載中的標準宣告。
    /// The standard claims carried in the payload of a Google id_token (a JWT).
    /// </summary>
    /// <remarks>
    /// <see cref="Parse(string)"/> <b>不會驗章</b>。只有在權杖是剛剛透過 TLS 從 Google 權杖端點拿到的
    /// (也就是 <see cref="GoogleTokenResponse.IdToken"/>)才可以這樣用;
    /// 由瀏覽器或第三方交過來的權杖必須另外做完整驗證。
    /// <see cref="Parse(string)"/> <b>does not verify the signature</b>. That is only safe for a token received
    /// directly from Google's token endpoint over TLS (the <see cref="GoogleTokenResponse.IdToken"/>);
    /// a token handed over by a browser or a third party needs full validation elsewhere.
    /// </remarks>
    public class GoogleIdTokenPayload
    {
        /// <summary>
        /// "sub" 宣告:Google 帳號的穩定識別碼。
        /// The "sub" claim: the stable Google user id.
        /// </summary>
        public string? Subject { get; set; }

        /// <summary>
        /// "email" 宣告:使用者信箱。
        /// The "email" claim: the user's mailbox address.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// "email_verified" 宣告:信箱是否已驗證。
        /// The "email_verified" claim: whether the address has been verified.
        /// </summary>
        public bool EmailVerified { get; set; }

        /// <summary>
        /// "name" 宣告:顯示名稱。
        /// The "name" claim: the display name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// "picture" 宣告:大頭貼網址。
        /// The "picture" claim: the avatar URL.
        /// </summary>
        public string? Picture { get; set; }

        /// <summary>
        /// "hd" 宣告:Google Workspace 網域,一般帳號沒有這個宣告。
        /// The "hd" claim: the Google Workspace domain; absent for consumer accounts.
        /// </summary>
        public string? HostedDomain { get; set; }

        /// <summary>
        /// "iss" 宣告:簽發者。
        /// The "iss" claim: the issuer.
        /// </summary>
        public string? Issuer { get; set; }

        /// <summary>
        /// "aud" 宣告:對象;若權杖給的是陣列,取第一個值。
        /// The "aud" claim: the audience; the first entry is taken when the token carries an array.
        /// </summary>
        public string? Audience { get; set; }

        /// <summary>
        /// "iat" 宣告:簽發時間,缺少時為 null。
        /// The "iat" claim: when the token was issued; null when absent.
        /// </summary>
        public DateTimeOffset? IssuedAt { get; set; }

        /// <summary>
        /// "exp" 宣告:到期時間,缺少時為 null。
        /// The "exp" claim: when the token expires; null when absent.
        /// </summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>
        /// 解出 JWT 第二段(酬載)並讀取標準宣告。<b>不驗章</b>。
        /// Base64url-decodes the JWT's payload segment and reads the standard claims. <b>The signature is not verified.</b>
        /// </summary>
        /// <param name="idToken">Google 發的 id_token,null 或空白時擲出例外。The id_token issued by Google; null or blank throws.</param>
        /// <returns>解析後的宣告。The parsed claims.</returns>
        /// <exception cref="ArgumentException"><paramref name="idToken"/> 為 null 或空白時擲出。Thrown when <paramref name="idToken"/> is null or blank.</exception>
        /// <exception cref="FormatException">內容不是合法 JWT 或酬載不是合法 JSON 時擲出。Thrown when the input is not a valid JWT or the payload is not valid JSON.</exception>
        public static GoogleIdTokenPayload Parse(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
                throw new ArgumentException("id_token 不可為 null 或空白。The id_token cannot be null or blank.", nameof(idToken));

            var segments = idToken.Split('.');
            if (segments.Length < 2)
                throw new FormatException("id_token 不是合法的 JWT(至少要有標頭與酬載兩段)。The id_token is not a valid JWT; it needs at least a header and a payload segment.");

            byte[] payload;
            try
            {
                payload = Base64Url.Decode(segments[1]);
            }
            catch (FormatException ex)
            {
                throw new FormatException("id_token 的酬載不是合法的 base64url。The id_token payload is not valid base64url.", ex);
            }

            try
            {
                using (var document = JsonDocument.Parse(Encoding.UTF8.GetString(payload)))
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                        throw new FormatException("id_token 的酬載不是 JSON 物件。The id_token payload is not a JSON object.");

                    return new GoogleIdTokenPayload
                    {
                        Subject = ReadString(root, "sub"),
                        Email = ReadString(root, "email"),
                        EmailVerified = ReadBoolean(root, "email_verified"),
                        Name = ReadString(root, "name"),
                        Picture = ReadString(root, "picture"),
                        HostedDomain = ReadString(root, "hd"),
                        Issuer = ReadString(root, "iss"),
                        Audience = ReadAudience(root),
                        IssuedAt = ReadUnixTime(root, "iat"),
                        ExpiresAt = ReadUnixTime(root, "exp"),
                    };
                }
            }
            catch (JsonException ex)
            {
                throw new FormatException("id_token 的酬載不是合法 JSON。The id_token payload is not valid JSON.", ex);
            }
        }

        /// <summary>
        /// 讀取字串宣告,不存在或不是字串時回傳 null。
        /// Reads a string claim, returning null when it is absent or not a string.
        /// </summary>
        /// <param name="root">酬載物件。The payload object.</param>
        /// <param name="claim">宣告名稱。The claim name.</param>
        /// <returns>宣告值或 null。The claim value, or null.</returns>
        private static string? ReadString(JsonElement root, string claim)
        {
            if (root.TryGetProperty(claim, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();

            return null;
        }

        /// <summary>
        /// 讀取布林宣告。Google 有時把它寫成布林、有時寫成 "true" 字串,兩種都接受。
        /// Reads a boolean claim. Google sometimes writes a real boolean and sometimes the string "true"; both are accepted.
        /// </summary>
        /// <param name="root">酬載物件。The payload object.</param>
        /// <param name="claim">宣告名稱。The claim name.</param>
        /// <returns>宣告值,不存在時為 false。The claim value; false when absent.</returns>
        private static bool ReadBoolean(JsonElement root, string claim)
        {
            if (!root.TryGetProperty(claim, out var value))
                return false;

            if (value.ValueKind == JsonValueKind.True)
                return true;

            if (value.ValueKind == JsonValueKind.False)
                return false;

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                return bool.TryParse(text, out var parsed) && parsed;
            }

            return false;
        }

        /// <summary>
        /// 讀取 "aud" 宣告,權杖給陣列時取第一個字串。
        /// Reads the "aud" claim, taking the first string when the token carries an array.
        /// </summary>
        /// <param name="root">酬載物件。The payload object.</param>
        /// <returns>對象值或 null。The audience, or null.</returns>
        private static string? ReadAudience(JsonElement root)
        {
            if (!root.TryGetProperty("aud", out var value))
                return null;

            if (value.ValueKind == JsonValueKind.String)
                return value.GetString();

            if (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0)
            {
                var first = value[0];
                return first.ValueKind == JsonValueKind.String ? first.GetString() : null;
            }

            return null;
        }

        /// <summary>
        /// 讀取 Unix 秒數宣告,數字與字串數字都接受。
        /// Reads a Unix-seconds claim, accepting both a number and a numeric string.
        /// </summary>
        /// <param name="root">酬載物件。The payload object.</param>
        /// <param name="claim">宣告名稱。The claim name.</param>
        /// <returns>時間點,無法取得時為 null。The point in time, or null when unavailable.</returns>
        private static DateTimeOffset? ReadUnixTime(JsonElement root, string claim)
        {
            if (!root.TryGetProperty(claim, out var value))
                return null;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds))
                return DateTimeOffset.FromUnixTimeSeconds(seconds);

            if (value.ValueKind == JsonValueKind.String
                && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return DateTimeOffset.FromUnixTimeSeconds(parsed);
            }

            return null;
        }
    }
}
