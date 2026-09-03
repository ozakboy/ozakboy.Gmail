namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// messages.attachments.get 的原始回應。內容以 base64url 字串傳回,對外才解碼成位元組。
    /// The raw messages.attachments.get response. The content arrives as a base64url string and is decoded before it is handed to the caller.
    /// </summary>
    internal sealed class GmailAttachmentBody
    {
        /// <summary>
        /// 附件識別碼。
        /// The attachment id.
        /// </summary>
        public string? AttachmentId { get; set; }

        /// <summary>
        /// Gmail 回報的附件大小(位元組)。
        /// The attachment size in bytes as reported by Gmail.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// base64url 編碼的附件內容。
        /// The base64url encoded attachment content.
        /// </summary>
        public string? Data { get; set; }
    }
}
