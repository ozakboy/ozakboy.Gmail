using System.Collections.Generic;
using Ozakboy.Gmail.Core;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// 要寄出的郵件。由內建的 RFC 822 組信器序列化,不依賴任何 MIME 函式庫。
    /// An outgoing message, serialised by the built-in RFC 822 writer with no MIME library involved.
    /// </summary>
    /// <remarks>
    /// 支援純文字與 HTML 內文(兩者都有時用 multipart/alternative)、附件(multipart/mixed)、Reply-To、Bcc、討論串標頭與額外標頭。
    /// 不支援內嵌圖片(cid:)、S/MIME 與巢狀郵件;需要這些請自行組好 RFC 822 bytes 改走 <see cref="IGmailClient.SendRawAsync"/>。
    /// Supports plain-text and HTML bodies (multipart/alternative when both are set), attachments (multipart/mixed), Reply-To, Bcc, threading headers and extra headers.
    /// Inline images (cid:), S/MIME and nested messages are not supported; compose the RFC 822 bytes yourself and use <see cref="IGmailClient.SendRawAsync"/> for those.
    /// </remarks>
    public class GmailOutgoingMessage
    {
        /// <summary>
        /// 寄件者。null 時不寫 From 標頭,Gmail 會填入已授權的信箱。
        /// The sender. When null no From header is written and Gmail fills in the authenticated mailbox.
        /// </summary>
        public GmailAddress? From { get; set; }

        /// <summary>
        /// 主要收件者,永不為 null。
        /// The primary recipients; never null.
        /// </summary>
        public List<GmailAddress> To { get; } = new List<GmailAddress>();

        /// <summary>
        /// 副本收件者,永不為 null。
        /// The Cc recipients; never null.
        /// </summary>
        public List<GmailAddress> Cc { get; } = new List<GmailAddress>();

        /// <summary>
        /// 密件副本收件者,永不為 null。Gmail 會依這個標頭投遞並在送出時移除它。
        /// The Bcc recipients; never null. Gmail delivers to them and strips the header on the way out.
        /// </summary>
        public List<GmailAddress> Bcc { get; } = new List<GmailAddress>();

        /// <summary>
        /// 回覆地址;null 時不寫 Reply-To 標頭。
        /// The reply address; null writes no Reply-To header.
        /// </summary>
        public GmailAddress? ReplyTo { get; set; }

        /// <summary>
        /// 主旨;null 或空字串時不寫 Subject 標頭。非 ASCII 會以 RFC 2047 編碼。
        /// The subject; null or empty writes no Subject header. Non-ASCII text is RFC 2047 encoded.
        /// </summary>
        public string? Subject { get; set; }

        /// <summary>
        /// 純文字內文;null 表示沒有純文字版本。
        /// The plain-text body; null means there is no plain-text version.
        /// </summary>
        public string? TextBody { get; set; }

        /// <summary>
        /// HTML 內文;null 表示沒有 HTML 版本。
        /// The HTML body; null means there is no HTML version.
        /// </summary>
        public string? HtmlBody { get; set; }

        /// <summary>
        /// 附件,永不為 null。
        /// The attachments; never null.
        /// </summary>
        public List<GmailAttachmentContent> Attachments { get; } = new List<GmailAttachmentContent>();

        /// <summary>
        /// 被回覆那封信的 Message-ID。缺角括號時序列化會自動補上;null 或空白時不寫 In-Reply-To。
        /// The Message-ID of the message being replied to. Angle brackets are added when missing; null or blank writes no In-Reply-To.
        /// </summary>
        public string? InReplyTo { get; set; }

        /// <summary>
        /// 討論串裡前幾封信的 Message-ID,永不為 null。缺角括號時序列化會自動補上。
        /// The Message-IDs of earlier messages in the thread; never null. Angle brackets are added when missing.
        /// </summary>
        public List<string> References { get; } = new List<string>();

        /// <summary>
        /// 額外標頭(例如 X-… 自訂標頭),永不為 null。名稱與組信器自己產生的標頭相同(不分大小寫)時,序列化會擲出例外。
        /// Extra headers (X-… custom headers, say); never null. A name that clashes with a header the writer generates itself (case-insensitive) makes serialisation throw.
        /// </summary>
        public List<GmailHeader> Headers { get; } = new List<GmailHeader>();

        /// <summary>
        /// 序列化成 RFC 822 郵件的 UTF-8 位元組,正是 <see cref="IGmailClient.SendAsync"/> 會上傳的內容。
        /// Serialises the message into RFC 822 bytes — exactly what <see cref="IGmailClient.SendAsync"/> uploads.
        /// </summary>
        /// <returns>RFC 822 郵件內容。The RFC 822 message.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// <see cref="To"/>、<see cref="Cc"/>、<see cref="Bcc"/> 全空,額外標頭名稱不合法或與內建標頭衝突,或 <see cref="InReplyTo"/> / <see cref="References"/> / 標頭值含換行字元時擲出。
        /// Thrown when <see cref="To"/>, <see cref="Cc"/> and <see cref="Bcc"/> are all empty, when an extra header has an invalid or clashing name, or when <see cref="InReplyTo"/> / <see cref="References"/> / a header value contains a line break.
        /// </exception>
        public byte[] ToRfc822Bytes()
        {
            return Rfc822Writer.Write(this);
        }
    }
}
