using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ozakboy.Gmail.Core;
using Ozakboy.Gmail.Tests.TestSupport;
using Xunit;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// 重試策略與錯誤映射的測試。重試延遲一律設為零,測試不會真的等待。
    /// </summary>
    public class GmailClientRetryTests
    {
        private const string RateLimitBody =
            "{\"error\":{\"code\":403,\"message\":\"Rate Limit Exceeded\",\"errors\":[{\"reason\":\"rateLimitExceeded\",\"message\":\"Rate Limit Exceeded\"}]}}";

        private const string NotFoundBody =
            "{\"error\":{\"code\":404,\"message\":\"Requested entity was not found.\",\"errors\":[{\"reason\":\"notFound\",\"message\":\"Not Found\"}]}}";

        [Fact]
        public async Task 收到429後重試_第二次成功()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.TooManyRequests, "{\"error\":{\"code\":429,\"message\":\"Too many\",\"errors\":[{\"reason\":\"rateLimitExceeded\"}]}}");
            handler.EnqueueJson("{\"emailAddress\":\"user@example.com\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var profile = await client.GetProfileAsync();

            Assert.Equal(2, handler.RequestCount);
            Assert.Equal("user@example.com", profile.EmailAddress);
        }

        [Fact]
        public async Task 收到500持續失敗_重試用盡後拋例外且嘗試次數為MaxRetries加一()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.InternalServerError, "{\"error\":{\"code\":500,\"message\":\"Backend Error\"}}");
            var client = GmailTestFactory.CreateClient(handler, GmailTestFactory.NoDelayOptions(maxRetries: 2));

            var exception = await Assert.ThrowsAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.Equal(3, handler.RequestCount);
            Assert.Equal(500, exception.StatusCode);
            Assert.False(exception.IsRateLimited);
        }

        [Fact]
        public async Task 收到403rateLimitExceeded_會重試且旗標為限流()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.Forbidden, RateLimitBody);
            var client = GmailTestFactory.CreateClient(handler, GmailTestFactory.NoDelayOptions(maxRetries: 1));

            var exception = await Assert.ThrowsAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.Equal(2, handler.RequestCount);
            Assert.True(exception.IsRateLimited);
            Assert.Equal("rateLimitExceeded", exception.Reason);
        }

        [Fact]
        public async Task 收到403其他原因_不重試也不算限流()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(
                HttpStatusCode.Forbidden,
                "{\"error\":{\"code\":403,\"message\":\"Insufficient Permission\",\"errors\":[{\"reason\":\"insufficientPermissions\"}]}}");
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.Equal(1, handler.RequestCount);
            Assert.False(exception.IsRateLimited);
            Assert.Equal("insufficientPermissions", exception.Reason);
        }

        [Fact]
        public async Task 收到401_不重試且IsUnauthorized為真()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(
                HttpStatusCode.Unauthorized,
                "{\"error\":{\"code\":401,\"message\":\"Invalid Credentials\",\"errors\":[{\"reason\":\"authError\"}]}}");
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.Equal(1, handler.RequestCount);
            Assert.True(exception.IsUnauthorized);
            Assert.False(exception.IsNotFound);
        }

        [Fact]
        public async Task 一般404_IsNotFound為真且不是歷程過期()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.NotFound, NotFoundBody);
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsAsync<GmailApiException>(() => client.GetMessageAsync("m1"));

            Assert.True(exception.IsNotFound);
            Assert.False(exception.IsHistoryExpired);
            Assert.Equal("notFound", exception.Reason);
            Assert.Equal("Requested entity was not found.", exception.ErrorMessage);
        }

        [Fact]
        public async Task 歷程查詢404_IsHistoryExpired為真且IsNotFound為假()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.NotFound, NotFoundBody);
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsAsync<GmailApiException>(() => client.ListHistoryAsync("12345"));

            Assert.True(exception.IsHistoryExpired);
            Assert.False(exception.IsNotFound);
        }

        [Fact]
        public async Task RetryAfter秒數會取代計算出的延遲()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.TooManyRequests, "{}", retryAfterSeconds: "0");
            handler.EnqueueJson("{}");

            // 基準延遲設成十分鐘,若沒有採用 Retry-After 這個測試會直接逾時
            var options = new GmailClientOptions { MaxRetries = 3, RetryBaseDelay = TimeSpan.FromMinutes(10) };
            var client = GmailTestFactory.CreateClient(handler, options);

            await client.GetProfileAsync();

            Assert.Equal(2, handler.RequestCount);
        }

        [Fact]
        public async Task RetryAfter日期格式會被採用()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.ServiceUnavailable, "{}", retryAfterDate: DateTimeOffset.UtcNow.AddMinutes(-5));
            handler.EnqueueJson("{}");

            var options = new GmailClientOptions { MaxRetries = 3, RetryBaseDelay = TimeSpan.FromMinutes(10) };
            var client = GmailTestFactory.CreateClient(handler, options);

            await client.GetProfileAsync();

            Assert.Equal(2, handler.RequestCount);
        }

        [Fact]
        public async Task MaxRetries為零_完全不重試()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.InternalServerError, "{}");
            var client = GmailTestFactory.CreateClient(handler, GmailTestFactory.NoDelayOptions(maxRetries: 0));

            await Assert.ThrowsAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.Equal(1, handler.RequestCount);
        }

        [Fact]
        public async Task 等待重試期間被取消_拋出OperationCanceledException()
        {
            using var cancellation = new CancellationTokenSource();
            var handler = new RecordingHandler();
            handler.OnRequest = _ => cancellation.Cancel();
            handler.EnqueueError(HttpStatusCode.TooManyRequests, "{}");
            var client = GmailTestFactory.CreateClient(handler);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetProfileAsync(cancellation.Token));
        }

        [Fact]
        public async Task 存取權杖提供者回傳空字串_拋出InvalidOperationException()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var client = GmailTestFactory.CreateClient(handler, accessTokenProvider: _ => Task.FromResult(string.Empty));

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetProfileAsync());
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task 存取權杖提供者回傳null_拋出InvalidOperationException()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var client = GmailTestFactory.CreateClient(handler, accessTokenProvider: _ => Task.FromResult<string>(null));

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetProfileAsync());
        }

        [Fact]
        public async Task 重試時不會再次呼叫存取權杖提供者()
        {
            var calls = 0;
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.ServiceUnavailable, "{}");
            handler.EnqueueError(HttpStatusCode.ServiceUnavailable, "{}");
            handler.EnqueueJson("{}");

            var client = GmailTestFactory.CreateClient(handler, accessTokenProvider: _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult("token-" + calls.ToString(System.Globalization.CultureInfo.InvariantCulture));
            });

            await client.GetProfileAsync();

            Assert.Equal(1, calls);
            Assert.Equal(3, handler.RequestCount);
            Assert.All(handler.Requests, request => Assert.Equal("Bearer token-1", request.Authorization));
        }

        [Fact]
        public async Task 例外訊息含方法路徑狀態碼與原因()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.NotFound, NotFoundBody);
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsAsync<GmailApiException>(() => client.GetMessageAsync("m1"));

            Assert.Equal(
                "Gmail API GET /gmail/v1/users/me/messages/m1?format=full failed with 404 (notFound): Requested entity was not found.",
                exception.Message);
            Assert.Equal("GET", exception.RequestMethod);
            Assert.Equal("/gmail/v1/users/me/messages/m1?format=full", exception.RequestPath);
            Assert.Equal(NotFoundBody, exception.ResponseBody);
        }

        [Fact]
        public async Task 錯誤主體不是JSON_原因與描述為null但仍保留原文()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.BadGateway, "<html>502</html>");
            var client = GmailTestFactory.CreateClient(handler, GmailTestFactory.NoDelayOptions(maxRetries: 0));

            var exception = await Assert.ThrowsAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.Null(exception.Reason);
            Assert.Null(exception.ErrorMessage);
            Assert.Equal("<html>502</html>", exception.ResponseBody);
            Assert.Contains("(-): -", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task 網路層例外不會被包裝也不重試()
        {
            var handler = new RecordingHandler { ThrowOnSend = new HttpRequestException("DNS 失敗") };
            var client = GmailTestFactory.CreateClient(handler);

            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetProfileAsync());
            Assert.Equal(1, handler.RequestCount);
        }

        [Theory]
        [InlineData(429, null, true)]
        [InlineData(500, null, true)]
        [InlineData(503, null, true)]
        [InlineData(403, "rateLimitExceeded", true)]
        [InlineData(403, "userRateLimitExceeded", true)]
        [InlineData(403, "insufficientPermissions", false)]
        [InlineData(401, null, false)]
        [InlineData(400, null, false)]
        [InlineData(404, null, false)]
        [InlineData(409, null, false)]
        public void RetryPolicy_判斷可重試的狀態碼與原因(int statusCode, string reason, bool expected)
        {
            Assert.Equal(expected, RetryPolicy.IsRetryable(statusCode, reason));
        }

        [Fact]
        public void RetryPolicy_延遲以基準時間加倍成長()
        {
            var policy = new RetryPolicy(3, TimeSpan.FromSeconds(1));

            Assert.Equal(TimeSpan.FromSeconds(1), policy.GetDelay(1, null));
            Assert.Equal(TimeSpan.FromSeconds(2), policy.GetDelay(2, null));
            Assert.Equal(TimeSpan.FromSeconds(4), policy.GetDelay(3, null));
        }

        [Fact]
        public void RetryPolicy_有RetryAfter時直接採用且負值視為零()
        {
            var policy = new RetryPolicy(3, TimeSpan.FromSeconds(1));

            Assert.Equal(TimeSpan.FromSeconds(30), policy.GetDelay(3, TimeSpan.FromSeconds(30)));
            Assert.Equal(TimeSpan.Zero, policy.GetDelay(1, TimeSpan.FromSeconds(-5)));
        }
    }
}
