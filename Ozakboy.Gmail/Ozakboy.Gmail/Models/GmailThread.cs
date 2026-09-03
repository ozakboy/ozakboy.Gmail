using System.Collections.Generic;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// 一組討論串。實際帶回哪些欄位取決於請求時指定的 <see cref="GmailMessageFormat"/>。
    /// A Gmail thread. Which fields are populated depends on the <see cref="GmailMessageFormat"/> used to fetch it.
    /// </summary>
    /// <remarks>
    /// 改標籤 / 移垃圾桶的端點只會回傳 Id 與 Messages 的精簡內容,不要預期拿得到完整內文。
    /// The modify / trash endpoints answer with just the id and a trimmed-down Messages list; do not expect full bodies there.
    /// </remarks>
    public class GmailThread
    {
        /// <summary>
        /// 討論串識別碼,Gmail 未回傳時為 null。
        /// The thread id; null when Gmail did not return it.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// 這個討論串對應的歷程識別碼,Gmail 未回傳時為 null。
        /// The history id of this thread; null when Gmail did not return it.
        /// </summary>
        public string? HistoryId { get; set; }

        /// <summary>
        /// 討論串內容摘要,Gmail 未回傳時為 null。
        /// A short snippet of the thread content; null when Gmail did not return it.
        /// </summary>
        public string? Snippet { get; set; }

        /// <summary>
        /// 討論串中的郵件,依 Gmail 回傳的順序排列,永不為 null。
        /// The messages in the thread, in the order Gmail returned them; never null.
        /// </summary>
        public List<GmailMessage> Messages { get; set; } = new List<GmailMessage>();
    }
}
