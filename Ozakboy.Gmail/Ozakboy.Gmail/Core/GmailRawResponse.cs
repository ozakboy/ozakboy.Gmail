namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// 成功回應的原始內容:主體字串與 Content-Type 標頭。
    /// The raw content of a successful response: the body string and the Content-Type header.
    /// </summary>
    /// <remarks>
    /// 內部型別,不屬於公開 API。batch 端點必須拿到 Content-Type 才能取得 multipart 的 boundary,
    /// 一般 JSON 端點只會用到 <see cref="Body"/>。
    /// Internal type; not part of the public API surface. The batch endpoint needs the Content-Type to find the multipart boundary,
    /// while the ordinary JSON endpoints only use <see cref="Body"/>.
    /// </remarks>
    internal sealed class GmailRawResponse
    {
        /// <summary>
        /// 建立原始回應內容。
        /// Creates the raw response content.
        /// </summary>
        /// <param name="statusCode">HTTP 狀態碼。The HTTP status code.</param>
        /// <param name="body">回應主體,可能為空字串。The response body; possibly empty.</param>
        /// <param name="contentType">Content-Type 標頭值,沒有內容時為 null。The Content-Type header value; null when there is no content.</param>
        internal GmailRawResponse(int statusCode, string body, string? contentType)
        {
            StatusCode = statusCode;
            Body = body;
            ContentType = contentType;
        }

        /// <summary>
        /// HTTP 狀態碼。
        /// The HTTP status code.
        /// </summary>
        internal int StatusCode { get; }

        /// <summary>
        /// 回應主體,永不為 null。
        /// The response body; never null.
        /// </summary>
        internal string Body { get; }

        /// <summary>
        /// Content-Type 標頭值(含參數),沒有內容時為 null。
        /// The Content-Type header value including its parameters; null when the response carried no content.
        /// </summary>
        internal string? ContentType { get; }
    }
}
