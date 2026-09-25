using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ozakboy.Gmail.Core;
using Ozakboy.Gmail.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// 重試策略與錯誤映射的測試。重試延遲一律設為零,測試不會真的等待。
    /// </summary>
    [TestClass]
    public class GmailClientRetryTests
    {
        private const string RateLimitBody =
            "{\"error\":{\"code\":403,\"message\":\"Rate Limit Exceeded\",\"errors\":[{\"reason\":\"rateLimitExceeded\",\"message\":\"Rate Limit Exceeded\"}]}}";

        private const string NotFoundBody =
            "{\"error\":{\"code\":404,\"message\":\"Requested entity was not found.\",\"errors\":[{\"reason\":\"notFound\",\"message\":\"Not Found\"}]}}";

        [TestMethod]
        public async Task 收到429後重試_第二次成功()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError((HttpStatusCode)429   /* net48 沒有 TooManyRequests 列舉值 */, "{\"error\":{\"code\":429,\"message\":\"Too many\",\"errors\":[{\"reason\":\"rateLimitExceeded\"}]}}");
            handler.EnqueueJson("{\"emailAddress\":\"user@example.com\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var profile = await client.GetProfileAsync();

            Assert.AreEqual(2, handler.RequestCount);
            Assert.AreEqual("user@example.com", profile.EmailAddress);
        }

        [TestMethod]
        public async Task 收到500持續失敗_重試用盡後拋例外且嘗試次數為MaxRetries加一()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.InternalServerError, "{\"error\":{\"code\":500,\"message\":\"Backend Error\"}}");
            var client = GmailTestFactory.CreateClient(handler, GmailTestFactory.NoDelayOptions(maxRetries: 2));

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.AreEqual(3, handler.RequestCount);
            Assert.AreEqual(500, exception.StatusCode);
            Assert.IsFalse(exception.IsRateLimited);
        }

        [TestMethod]
        public async Task 收到403rateLimitExceeded_會重試且旗標為限流()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.Forbidden, RateLimitBody);
            var client = GmailTestFactory.CreateClient(handler, GmailTestFactory.NoDelayOptions(maxRetries: 1));

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.AreEqual(2, handler.RequestCount);
            Assert.IsTrue(exception.IsRateLimited);
            Assert.AreEqual("rateLimitExceeded", exception.Reason);
        }

        [TestMethod]
        public async Task 收到403其他原因_不重試也不算限流()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(
                HttpStatusCode.Forbidden,
                "{\"error\":{\"code\":403,\"message\":\"Insufficient Permission\",\"errors\":[{\"reason\":\"insufficientPermissions\"}]}}");
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.AreEqual(1, handler.RequestCount);
            Assert.IsFalse(exception.IsRateLimited);
            Assert.AreEqual("insufficientPermissions", exception.Reason);
        }

        [TestMethod]
        public async Task 收到401_不重試且IsUnauthorized為真()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(
                HttpStatusCode.Unauthorized,
                "{\"error\":{\"code\":401,\"message\":\"Invalid Credentials\",\"errors\":[{\"reason\":\"authError\"}]}}");
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.AreEqual(1, handler.RequestCount);
            Assert.IsTrue(exception.IsUnauthorized);
            Assert.IsFalse(exception.IsNotFound);
        }

        [TestMethod]
        public async Task 一般404_IsNotFound為真且不是歷程過期()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.NotFound, NotFoundBody);
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(() => client.GetMessageAsync("m1"));

            Assert.IsTrue(exception.IsNotFound);
            Assert.IsFalse(exception.IsHistoryExpired);
            Assert.AreEqual("notFound", exception.Reason);
            Assert.AreEqual("Requested entity was not found.", exception.ErrorMessage);
        }

        [TestMethod]
        public async Task 歷程查詢404_IsHistoryExpired為真且IsNotFound為假()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.NotFound, NotFoundBody);
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(() => client.ListHistoryAsync("12345"));

            Assert.IsTrue(exception.IsHistoryExpired);
            Assert.IsFalse(exception.IsNotFound);
        }

        [TestMethod]
        public async Task RetryAfter秒數會取代計算出的延遲()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError((HttpStatusCode)429   /* net48 沒有 TooManyRequests 列舉值 */, "{}", retryAfterSeconds: "0");
            handler.EnqueueJson("{}");

            // 基準延遲設成十分鐘,若沒有採用 Retry-After 這個測試會直接逾時
            var options = new GmailClientOptions { MaxRetries = 3, RetryBaseDelay = TimeSpan.FromMinutes(10) };
            var client = GmailTestFactory.CreateClient(handler, options);

            await client.GetProfileAsync();

            Assert.AreEqual(2, handler.RequestCount);
        }

        [TestMethod]
        public async Task RetryAfter日期格式會被採用()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.ServiceUnavailable, "{}", retryAfterDate: DateTimeOffset.UtcNow.AddMinutes(-5));
            handler.EnqueueJson("{}");

            var options = new GmailClientOptions { MaxRetries = 3, RetryBaseDelay = TimeSpan.FromMinutes(10) };
            var client = GmailTestFactory.CreateClient(handler, options);

            await client.GetProfileAsync();

            Assert.AreEqual(2, handler.RequestCount);
        }

        [TestMethod]
        public async Task MaxRetries為零_完全不重試()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.InternalServerError, "{}");
            var client = GmailTestFactory.CreateClient(handler, GmailTestFactory.NoDelayOptions(maxRetries: 0));

            await Assert.ThrowsExactlyAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.AreEqual(1, handler.RequestCount);
        }

        [TestMethod]
        public async Task 等待重試期間被取消_拋出OperationCanceledException()
        {
            using var cancellation = new CancellationTokenSource();
            var handler = new RecordingHandler();
            handler.OnRequest = _ => cancellation.Cancel();
            handler.EnqueueError((HttpStatusCode)429   /* net48 沒有 TooManyRequests 列舉值 */, "{}");
            var client = GmailTestFactory.CreateClient(handler);

            await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetProfileAsync(cancellation.Token));
        }

        [TestMethod]
        public async Task 存取權杖提供者回傳空字串_拋出InvalidOperationException()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var client = GmailTestFactory.CreateClient(handler, accessTokenProvider: _ => Task.FromResult(string.Empty));

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.GetProfileAsync());
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task 存取權杖提供者回傳null_拋出InvalidOperationException()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var client = GmailTestFactory.CreateClient(handler, accessTokenProvider: _ => Task.FromResult<string>(null));

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.GetProfileAsync());
        }

        [TestMethod]
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

            Assert.AreEqual(1, calls);
            Assert.AreEqual(3, handler.RequestCount);
            foreach (var request in handler.Requests)
            {
                Assert.AreEqual("Bearer token-1", request.Authorization);
            }
        }

        [TestMethod]
        public async Task 例外訊息含方法路徑狀態碼與原因()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.NotFound, NotFoundBody);
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(() => client.GetMessageAsync("m1"));

            Assert.AreEqual(
                "Gmail API GET /gmail/v1/users/me/messages/m1?format=full failed with 404 (notFound): Requested entity was not found.",
                exception.Message);
            Assert.AreEqual("GET", exception.RequestMethod);
            Assert.AreEqual("/gmail/v1/users/me/messages/m1?format=full", exception.RequestPath);
            Assert.AreEqual(NotFoundBody, exception.ResponseBody);
        }

        [TestMethod]
        public async Task 錯誤主體不是JSON_原因與描述為null但仍保留原文()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.BadGateway, "<html>502</html>");
            var client = GmailTestFactory.CreateClient(handler, GmailTestFactory.NoDelayOptions(maxRetries: 0));

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.IsNull(exception.Reason);
            Assert.IsNull(exception.ErrorMessage);
            Assert.AreEqual("<html>502</html>", exception.ResponseBody);
            Assert.Contains("(-): -", exception.Message, StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task 網路層例外不會被包裝也不重試()
        {
            var handler = new RecordingHandler { ThrowOnSend = new HttpRequestException("DNS 失敗") };
            var client = GmailTestFactory.CreateClient(handler);

            await Assert.ThrowsExactlyAsync<HttpRequestException>(() => client.GetProfileAsync());
            Assert.AreEqual(1, handler.RequestCount);
        }

        [TestMethod]
        [DataRow(429, null, true)]
        [DataRow(500, null, true)]
        [DataRow(503, null, true)]
        [DataRow(403, "rateLimitExceeded", true)]
        [DataRow(403, "userRateLimitExceeded", true)]
        [DataRow(403, "insufficientPermissions", false)]
        [DataRow(401, null, false)]
        [DataRow(400, null, false)]
        [DataRow(404, null, false)]
        [DataRow(409, null, false)]
        public void RetryPolicy_判斷可重試的狀態碼與原因(int statusCode, string reason, bool expected)
        {
            Assert.AreEqual(expected, RetryPolicy.IsRetryable(statusCode, reason));
        }

        [TestMethod]
        public void RetryPolicy_延遲以基準時間加倍成長()
        {
            var policy = new RetryPolicy(3, TimeSpan.FromSeconds(1));

            Assert.AreEqual(TimeSpan.FromSeconds(1), policy.GetDelay(1, null));
            Assert.AreEqual(TimeSpan.FromSeconds(2), policy.GetDelay(2, null));
            Assert.AreEqual(TimeSpan.FromSeconds(4), policy.GetDelay(3, null));
        }

        [TestMethod]
        public void RetryPolicy_有RetryAfter時直接採用且負值視為零()
        {
            var policy = new RetryPolicy(3, TimeSpan.FromSeconds(1));

            Assert.AreEqual(TimeSpan.FromSeconds(30), policy.GetDelay(3, TimeSpan.FromSeconds(30)));
            Assert.AreEqual(TimeSpan.Zero, policy.GetDelay(1, TimeSpan.FromSeconds(-5)));
        }

        [TestMethod]
        public async Task 失敗回應帶RetryAfter_例外帶回該值()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError((HttpStatusCode)429   /* net48 沒有 TooManyRequests 列舉值 */, "{}", retryAfterSeconds: "5");
            var client = GmailTestFactory.CreateClient(handler, GmailTestFactory.NoDelayOptions(maxRetries: 0));

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.AreEqual(TimeSpan.FromSeconds(5), exception.RetryAfter);
        }

        [TestMethod]
        public async Task 失敗回應沒有RetryAfter_例外的RetryAfter為null()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.NotFound, NotFoundBody);
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(() => client.GetMessageAsync("m1"));

            Assert.IsNull(exception.RetryAfter);
        }

        [TestMethod]
        public void GmailApiException_公開建構子的RetryAfter為null()
        {
            Assert.IsNull(new GmailApiException().RetryAfter);
            Assert.IsNull(new GmailApiException("訊息").RetryAfter);
            Assert.IsNull(new GmailApiException("訊息", new InvalidOperationException()).RetryAfter);
        }

        [TestMethod]
        public async Task RetryAfter超過MaxRetryDelay_立即拋出不等待也不重試()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError((HttpStatusCode)429   /* net48 沒有 TooManyRequests 列舉值 */, "{}", retryAfterSeconds: "120");

            // 上限設 10 秒,若沒有生效這個測試會真的等 120 秒
            var options = new GmailClientOptions
            {
                MaxRetries = 3,
                RetryBaseDelay = TimeSpan.Zero,
                MaxRetryDelay = TimeSpan.FromSeconds(10),
            };
            var client = GmailTestFactory.CreateClient(handler, options);

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.AreEqual(1, handler.RequestCount);
            Assert.AreEqual(TimeSpan.FromSeconds(120), exception.RetryAfter);
        }

        [TestMethod]
        public async Task 指數退避超過MaxRetryDelay_立即拋出不重試()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.ServiceUnavailable, "{}");

            var options = new GmailClientOptions
            {
                MaxRetries = 3,
                RetryBaseDelay = TimeSpan.FromMinutes(5),
                MaxRetryDelay = TimeSpan.FromSeconds(1),
            };
            var client = GmailTestFactory.CreateClient(handler, options);

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(() => client.GetProfileAsync());

            Assert.AreEqual(1, handler.RequestCount);
            Assert.IsNull(exception.RetryAfter);
        }

        [TestMethod]
        public async Task MaxRetryDelay為null_不設上限仍照常重試()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError((HttpStatusCode)429   /* net48 沒有 TooManyRequests 列舉值 */, "{}", retryAfterSeconds: "0");
            handler.EnqueueJson("{}");

            var options = new GmailClientOptions
            {
                MaxRetries = 3,
                RetryBaseDelay = TimeSpan.Zero,
                MaxRetryDelay = null,
            };
            var client = GmailTestFactory.CreateClient(handler, options);

            await client.GetProfileAsync();

            Assert.AreEqual(2, handler.RequestCount);
        }

        [TestMethod]
        public void RetryPolicy_MaxRetryDelay為null_任何延遲都不算超過上限()
        {
            var policy = new RetryPolicy(3, TimeSpan.FromSeconds(1), null);

            Assert.IsFalse(policy.ExceedsMaxDelay(TimeSpan.FromHours(1)));
            Assert.IsFalse(policy.ExceedsMaxDelay(TimeSpan.MaxValue));
        }

        [TestMethod]
        public void RetryPolicy_延遲剛好等於上限不算超過_超過才算()
        {
            var policy = new RetryPolicy(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));

            Assert.IsFalse(policy.ExceedsMaxDelay(TimeSpan.FromSeconds(10)));
            Assert.IsTrue(policy.ExceedsMaxDelay(TimeSpan.FromSeconds(11)));
        }

        [TestMethod]
        public async Task RetryOnNetworkErrors為false_網路層例外立即上拋只送一次()
        {
            var handler = new RecordingHandler { ThrowOnSend = new HttpRequestException("DNS 失敗") };
            var options = new GmailClientOptions
            {
                MaxRetries = 3,
                RetryBaseDelay = TimeSpan.Zero,
                RetryOnNetworkErrors = false,
            };
            var client = GmailTestFactory.CreateClient(handler, options);

            await Assert.ThrowsExactlyAsync<HttpRequestException>(() => client.GetProfileAsync());
            Assert.AreEqual(1, handler.RequestCount);
        }

        [TestMethod]
        public async Task RetryOnNetworkErrors為true_第一次網路失敗第二次成功()
        {
            var handler = new RecordingHandler
            {
                ThrowOnSend = new HttpRequestException("DNS 失敗"),
                ThrowOnSendCount = 1,
            };
            handler.EnqueueJson("{\"emailAddress\":\"user@example.com\"}");

            var options = new GmailClientOptions
            {
                MaxRetries = 3,
                RetryBaseDelay = TimeSpan.Zero,
                RetryOnNetworkErrors = true,
            };
            var client = GmailTestFactory.CreateClient(handler, options);

            var profile = await client.GetProfileAsync();

            Assert.AreEqual(2, handler.RequestCount);
            Assert.AreEqual("user@example.com", profile.EmailAddress);
        }

        [TestMethod]
        public async Task RetryOnNetworkErrors為true_重試用盡後拋出的仍是HttpRequestException()
        {
            var handler = new RecordingHandler { ThrowOnSend = new HttpRequestException("DNS 失敗") };
            var options = new GmailClientOptions
            {
                MaxRetries = 2,
                RetryBaseDelay = TimeSpan.Zero,
                RetryOnNetworkErrors = true,
            };
            var client = GmailTestFactory.CreateClient(handler, options);

            var exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(() => client.GetProfileAsync());

            Assert.AreEqual(3, handler.RequestCount);
            Assert.AreEqual("DNS 失敗", exception.Message);
        }

        [TestMethod]
        public async Task RetryOnNetworkErrors為true_取消仍然不重試()
        {
            using var cancellation = new CancellationTokenSource();
            var handler = new RecordingHandler();
            handler.OnRequest = _ => cancellation.Cancel();
            handler.EnqueueError((HttpStatusCode)429   /* net48 沒有 TooManyRequests 列舉值 */, "{}");

            var options = new GmailClientOptions
            {
                MaxRetries = 3,
                RetryBaseDelay = TimeSpan.Zero,
                RetryOnNetworkErrors = true,
            };
            var client = GmailTestFactory.CreateClient(handler, options);

            await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetProfileAsync(cancellation.Token));
            Assert.AreEqual(1, handler.RequestCount);
        }
    }
}
