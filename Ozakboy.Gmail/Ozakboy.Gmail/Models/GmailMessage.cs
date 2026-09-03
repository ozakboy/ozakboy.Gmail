using System;
using System.Collections.Generic;
using System.Text;
using Ozakboy.Gmail.Core;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// 一封 Gmail 郵件。實際帶回哪些欄位取決於請求時指定的 <see cref="GmailMessageFormat"/>。
    /// A Gmail message. Which fields are populated depends on the <see cref="GmailMessageFormat"/> used to fetch it.
    /// </summary>
    public class GmailMessage
    {
        /// <summary>
        /// 郵件識別碼,Gmail 未回傳時為 null。
        /// The message id; null when Gmail did not return it.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// 所屬討論串識別碼,Gmail 未回傳時為 null。
        /// The id of the thread the message belongs to; null when Gmail did not return it.
        /// </summary>
        public string? ThreadId { get; set; }

        /// <summary>
        /// 套用在這封郵件上的標籤識別碼,永不為 null。
        /// The label ids applied to this message; never null.
        /// </summary>
        public List<string> LabelIds { get; set; } = new List<string>();

        /// <summary>
        /// 郵件內容摘要,Gmail 未回傳時為 null。
        /// A short snippet of the message content; null when Gmail did not return it.
        /// </summary>
        public string? Snippet { get; set; }

        /// <summary>
        /// 這封郵件對應的歷程識別碼,Gmail 未回傳時為 null。
        /// The history id of this message; null when Gmail did not return it.
        /// </summary>
        public string? HistoryId { get; set; }

        /// <summary>
        /// 郵件的內部時間(Unix epoch 毫秒),Gmail 未回傳時為 0。
        /// The message's internal date in Unix epoch milliseconds; 0 when Gmail did not return it.
        /// </summary>
        public long InternalDate { get; set; }

        /// <summary>
        /// Gmail 估算的郵件大小(位元組)。
        /// Gmail's estimate of the message size in bytes.
        /// </summary>
        public long SizeEstimate { get; set; }

        /// <summary>
        /// 郵件的 MIME 結構根區段,format 為 <see cref="GmailMessageFormat.Minimal"/> 時為 null。
        /// The root MIME part of the message; null for <see cref="GmailMessageFormat.Minimal"/>.
        /// </summary>
        public GmailMessagePart? Payload { get; set; }

        /// <summary>
        /// base64url 編碼的完整 RFC 822 郵件,只有 format 為 <see cref="GmailMessageFormat.Raw"/> 時才有值。
        /// The complete RFC 822 message in base64url, present only for <see cref="GmailMessageFormat.Raw"/>.
        /// </summary>
        public string? Raw { get; set; }

        /// <summary>
        /// 將 <see cref="InternalDate"/> 轉成時間點;為 0(Gmail 未回傳)時為 null。
        /// <see cref="InternalDate"/> converted to a point in time; null when it is 0 because Gmail did not return it.
        /// </summary>
        public DateTimeOffset? InternalDateTime
        {
            get
            {
                return InternalDate == 0 ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeMilliseconds(InternalDate);
            }
        }

        /// <summary>
        /// 以不分大小寫的方式取出頂層 payload 的標頭值,這正是 format=metadata 會回傳的內容。
        /// Looks up a header on the top-level payload, ignoring case — exactly what format=metadata returns.
        /// </summary>
        /// <param name="name">標頭名稱,null 或空字串一律回傳 null。The header name; null or empty always yields null.</param>
        /// <returns>標頭值;<see cref="Payload"/> 為 null 或找不到標頭時為 null。The header value, or null when <see cref="Payload"/> is null or no header matches.</returns>
        public string? GetHeader(string name)
        {
            return Payload?.GetHeader(name);
        }

        /// <summary>
        /// 將 <see cref="Raw"/> 做 base64url 解碼,得到完整的 RFC 822 郵件位元組;可交給任何 MIME 函式庫解析。
        /// Decodes <see cref="Raw"/> from base64url into the complete RFC 822 message bytes, ready for any MIME parser.
        /// </summary>
        /// <returns>解碼後的位元組;<see cref="Raw"/> 為 null 時為 null。The decoded bytes, or null when <see cref="Raw"/> is null.</returns>
        /// <exception cref="FormatException"><see cref="Raw"/> 不是合法的 base64url 時擲出。Thrown when <see cref="Raw"/> is not valid base64url.</exception>
        public byte[]? DecodeRaw()
        {
            return Raw == null ? null : Base64Url.Decode(Raw);
        }

        /// <summary>
        /// 取出純文字內文:深度優先走訪 Gmail 已切好的 MIME 區段,回傳第一個非附件的 text/plain 區段解碼後的內容。
        /// Returns the plain text body: walks the MIME parts Gmail already split, depth first, and decodes the first text/plain part that is not an attachment.
        /// </summary>
        /// <remarks>
        /// 內容由 Gmail 轉成 UTF-8,因此一律以 UTF-8 解碼。要區分 multipart/alternative 中的哪一段時,請直接走 <see cref="Payload"/>。
        /// Gmail normalises the content to UTF-8, so it is always decoded as UTF-8. Walk <see cref="Payload"/> yourself when you need to pick a specific branch of a multipart/alternative.
        /// </remarks>
        /// <returns>純文字內文;<see cref="Payload"/> 為 null 或沒有這種區段時為 null。The plain text body, or null when <see cref="Payload"/> is null or no such part exists.</returns>
        /// <exception cref="FormatException">區段內容不是合法的 base64url 時擲出。Thrown when the part content is not valid base64url.</exception>
        public string? GetTextBody()
        {
            return FindBody("text/plain");
        }

        /// <summary>
        /// 取出 HTML 內文:深度優先走訪 Gmail 已切好的 MIME 區段,回傳第一個非附件的 text/html 區段解碼後的內容。
        /// Returns the HTML body: walks the MIME parts Gmail already split, depth first, and decodes the first text/html part that is not an attachment.
        /// </summary>
        /// <returns>HTML 內文;<see cref="Payload"/> 為 null 或沒有這種區段時為 null。The HTML body, or null when <see cref="Payload"/> is null or no such part exists.</returns>
        /// <exception cref="FormatException">區段內容不是合法的 base64url 時擲出。Thrown when the part content is not valid base64url.</exception>
        public string? GetHtmlBody()
        {
            return FindBody("text/html");
        }

        /// <summary>
        /// 列出郵件中的所有附件區段(含以 Content-ID 內嵌在 HTML 內文裡的圖片),依 MIME 樹的深度優先順序排列。
        /// Lists every attachment part of the message, including images inlined into the HTML body by Content-ID, in the depth-first order of the MIME tree.
        /// </summary>
        /// <remarks>
        /// 判定條件是「有檔名」或「有 attachmentId」,multipart/* 容器一律排除。
        /// 回傳的項目不含附件內容:<see cref="GmailAttachmentInfo.AttachmentId"/> 有值時要另外呼叫 GetAttachmentAsync,
        /// 為 null 表示內容已在 <c>Part.Body.Data</c>。
        /// A part counts when it has a file name or an attachment id; multipart/* containers are always skipped.
        /// The entries carry no content: fetch it with GetAttachmentAsync when <see cref="GmailAttachmentInfo.AttachmentId"/> is set,
        /// and read <c>Part.Body.Data</c> when it is null.
        /// </remarks>
        /// <returns>附件摘要清單,永不為 null;<see cref="Payload"/> 為 null 或沒有附件時為空清單。The attachment summaries; never null, and empty when <see cref="Payload"/> is null or there are no attachments.</returns>
        public List<GmailAttachmentInfo> GetAttachments()
        {
            var result = new List<GmailAttachmentInfo>();

            foreach (var part in MessagePartWalker.Flatten(Payload))
            {
                // multipart/* 只是容器,本身不是附件
                // A multipart/* part is only a container, never an attachment itself.
                if (part.MimeType != null && part.MimeType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var hasFileName = !string.IsNullOrEmpty(part.Filename);
                var attachmentId = part.Body?.AttachmentId;
                if (!hasFileName && attachmentId == null)
                    continue;

                result.Add(new GmailAttachmentInfo
                {
                    PartId = part.PartId,
                    FileName = part.Filename,
                    MimeType = part.MimeType,
                    Size = part.Body == null ? 0L : part.Body.Size,
                    AttachmentId = attachmentId,
                    ContentId = TrimAngleBrackets(part.GetHeader("Content-ID")),
                    Part = part,
                });
            }

            return result;
        }

        /// <summary>
        /// 找出第一個符合 MIME 類型、非附件且帶內容的區段,並以 UTF-8 解碼。
        /// Finds the first part with the given MIME type that is not an attachment and carries content, then decodes it as UTF-8.
        /// </summary>
        /// <param name="mimeType">要比對的 MIME 類型(不分大小寫)。The MIME type to match, ignoring case.</param>
        /// <returns>解碼後的內容,找不到時為 null。The decoded content, or null when no part matches.</returns>
        private string? FindBody(string mimeType)
        {
            foreach (var part in MessagePartWalker.Flatten(Payload))
            {
                if (!string.Equals(part.MimeType, mimeType, StringComparison.OrdinalIgnoreCase))
                    continue;

                // 有檔名的 text/plain 是附件(例如 .txt 檔),不是內文
                // A text/plain part with a file name is an attachment (a .txt file, say), not the body.
                if (!string.IsNullOrEmpty(part.Filename))
                    continue;

                var data = part.Body?.Data;
                if (data == null)
                    continue;

                return Encoding.UTF8.GetString(Base64Url.Decode(data));
            }

            return null;
        }

        /// <summary>
        /// 去掉 Content-ID 前後的角括號。
        /// Strips the angle brackets around a Content-ID.
        /// </summary>
        /// <param name="value">原始標頭值,可為 null。The raw header value; may be null.</param>
        /// <returns>去掉角括號後的值;<paramref name="value"/> 為 null 時為 null。The value without brackets, or null when <paramref name="value"/> is null.</returns>
        private static string? TrimAngleBrackets(string? value)
        {
            if (value == null)
                return null;

            var trimmed = value.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '<' && trimmed[trimmed.Length - 1] == '>')
                return trimmed.Substring(1, trimmed.Length - 2);

            return trimmed;
        }
    }
}
