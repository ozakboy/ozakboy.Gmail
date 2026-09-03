using Ozakboy.Gmail.Core;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// MIME 區段的內容。內嵌內容放在 <see cref="Data"/>,附件則只給 <see cref="AttachmentId"/>。
    /// The body of a MIME part. Inline content arrives in <see cref="Data"/>; attachments only carry an <see cref="AttachmentId"/>.
    /// </summary>
    public class GmailMessagePartBody
    {
        /// <summary>
        /// 附件識別碼,內容非內嵌時才有值;要取得內容請呼叫 GetAttachmentAsync。
        /// The attachment id, present only when the content is not inline; fetch the content with GetAttachmentAsync.
        /// </summary>
        public string? AttachmentId { get; set; }

        /// <summary>
        /// 內容大小(位元組)。
        /// The content size in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// base64url 編碼的內嵌內容,非內嵌時為 null。
        /// The base64url encoded inline content; null when the content is not inline.
        /// </summary>
        public string? Data { get; set; }

        /// <summary>
        /// 把 <see cref="Data"/> 從 base64url 解碼成位元組。
        /// Decodes <see cref="Data"/> from base64url into bytes.
        /// </summary>
        /// <returns>解碼後的內容;<see cref="Data"/> 為 null 時回傳 null。The decoded content, or null when <see cref="Data"/> is null.</returns>
        /// <exception cref="System.FormatException"><see cref="Data"/> 不是合法的 base64url 時擲出。Thrown when <see cref="Data"/> is not valid base64url.</exception>
        public byte[]? DecodeData()
        {
            return Data == null ? null : Base64Url.Decode(Data);
        }
    }
}
