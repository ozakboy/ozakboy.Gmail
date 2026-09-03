namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// labels.create 與 labels.patch 共用的請求主體。為 null 的欄位不會被寫進 JSON,對 patch 而言等同「維持原值」。
    /// The request body shared by labels.create and labels.patch. Null fields are omitted from the JSON, which for a patch means "leave unchanged".
    /// </summary>
    internal sealed class GmailLabelWriteRequest
    {
        /// <summary>
        /// 標籤名稱,巢狀標籤以 '/' 分隔。
        /// The label name; nested labels are separated with '/'.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 標籤在標籤列的顯示方式。
        /// How the label shows in the label list.
        /// </summary>
        public string? LabelListVisibility { get; set; }

        /// <summary>
        /// 標籤在郵件列表的顯示方式。
        /// How the label shows in the message list.
        /// </summary>
        public string? MessageListVisibility { get; set; }

        /// <summary>
        /// 標籤顏色,底色與文字色必須成對設定。
        /// The label colour; the background and text colours have to be set together.
        /// </summary>
        public GmailLabelColor? Color { get; set; }
    }
}
