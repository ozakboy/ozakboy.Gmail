using System.Collections.Generic;

namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// labels.list 的回應外殼,Gmail 把標籤包在 labels 陣列裡。
    /// The labels.list response envelope; Gmail wraps the labels in a labels array.
    /// </summary>
    internal sealed class GmailLabelListResponse
    {
        /// <summary>
        /// 標籤清單,永不為 null。
        /// The label list; never null.
        /// </summary>
        public List<GmailLabel> Labels { get; set; } = new List<GmailLabel>();
    }
}
