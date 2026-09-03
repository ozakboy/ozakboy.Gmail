namespace Ozakboy.Gmail
{
    /// <summary>
    /// 取得郵件時要求 Gmail 回傳多少內容。
    /// How much of a message Gmail should return when it is fetched.
    /// </summary>
    public enum GmailMessageFormat
    {
        /// <summary>
        /// 完整郵件內容,含 MIME 結構(對應 format=full)。
        /// The full message including its MIME structure (format=full).
        /// </summary>
        Full = 0,

        /// <summary>
        /// 只回標頭與摘要,不含內文(對應 format=metadata)。
        /// Headers and snippet only, without the body (format=metadata).
        /// </summary>
        Metadata = 1,

        /// <summary>
        /// 只回識別碼、標籤與摘要(對應 format=minimal)。
        /// Ids, labels and snippet only (format=minimal).
        /// </summary>
        Minimal = 2,

        /// <summary>
        /// 回傳 base64url 編碼的完整 RFC 822 郵件(對應 format=raw)。
        /// The complete RFC 822 message in base64url (format=raw).
        /// </summary>
        Raw = 3,
    }
}
