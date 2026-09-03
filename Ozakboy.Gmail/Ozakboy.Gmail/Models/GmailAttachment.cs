using System;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// 一個已下載並解碼完成的附件。
    /// A downloaded attachment with its content already decoded.
    /// </summary>
    /// <remarks>
    /// Gmail 是把附件包成 base64url 放在 JSON 主體回傳的,所以整個附件一定會先進記憶體,線路上沒有真正的串流。
    /// Gmail returns attachments as base64url inside a JSON body, so the whole attachment is buffered in memory once; there is no true streaming on the wire.
    /// </remarks>
    public class GmailAttachment
    {
        /// <summary>
        /// 附件識別碼,Gmail 未回傳時為 null。
        /// The attachment id; null when Gmail did not return it.
        /// </summary>
        public string? AttachmentId { get; set; }

        /// <summary>
        /// Gmail 回報的附件大小(位元組)。
        /// The attachment size in bytes as reported by Gmail.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// 解碼後的附件內容,永不為 null;Gmail 沒有回傳內容時為空陣列。
        /// The decoded attachment content; never null, and an empty array when Gmail returned no data.
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }
}
