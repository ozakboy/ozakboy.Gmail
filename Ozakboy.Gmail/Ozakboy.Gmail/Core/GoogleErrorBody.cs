using System.Text.Json;

namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// Google 錯誤回應主體的解析結果。同時支援 Gmail 的巢狀格式與 OAuth 的扁平格式。
    /// The parsed body of a Google error response. Handles both the nested Gmail format and the flat OAuth format.
    /// </summary>
    /// <remarks>
    /// Gmail:<c>{ "error": { "code": 404, "message": "…", "errors": [ { "reason": "notFound" } ] } }</c>;
    /// OAuth:<c>{ "error": "invalid_grant", "error_description": "…" }</c>。
    /// Gmail uses the nested shape, OAuth the flat one; both are recognised here.
    /// </remarks>
    internal sealed class GoogleErrorBody
    {
        /// <summary>
        /// 無法解析或內容為空時共用的空結果。
        /// The shared empty result used when the body is missing or unparsable.
        /// </summary>
        private static readonly GoogleErrorBody EmptyBody = new GoogleErrorBody(null, null);

        /// <summary>
        /// 建立解析結果。
        /// Creates a parse result.
        /// </summary>
        /// <param name="reason">錯誤原因代碼。The error reason code.</param>
        /// <param name="errorMessage">錯誤描述。The error description.</param>
        private GoogleErrorBody(string? reason, string? errorMessage)
        {
            Reason = reason;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// 錯誤原因代碼。Gmail 取 <c>error.errors[0].reason</c>,OAuth 取 <c>error</c>;無法取得時為 null。
        /// The error reason code: <c>error.errors[0].reason</c> for Gmail, <c>error</c> for OAuth; null when unavailable.
        /// </summary>
        internal string? Reason { get; }

        /// <summary>
        /// 錯誤描述。Gmail 取 <c>error.message</c>,OAuth 取 <c>error_description</c>;無法取得時為 null。
        /// The error description: <c>error.message</c> for Gmail, <c>error_description</c> for OAuth; null when unavailable.
        /// </summary>
        internal string? ErrorMessage { get; }

        /// <summary>
        /// 解析錯誤回應主體。內容為空、不是 JSON 或缺少 error 欄位時,回傳兩個屬性皆為 null 的結果。
        /// Parses an error response body. Returns a result with both properties null when the body is empty, not JSON, or has no error field.
        /// </summary>
        /// <param name="body">回應主體,可為 null。The response body; may be null.</param>
        /// <returns>解析結果,永不為 null。The parse result; never null.</returns>
        internal static GoogleErrorBody Parse(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return EmptyBody;

            try
            {
                using (var document = JsonDocument.Parse(body!))
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out var error))
                        return EmptyBody;

                    // OAuth 扁平格式:error 是字串
                    // The flat OAuth shape: error is a string.
                    if (error.ValueKind == JsonValueKind.String)
                        return new GoogleErrorBody(error.GetString(), ReadString(root, "error_description"));

                    // Gmail 巢狀格式:error 是物件
                    // The nested Gmail shape: error is an object.
                    if (error.ValueKind == JsonValueKind.Object)
                        return new GoogleErrorBody(ReadFirstReason(error), ReadString(error, "message"));

                    return EmptyBody;
                }
            }
            catch (JsonException)
            {
                // 錯誤回應不見得是 JSON(例如反向代理回的 HTML),解析失敗就當作沒有結構化資訊
                // Error bodies are not always JSON (an HTML page from a proxy, say); an unparsable body simply carries no structured data.
                return EmptyBody;
            }
        }

        /// <summary>
        /// 讀取物件下的字串屬性,不存在或不是字串時回傳 null。
        /// Reads a string property from an object, returning null when it is absent or not a string.
        /// </summary>
        /// <param name="element">來源物件。The source object.</param>
        /// <param name="propertyName">屬性名稱。The property name.</param>
        /// <returns>屬性值或 null。The property value, or null.</returns>
        private static string? ReadString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();

            return null;
        }

        /// <summary>
        /// 取出 Gmail 錯誤物件中第一筆 errors 的 reason。
        /// Reads the reason of the first entry in a Gmail error object's errors array.
        /// </summary>
        /// <param name="error">Gmail 的 error 物件。The Gmail error object.</param>
        /// <returns>錯誤原因或 null。The reason, or null.</returns>
        private static string? ReadFirstReason(JsonElement error)
        {
            if (!error.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() == 0)
                return null;

            var first = errors[0];
            if (first.ValueKind != JsonValueKind.Object)
                return null;

            return ReadString(first, "reason");
        }
    }
}
