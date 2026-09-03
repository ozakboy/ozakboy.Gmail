namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// batch 回應中的一個子回應:內嵌 HTTP 回應的狀態碼、Content-ID 與主體。
    /// One sub-response inside a batch response: the status code, Content-ID and body of the embedded HTTP response.
    /// </summary>
    /// <remarks>
    /// 內部型別,不屬於公開 API。
    /// Internal type; not part of the public API surface.
    /// </remarks>
    internal sealed class BatchSubResponse
    {
        /// <summary>
        /// 建立子回應。
        /// Creates a sub-response.
        /// </summary>
        /// <param name="statusCode">內嵌 HTTP 回應的狀態碼,無法解析時為 0。The embedded response's status code; 0 when it could not be parsed.</param>
        /// <param name="contentId">部件的 Content-ID(已去掉角括號),沒有時為 null。The part's Content-ID with its angle brackets stripped; null when absent.</param>
        /// <param name="body">內嵌 HTTP 回應的主體,沒有時為空字串。The embedded response's body; an empty string when there is none.</param>
        internal BatchSubResponse(int statusCode, string? contentId, string body)
        {
            StatusCode = statusCode;
            ContentId = contentId;
            Body = body;
        }

        /// <summary>
        /// 內嵌 HTTP 回應的狀態碼,無法解析時為 0。
        /// The embedded response's status code; 0 when it could not be parsed.
        /// </summary>
        internal int StatusCode { get; }

        /// <summary>
        /// 部件的 Content-ID(已去掉角括號),沒有這個標頭時為 null。
        /// The part's Content-ID with its angle brackets stripped; null when the header is absent.
        /// </summary>
        internal string? ContentId { get; }

        /// <summary>
        /// 內嵌 HTTP 回應的主體,永不為 null。
        /// The embedded response's body; never null.
        /// </summary>
        internal string Body { get; }
    }
}
