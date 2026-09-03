namespace Ozakboy.Gmail
{
    /// <summary>
    /// 信箱基本資料,對應 Gmail 的 users.getProfile 回應。
    /// The mailbox profile returned by Gmail's users.getProfile.
    /// </summary>
    /// <remarks>
    /// <see cref="HistoryId"/> 是首次全量抓取前要先存下來的值,之後才能用它做增量同步。
    /// <see cref="HistoryId"/> is the value to store before the first backfill so incremental sync can start from it later.
    /// </remarks>
    public class GmailProfile
    {
        /// <summary>
        /// 信箱位址,Gmail 未回傳時為 null。
        /// The mailbox address; null when Gmail did not return it.
        /// </summary>
        public string? EmailAddress { get; set; }

        /// <summary>
        /// 信箱內的郵件總數。
        /// The total number of messages in the mailbox.
        /// </summary>
        public long MessagesTotal { get; set; }

        /// <summary>
        /// 信箱內的討論串總數。
        /// The total number of threads in the mailbox.
        /// </summary>
        public long ThreadsTotal { get; set; }

        /// <summary>
        /// 信箱目前的歷程識別碼,Gmail 未回傳時為 null。
        /// The mailbox's current history id; null when Gmail did not return it.
        /// </summary>
        public string? HistoryId { get; set; }
    }
}
