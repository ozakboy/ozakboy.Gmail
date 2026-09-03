namespace Ozakboy.Gmail
{
    /// <summary>
    /// 郵件中一個附件區段的摘要,由 <see cref="GmailMessage.GetAttachments"/> 從已解析的 MIME 結構整理而來。
    /// A summary of one attachment part, produced by <see cref="GmailMessage.GetAttachments"/> from the MIME tree Gmail already parsed.
    /// </summary>
    /// <remarks>
    /// 這個型別只描述附件,不含內容:<see cref="AttachmentId"/> 有值時要另外呼叫 GetAttachmentAsync 取內容;
    /// 為 null 表示 Gmail 已把內容直接放在 <c>Part.Body.Data</c>,解碼即可使用。
    /// The type describes an attachment without carrying its bytes: when <see cref="AttachmentId"/> is set, fetch the content with GetAttachmentAsync;
    /// when it is null Gmail inlined the content in <c>Part.Body.Data</c>, which only needs decoding.
    /// </remarks>
    public class GmailAttachmentInfo
    {
        /// <summary>
        /// 區段識別碼,Gmail 未回傳時為 null。
        /// The part id; null when Gmail did not return it.
        /// </summary>
        public string? PartId { get; set; }

        /// <summary>
        /// 附件檔名,Gmail 未回傳時為 null;內嵌圖片可能是空字串。
        /// The attachment file name; null when Gmail did not return it, and possibly an empty string for an inline image.
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// 附件的 MIME 類型(例如 application/pdf),Gmail 未回傳時為 null。
        /// The attachment's MIME type (application/pdf…); null when Gmail did not return it.
        /// </summary>
        public string? MimeType { get; set; }

        /// <summary>
        /// 附件大小(位元組),取自區段的 Body.Size;沒有 Body 時為 0。
        /// The attachment size in bytes, taken from the part's Body.Size; 0 when the part has no body.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// 附件識別碼,只有需要另外下載的附件才有;為 null 表示內容已直接放在 <see cref="Part"/> 的 Body.Data。
        /// The attachment id, present only when the content has to be fetched separately; null means the content is already in <see cref="Part"/>'s Body.Data.
        /// </summary>
        public string? AttachmentId { get; set; }

        /// <summary>
        /// Content-ID 標頭值(已去掉前後角括號),用來對應 HTML 內文的 cid: 參照;沒有這個標頭時為 null。
        /// The Content-ID header with its angle brackets stripped, matching the cid: references in an HTML body; null when the header is absent.
        /// </summary>
        public string? ContentId { get; set; }

        /// <summary>
        /// 原始的 MIME 區段,需要標頭或內嵌內容時可直接使用;永不為 null。
        /// The original MIME part, for reading headers or inline content; never null.
        /// </summary>
        public GmailMessagePart? Part { get; set; }
    }
}
