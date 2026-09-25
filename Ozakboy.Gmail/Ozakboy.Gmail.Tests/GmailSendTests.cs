using System;
using System.Text;
using System.Threading.Tasks;
using Ozakboy.Gmail.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// 寄信(messages.send 多段上傳)的組裝與回應解析測試:SendAsync 走內建組信器、SendRawAsync 走呼叫端 bytes。
    /// </summary>
    [TestClass]
    public class GmailSendTests
    {
        private const string SendUrl = "https://gmail.googleapis.com/upload/gmail/v1/users/me/messages/send?uploadType=multipart";

        private static GmailOutgoingMessage CreateMessage(string subject = "測試主旨")
        {
            var message = new GmailOutgoingMessage
            {
                From = new GmailAddress("sender@example.com", "寄件者"),
                Subject = subject,
                TextBody = "內文",
            };
            message.To.Add(new GmailAddress("receiver@example.com", "收件者"));
            return message;
        }

        [TestMethod]
        public async Task SendAsync_送到多段上傳端點()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\",\"threadId\":\"t1\",\"labelIds\":[\"SENT\"]}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.SendAsync(CreateMessage());

            Assert.AreEqual("POST", handler.LastRequest.Method);
            Assert.AreEqual(SendUrl, handler.LastRequest.Url);
            Assert.Contains("multipart/related", handler.LastRequest.ContentType, StringComparison.OrdinalIgnoreCase);
        }

        [TestMethod]
        public async Task SendAsync_主體含rfc822段且內容等於ToRfc822Bytes()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);
            var message = CreateMessage();

            await client.SendAsync(message);

            var body = handler.LastRequest.Body;
            Assert.Contains("message/rfc822", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("application/json", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("To: ", body, StringComparison.Ordinal);
            Assert.Contains("sender@example.com", body, StringComparison.Ordinal);

            // 組信器的輸出會原封不動出現在 rfc822 段裡(標頭段落一定含 MIME-Version)
            var expected = Encoding.UTF8.GetString(message.ToRfc822Bytes());
            var headersOnly = expected.Substring(0, expected.IndexOf("MIME-Version: 1.0", StringComparison.Ordinal));
            Assert.Contains(headersOnly, body, StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task SendAsync_未指定討論串_中繼資料為空物件()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.SendAsync(CreateMessage());

            Assert.Contains("{}", handler.LastRequest.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("threadId", handler.LastRequest.Body, StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task SendAsync_指定討論串_中繼資料含threadId()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\",\"threadId\":\"t42\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.SendAsync(CreateMessage(), "t42");

            Assert.Contains("{\"threadId\":\"t42\"}", handler.LastRequest.Body, StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task SendAsync_解析回應的識別碼與標籤()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\",\"threadId\":\"t1\",\"labelIds\":[\"SENT\"]}");
            var client = GmailTestFactory.CreateClient(handler);

            var sent = await client.SendAsync(CreateMessage());

            Assert.AreEqual("m1", sent.Id);
            Assert.AreEqual("t1", sent.ThreadId);
            Assert.AreEqual("SENT", Assert.ContainsSingle(sent.LabelIds));
        }

        [TestMethod]
        public async Task SendAsync_重試時會重建同一份多段內容()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(System.Net.HttpStatusCode.ServiceUnavailable, "{}");
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.SendAsync(CreateMessage());

            Assert.AreEqual(2, handler.RequestCount);
            Assert.Contains("message/rfc822", handler.Requests[0].Body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("message/rfc822", handler.Requests[1].Body, StringComparison.OrdinalIgnoreCase);
        }

        [TestMethod]
        public async Task SendAsync_沒有收件人_送出前就拋InvalidOperationException()
        {
            var handler = new RecordingHandler();
            var client = GmailTestFactory.CreateClient(handler);
            var message = new GmailOutgoingMessage { Subject = "沒人收" };

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.SendAsync(message));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task SendRawAsync_原封不動上傳呼叫端給的位元組()
        {
            const string Rfc822 = "From: sender@example.com\r\nTo: receiver@example.com\r\nSubject: raw\r\n\r\nhello";
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m9\",\"threadId\":\"t9\"}");
            var client = GmailTestFactory.CreateClient(handler);

            var sent = await client.SendRawAsync(Encoding.UTF8.GetBytes(Rfc822), "t9");

            Assert.AreEqual(SendUrl, handler.LastRequest.Url);
            Assert.Contains("message/rfc822", handler.LastRequest.Body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Rfc822, handler.LastRequest.Body, StringComparison.Ordinal);
            Assert.Contains("{\"threadId\":\"t9\"}", handler.LastRequest.Body, StringComparison.Ordinal);
            Assert.AreEqual("m9", sent.Id);
        }
    }
}
