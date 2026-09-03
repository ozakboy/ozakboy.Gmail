using System.Collections.Generic;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// 單筆歷程紀錄。同一筆紀錄可能同時包含新增、刪除與標籤異動。
    /// A single history record. One record may carry additions, deletions and label changes at the same time.
    /// </summary>
    public class GmailHistoryRecord
    {
        /// <summary>
        /// 歷程識別碼,Gmail 未回傳時為 null。
        /// The history id; null when Gmail did not return it.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// 這筆歷程涉及的郵件,永不為 null。
        /// The messages this record refers to; never null.
        /// </summary>
        public List<GmailMessageRef> Messages { get; set; } = new List<GmailMessageRef>();

        /// <summary>
        /// 新增的郵件,永不為 null。
        /// The messages that were added; never null.
        /// </summary>
        public List<GmailHistoryMessageChange> MessagesAdded { get; set; } = new List<GmailHistoryMessageChange>();

        /// <summary>
        /// 刪除的郵件,永不為 null。
        /// The messages that were deleted; never null.
        /// </summary>
        public List<GmailHistoryMessageChange> MessagesDeleted { get; set; } = new List<GmailHistoryMessageChange>();

        /// <summary>
        /// 被加上標籤的郵件,永不為 null。
        /// The messages that had labels added; never null.
        /// </summary>
        public List<GmailHistoryLabelChange> LabelsAdded { get; set; } = new List<GmailHistoryLabelChange>();

        /// <summary>
        /// 被移除標籤的郵件,永不為 null。
        /// The messages that had labels removed; never null.
        /// </summary>
        public List<GmailHistoryLabelChange> LabelsRemoved { get; set; } = new List<GmailHistoryLabelChange>();
    }
}
