using System;

namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// base64url(RFC 4648 §5)編解碼工具。Gmail 的 raw 郵件、附件內容與 JWT 區段都使用這個變體。
    /// base64url (RFC 4648 §5) helpers. Gmail uses this variant for raw messages, attachment payloads and JWT segments.
    /// </summary>
    /// <remarks>
    /// 內部型別,不屬於公開 API。
    /// Internal type; not part of the public API surface.
    /// </remarks>
    internal static class Base64Url
    {
        /// <summary>
        /// 將位元組陣列編碼為 base64url 字串(不含結尾的 '=' 填補)。
        /// Encodes a byte array as a base64url string without trailing '=' padding.
        /// </summary>
        /// <param name="bytes">待編碼的位元組。The bytes to encode.</param>
        /// <returns>base64url 字串。The base64url string.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> 為 null 時擲出。Thrown when <paramref name="bytes"/> is null.</exception>
        internal static string Encode(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// 將 base64url 字串解碼為位元組陣列,會自動補回省略的 '=' 填補。
        /// Decodes a base64url string into bytes, restoring the omitted '=' padding automatically.
        /// </summary>
        /// <param name="value">base64url 字串。The base64url string.</param>
        /// <returns>解碼後的位元組。The decoded bytes.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> 為 null 時擲出。Thrown when <paramref name="value"/> is null.</exception>
        /// <exception cref="FormatException">內容不是合法的 base64url 時擲出。Thrown when the content is not valid base64url.</exception>
        internal static byte[] Decode(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Length == 0)
                return Array.Empty<byte>();

            var normalized = value.Replace('-', '+').Replace('_', '/');

            // 補回 base64 需要的 '=' 填補,長度必須是 4 的倍數
            // Restore the '=' padding base64 requires; the length has to be a multiple of four.
            switch (normalized.Length % 4)
            {
                case 2:
                    normalized += "==";
                    break;
                case 3:
                    normalized += "=";
                    break;
                default:
                    break;
            }

            return Convert.FromBase64String(normalized);
        }
    }
}
