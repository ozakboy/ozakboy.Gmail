using System;
using System.Collections.Generic;

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
    }
}
