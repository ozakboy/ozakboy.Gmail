using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ozakboy.Gmail.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// 參數驗證測試,對應 API 文件的 null 行為總表。所有驗證都必須在送出請求之前完成。
    /// </summary>
    [TestClass]
    public class GmailClientArgumentTests
    {
        private static (GmailClient Client, RecordingHandler Handler) Create()
        {
            var handler = new RecordingHandler();
            return (GmailTestFactory.CreateClient(handler), handler);
        }

        [TestMethod]
        public void 建構子_HttpClient為null_拋出ArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new GmailClient(null, _ => Task.FromResult("token")));
        }

        [TestMethod]
        public void 建構子_權杖提供者為null_拋出ArgumentNullException()
        {
            using var httpClient = new HttpClient();
            Assert.ThrowsExactly<ArgumentNullException>(() => new GmailClient(httpClient, null));
        }

        [TestMethod]
        public void 建構子_MaxRetries為負值_拋出ArgumentOutOfRangeException()
        {
            using var httpClient = new HttpClient();
            var options = new GmailClientOptions { MaxRetries = -1 };

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GmailClient(httpClient, _ => Task.FromResult("token"), options));
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        [DataRow(101)]
        public void 建構子_BatchSize超出範圍_拋出ArgumentOutOfRangeException(int batchSize)
        {
            using var httpClient = new HttpClient();
            var options = new GmailClientOptions { BatchSize = batchSize };

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GmailClient(httpClient, _ => Task.FromResult("token"), options));
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(50)]
        [DataRow(100)]
        public void 建構子_BatchSize在範圍內_可正常建立(int batchSize)
        {
            using var httpClient = new HttpClient();
            var options = new GmailClientOptions { BatchSize = batchSize };

            Assert.IsNotNull(new GmailClient(httpClient, _ => Task.FromResult("token"), options));
        }

        [TestMethod]
        public void 建構子_MaxRetryDelay為負值_拋出ArgumentOutOfRangeException()
        {
            using var httpClient = new HttpClient();
            var options = new GmailClientOptions { MaxRetryDelay = TimeSpan.FromSeconds(-1) };

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GmailClient(httpClient, _ => Task.FromResult("token"), options));
        }

        [TestMethod]
        public void 建構子_MaxRetryDelay為null_可正常建立()
        {
            using var httpClient = new HttpClient();
            var options = new GmailClientOptions { MaxRetryDelay = null };

            Assert.IsNotNull(new GmailClient(httpClient, _ => Task.FromResult("token"), options));
        }

        [TestMethod]
        public void OAuth建構子_MaxRetryDelay為負值_拋出ArgumentOutOfRangeException()
        {
            using var httpClient = new HttpClient();
            var oauthOptions = new Ozakboy.Gmail.OAuth.GoogleOAuthOptions
            {
                ClientId = "client-id.apps.googleusercontent.com",
                ClientSecret = "client-secret",
            };
            var options = new GmailClientOptions { MaxRetryDelay = TimeSpan.FromSeconds(-1) };

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => new Ozakboy.Gmail.OAuth.GoogleOAuthClient(httpClient, oauthOptions, options));
        }

        [TestMethod]
        public void 建構子_options為null_採用預設值()
        {
            using var httpClient = new HttpClient();
            var client = new GmailClient(httpClient, _ => Task.FromResult("token"), null);

            Assert.IsNotNull(client);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task GetMessageAsync_識別碼為空_拋出ArgumentException(string id)
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.GetMessageAsync(id));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public async Task GetMessageRawAsync_識別碼為空_拋出ArgumentException(string id)
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.GetMessageRawAsync(id));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public async Task ListHistoryAsync_起始歷程識別碼為空_拋出ArgumentException(string startHistoryId)
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.ListHistoryAsync(startHistoryId));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task ModifyLabelsAsync_識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.ModifyLabelsAsync("", new[] { "SPAM" }, null));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task ModifyLabelsAsync_兩個標籤清單皆為null_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.ModifyLabelsAsync("m1", null, null));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task ModifyLabelsAsync_兩個標籤清單皆為空序列_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => client.ModifyLabelsAsync("m1", Array.Empty<string>(), Array.Empty<string>()));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task BatchModifyLabelsAsync_識別碼清單為null_拋出ArgumentNullException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.BatchModifyLabelsAsync(null, new[] { "SPAM" }, null));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task BatchModifyLabelsAsync_超過一千筆_拋出ArgumentException()
        {
            var (client, handler) = Create();
            var ids = new List<string>();
            for (var i = 0; i < 1001; i++)
                ids.Add("m" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.BatchModifyLabelsAsync(ids, new[] { "SPAM" }, null));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task BatchModifyLabelsAsync_兩個標籤清單皆為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.BatchModifyLabelsAsync(new[] { "m1" }, null, Array.Empty<string>()));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task TrashAsync_識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.TrashAsync(null));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task UntrashAsync_識別碼為空_拋出ArgumentException()
        {
            var (client, _) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.UntrashAsync(""));
        }

        [TestMethod]
        public async Task ReportSpamAsync_識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.ReportSpamAsync(null));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task CreateLabelAsync_名稱為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.CreateLabelAsync("  "));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task UpdateLabelAsync_識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.UpdateLabelAsync(null, "新名稱"));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task DeleteLabelAsync_識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.DeleteLabelAsync(""));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task GetAttachmentAsync_郵件識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.GetAttachmentAsync(null, "a1"));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task GetAttachmentAsync_附件識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.GetAttachmentAsync("m1", ""));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task DownloadAttachmentAsync_目標串流為null_拋出ArgumentNullException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.DownloadAttachmentAsync("m1", "a1", null));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task DownloadAttachmentAsync_目標串流不可寫_拋出ArgumentException()
        {
            var (client, handler) = Create();
            using var destination = new MemoryStream(new byte[4], false);

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.DownloadAttachmentAsync("m1", "a1", destination));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task SendAsync_郵件為null_拋出ArgumentNullException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.SendAsync(null));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task 取消權杖已取消_不會送出請求()
        {
            var (client, handler) = Create();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetProfileAsync(cancellation.Token));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task SendAsync_郵件為null_拋ArgumentNullException()
        {
            var client = GmailTestFactory.CreateClient(new RecordingHandler());

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.SendAsync(null));
        }

        [TestMethod]
        public async Task SendRawAsync_位元組為null_拋ArgumentNullException()
        {
            var client = GmailTestFactory.CreateClient(new RecordingHandler());

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.SendRawAsync(null));
        }

        [TestMethod]
        public async Task SendRawAsync_位元組為空_拋ArgumentException()
        {
            var handler = new RecordingHandler();
            var client = GmailTestFactory.CreateClient(handler);

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.SendRawAsync(Array.Empty<byte>()));
            Assert.AreEqual(0, handler.RequestCount);
        }
    }
}
