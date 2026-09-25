using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Ozakboy.Gmail.Core;
using Ozakboy.Gmail.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// 批次取信的測試:外層請求組裝、子請求格式、multipart 回應解析與逐筆失敗分類。
    /// 回應樣本刻意寫成 Gmail 真實回傳的形狀(application/http 部件包一整段 HTTP 回應)。
    /// </summary>
    [TestClass]
    public class GmailBatchTests
    {
        private const string BatchUrl = "https://gmail.googleapis.com/batch/gmail/v1";
        private const string Boundary = "batch_ABC123";

        private const string NotFoundJson =
            "{\"error\":{\"code\":404,\"message\":\"Requested entity was not found.\",\"errors\":[{\"reason\":\"notFound\"}]}}";

        private const string RateLimitJson =
            "{\"error\":{\"code\":429,\"message\":\"Too many requests\",\"errors\":[{\"reason\":\"rateLimitExceeded\"}]}}";

        [TestMethod]
        public async Task BatchGetMessagesAsync_送出POST到批次端點且為multipart()
        {
            var handler = new RecordingHandler();
            handler.EnqueueMultipart(SuccessBody(new[] { "m1", "m2" }), Boundary);
            var client = GmailTestFactory.CreateClient(handler);

            await client.BatchGetMessagesAsync(new[] { "m1", "m2" });

            Assert.AreEqual(1, handler.RequestCount);
            Assert.AreEqual("POST", handler.LastRequest.Method);
            Assert.AreEqual(BatchUrl, handler.LastRequest.Url);
            Assert.StartsWith("multipart/mixed", handler.LastRequest.ContentType, StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_每個子請求都有GET路徑與ContentID()
        {
            var handler = new RecordingHandler();
            handler.EnqueueMultipart(SuccessBody(new[] { "m1", "m2" }), Boundary);
            var client = GmailTestFactory.CreateClient(handler);

            await client.BatchGetMessagesAsync(new[] { "m1", "m2" });

            var body = handler.LastRequest.Body;
            Assert.Contains("Content-Type: application/http", body, StringComparison.Ordinal);
            Assert.Contains("Content-ID: <item0>", body, StringComparison.Ordinal);
            Assert.Contains("Content-ID: <item1>", body, StringComparison.Ordinal);
            Assert.Contains("GET /gmail/v1/users/me/messages/m1?format=full HTTP/1.1", body, StringComparison.Ordinal);
            Assert.Contains("GET /gmail/v1/users/me/messages/m2?format=full HTTP/1.1", body, StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_Metadata格式_子請求帶重複的metadataHeaders()
        {
            var handler = new RecordingHandler();
            handler.EnqueueMultipart(SuccessBody(new[] { "m1" }), Boundary);
            var client = GmailTestFactory.CreateClient(handler);

            await client.BatchGetMessagesAsync(new[] { "m1" }, GmailMessageFormat.Metadata, new[] { "From", "Subject" });

            Assert.Contains(
                "GET /gmail/v1/users/me/messages/m1?format=metadata&metadataHeaders=From&metadataHeaders=Subject HTTP/1.1",
                handler.LastRequest.Body,
                StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_非Metadata格式_不送metadataHeaders()
        {
            var handler = new RecordingHandler();
            handler.EnqueueMultipart(SuccessBody(new[] { "m1" }), Boundary);
            var client = GmailTestFactory.CreateClient(handler);

            await client.BatchGetMessagesAsync(new[] { "m1" }, GmailMessageFormat.Full, new[] { "From" });

            Assert.DoesNotContain("metadataHeaders", handler.LastRequest.Body, StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_識別碼會做URL轉義()
        {
            var handler = new RecordingHandler();
            handler.EnqueueMultipart(SuccessBody(new[] { "a/b" }), Boundary);
            var client = GmailTestFactory.CreateClient(handler);

            await client.BatchGetMessagesAsync(new[] { "a/b" });

            Assert.Contains("/messages/a%2Fb?format=full", handler.LastRequest.Body, StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_授權標頭只掛在外層請求()
        {
            var handler = new RecordingHandler();
            handler.EnqueueMultipart(SuccessBody(new[] { "m1", "m2" }), Boundary);
            var client = GmailTestFactory.CreateClient(handler);

            await client.BatchGetMessagesAsync(new[] { "m1", "m2" });

            Assert.AreEqual("Bearer " + GmailTestFactory.AccessToken, handler.LastRequest.Authorization);
            Assert.DoesNotContain("Authorization", handler.LastRequest.Body, StringComparison.OrdinalIgnoreCase);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_全部成功_解析出每一封郵件()
        {
            var handler = new RecordingHandler();
            handler.EnqueueMultipart(SuccessBody(new[] { "m1", "m2", "m3" }), Boundary);
            var client = GmailTestFactory.CreateClient(handler);

            var result = await client.BatchGetMessagesAsync(new[] { "m1", "m2", "m3" });

            Assert.IsEmpty(result.Failures);
            Assert.AreEqual(3, result.Messages.Count);
            Assert.AreEqual("m1", result.Messages[0].Id);
            Assert.AreEqual("m3", result.Messages[2].Id);
            Assert.AreEqual("t0", result.Messages[0].ThreadId);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_其中一筆404_進失敗清單且對回正確識別碼()
        {
            var handler = new RecordingHandler();
            var body = new StringBuilder();
            AppendPart(body, 0, 200, "{\"id\":\"m1\",\"threadId\":\"t1\"}");
            AppendPart(body, 1, 404, NotFoundJson);
            AppendPart(body, 2, 200, "{\"id\":\"m3\",\"threadId\":\"t3\"}");
            AppendClosing(body);
            handler.EnqueueMultipart(body.ToString(), Boundary);
            var client = GmailTestFactory.CreateClient(handler);

            var result = await client.BatchGetMessagesAsync(new[] { "m1", "m2", "m3" });

            Assert.AreEqual(2, result.Messages.Count);
            Assert.ContainsSingle(result.Failures);

            var failure = result.Failures[0];
            Assert.AreEqual("m2", failure.Id);
            Assert.AreEqual(404, failure.StatusCode);
            Assert.AreEqual("notFound", failure.Reason);
            Assert.AreEqual("Requested entity was not found.", failure.ErrorMessage);
            Assert.IsTrue(failure.IsNotFound);
            Assert.IsFalse(failure.IsRateLimited);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_子部件429_標記為限流且不重試()
        {
            var handler = new RecordingHandler();
            var body = new StringBuilder();
            AppendPart(body, 0, 429, RateLimitJson);
            AppendClosing(body);
            handler.EnqueueMultipart(body.ToString(), Boundary);
            var client = GmailTestFactory.CreateClient(handler);

            var result = await client.BatchGetMessagesAsync(new[] { "m1" });

            Assert.AreEqual(1, handler.RequestCount);
            Assert.IsEmpty(result.Messages);
            Assert.ContainsSingle(result.Failures);
            Assert.IsTrue(result.Failures[0].IsRateLimited);
            Assert.IsFalse(result.Failures[0].IsNotFound);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_一百二十筆_預設分成三個HTTP請求()
        {
            var handler = new RecordingHandler { RepeatLastResponse = false };
            var ids = CreateIds(120);
            handler.EnqueueMultipart(SuccessBody(ids.GetRange(0, 50)), Boundary);
            handler.EnqueueMultipart(SuccessBody(ids.GetRange(50, 50)), Boundary);
            handler.EnqueueMultipart(SuccessBody(ids.GetRange(100, 20)), Boundary);
            var client = GmailTestFactory.CreateClient(handler);

            var result = await client.BatchGetMessagesAsync(ids);

            Assert.AreEqual(3, handler.RequestCount);
            Assert.AreEqual(120, result.Messages.Count);
            Assert.IsEmpty(result.Failures);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_BatchSize為一百_一百二十筆分成兩個HTTP請求()
        {
            var handler = new RecordingHandler { RepeatLastResponse = false };
            var ids = CreateIds(120);
            handler.EnqueueMultipart(SuccessBody(ids.GetRange(0, 100)), Boundary);
            handler.EnqueueMultipart(SuccessBody(ids.GetRange(100, 20)), Boundary);

            var options = GmailTestFactory.NoDelayOptions();
            options.BatchSize = 100;
            var client = GmailTestFactory.CreateClient(handler, options);

            var result = await client.BatchGetMessagesAsync(ids);

            Assert.AreEqual(2, handler.RequestCount);
            Assert.AreEqual(120, result.Messages.Count);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_空序列_不送請求且回傳空結果()
        {
            var handler = new RecordingHandler();
            var client = GmailTestFactory.CreateClient(handler);

            var result = await client.BatchGetMessagesAsync(Array.Empty<string>());

            Assert.AreEqual(0, handler.RequestCount);
            Assert.IsEmpty(result.Messages);
            Assert.IsEmpty(result.Failures);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_識別碼清單為null_拋出ArgumentNullException()
        {
            var handler = new RecordingHandler();
            var client = GmailTestFactory.CreateClient(handler);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.BatchGetMessagesAsync(null));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task BatchGetMessagesAsync_含空白識別碼_拋出ArgumentException(string bad)
        {
            var handler = new RecordingHandler();
            var client = GmailTestFactory.CreateClient(handler);

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.BatchGetMessagesAsync(new[] { "m1", bad }));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_部件數與識別碼數不符_拋出批次解析例外()
        {
            var handler = new RecordingHandler();
            var body = new StringBuilder();
            AppendPart(body, 0, 200, "{\"id\":\"m1\"}");
            AppendClosing(body);
            handler.EnqueueMultipart(body.ToString(), Boundary);
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(
                () => client.BatchGetMessagesAsync(new[] { "m1", "m2" }));

            Assert.AreEqual("batchParseError", exception.Reason);
            Assert.AreEqual(200, exception.StatusCode);
            Assert.AreEqual("/batch/gmail/v1", exception.RequestPath);
            Assert.IsNotNull(exception.ResponseBody);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_回應沒有boundary_拋出批次解析例外()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"not\":\"multipart\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(
                () => client.BatchGetMessagesAsync(new[] { "m1" }));

            Assert.AreEqual("batchParseError", exception.Reason);
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_外層401_拋出未授權例外()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(
                HttpStatusCode.Unauthorized,
                "{\"error\":{\"code\":401,\"message\":\"Invalid Credentials\",\"errors\":[{\"reason\":\"authError\"}]}}");
            var client = GmailTestFactory.CreateClient(handler);

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(
                () => client.BatchGetMessagesAsync(new[] { "m1" }));

            Assert.AreEqual(1, handler.RequestCount);
            Assert.IsTrue(exception.IsUnauthorized);
        }

        [TestMethod]
        public void BatchResponseParser_取出帶引號與不帶引號的boundary()
        {
            Assert.IsTrue(BatchResponseParser.TryGetBoundary("multipart/mixed; boundary=batch_1", out var plain));
            Assert.AreEqual("batch_1", plain);

            Assert.IsTrue(BatchResponseParser.TryGetBoundary("multipart/mixed; boundary=\"batch_2\"; charset=utf-8", out var quoted));
            Assert.AreEqual("batch_2", quoted);

            Assert.IsFalse(BatchResponseParser.TryGetBoundary("application/json", out var missing));
            Assert.AreEqual(string.Empty, missing);
            Assert.IsFalse(BatchResponseParser.TryGetBoundary(null, out _));
        }

        [TestMethod]
        public void BatchResponseParser_解析出狀態碼ContentID與主體()
        {
            var body = new StringBuilder();
            AppendPart(body, 0, 200, "{\"id\":\"m1\"}");
            AppendPart(body, 1, 404, NotFoundJson);
            AppendClosing(body);

            var parts = BatchResponseParser.Parse(body.ToString(), Boundary);

            Assert.AreEqual(2, parts.Count);
            Assert.AreEqual(200, parts[0].StatusCode);
            Assert.AreEqual("response-item0", parts[0].ContentId);
            Assert.AreEqual("{\"id\":\"m1\"}", parts[0].Body);
            Assert.AreEqual(404, parts[1].StatusCode);
            Assert.AreEqual(NotFoundJson, parts[1].Body);
        }

        [TestMethod]
        public void BatchResponseParser_沒有ContentID時依部件順序排列()
        {
            var body = new StringBuilder();
            AppendPart(body, null, 200, "{\"id\":\"first\"}");
            AppendPart(body, null, 200, "{\"id\":\"second\"}");
            AppendClosing(body);

            var parts = BatchResponseParser.Parse(body.ToString(), Boundary);

            Assert.AreEqual(2, parts.Count);
            Assert.IsNull(parts[0].ContentId);
            Assert.IsNull(parts[1].ContentId);
            Assert.AreEqual("{\"id\":\"first\"}", parts[0].Body);
            Assert.AreEqual("{\"id\":\"second\"}", parts[1].Body);
        }

        [TestMethod]
        public void BatchResponseParser_主體或boundary為空_回傳空清單()
        {
            Assert.IsEmpty(BatchResponseParser.Parse(null, Boundary));
            Assert.IsEmpty(BatchResponseParser.Parse(string.Empty, Boundary));
            Assert.IsEmpty(BatchResponseParser.Parse("--x--", null));
        }

        [TestMethod]
        public async Task BatchGetMessagesAsync_ContentID錯序_仍依ContentID對回識別碼()
        {
            var handler = new RecordingHandler();
            var body = new StringBuilder();

            // Gmail 不保證子回應與子請求同序,對應一律看 Content-ID
            AppendPart(body, 1, 404, NotFoundJson);
            AppendPart(body, 0, 200, "{\"id\":\"m1\"}");
            AppendClosing(body);
            handler.EnqueueMultipart(body.ToString(), Boundary);
            var client = GmailTestFactory.CreateClient(handler);

            var result = await client.BatchGetMessagesAsync(new[] { "m1", "m2" });

            Assert.ContainsSingle(result.Messages);
            Assert.ContainsSingle(result.Failures);
            Assert.AreEqual("m2", result.Failures[0].Id);
        }

        /// <summary>組出一整份全部成功的 batch 回應主體。</summary>
        private static string SuccessBody(IReadOnlyList<string> ids)
        {
            var body = new StringBuilder();
            for (var i = 0; i < ids.Count; i++)
            {
                AppendPart(
                    body,
                    i,
                    200,
                    "{\"id\":\"" + ids[i] + "\",\"threadId\":\"t" + i.ToString(CultureInfo.InvariantCulture) + "\"}");
            }

            AppendClosing(body);
            return body.ToString();
        }

        /// <summary>附加一個 application/http 部件,index 為 null 時不寫 Content-ID。</summary>
        private static void AppendPart(StringBuilder body, int? index, int statusCode, string json)
        {
            body.Append("--").Append(Boundary).Append("\r\n");
            body.Append("Content-Type: application/http\r\n");

            if (index.HasValue)
            {
                body.Append("Content-ID: <response-item")
                    .Append(index.Value.ToString(CultureInfo.InvariantCulture))
                    .Append(">\r\n");
            }

            body.Append("\r\n");
            body.Append("HTTP/1.1 ").Append(statusCode.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(statusCode == 200 ? "OK" : "Error").Append("\r\n");
            body.Append("Content-Type: application/json; charset=UTF-8\r\n");
            body.Append("Content-Length: ").Append(json.Length.ToString(CultureInfo.InvariantCulture)).Append("\r\n\r\n");
            body.Append(json).Append("\r\n\r\n");
        }

        /// <summary>附加結束分隔線。</summary>
        private static void AppendClosing(StringBuilder body)
        {
            body.Append("--").Append(Boundary).Append("--\r\n");
        }

        /// <summary>產生 count 個測試用郵件識別碼。</summary>
        private static List<string> CreateIds(int count)
        {
            var ids = new List<string>(count);
            for (var i = 0; i < count; i++)
                ids.Add("m" + i.ToString(CultureInfo.InvariantCulture));

            return ids;
        }
    }
}
