using System;
using System.Threading.Tasks;
using Ozakboy.Gmail.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// 討論串端點的請求組裝與回應解析測試。全部離線,靠 RecordingHandler 假造回應。
    /// </summary>
    [TestClass]
    public class GmailThreadTests
    {
        private const string BaseUrl = "https://gmail.googleapis.com/gmail/v1/users/me/";

        private const string ThreadJson =
            "{\"id\":\"t1\",\"historyId\":\"9001\",\"snippet\":\"哈囉\",\"messages\":[" +
            "{\"id\":\"m1\",\"threadId\":\"t1\",\"labelIds\":[\"INBOX\"]}," +
            "{\"id\":\"m2\",\"threadId\":\"t1\",\"labelIds\":[\"INBOX\",\"UNREAD\"]}]}";

        [TestMethod]
        public async Task GetThreadAsync_預設格式為full()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetThreadAsync("t1");

            Assert.AreEqual("GET", handler.LastRequest.Method);
            Assert.AreEqual(BaseUrl + "threads/t1?format=full", handler.LastRequest.Url);
            Assert.AreEqual("Bearer " + GmailTestFactory.AccessToken, handler.LastRequest.Authorization);
        }

        [TestMethod]
        public async Task GetThreadAsync_Metadata格式_送出重複的metadataHeaders()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetThreadAsync("t1", GmailMessageFormat.Metadata, new[] { "From", "Subject" });

            Assert.AreEqual(
                BaseUrl + "threads/t1?format=metadata&metadataHeaders=From&metadataHeaders=Subject",
                handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task GetThreadAsync_非Metadata格式_不送metadataHeaders()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetThreadAsync("t1", GmailMessageFormat.Minimal, new[] { "From" });

            Assert.AreEqual(BaseUrl + "threads/t1?format=minimal", handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task GetThreadAsync_解析出討論串與串上的多封郵件()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            var thread = await client.GetThreadAsync("t1");

            Assert.AreEqual("t1", thread.Id);
            Assert.AreEqual("9001", thread.HistoryId);
            Assert.AreEqual("哈囉", thread.Snippet);
            Assert.AreEqual(2, thread.Messages.Count);
            Assert.AreEqual("m2", thread.Messages[1].Id);
            Assert.Contains("UNREAD", thread.Messages[1].LabelIds);
        }

        [TestMethod]
        public async Task GetThreadAsync_回應沒有messages_清單為空而非null()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"t1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var thread = await client.GetThreadAsync("t1");

            Assert.IsNotNull(thread.Messages);
            Assert.IsEmpty(thread.Messages);
        }

        [TestMethod]
        public async Task GetThreadAsync_識別碼會做URL轉義()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetThreadAsync("a/b");

            Assert.AreEqual(BaseUrl + "threads/a%2Fb?format=full", handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task ModifyThreadAsync_送出POST與標籤主體()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            var thread = await client.ModifyThreadAsync("t1", new[] { "STARRED" }, new[] { "UNREAD" });

            Assert.AreEqual("POST", handler.LastRequest.Method);
            Assert.AreEqual(BaseUrl + "threads/t1/modify", handler.LastRequest.Url);
            Assert.AreEqual("{\"addLabelIds\":[\"STARRED\"],\"removeLabelIds\":[\"UNREAD\"]}", handler.LastRequest.Body);
            Assert.AreEqual("t1", thread.Id);
        }

        [TestMethod]
        public async Task ModifyThreadAsync_只有其中一邊_另一邊不寫進主體()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            await client.ModifyThreadAsync("t1", null, new[] { "INBOX" });

            Assert.AreEqual("{\"removeLabelIds\":[\"INBOX\"]}", handler.LastRequest.Body);
        }

        [TestMethod]
        public async Task ModifyThreadAsync_識別碼為空_拋出ArgumentException()
        {
            var handler = new RecordingHandler();
            var client = GmailTestFactory.CreateClient(handler);

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.ModifyThreadAsync("", new[] { "INBOX" }, null));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task ModifyThreadAsync_兩個標籤清單皆為空_拋出ArgumentException()
        {
            var handler = new RecordingHandler();
            var client = GmailTestFactory.CreateClient(handler);

            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => client.ModifyThreadAsync("t1", Array.Empty<string>(), null));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task TrashThreadAsync_送出POST到trash端點()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            var thread = await client.TrashThreadAsync("t1");

            Assert.AreEqual("POST", handler.LastRequest.Method);
            Assert.AreEqual(BaseUrl + "threads/t1/trash", handler.LastRequest.Url);
            Assert.IsNull(handler.LastRequest.Body);
            Assert.AreEqual(2, thread.Messages.Count);
        }

        [TestMethod]
        public async Task UntrashThreadAsync_送出POST到untrash端點()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(ThreadJson);
            var client = GmailTestFactory.CreateClient(handler);

            await client.UntrashThreadAsync("t1");

            Assert.AreEqual("POST", handler.LastRequest.Method);
            Assert.AreEqual(BaseUrl + "threads/t1/untrash", handler.LastRequest.Url);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task 討論串端點_識別碼為空_一律拋出ArgumentException(string id)
        {
            var handler = new RecordingHandler();
            var client = GmailTestFactory.CreateClient(handler);

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.GetThreadAsync(id));
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.TrashThreadAsync(id));
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.UntrashThreadAsync(id));
            Assert.AreEqual(0, handler.RequestCount);
        }
    }
}
