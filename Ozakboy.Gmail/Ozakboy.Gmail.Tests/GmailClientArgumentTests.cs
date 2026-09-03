using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;
using Ozakboy.Gmail.Tests.TestSupport;
using Xunit;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// 參數驗證測試,對應 API 文件的 null 行為總表。所有驗證都必須在送出請求之前完成。
    /// </summary>
    public class GmailClientArgumentTests
    {
        private static (GmailClient Client, RecordingHandler Handler) Create()
        {
            var handler = new RecordingHandler();
            return (GmailTestFactory.CreateClient(handler), handler);
        }

        [Fact]
        public void 建構子_HttpClient為null_拋出ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GmailClient(null, _ => Task.FromResult("token")));
        }

        [Fact]
        public void 建構子_權杖提供者為null_拋出ArgumentNullException()
        {
            using var httpClient = new HttpClient();
            Assert.Throws<ArgumentNullException>(() => new GmailClient(httpClient, null));
        }

        [Fact]
        public void 建構子_MaxRetries為負值_拋出ArgumentOutOfRangeException()
        {
            using var httpClient = new HttpClient();
            var options = new GmailClientOptions { MaxRetries = -1 };

            Assert.Throws<ArgumentOutOfRangeException>(() => new GmailClient(httpClient, _ => Task.FromResult("token"), options));
        }

        [Fact]
        public void 建構子_options為null_採用預設值()
        {
            using var httpClient = new HttpClient();
            var client = new GmailClient(httpClient, _ => Task.FromResult("token"), null);

            Assert.NotNull(client);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetMessageAsync_識別碼為空_拋出ArgumentException(string id)
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.GetMessageAsync(id));
            Assert.Equal(0, handler.RequestCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetMessageRawAsync_識別碼為空_拋出ArgumentException(string id)
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.GetMessageRawAsync(id));
            Assert.Equal(0, handler.RequestCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task ListHistoryAsync_起始歷程識別碼為空_拋出ArgumentException(string startHistoryId)
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.ListHistoryAsync(startHistoryId));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task ModifyLabelsAsync_識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.ModifyLabelsAsync("", new[] { "SPAM" }, null));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task ModifyLabelsAsync_兩個標籤清單皆為null_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.ModifyLabelsAsync("m1", null, null));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task ModifyLabelsAsync_兩個標籤清單皆為空序列_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(
                () => client.ModifyLabelsAsync("m1", Array.Empty<string>(), Array.Empty<string>()));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task BatchModifyLabelsAsync_識別碼清單為null_拋出ArgumentNullException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentNullException>(() => client.BatchModifyLabelsAsync(null, new[] { "SPAM" }, null));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task BatchModifyLabelsAsync_超過一千筆_拋出ArgumentException()
        {
            var (client, handler) = Create();
            var ids = new List<string>();
            for (var i = 0; i < 1001; i++)
                ids.Add("m" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));

            await Assert.ThrowsAsync<ArgumentException>(() => client.BatchModifyLabelsAsync(ids, new[] { "SPAM" }, null));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task BatchModifyLabelsAsync_兩個標籤清單皆為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.BatchModifyLabelsAsync(new[] { "m1" }, null, Array.Empty<string>()));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task TrashAsync_識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.TrashAsync(null));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task UntrashAsync_識別碼為空_拋出ArgumentException()
        {
            var (client, _) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.UntrashAsync(""));
        }

        [Fact]
        public async Task ReportSpamAsync_識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.ReportSpamAsync(null));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task CreateLabelAsync_名稱為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.CreateLabelAsync("  "));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task UpdateLabelAsync_識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.UpdateLabelAsync(null, "新名稱"));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task DeleteLabelAsync_識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.DeleteLabelAsync(""));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task GetAttachmentAsync_郵件識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.GetAttachmentAsync(null, "a1"));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task GetAttachmentAsync_附件識別碼為空_拋出ArgumentException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentException>(() => client.GetAttachmentAsync("m1", ""));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task DownloadAttachmentAsync_目標串流為null_拋出ArgumentNullException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentNullException>(() => client.DownloadAttachmentAsync("m1", "a1", null));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task DownloadAttachmentAsync_目標串流不可寫_拋出ArgumentException()
        {
            var (client, handler) = Create();
            using var destination = new MemoryStream(new byte[4], false);

            await Assert.ThrowsAsync<ArgumentException>(() => client.DownloadAttachmentAsync("m1", "a1", destination));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task SendAsync_郵件為null_拋出ArgumentNullException()
        {
            var (client, handler) = Create();

            await Assert.ThrowsAsync<ArgumentNullException>(() => client.SendAsync(null));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task 取消權杖已取消_不會送出請求()
        {
            var (client, handler) = Create();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetProfileAsync(cancellation.Token));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public void MimeKit郵件可以被組出來供寄送測試使用()
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("寄件者", "sender@example.com"));
            message.To.Add(new MailboxAddress("收件者", "receiver@example.com"));
            message.Subject = "主旨";
            message.Body = new TextPart("plain") { Text = "內文" };

            Assert.Equal("主旨", message.Subject);
        }
    }
}
