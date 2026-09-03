using System;
using System.Threading.Tasks;
using MimeKit;
using Ozakboy.Gmail.Tests.TestSupport;
using Xunit;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// 寄信(messages.send 多段上傳)的組裝與回應解析測試。
    /// </summary>
    public class GmailSendTests
    {
        private const string SendUrl = "https://gmail.googleapis.com/upload/gmail/v1/users/me/messages/send?uploadType=multipart";

        private static MimeMessage CreateMessage(string subject = "測試主旨")
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("寄件者", "sender@example.com"));
            message.To.Add(new MailboxAddress("收件者", "receiver@example.com"));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = "內文" };
            return message;
        }

        [Fact]
        public async Task SendAsync_送到多段上傳端點()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\",\"threadId\":\"t1\",\"labelIds\":[\"SENT\"]}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.SendAsync(CreateMessage());

            Assert.Equal("POST", handler.LastRequest.Method);
            Assert.Equal(SendUrl, handler.LastRequest.Url);
            Assert.Contains("multipart/related", handler.LastRequest.ContentType, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SendAsync_主體含rfc822段與郵件原文()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.SendAsync(CreateMessage());

            var body = handler.LastRequest.Body;
            Assert.Contains("message/rfc822", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("application/json", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("To: ", body, StringComparison.Ordinal);
            Assert.Contains("sender@example.com", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SendAsync_未指定討論串_中繼資料為空物件()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.SendAsync(CreateMessage());

            Assert.Contains("{}", handler.LastRequest.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("threadId", handler.LastRequest.Body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SendAsync_指定討論串_中繼資料含threadId()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\",\"threadId\":\"t42\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.SendAsync(CreateMessage(), "t42");

            Assert.Contains("{\"threadId\":\"t42\"}", handler.LastRequest.Body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SendAsync_解析回應的識別碼與標籤()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"id\":\"m1\",\"threadId\":\"t1\",\"labelIds\":[\"SENT\"]}");
            var client = GmailTestFactory.CreateClient(handler);

            var sent = await client.SendAsync(CreateMessage());

            Assert.Equal("m1", sent.Id);
            Assert.Equal("t1", sent.ThreadId);
            Assert.Equal("SENT", Assert.Single(sent.LabelIds));
        }

        [Fact]
        public async Task SendAsync_重試時會重建同一份多段內容()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(System.Net.HttpStatusCode.ServiceUnavailable, "{}");
            handler.EnqueueJson("{\"id\":\"m1\"}");
            var client = GmailTestFactory.CreateClient(handler);

            await client.SendAsync(CreateMessage());

            Assert.Equal(2, handler.RequestCount);
            Assert.Contains("message/rfc822", handler.Requests[0].Body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("message/rfc822", handler.Requests[1].Body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
