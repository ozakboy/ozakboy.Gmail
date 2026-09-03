namespace Ozakboy.Gmail
{
    /// <summary>
    /// 增量同步時要取回的歷程事件類型。
    /// The kinds of history events to fetch during an incremental sync.
    /// </summary>
    public enum GmailHistoryType
    {
        /// <summary>
        /// 新郵件進入信箱(對應 messageAdded)。
        /// A message arrived in the mailbox (messageAdded).
        /// </summary>
        MessageAdded = 0,

        /// <summary>
        /// 郵件被刪除(對應 messageDeleted)。
        /// A message was deleted (messageDeleted).
        /// </summary>
        MessageDeleted = 1,

        /// <summary>
        /// 郵件被加上標籤(對應 labelAdded)。
        /// A label was added to a message (labelAdded).
        /// </summary>
        LabelAdded = 2,

        /// <summary>
        /// 郵件被移除標籤(對應 labelRemoved)。
        /// A label was removed from a message (labelRemoved).
        /// </summary>
        LabelRemoved = 3,
    }
}
