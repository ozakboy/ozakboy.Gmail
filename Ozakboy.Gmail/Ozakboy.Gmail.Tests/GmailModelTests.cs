using System;
using System.Text;
using Ozakboy.Gmail.Core;
using Xunit;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// 模型輔助成員與 JSON 反序列化行為的測試。
    /// </summary>
    public class GmailModelTests
    {
        [Fact]
        public void InternalDateTime_為零時回傳null()
        {
            var message = new GmailMessage { InternalDate = 0 };

            Assert.Null(message.InternalDateTime);
        }

        [Fact]
        public void InternalDateTime_換算成對應的時間點()
        {
            var message = new GmailMessage { InternalDate = 1725000000000L };

            Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1725000000000L), message.InternalDateTime);
        }

        [Fact]
        public void GetHeader_不分大小寫且取第一個符合的標頭()
        {
            var message = new GmailMessage
            {
                Payload = new GmailMessagePart(),
            };
            message.Payload.Headers.Add(new GmailHeader { Name = "Subject", Value = "第一個" });
            message.Payload.Headers.Add(new GmailHeader { Name = "subject", Value = "第二個" });

            Assert.Equal("第一個", message.GetHeader("SUBJECT"));
        }

        [Fact]
        public void GetHeader_找不到時回傳null()
        {
            var message = new GmailMessage { Payload = new GmailMessagePart() };

            Assert.Null(message.GetHeader("List-Unsubscribe"));
        }

        [Fact]
        public void GetHeader_Payload為null時回傳null()
        {
            var message = new GmailMessage();

            Assert.Null(message.GetHeader("Subject"));
        }

        [Fact]
        public void GetHeader_名稱為空時回傳null()
        {
            var part = new GmailMessagePart();
            part.Headers.Add(new GmailHeader { Name = "From", Value = "sender@example.com" });

            Assert.Null(part.GetHeader(null));
            Assert.Null(part.GetHeader(string.Empty));
        }

        [Fact]
        public void DecodeData_Data為null時回傳null()
        {
            var body = new GmailMessagePartBody();

            Assert.Null(body.DecodeData());
        }

        [Fact]
        public void DecodeData_解出base64url內容()
        {
            var body = new GmailMessagePartBody { Data = "aGVsbG8" };

            Assert.Equal("hello", Encoding.UTF8.GetString(body.DecodeData()));
        }

        [Fact]
        public void 清單屬性預設為空清單而非null()
        {
            Assert.NotNull(new GmailMessage().LabelIds);
            Assert.NotNull(new GmailMessageList().Messages);
            Assert.NotNull(new GmailMessagePart().Headers);
            Assert.NotNull(new GmailMessagePart().Parts);
            Assert.NotNull(new GmailHistoryList().History);
            Assert.NotNull(new GmailHistoryRecord().MessagesAdded);
            Assert.NotNull(new GmailHistoryRecord().MessagesDeleted);
            Assert.NotNull(new GmailHistoryRecord().LabelsAdded);
            Assert.NotNull(new GmailHistoryRecord().LabelsRemoved);
            Assert.NotNull(new GmailHistoryRecord().Messages);
            Assert.NotNull(new GmailHistoryLabelChange().LabelIds);
            Assert.NotNull(new GmailAttachment().Data);
        }

        [Fact]
        public void 反序列化_字串型態的數字欄位可以讀成數值()
        {
            var message = GmailJson.Deserialize<GmailMessage>(
                "{\"id\":\"m1\",\"internalDate\":\"1725000000000\",\"sizeEstimate\":\"4096\",\"historyId\":\"12345\"}");

            Assert.Equal(1725000000000L, message.InternalDate);
            Assert.Equal(4096, message.SizeEstimate);
            Assert.Equal("12345", message.HistoryId);
        }

        [Fact]
        public void 反序列化_欄位缺漏時維持預設值與空清單()
        {
            var message = GmailJson.Deserialize<GmailMessage>("{\"id\":\"m1\"}");

            Assert.Null(message.Snippet);
            Assert.Null(message.Payload);
            Assert.Equal(0, message.InternalDate);
            Assert.NotNull(message.LabelIds);
            Assert.Empty(message.LabelIds);
        }

        [Fact]
        public void 反序列化_巢狀MIME結構()
        {
            var message = GmailJson.Deserialize<GmailMessage>(
                "{\"payload\":{\"mimeType\":\"multipart/alternative\",\"parts\":[" +
                "{\"partId\":\"0\",\"mimeType\":\"text/plain\",\"body\":{\"size\":\"5\",\"data\":\"aGVsbG8\"}}," +
                "{\"partId\":\"1\",\"mimeType\":\"text/html\",\"filename\":\"\",\"body\":{\"attachmentId\":\"a1\",\"size\":\"99\"}}]}}");

            Assert.Equal(2, message.Payload.Parts.Count);
            Assert.Equal("text/plain", message.Payload.Parts[0].MimeType);
            Assert.Equal("hello", Encoding.UTF8.GetString(message.Payload.Parts[0].Body.DecodeData()));
            Assert.Equal("a1", message.Payload.Parts[1].Body.AttachmentId);
            Assert.Equal(99, message.Payload.Parts[1].Body.Size);
        }

        [Fact]
        public void 序列化_null欄位不會寫進JSON()
        {
            var json = GmailJson.Serialize(new GmailLabelColor { BackgroundColor = "#ffffff" });

            Assert.Equal("{\"backgroundColor\":\"#ffffff\"}", json);
        }
    }
}
