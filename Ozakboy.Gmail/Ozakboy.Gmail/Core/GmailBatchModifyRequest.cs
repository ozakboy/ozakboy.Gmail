using System.Collections.Generic;

namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// messages.batchModify 的請求主體。為 null 的清單不會被寫進 JSON。
    /// The messages.batchModify request body. Null lists are omitted from the JSON.
    /// </summary>
    internal sealed class GmailBatchModifyRequest
    {
        /// <summary>
        /// 要套用變更的郵件識別碼,Gmail 單次最多 1000 筆。
        /// The message ids to change; Gmail accepts at most 1000 per call.
        /// </summary>
        public List<string> Ids { get; set; } = new List<string>();

        /// <summary>
        /// 要加上的標籤識別碼。
        /// The label ids to add.
        /// </summary>
        public List<string>? AddLabelIds { get; set; }

        /// <summary>
        /// 要移除的標籤識別碼。
        /// The label ids to remove.
        /// </summary>
        public List<string>? RemoveLabelIds { get; set; }
    }
}
