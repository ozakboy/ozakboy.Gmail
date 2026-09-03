using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ozakboy.Gmail.Tests.TestSupport;
using Xunit;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// GmailClient 各方法的請求組裝與回應解析測試。全部離線,靠 RecordingHandler 假造回應。
    /// </summary>
    public class GmailClientTests
    {
        private const string BaseUrl = "https://gmail.googleapis.com/gmail/v1/users/me/";

        [Fact]
        public async Task GetProfileAsync_送出GET與正確網址並掛上Bearer()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"emailAddress\":\"user@example.com\",\"messagesTotal\":\"1200\",\"threadsTotal\":\"900\",\"historyId\":\"123456\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var profile = await client.GetProfileAsync();

            Assert.Equal("GET", handler.LastRequest.Method);
            Assert.Equal(BaseUrl + "profile", handler.LastRequest.Url);
            Assert.Equal("Bearer " + GmailTestFactory.AccessToken, handler.LastRequest.Authorization);
            Assert.Equal("user@example.com", profile.EmailAddress);
            Assert.Equal(1200, profile.MessagesTotal);
            Assert.Equal(900, profile.ThreadsTotal);
            Assert.Equal("123456", profile.HistoryId);
        }

        [Fact]
        public async Task ListMessagesAsync_有查詢與標籤_組出正確查詢字串()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"messages\":[],\"resultSizeEstimate\":0}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.ListMessagesAsync("newer_than:30d", new[] { "INBOX", "UNREAD" }, 25, "PAGE-2", true);

            Assert.Equal(
                BaseUrl + "messages?q=newer_than%3A30d&labelIds=INBOX&labelIds=UNREAD&maxResults=25&pageToken=PAGE-2&includeSpamTrash=true",
                handler.LastRequest.Url);
        }

        [Fact]
        public async Task ListMessagesAsync_全部參數為預設_不送任何查詢參數()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.ListMessagesAsync();

            Assert.Equal(BaseUrl + "messages", handler.LastRequest.Url);
        }

        [Fact]
        public async Task ListMessagesAsync_空標籤序列_不送labelIds()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.ListMessagesAsync(labelIds: Array.Empty<string>());

            Assert.Equal(BaseUrl + "messages", handler.LastRequest.Url);
        }

        [Fact]
        public async Task ListMessagesAsync_回應解析出郵件參考與分頁資訊()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"messages\":[{\"id\":\"m1\",\"threadId\":\"t1\"},{\"id\":\"m2\",\"threadId\":\"t2\"}],\"nextPageToken\":\"NEXT\",\"resultSizeEstimate\":\"42\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var list = await client.ListMessagesAsync();

            Assert.Equal(2, list.Messages.Count);
            Assert.Equal("m1", list.Messages[0].Id);
            Assert.Equal("t2", list.Messages[1].ThreadId);
            Assert.Equal("NEXT", list.NextPageToken);
            Assert.Equal(42, list.ResultSizeEstimate);
        }

        [Fact]
        public async Task ListMessagesAsync_回應沒有messages_清單為空而非null()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"resultSizeEstimate\":0}");
            var client = GmailTestFactory.CreateClient(handler);

            var list = await client.ListMessagesAsync();

            Assert.NotNull(list.Messages);
            Assert.Empty(list.Messages);
        }

        [Fact]
        public async Task GetMessageAsync_預設格式為full()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetMessageAsync("m1");

            Assert.Equal(BaseUrl + "messages/m1?format=full", handler.LastRequest.Url);
        }

        [Fact]
        public async Task GetMessageAsync_Metadata格式_送出重複的metadataHeaders()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetMessageAsync("m1", GmailMessageFormat.Metadata, new[] { "From", "Subject", "Date" });

            Assert.Equal(
                BaseUrl + "messages/m1?format=metadata&metadataHeaders=From&metadataHeaders=Subject&metadataHeaders=Date",
                handler.LastRequest.Url);
        }

        [Fact]
        public async Task GetMessageAsync_非Metadata格式_不送metadataHeaders()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetMessageAsync("m1", GmailMessageFormat.Minimal, new[] { "From" });

            Assert.Equal(BaseUrl + "messages/m1?format=minimal", handler.LastRequest.Url);
        }

        [Fact]
        public async Task GetMessageAsync_識別碼會被轉義()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"a b\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.GetMessageAsync("a b");

            Assert.Equal(BaseUrl + "messages/a%20b?format=full", handler.LastRequest.Url);
        }

        [Fact]
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

            Assert.Equal("m1", message.Id);
            Assert.Equal(new[] { "INBOX", "UNREAD" }, message.LabelIds);
            Assert.Equal("98765", message.HistoryId);
            Assert.Equal(1725000000000L, message.InternalDate);
            Assert.Equal(2048, message.SizeEstimate);
            Assert.Equal("text/plain", message.Payload.MimeType);
            Assert.Equal("sender@example.com", message.GetHeader("from"));
            Assert.Equal(12, message.Payload.Body.Size);
        }

        [Fact]
        public async Task GetMessageRawAsync_以raw格式取回並交給MimeKit解析()
        {
            const string Rfc822 = "From: sender@example.com\r\nTo: receiver@example.com\r\nSubject: 測試主旨\r\n\r\n內文";
            var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(Rfc822)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\",\"raw\":\"" + raw + "\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var message = await client.GetMessageRawAsync("m1");

            Assert.Equal(BaseUrl + "messages/m1?format=raw", handler.LastRequest.Url);
            Assert.Equal("測試主旨", message.Subject);
        }

        [Fact]
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

            Assert.Equal(
                BaseUrl + "history?startHistoryId=12345&historyTypes=messageAdded&historyTypes=labelRemoved&pageToken=PAGE-9&labelId=INBOX&maxResults=100",
                handler.LastRequest.Url);
        }

        [Fact]
        public async Task ListHistoryAsync_解析變更紀錄()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(
                "{\"history\":[{\"id\":\"777\",\"messagesAdded\":[{\"message\":{\"id\":\"m9\",\"threadId\":\"t9\",\"labelIds\":[\"INBOX\"]}}]," +
                "\"labelsRemoved\":[{\"message\":{\"id\":\"m8\"},\"labelIds\":[\"UNREAD\"]}]}],\"historyId\":\"888\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var history = await client.ListHistoryAsync("100");

            var record = Assert.Single(history.History);
            Assert.Equal("777", record.Id);
            Assert.Equal("m9", Assert.Single(record.MessagesAdded).Message.Id);
            Assert.Equal("UNREAD", Assert.Single(Assert.Single(record.LabelsRemoved).LabelIds));
            Assert.Empty(record.MessagesDeleted);
            Assert.Equal("888", history.HistoryId);
        }

        [Fact]
        public async Task ModifyLabelsAsync_送出POST與兩個標籤清單()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\",\"labelIds\":[\"STARRED\"]}");
            var client = GmailTestFactory.CreateClient(handler);

            var message = await client.ModifyLabelsAsync("m1", new[] { "STARRED" }, new[] { "UNREAD" });

            Assert.Equal("POST", handler.LastRequest.Method);
            Assert.Equal(BaseUrl + "messages/m1/modify", handler.LastRequest.Url);
            Assert.Equal("{\"addLabelIds\":[\"STARRED\"],\"removeLabelIds\":[\"UNREAD\"]}", handler.LastRequest.Body);
            Assert.Contains("application/json", handler.LastRequest.ContentType, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("STARRED", Assert.Single(message.LabelIds));
        }

        [Fact]
        public async Task ModifyLabelsAsync_只給移除清單_body不含addLabelIds()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.ModifyLabelsAsync("m1", null, new[] { "UNREAD" });

            Assert.Equal("{\"removeLabelIds\":[\"UNREAD\"]}", handler.LastRequest.Body);
        }

        [Fact]
        public async Task BatchModifyLabelsAsync_送出批次主體且不解析回應()
        {
            var handler = new RecordingHandler();
            handler.EnqueueEmpty();
            var client = GmailTestFactory.CreateClient(handler);

            await client.BatchModifyLabelsAsync(new[] { "m1", "m2" }, new[] { "SPAM" }, null);

            Assert.Equal("POST", handler.LastRequest.Method);
            Assert.Equal(BaseUrl + "messages/batchModify", handler.LastRequest.Url);
            Assert.Equal("{\"ids\":[\"m1\",\"m2\"],\"addLabelIds\":[\"SPAM\"]}", handler.LastRequest.Body);
        }

        [Fact]
        public async Task BatchModifyLabelsAsync_識別碼為空_不送任何請求()
        {
            var handler = new RecordingHandler();
            var client = GmailTestFactory.CreateClient(handler);

            await client.BatchModifyLabelsAsync(Array.Empty<string>(), new[] { "SPAM" }, null);

            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task TrashAsync_送出trash端點()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\",\"labelIds\":[\"TRASH\"]}");
            var client = GmailTestFactory.CreateClient(handler);

            var message = await client.TrashAsync("m1");

            Assert.Equal("POST", handler.LastRequest.Method);
            Assert.Equal(BaseUrl + "messages/m1/trash", handler.LastRequest.Url);
            Assert.Equal("TRASH", Assert.Single(message.LabelIds));
        }

        [Fact]
        public async Task UntrashAsync_送出untrash端點()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.UntrashAsync("m1");

            Assert.Equal("POST", handler.LastRequest.Method);
            Assert.Equal(BaseUrl + "messages/m1/untrash", handler.LastRequest.Url);
        }

        [Fact]
        public async Task ReportSpamAsync_加上SPAM並移除INBOX()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.ReportSpamAsync("m1");

            Assert.Equal(BaseUrl + "messages/m1/modify", handler.LastRequest.Url);
            Assert.Equal("{\"addLabelIds\":[\"SPAM\"],\"removeLabelIds\":[\"INBOX\"]}", handler.LastRequest.Body);
        }

        [Fact]
        public async Task ListLabelsAsync_解析labels陣列()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(
                "{\"labels\":[{\"id\":\"INBOX\",\"name\":\"INBOX\",\"type\":\"system\"}," +
                "{\"id\":\"Label_1\",\"name\":\"Sower/Ads\",\"type\":\"user\",\"messagesTotal\":\"5\",\"color\":{\"backgroundColor\":\"#ffffff\",\"textColor\":\"#000000\"}}]}");
            var client = GmailTestFactory.CreateClient(handler);

            var labels = await client.ListLabelsAsync();

            Assert.Equal("GET", handler.LastRequest.Method);
            Assert.Equal(BaseUrl + "labels", handler.LastRequest.Url);
            Assert.Equal(2, labels.Count);
            Assert.Equal("Sower/Ads", labels[1].Name);
            Assert.Equal(5, labels[1].MessagesTotal);
            Assert.Equal("#ffffff", labels[1].Color.BackgroundColor);
        }

        [Fact]
        public async Task ListLabelsAsync_回應沒有labels_回空清單()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var client = GmailTestFactory.CreateClient(handler);

            var labels = await client.ListLabelsAsync();

            Assert.NotNull(labels);
            Assert.Empty(labels);
        }

        [Fact]
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

            Assert.Equal("POST", handler.LastRequest.Method);
            Assert.Equal(BaseUrl + "labels", handler.LastRequest.Url);
            Assert.Equal(
                "{\"name\":\"Sower\",\"labelListVisibility\":\"labelShow\",\"messageListVisibility\":\"show\",\"color\":{\"backgroundColor\":\"#ffffff\",\"textColor\":\"#000000\"}}",
                handler.LastRequest.Body);
            Assert.Equal("Label_2", label.Id);
        }

        [Fact]
        public async Task CreateLabelAsync_未給選項_主體只含名稱()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"Label_3\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.CreateLabelAsync("Sower/Ads");

            Assert.Equal("{\"name\":\"Sower/Ads\"}", handler.LastRequest.Body);
        }

        [Fact]
        public async Task UpdateLabelAsync_使用PATCH並帶新名稱()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"Label_2\",\"name\":\"Sower/Renamed\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.UpdateLabelAsync("Label_2", "Sower/Renamed");

            Assert.Equal("PATCH", handler.LastRequest.Method);
            Assert.Equal(BaseUrl + "labels/Label_2", handler.LastRequest.Url);
            Assert.Equal("{\"name\":\"Sower/Renamed\"}", handler.LastRequest.Body);
        }

        [Fact]
        public async Task UpdateLabelAsync_名稱為null_主體不含name()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"Label_2\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.UpdateLabelAsync("Label_2", null, new GmailLabelOptions { LabelListVisibility = "labelHide" });

            Assert.Equal("{\"labelListVisibility\":\"labelHide\"}", handler.LastRequest.Body);
        }

        [Fact]
        public async Task DeleteLabelAsync_使用DELETE且接受204()
        {
            var handler = new RecordingHandler();
            handler.EnqueueEmpty();
            var client = GmailTestFactory.CreateClient(handler);

            await client.DeleteLabelAsync("Label_2");

            Assert.Equal("DELETE", handler.LastRequest.Method);
            Assert.Equal(BaseUrl + "labels/Label_2", handler.LastRequest.Url);
        }

        [Fact]
        public async Task GetAttachmentAsync_解碼base64url內容()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"attachmentId\":\"a1\",\"size\":\"5\",\"data\":\"aGVsbG8\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var attachment = await client.GetAttachmentAsync("m1", "a1");

            Assert.Equal(BaseUrl + "messages/m1/attachments/a1", handler.LastRequest.Url);
            Assert.Equal("a1", attachment.AttachmentId);
            Assert.Equal(5, attachment.Size);
            Assert.Equal("hello", Encoding.UTF8.GetString(attachment.Data));
        }

        [Fact]
        public async Task GetAttachmentAsync_沒有data_回空陣列()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"attachmentId\":\"a1\",\"size\":\"0\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var attachment = await client.GetAttachmentAsync("m1", "a1");

            Assert.NotNull(attachment.Data);
            Assert.Empty(attachment.Data);
        }

        [Fact]
        public async Task DownloadAttachmentAsync_寫入串流並回傳位元組數且不關閉串流()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"attachmentId\":\"a1\",\"size\":\"5\",\"data\":\"aGVsbG8\"}");
            var client = GmailTestFactory.CreateClient(handler);

            using var destination = new MemoryStream();
            var written = await client.DownloadAttachmentAsync("m1", "a1", destination);

            Assert.Equal(5, written);
            Assert.Equal("hello", Encoding.UTF8.GetString(destination.ToArray()));
            Assert.True(destination.CanWrite);
        }

        [Fact]
        public async Task 自訂UserId會出現在網址且被轉義()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var client = GmailTestFactory.CreateClient(handler, GmailTestFactory.NoDelayOptions(userId: "user@example.com"));

            await client.GetProfileAsync();

            Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/user%40example.com/profile", handler.LastRequest.Url);
        }

        [Fact]
        public async Task 建構後再改options不影響已建立的用戶端()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{}");
            var options = GmailTestFactory.NoDelayOptions();
            var client = GmailTestFactory.CreateClient(handler, options);

            options.UserId = "someone@example.com";
            await client.GetProfileAsync();

            Assert.Equal(BaseUrl + "profile", handler.LastRequest.Url);
        }
    }
}
