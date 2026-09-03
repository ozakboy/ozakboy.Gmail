using System;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// 寄件用的附件:檔名、內容型別與已載入記憶體的內容。
    /// An attachment for outgoing mail: file name, content type and the in-memory content.
    /// </summary>
    public class GmailAttachmentContent
    {
        private byte[] _content = Array.Empty<byte>();

        /// <summary>
        /// 收件者看到的檔名。空白時序列化會改用 "attachment"。
        /// The file name recipients see. A blank value is written as "attachment".
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// MIME 內容型別,例如 application/pdf。空白時退回 application/octet-stream。
        /// The MIME content type, for example application/pdf. A blank value falls back to application/octet-stream.
        /// </summary>
        public string ContentType { get; set; } = "application/octet-stream";

        /// <summary>
        /// 附件內容。永不為 null——設為 null 會存成空陣列。
        /// The attachment bytes. Never null — assigning null stores an empty array.
        /// </summary>
        public byte[] Content
        {
            get { return _content; }
            set { _content = value ?? Array.Empty<byte>(); }
        }
    }
}
