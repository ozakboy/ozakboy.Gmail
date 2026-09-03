namespace Ozakboy.Gmail
{
    /// <summary>
    /// 歷程中的郵件新增或刪除事件。
    /// A message addition or deletion inside a history record.
    /// </summary>
    public class GmailHistoryMessageChange
    {
        /// <summary>
        /// 事件對應的郵件;Gmail 只會填入 Id、ThreadId 與 LabelIds。
        /// The message this change refers to; Gmail only populates Id, ThreadId and LabelIds.
        /// </summary>
        public GmailMessage? Message { get; set; }
    }
}
