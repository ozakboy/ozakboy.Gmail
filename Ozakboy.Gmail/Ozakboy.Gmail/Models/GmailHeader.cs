namespace Ozakboy.Gmail
{
    /// <summary>
    /// 郵件標頭的名稱與值。
    /// A message header's name and value.
    /// </summary>
    public class GmailHeader
    {
        /// <summary>
        /// 標頭名稱(例如 From、Subject),Gmail 未回傳時為 null。
        /// The header name (From, Subject…); null when Gmail did not return it.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 標頭值,Gmail 未回傳時為 null。
        /// The header value; null when Gmail did not return it.
        /// </summary>
        public string? Value { get; set; }
    }
}
