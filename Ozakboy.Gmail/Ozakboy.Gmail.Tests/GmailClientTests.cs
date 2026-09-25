using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ozakboy.Gmail.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// GmailClient 各方法的請求組裝與回應解析測試。全部離線,靠 RecordingHandler 假造回應。
    /// </summary>
    [TestClass]
    public class GmailClientTests
    {
        private const string BaseUrl = "https://gmail.googleapis.com/gmail/v1/users/me/";

        [TestMethod]
        public async Task GetProfileAsync_送出GET與正確網址並掛上Bearer()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"emailAddress\":\"user@example.com\",\"messagesTotal\":\"1200\",\"threadsTotal\":\"900\",\"historyId\":\"123456\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var profile = await client.GetProfileAsync();

            Assert.AreEqual("GET", handler.LastRequest.Method);
            Assert.AreEqual(BaseUrl + "profile", handler.LastRequest.Url);
            Assert.AreEqual("Bearer " + GmailTestFactory.AccessToken, handler.LastRequest.Authorization);
            Assert.AreEqual("user@example.com", profile.EmailAddress);
            Assert.AreEqual(1200, profile.MessagesTotal);
            Assert.AreEqual(900, profile.ThreadsTotal);
            Assert.AreEqual("123456", profile.HistoryId);
        }

        [TestMethod]
        public async Task ListMessagesAsync_有查詢與標籤_組出正確查詢字串()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"messages\":[],\"resultSizeEstimate\":0}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.ListMessagesAsync("newer_than:30d", new[] { "INBOX", "UNREAD" }, 25, "PAGE-2", true);

            Assert.AreEqual(
                BaseUrl + "messages?q=newer_than%3A30d&labelIds=INBOX&labelIds=UNREAD&maxResults=25&pageToken=PAGE-2&includeSpamTrash=true",
                handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task ListMessagesAsync_全部參數為預設_不送任何查詢參數()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.ListMessagesAsync();

            Assert.AreEqual(BaseUrl + "messages", handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task ListMessagesAsync_空標籤序列_不送labelIds()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.ListMessagesAsync(labelIds: Array.Empty<string>());

            Assert.AreEqual(BaseUrl + "messages", handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task ListMessagesAsync_回應解析出郵件參考與分頁資訊()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"messages\":[{\"id\":\"m1\",\"threadId\":\"t1\"},{\"id\":\"m2\",\"threadId\":\"t2\"}],\"nextPageToken\":\"NEXT\",\"resultSizeEstimate\":\"42\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var list = await client.ListMessagesAsync();

            Assert.AreEqual(2, list.Messages.Count);
            Assert.AreEqual("m1", list.Messages[0].Id);
            Assert.AreEqual("t2", list.Messages[1].ThreadId);
            Assert.AreEqual("NEXT", list.NextPageToken);
            Assert.AreEqual(42, list.ResultSizeEstimate);
        }

        [TestMethod]
        public async Task ListMessagesAsync_回應沒有messages_清單為空而非null()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"resultSizeEstimate\":0}");
            var client = GmailTestFactory.CreateClient(handler);

            var list = await client.ListMessagesAsync();

            Assert.IsNotNull(list.Messages);
            Assert.IsEmpty(list.Messages);
        }

        [TestMethod]
        public async Task GetMessageAsync_預設格式為full()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetMessageAsync("m1");

            Assert.AreEqual(BaseUrl + "messages/m1?format=full", handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task GetMessageAsync_Metadata格式_送出重複的metadataHeaders()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetMessageAsync("m1", GmailMessageFormat.Metadata, new[] { "From", "Subject", "Date" });

            Assert.AreEqual(
                BaseUrl + "messages/m1?format=metadata&metadataHeaders=From&metadataHeaders=Subject&metadataHeaders=Date",
                handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task GetMessageAsync_非Metadata格式_不送metadataHeaders()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetMessageAsync("m1", GmailMessageFormat.Minimal, new[] { "From" });

            Assert.AreEqual(BaseUrl + "messages/m1?format=minimal", handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task GetMessageAsync_識別碼會被轉義()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"a b\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetMessageAsync("a b");

            Assert.AreEqual(BaseUrl + "messages/a%20b?format=full", handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task GetMessageAsync_解析出payload與標頭()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(
                "{\"id\":\"m1\",\"threadId\":\"t1\",\"labelIds\":[\"INBOX\",\"UNREAD\"],\"snippet\":\"摘要\",\"historyId\":\"98765\"," +
                "\"internalDate\":\"1725000000000\",\"sizeEstimate\":\"2048\"," +
                "\"payload\":{\"partId\":\"\",\"mimeType\":\"text/plain\",\"filename\":\"\"," +
                "\"headers\":[{\"name\":\"From\",\"value\":\"sender@example.com\"},{\"name\":\"Subject\",\"value\":\"主旨\"}]," +
                "\"body\":{\"size\":\"12\",\"data\":\"5ZWK\"}}}");
            var client = GmailTestFactory.CreateClient(handler);

            var message = await client.GetMessageAsync("m1");

            Assert.AreEqual("m1", message.Id);
            CollectionAssert.AreEqual(new[] { "INBOX", "UNREAD" }, message.LabelIds);
            Assert.AreEqual("98765", message.HistoryId);
            Assert.AreEqual(1725000000000L, message.InternalDate);
            Assert.AreEqual(2048, message.SizeEstimate);
            Assert.AreEqual("text/plain", message.Payload.MimeType);
            Assert.AreEqual("sender@example.com", message.GetHeader("from"));
            Assert.AreEqual(12, message.Payload.Body.Size);
        }

        [TestMethod]
        public async Task GetMessageRawAsync_以raw格式取回並解碼成RFC822位元組()
        {
            const string Rfc822 = "From: sender@example.com\r\nTo: receiver@example.com\r\nSubject: 測試主旨\r\n\r\n內文";
            var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(Rfc822)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\",\"raw\":\"" + raw + "\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var bytes = await client.GetMessageRawAsync("m1");

            Assert.AreEqual(BaseUrl + "messages/m1?format=raw", handler.LastRequest.Url);
            Assert.AreEqual(Rfc822, Encoding.UTF8.GetString(bytes));
        }

        [TestMethod]
        public async Task GetMessageRawAsync_回應缺raw欄位_回空陣列()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var bytes = await client.GetMessageRawAsync("m1");

            Assert.IsEmpty(bytes);
        }

        [TestMethod]
        public async Task ListHistoryAsync_組出重複的historyTypes與其他參數()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"history\":[],\"historyId\":\"1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.ListHistoryAsync(
                "12345",
                new[] { GmailHistoryType.MessageAdded, GmailHistoryType.LabelRemoved },
                "PAGE-9",
                "INBOX",
                100);

            Assert.AreEqual(
                BaseUrl + "history?startHistoryId=12345&historyTypes=messageAdded&historyTypes=labelRemoved&pageToken=PAGE-9&labelId=INBOX&maxResults=100",
                handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task ListHistoryAsync_解析變更紀錄()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(
                "{\"history\":[{\"id\":\"777\",\"messagesAdded\":[{\"message\":{\"id\":\"m9\",\"threadId\":\"t9\",\"labelIds\":[\"INBOX\"]}}]," +
                "\"labelsRemoved\":[{\"message\":{\"id\":\"m8\"},\"labelIds\":[\"UNREAD\"]}]}],\"historyId\":\"888\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var history = await client.ListHistoryAsync("100");

            var record = Assert.ContainsSingle(history.History);
            Assert.AreEqual("777", record.Id);
            Assert.AreEqual("m9", Assert.ContainsSingle(record.MessagesAdded).Message.Id);
            Assert.AreEqual("UNREAD", Assert.ContainsSingle(Assert.ContainsSingle(record.LabelsRemoved).LabelIds));
            Assert.IsEmpty(record.MessagesDeleted);
            Assert.AreEqual("888", history.HistoryId);
        }

        [TestMethod]
        public async Task ModifyLabelsAsync_送出POST與兩個標籤清單()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\",\"labelIds\":[\"STARRED\"]}");
            var client = GmailTestFactory.CreateClient(handler);

            var message = await client.ModifyLabelsAsync("m1", new[] { "STARRED" }, new[] { "UNREAD" });

            Assert.AreEqual("POST", handler.LastRequest.Method);
            Assert.AreEqual(BaseUrl + "messages/m1/modify", handler.LastRequest.Url);
            Assert.AreEqual("{\"addLabelIds\":[\"STARRED\"],\"removeLabelIds\":[\"UNREAD\"]}", handler.LastRequest.Body);
            Assert.Contains("application/json", handler.LastRequest.ContentType, StringComparison.OrdinalIgnoreCase);
            Assert.AreEqual("STARRED", Assert.ContainsSingle(message.LabelIds));
        }

        [TestMethod]
        public async Task ModifyLabelsAsync_只給移除清單_body不含addLabelIds()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.ModifyLabelsAsync("m1", null, new[] { "UNREAD" });

            Assert.AreEqual("{\"removeLabelIds\":[\"UNREAD\"]}", handler.LastRequest.Body);
        }

        [TestMethod]
        public async Task BatchModifyLabelsAsync_送出批次主體且不解析回應()
        {
            var handler = new RecordingHandler();
            handler.EnqueueEmpty();
            var client = GmailTestFactory.CreateClient(handler);

            await client.BatchModifyLabelsAsync(new[] { "m1", "m2" }, new[] { "SPAM" }, null);

            Assert.AreEqual("POST", handler.LastRequest.Method);
            Assert.AreEqual(BaseUrl + "messages/batchModify", handler.LastRequest.Url);
            Assert.AreEqual("{\"ids\":[\"m1\",\"m2\"],\"addLabelIds\":[\"SPAM\"]}", handler.LastRequest.Body);
        }

        [TestMethod]
        public async Task BatchModifyLabelsAsync_識別碼為空_不送任何請求()
        {
            var handler = new RecordingHandler();
            var client = GmailTestFactory.CreateClient(handler);

            await client.BatchModifyLabelsAsync(Array.Empty<string>(), new[] { "SPAM" }, null);

            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task TrashAsync_送出trash端點()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\",\"labelIds\":[\"TRASH\"]}");
            var client = GmailTestFactory.CreateClient(handler);

            var message = await client.TrashAsync("m1");

            Assert.AreEqual("POST", handler.LastRequest.Method);
            Assert.AreEqual(BaseUrl + "messages/m1/trash", handler.LastRequest.Url);
            Assert.AreEqual("TRASH", Assert.ContainsSingle(message.LabelIds));
        }

        [TestMethod]
        public async Task UntrashAsync_送出untrash端點()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.UntrashAsync("m1");

            Assert.AreEqual("POST", handler.LastRequest.Method);
            Assert.AreEqual(BaseUrl + "messages/m1/untrash", handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task ReportSpamAsync_加上SPAM並移除INBOX()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.ReportSpamAsync("m1");

            Assert.AreEqual(BaseUrl + "messages/m1/modify", handler.LastRequest.Url);
            Assert.AreEqual("{\"addLabelIds\":[\"SPAM\"],\"removeLabelIds\":[\"INBOX\"]}", handler.LastRequest.Body);
        }

        [TestMethod]
        public async Task ListLabelsAsync_解析labels陣列()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(
                "{\"labels\":[{\"id\":\"INBOX\",\"name\":\"INBOX\",\"type\":\"system\"}," +
                "{\"id\":\"Label_1\",\"name\":\"Sower/Ads\",\"type\":\"user\",\"messagesTotal\":\"5\",\"color\":{\"backgroundColor\":\"#ffffff\",\"textColor\":\"#000000\"}}]}");
            var client = GmailTestFactory.CreateClient(handler);

            var labels = await client.ListLabelsAsync();

            Assert.AreEqual("GET", handler.LastRequest.Method);
            Assert.AreEqual(BaseUrl + "labels", handler.LastRequest.Url);
            Assert.AreEqual(2, labels.Count);
            Assert.AreEqual("Sower/Ads", labels[1].Name);
            Assert.AreEqual(5, labels[1].MessagesTotal);
            Assert.AreEqual("#ffffff", labels[1].Color.BackgroundColor);
        }

        [TestMethod]
        public async Task ListLabelsAsync_回應沒有labels_回空清單()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var client = GmailTestFactory.CreateClient(handler);

            var labels = await client.ListLabelsAsync();

            Assert.IsNotNull(labels);
            Assert.IsEmpty(labels);
        }

        [TestMethod]
        public async Task CreateLabelAsync_送出名稱顯示方式與顏色()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"Label_2\",\"name\":\"Sower\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var options = new GmailLabelOptions
            {
                LabelListVisibility = "labelShow",
                MessageListVisibility = "show",
                BackgroundColor = "#ffffff",
                TextColor = "#000000",
            };

            var label = await client.CreateLabelAsync("Sower", options);

            Assert.AreEqual("POST", handler.LastRequest.Method);
            Assert.AreEqual(BaseUrl + "labels", handler.LastRequest.Url);
            Assert.AreEqual(
                "{\"name\":\"Sower\",\"labelListVisibility\":\"labelShow\",\"messageListVisibility\":\"show\",\"color\":{\"backgroundColor\":\"#ffffff\",\"textColor\":\"#000000\"}}",
                handler.LastRequest.Body);
            Assert.AreEqual("Label_2", label.Id);
        }

        [TestMethod]
        public async Task CreateLabelAsync_未給選項_主體只含名稱()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"Label_3\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.CreateLabelAsync("Sower/Ads");

            Assert.AreEqual("{\"name\":\"Sower/Ads\"}", handler.LastRequest.Body);
        }

        [TestMethod]
        public async Task UpdateLabelAsync_使用PATCH並帶新名稱()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"Label_2\",\"name\":\"Sower/Renamed\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.UpdateLabelAsync("Label_2", "Sower/Renamed");

            Assert.AreEqual("PATCH", handler.LastRequest.Method);
            Assert.AreEqual(BaseUrl + "labels/Label_2", handler.LastRequest.Url);
            Assert.AreEqual("{\"name\":\"Sower/Renamed\"}", handler.LastRequest.Body);
        }

        [TestMethod]
        public async Task UpdateLabelAsync_名稱為null_主體不含name()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"Label_2\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.UpdateLabelAsync("Label_2", null, new GmailLabelOptions { LabelListVisibility = "labelHide" });

            Assert.AreEqual("{\"labelListVisibility\":\"labelHide\"}", handler.LastRequest.Body);
        }

        [TestMethod]
        public async Task DeleteLabelAsync_使用DELETE且接受204()
        {
            var handler = new RecordingHandler();
            handler.EnqueueEmpty();
            var client = GmailTestFactory.CreateClient(handler);

            await client.DeleteLabelAsync("Label_2");

            Assert.AreEqual("DELETE", handler.LastRequest.Method);
            Assert.AreEqual(BaseUrl + "labels/Label_2", handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task GetAttachmentAsync_解碼base64url內容()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"attachmentId\":\"a1\",\"size\":\"5\",\"data\":\"aGVsbG8\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var attachment = await client.GetAttachmentAsync("m1", "a1");

            Assert.AreEqual(BaseUrl + "messages/m1/attachments/a1", handler.LastRequest.Url);
            Assert.AreEqual("a1", attachment.AttachmentId);
            Assert.AreEqual(5, attachment.Size);
            Assert.AreEqual("hello", Encoding.UTF8.GetString(attachment.Data));
        }

        [TestMethod]
        public async Task GetAttachmentAsync_沒有data_回空陣列()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"attachmentId\":\"a1\",\"size\":\"0\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var attachment = await client.GetAttachmentAsync("m1", "a1");

            Assert.IsNotNull(attachment.Data);
            Assert.IsEmpty(attachment.Data);
        }

        [TestMethod]
        public async Task DownloadAttachmentAsync_寫入串流並回傳位元組數且不關閉串流()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"attachmentId\":\"a1\",\"size\":\"5\",\"data\":\"aGVsbG8\"}");
            var client = GmailTestFactory.CreateClient(handler);

            using var destination = new MemoryStream();
            var written = await client.DownloadAttachmentAsync("m1", "a1", destination);

            Assert.AreEqual(5, written);
            Assert.AreEqual("hello", Encoding.UTF8.GetString(destination.ToArray()));
            Assert.IsTrue(destination.CanWrite);
        }

        [TestMethod]
        public async Task 自訂UserId會出現在網址且被轉義()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var client = GmailTestFactory.CreateClient(handler, GmailTestFactory.NoDelayOptions(userId: "user@example.com"));

            await client.GetProfileAsync();

            Assert.AreEqual("https://gmail.googleapis.com/gmail/v1/users/user%40example.com/profile", handler.LastRequest.Url);
        }

        [TestMethod]
        public async Task 建構後再改options不影響已建立的用戶端()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var options = GmailTestFactory.NoDelayOptions();
            var client = GmailTestFactory.CreateClient(handler, options);

            options.UserId = "someone@example.com";
            await client.GetProfileAsync();

            Assert.AreEqual(BaseUrl + "profile", handler.LastRequest.Url);
        }
    }
}
