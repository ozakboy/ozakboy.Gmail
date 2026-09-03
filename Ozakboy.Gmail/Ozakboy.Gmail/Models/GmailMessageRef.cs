namespace Ozakboy.Gmail
{
    /// <summary>
    /// 郵件參考,只帶識別碼與所屬討論串;列表端點回傳的就是這種輕量物件。
    /// A lightweight message reference carrying only the ids; this is what the list endpoints return.
    /// </summary>
    public class GmailMessageRef
    {
        /// <summary>
        /// 郵件識別碼,Gmail 未回傳時為 null。
        /// The message id; null when Gmail did not return it.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// 所屬討論串識別碼,Gmail 未回傳時為 null。
        /// The id of the thread the message belongs to; null when Gmail did not return it.
        /// </summary>
        public string? ThreadId { get; set; }
    }
}
