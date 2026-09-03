using System;
using System.Collections.Generic;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// 郵件的 MIME 區段。多段郵件的子區段放在 <see cref="Parts"/>,形成樹狀結構。
    /// A MIME part of a message. Sub-parts of a multipart message live in <see cref="Parts"/>, forming a tree.
    /// </summary>
    public class GmailMessagePart
    {
        /// <summary>
        /// 區段識別碼,Gmail 未回傳時為 null。
        /// The part id; null when Gmail did not return it.
        /// </summary>
        public string? PartId { get; set; }

        /// <summary>
        /// 區段的 MIME 類型(例如 text/plain),Gmail 未回傳時為 null。
        /// The part's MIME type (text/plain…); null when Gmail did not return it.
        /// </summary>
        public string? MimeType { get; set; }

        /// <summary>
        /// 附件檔名,非附件區段通常為空字串。
        /// The attachment file name; usually an empty string for parts that are not attachments.
        /// </summary>
        public string? Filename { get; set; }

        /// <summary>
        /// 本區段的標頭清單,永不為 null。
        /// The part's headers; never null.
        /// </summary>
        public List<GmailHeader> Headers { get; set; } = new List<GmailHeader>();

        /// <summary>
        /// 本區段的內容,Gmail 未回傳時為 null。
        /// The part's body; null when Gmail did not return it.
        /// </summary>
        public GmailMessagePartBody? Body { get; set; }

        /// <summary>
        /// 子區段清單,永不為 null;單一區段郵件為空清單。
        /// The child parts; never null, and empty for a single-part message.
        /// </summary>
        public List<GmailMessagePart> Parts { get; set; } = new List<GmailMessagePart>();

        /// <summary>
        /// 以不分大小寫的方式取出第一個符合名稱的標頭值。
        /// Looks up the first header with the given name, ignoring case.
        /// </summary>
        /// <param name="name">標頭名稱,null 或空字串一律回傳 null。The header name; null or empty always yields null.</param>
        /// <returns>標頭值,找不到時為 null。The header value, or null when no header matches.</returns>
        public string? GetHeader(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            foreach (var header in Headers)
            {
                if (header != null && string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
                    return header.Value;
            }

            return null;
        }
    }
}
