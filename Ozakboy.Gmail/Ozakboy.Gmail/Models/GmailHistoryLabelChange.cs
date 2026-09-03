using System.Collections.Generic;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// 歷程中的標籤異動事件。
    /// A label change inside a history record.
    /// </summary>
    public class GmailHistoryLabelChange
    {
        /// <summary>
        /// 異動對應的郵件;Gmail 只會填入 Id、ThreadId 與 LabelIds。
        /// The message this change refers to; Gmail only populates Id, ThreadId and LabelIds.
        /// </summary>
        public GmailMessage? Message { get; set; }

        /// <summary>
        /// 本次被加上或移除的標籤識別碼,永不為 null。
        /// The label ids that were added or removed; never null.
        /// </summary>
        public List<string> LabelIds { get; set; } = new List<string>();
    }
}
