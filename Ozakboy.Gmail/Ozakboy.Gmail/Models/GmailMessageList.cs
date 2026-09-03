using System.Collections.Generic;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// messages.list 的回應:郵件參考清單與分頁資訊。
    /// The messages.list response: a page of message references plus the paging information.
    /// </summary>
    public class GmailMessageList
    {
        /// <summary>
        /// 本頁的郵件參考,只有 Id 與 ThreadId;永不為 null。
        /// The message references on this page, carrying only Id and ThreadId; never null.
        /// </summary>
        public List<GmailMessageRef> Messages { get; set; } = new List<GmailMessageRef>();

        /// <summary>
        /// 下一頁的頁籤,沒有更多資料時為 null;把它傳回 pageToken 就能續抓。
        /// The token for the next page; null when there is no more data. Pass it back as pageToken to continue.
        /// </summary>
        public string? NextPageToken { get; set; }

        /// <summary>
        /// Gmail 估算的結果總數,未回傳時為 null。
        /// Gmail's estimate of the total result count; null when absent.
        /// </summary>
        public long? ResultSizeEstimate { get; set; }
    }
}
