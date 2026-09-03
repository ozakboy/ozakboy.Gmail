namespace Ozakboy.Gmail
{
    /// <summary>
    /// 建立或更新標籤時的可選設定。每個欄位為 null 代表「不送這個欄位」:建立時採用 Gmail 預設,更新時維持原值。
    /// Optional settings for creating or updating a label. A null field is simply not sent: Gmail's default applies on create, and the current value is kept on update.
    /// </summary>
    public class GmailLabelOptions
    {
        /// <summary>
        /// 在標籤列中的顯示方式:"labelShow"、"labelShowIfUnread" 或 "labelHide";null 表示採用 Gmail 預設 "labelShow"。
        /// Visibility in the label list: "labelShow", "labelShowIfUnread" or "labelHide"; null means Gmail's default of "labelShow".
        /// </summary>
        public string? LabelListVisibility { get; set; }

        /// <summary>
        /// 在郵件列表中的顯示方式:"show" 或 "hide";null 表示採用 Gmail 預設 "show"。
        /// Visibility in the message list: "show" or "hide"; null means Gmail's default of "show".
        /// </summary>
        public string? MessageListVisibility { get; set; }

        /// <summary>
        /// 標籤底色,必須與 <see cref="TextColor"/> 成對設定,否則 Gmail 會回 400。
        /// The label background colour; it has to be set together with <see cref="TextColor"/> or Gmail answers 400.
        /// </summary>
        public string? BackgroundColor { get; set; }

        /// <summary>
        /// 標籤文字顏色,必須與 <see cref="BackgroundColor"/> 成對設定,否則 Gmail 會回 400。
        /// The label text colour; it has to be set together with <see cref="BackgroundColor"/> or Gmail answers 400.
        /// </summary>
        public string? TextColor { get; set; }
    }
}
