using System.Collections.Generic;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// history.list 的回應:自指定歷程識別碼之後發生的變更。
    /// The history.list response: the changes that happened after a given history id.
    /// </summary>
    public class GmailHistoryList
    {
        /// <summary>
        /// 變更紀錄,永不為 null;沒有任何變更時為空清單。
        /// The history records; never null, and empty when nothing changed.
        /// </summary>
        public List<GmailHistoryRecord> History { get; set; } = new List<GmailHistoryRecord>();

        /// <summary>
        /// 下一頁的頁籤,沒有更多資料時為 null。
        /// The token for the next page; null when there is no more data.
        /// </summary>
        public string? NextPageToken { get; set; }

        /// <summary>
        /// 信箱目前的歷程識別碼,每次同步成功後應存下這個值。
        /// The mailbox's current history id; store it after each successful pass.
        /// </summary>
        public string? HistoryId { get; set; }
    }
}
