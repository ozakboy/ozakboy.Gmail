namespace Ozakboy.Gmail
{
    /// <summary>
    /// 標籤顏色。兩個顏色都必須是 Gmail 允許的色票值,否則 Gmail 會回 400。
    /// A label's colours. Both values have to come from Gmail's allowed palette or Gmail answers 400.
    /// </summary>
    public class GmailLabelColor
    {
        /// <summary>
        /// 底色,格式為 "#rrggbb"。
        /// The background colour in "#rrggbb" form.
        /// </summary>
        public string? BackgroundColor { get; set; }

        /// <summary>
        /// 文字顏色,格式為 "#rrggbb"。
        /// The text colour in "#rrggbb" form.
        /// </summary>
        public string? TextColor { get; set; }
    }
}
