namespace Ozakboy.Gmail
{
    /// <summary>
    /// 一個 Gmail 標籤,可能是系統標籤(INBOX、SPAM…)或使用者自訂標籤。
    /// A Gmail label, either a system label (INBOX, SPAM…) or a user label.
    /// </summary>
    public class GmailLabel
    {
        /// <summary>
        /// 標籤識別碼,Gmail 未回傳時為 null。
        /// The label id; null when Gmail did not return it.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// 標籤名稱,巢狀標籤以 '/' 分隔。
        /// The label name; nested labels are separated with '/'.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 標籤類型:"system" 或 "user"。
        /// The label type: "system" or "user".
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// 在郵件列表中的顯示方式:"show" 或 "hide"。
        /// Visibility in the message list: "show" or "hide".
        /// </summary>
        public string? MessageListVisibility { get; set; }

        /// <summary>
        /// 在標籤列中的顯示方式:"labelShow"、"labelShowIfUnread" 或 "labelHide"。
        /// Visibility in the label list: "labelShow", "labelShowIfUnread" or "labelHide".
        /// </summary>
        public string? LabelListVisibility { get; set; }

        /// <summary>
        /// 標籤下的郵件總數,只有 labels.get 與 labels.create 的回應才有值。
        /// The total message count; only present on labels.get and labels.create responses.
        /// </summary>
        public long? MessagesTotal { get; set; }

        /// <summary>
        /// 標籤下的未讀郵件數,只有 labels.get 與 labels.create 的回應才有值。
        /// The unread message count; only present on labels.get and labels.create responses.
        /// </summary>
        public long? MessagesUnread { get; set; }

        /// <summary>
        /// 標籤下的討論串總數,只有 labels.get 與 labels.create 的回應才有值。
        /// The total thread count; only present on labels.get and labels.create responses.
        /// </summary>
        public long? ThreadsTotal { get; set; }

        /// <summary>
        /// 標籤下的未讀討論串數,只有 labels.get 與 labels.create 的回應才有值。
        /// The unread thread count; only present on labels.get and labels.create responses.
        /// </summary>
        public long? ThreadsUnread { get; set; }

        /// <summary>
        /// 標籤顏色,未設定時為 null。
        /// The label colours; null when unset.
        /// </summary>
        public GmailLabelColor? Color { get; set; }
    }
}
