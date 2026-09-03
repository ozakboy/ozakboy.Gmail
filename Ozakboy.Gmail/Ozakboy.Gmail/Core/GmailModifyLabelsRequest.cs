using System.Collections.Generic;

namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// messages.modify 的請求主體。為 null 的清單不會被寫進 JSON。
    /// The messages.modify request body. Null lists are omitted from the JSON.
    /// </summary>
    internal sealed class GmailModifyLabelsRequest
    {
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
