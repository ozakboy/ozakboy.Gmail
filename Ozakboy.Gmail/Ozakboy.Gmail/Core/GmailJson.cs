using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// 全套件共用的 JSON 設定與序列化輔助。
    /// Shared JSON settings and (de)serialization helpers used across the package.
    /// </summary>
    /// <remarks>
    /// Gmail 會把 int64 / uint64 欄位(historyId、internalDate、size…)序列化成字串,
    /// 因此開啟 <see cref="JsonNumberHandling.AllowReadingFromString"/>。
    /// Gmail serialises int64 / uint64 fields (historyId, internalDate, size…) as strings,
    /// hence <see cref="JsonNumberHandling.AllowReadingFromString"/> is enabled.
    /// </remarks>
    internal static class GmailJson
    {
        /// <summary>
        /// 共用的序列化選項:camelCase 命名、忽略大小寫、允許字串數字、寫出時略過 null。
        /// The shared serializer options: camelCase naming, case-insensitive reads, string numbers allowed, nulls omitted when writing.
        /// </summary>
        internal static readonly JsonSerializerOptions Options = CreateOptions();

        /// <summary>
        /// 將 JSON 字串反序列化為指定型別。
        /// Deserializes a JSON string into the given type.
        /// </summary>
        /// <typeparam name="T">目標型別。The target type.</typeparam>
        /// <param name="json">JSON 內容。The JSON content.</param>
        /// <returns>反序列化結果,內容為 JSON null 時回傳 null。The deserialized value, or null when the JSON is null.</returns>
        /// <exception cref="JsonException">內容不是合法 JSON 時擲出。Thrown when the content is not valid JSON.</exception>
        internal static T? Deserialize<T>(string json)
            where T : class
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        /// <summary>
        /// 將物件序列化為 JSON 字串。
        /// Serializes an object into a JSON string.
        /// </summary>
        /// <typeparam name="T">來源型別。The source type.</typeparam>
        /// <param name="value">待序列化的物件。The value to serialize.</param>
        /// <returns>JSON 字串。The JSON string.</returns>
        internal static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, Options);
        }

        /// <summary>
        /// 建立共用的序列化選項。
        /// Creates the shared serializer options.
        /// </summary>
        /// <returns>序列化選項。The serializer options.</returns>
        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            return options;
        }
    }
}
