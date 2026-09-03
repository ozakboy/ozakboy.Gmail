using System;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// 寄件用的電子郵件地址,含選填的顯示名稱。建構時即驗證格式,之後不可變。
    /// An email address for outgoing mail, with an optional display name. Validated on construction and immutable afterwards.
    /// </summary>
    public class GmailAddress
    {
        /// <summary>
        /// 只有地址、沒有顯示名稱。
        /// Creates an address without a display name.
        /// </summary>
        /// <param name="address">電子郵件地址;null、空白、缺 '@' 或含空白 / 換行時擲出例外。The email address; null, blank, missing '@' or containing whitespace / line breaks throws.</param>
        /// <exception cref="ArgumentException"><paramref name="address"/> 格式不合法時擲出。Thrown when <paramref name="address"/> is not a usable address.</exception>
        public GmailAddress(string address)
            : this(address, null)
        {
        }

        /// <summary>
        /// 地址加顯示名稱。
        /// Creates an address with a display name.
        /// </summary>
        /// <param name="address">電子郵件地址;null、空白、缺 '@' 或含空白 / 換行時擲出例外。The email address; null, blank, missing '@' or containing whitespace / line breaks throws.</param>
        /// <param name="name">顯示名稱;null 或空白視為沒有名稱,含換行字元時擲出例外。The display name; null or blank means no name, line breaks throw.</param>
        /// <exception cref="ArgumentException">地址不合法,或名稱含 CR / LF 時擲出。Thrown when the address is invalid or the name contains CR / LF.</exception>
        public GmailAddress(string address, string? name)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("電子郵件地址不可為 null 或空白。The email address cannot be null or blank.", nameof(address));

            var trimmed = address.Trim();
            if (trimmed.IndexOf('@') <= 0 || trimmed.IndexOf('@') == trimmed.Length - 1)
                throw new ArgumentException("電子郵件地址必須是 local@domain 的形式。The email address has to look like local@domain.", nameof(address));

            foreach (var c in trimmed)
            {
                if (char.IsWhiteSpace(c) || c == '<' || c == '>' || c == ',' || c == '"')
                    throw new ArgumentException("電子郵件地址不可含空白、換行或 < > , \" 字元。The email address cannot contain whitespace, line breaks or the characters < > , \".", nameof(address));
            }

            if (name != null && (name.IndexOf('\r') >= 0 || name.IndexOf('\n') >= 0))
                throw new ArgumentException("顯示名稱不可含換行字元。The display name cannot contain line breaks.", nameof(name));

            Address = trimmed;
            Name = string.IsNullOrWhiteSpace(name) ? null : name!.Trim();
        }

        /// <summary>
        /// 電子郵件地址(已去除前後空白)。
        /// The email address, trimmed.
        /// </summary>
        public string Address { get; }

        /// <summary>
        /// 顯示名稱;沒有名稱時為 null。
        /// The display name, or null when there is none.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// 顯示用字串:「名稱 &lt;地址&gt;」或純地址。不做 RFC 2047 編碼,請勿直接寫進郵件標頭。
        /// A display string: "Name &lt;address&gt;" or the bare address. Not RFC 2047 encoded — do not write it into a mail header directly.
        /// </summary>
        /// <returns>顯示用字串。The display string.</returns>
        public override string ToString()
        {
            return Name == null ? Address : Name + " <" + Address + ">";
        }
    }
}
