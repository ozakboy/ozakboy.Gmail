using System;
using System.Threading.Tasks;
using Ozakboy.Gmail.Tests.TestSupport;
using Xunit;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// 討論串端點的請求組裝與回應解析測試。全部離線,靠 RecordingHandler 假造回應。
    /// </summary>
    public class GmailThreadTests
    {
        private const string BaseUrl = "https://gmail.googleapis.com/gmail/v1/users/me/";

        private const string ThreadJson =
            "{\"id\":\"t1\",\"historyId\":\"9001\",\"snippet\":\"哈囉\",\"messages\":[" +
            "{\"id\":\"m1\",\"threadId\":\"t1\",\"labelIds\":[\"INBOX\"]}," +
            "{\"id\":\"m2\",\"threadId\":\"t1\",\"labelIds\":[\"INBOX\",\"UNREAD\"]}]}";

        [Fact]
        public async Task GetThreadAsync_預設格式為full()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetThreadAsync("t1");

            Assert.Equal("GET", handler.LastRequest.Method);
            Assert.Equal(BaseUrl + "threads/t1?format=full", handler.LastRequest.Url);
            Assert.Equal("Bearer " + GmailTestFactory.AccessToken, handler.LastRequest.Authorization);
        }

        [Fact]
        public async Task GetThreadAsync_Metadata格式_送出重複的metadataHeaders()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetThreadAsync("t1", GmailMessageFormat.Metadata, new[] { "From", "Subject" });

            Assert.Equal(
                BaseUrl + "threads/t1?format=metadata&metadataHeaders=From&metadataHeaders=Subject",
                handler.LastRequest.Url);
        }

        [Fact]
        public async Task GetThreadAsync_非Metadata格式_不送metadataHeaders()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetThreadAsync("t1", GmailMessageFormat.Minimal, new[] { "From" });

            Assert.Equal(BaseUrl + "threads/t1?format=minimal", handler.LastRequest.Url);
        }

        [Fact]
        public async Task GetThreadAsync_解析出討論串與串上的多封郵件()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            var thread = await client.GetThreadAsync("t1");

            Assert.Equal("t1", thread.Id);
            Assert.Equal("9001", thread.HistoryId);
            Assert.Equal("哈囉", thread.Snippet);
            Assert.Equal(2, thread.Messages.Count);
            Assert.Equal("m2", thread.Messages[1].Id);
            Assert.Contains("UNREAD", thread.Messages[1].LabelIds);
        }

        [Fact]
        public async Task GetThreadAsync_回應沒有messages_清單為空而非null()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"t1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var thread = await client.GetThreadAsync("t1");

            Assert.NotNull(thread.Messages);
            Assert.Empty(thread.Messages);
        }

        [Fact]
        public async Task GetThreadAsync_識別碼會做URL轉義()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetThreadAsync("a/b");

            Assert.Equal(BaseUrl + "threads/a%2Fb?format=full", handler.LastRequest.Url);
        }

        [Fact]
        public async Task ModifyThreadAsync_送出POST與標籤主體()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            var thread = await client.ModifyThreadAsync("t1", new[] { "STARRED" }, new[] { "UNREAD" });

            Assert.Equal("POST", handler.LastRequest.Method);
            Assert.Equal(BaseUrl + "threads/t1/modify", handler.LastRequest.Url);
            Assert.Equal("{\"addLabelIds\":[\"STARRED\"],\"removeLabelIds\":[\"UNREAD\"]}", handler.LastRequest.Body);
            Assert.Equal("t1", thread.Id);
        }

        [Fact]
        public async Task ModifyThreadAsync_只有其中一邊_另一邊不寫進主體()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            await client.ModifyThreadAsync("t1", null, new[] { "INBOX" });

            Assert.Equal("{\"removeLabelIds\":[\"INBOX\"]}", handler.LastRequest.Body);
        }

        [Fact]
        public async Task ModifyThreadAsync_識別碼為空_拋出ArgumentException()
        {
            var handler = new RecordingHandler();
            var client = GmailTestFactory.CreateClient(handler);

            await Assert.ThrowsAsync<ArgumentException>(() => client.ModifyThreadAsync("", new[] { "INBOX" }, null));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task ModifyThreadAsync_兩個標籤清單皆為空_拋出ArgumentException()
        {
            var handler = new RecordingHandler();
            var client = GmailTestFactory.CreateClient(handler);

            await Assert.ThrowsAsync<ArgumentException>(
                () => client.ModifyThreadAsync("t1", Array.Empty<string>(), null));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task TrashThreadAsync_送出POST到trash端點()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            var thread = await client.TrashThreadAsync("t1");

            Assert.Equal("POST", handler.LastRequest.Method);
            Assert.Equal(BaseUrl + "threads/t1/trash", handler.LastRequest.Url);
            Assert.Null(handler.LastRequest.Body);
            Assert.Equal(2, thread.Messages.Count);
        }

        [Fact]
        public async Task UntrashThreadAsync_送出POST到untrash端點()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            await client.UntrashThreadAsync("t1");

            Assert.Equal("POST", handler.LastRequest.Method);
            Assert.Equal(BaseUrl + "threads/t1/untrash", handler.LastRequest.Url);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task 討論串端點_識別碼為空_一律拋出ArgumentException(string id)
        {
            var handler = new RecordingHandler();
            var client = GmailTestFactory.CreateClient(handler);

            await Assert.ThrowsAsync<ArgumentException>(() => client.GetThreadAsync(id));
            await Assert.ThrowsAsync<ArgumentException>(() => client.TrashThreadAsync(id));
            await Assert.ThrowsAsync<ArgumentException>(() => client.UntrashThreadAsync(id));
            Assert.Equal(0, handler.RequestCount);
        }
    }
}
