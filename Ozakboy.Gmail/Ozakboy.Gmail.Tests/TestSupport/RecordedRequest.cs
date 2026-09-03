using System;

namespace Ozakboy.Gmail.Tests.TestSupport
{
    /// <summary>
    /// 被記錄下來的單筆請求。請求本身在送出後會被釋放,所以這裡只留下要斷言的欄位。
    /// </summary>
    public class RecordedRequest
    {
        /// <summary>HTTP 方法。</summary>
        public string Method { get; set; }

        /// <summary>完整請求網址。</summary>
        public Uri Uri { get; set; }

        /// <summary>Authorization 標頭字串,沒有時為 null。</summary>
        public string Authorization { get; set; }

        /// <summary>請求主體字串,沒有主體時為 null。</summary>
        public string Body { get; set; }

        /// <summary>請求主體的 Content-Type,沒有主體時為 null。</summary>
        public string ContentType { get; set; }

        /// <summary>完整網址(保留百分號轉義)。</summary>
        public string Url => Uri == null ? null : Uri.AbsoluteUri;

        /// <summary>路徑加查詢字串(保留百分號轉義)。</summary>
        public string PathAndQuery => Uri == null ? null : Uri.PathAndQuery;
    }
}
