using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ozakboy.Gmail.Tests.TestSupport
{
    /// <summary>
    /// 假的 HttpMessageHandler:依序回傳預先排入的回應,並記錄每一筆送出的請求。
    /// 全部測試都靠它離線執行,不會連到任何真實端點。
    /// </summary>
    public sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new Queue<Func<HttpResponseMessage>>();
        private Func<HttpResponseMessage> _lastResponse;

        /// <summary>排入的回應用完後,是否重複使用最後一個(重試測試需要)。</summary>
        public bool RepeatLastResponse { get; set; } = true;

        /// <summary>已送出的請求紀錄,依序排列。</summary>
        public List<RecordedRequest> Requests { get; } = new List<RecordedRequest>();

        /// <summary>送出請求時的回呼,用來在測試中製造取消等情境。</summary>
        public Action<HttpRequestMessage> OnRequest { get; set; }

        /// <summary>設定後,每次送出請求都會擲出這個例外(用來驗證網路層例外不被包裝)。</summary>
        public Exception ThrowOnSend { get; set; }

        /// <summary>已送出的請求數。</summary>
        public int RequestCount => Requests.Count;

        /// <summary>最後一筆請求紀錄。</summary>
        public RecordedRequest LastRequest => Requests.Count == 0 ? null : Requests[Requests.Count - 1];

        /// <summary>排入一個自訂回應工廠。</summary>
        public RecordingHandler Enqueue(Func<HttpResponseMessage> factory)
        {
            _responses.Enqueue(factory);
            return this;
        }

        /// <summary>排入一個 JSON 回應。</summary>
        public RecordingHandler EnqueueJson(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return Enqueue(() => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }

        /// <summary>排入一個沒有主體的回應(例如 Gmail 的 204)。</summary>
        public RecordingHandler EnqueueEmpty(HttpStatusCode statusCode = HttpStatusCode.NoContent)
        {
            return Enqueue(() => new HttpResponseMessage(statusCode));
        }

        /// <summary>排入一個錯誤回應,可帶 Retry-After(秒數或 HTTP 日期)。</summary>
        public RecordingHandler EnqueueError(HttpStatusCode statusCode, string json = null, string retryAfterSeconds = null, DateTimeOffset? retryAfterDate = null)
        {
            return Enqueue(() =>
            {
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(json ?? string.Empty, Encoding.UTF8, "application/json"),
                };

                if (retryAfterSeconds != null)
                    response.Headers.TryAddWithoutValidation("Retry-After", retryAfterSeconds);

                if (retryAfterDate.HasValue)
                    response.Headers.TryAddWithoutValidation("Retry-After", retryAfterDate.Value.ToString("r", System.Globalization.CultureInfo.InvariantCulture));

                return response;
            });
        }

        /// <summary>用這個 handler 建立 HttpClient。</summary>
        public HttpClient CreateClient()
        {
            return new HttpClient(this, false);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // 真實 handler 會在送出前觀察取消,這裡跟著做,測試才能驗證「已取消就不送出」
            cancellationToken.ThrowIfCancellationRequested();

            var record = new RecordedRequest
            {
                Method = request.Method.Method,
                Uri = request.RequestUri,
                Authorization = request.Headers.Authorization?.ToString(),
            };

            if (request.Content != null)
            {
                record.Body = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                record.ContentType = request.Content.Headers.ContentType?.ToString();
            }

            Requests.Add(record);

            OnRequest?.Invoke(request);

            if (ThrowOnSend != null)
                throw ThrowOnSend;

            if (_responses.Count > 0)
                _lastResponse = _responses.Dequeue();
            else if (!RepeatLastResponse || _lastResponse == null)
                throw new InvalidOperationException("測試沒有排入足夠的回應。");

            return _lastResponse();
        }
    }
}
