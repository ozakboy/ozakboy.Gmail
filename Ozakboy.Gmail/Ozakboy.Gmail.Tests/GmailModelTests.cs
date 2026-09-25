using System;
using System.Text;
using Ozakboy.Gmail.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// 模型輔助成員與 JSON 反序列化行為的測試。
    /// </summary>
    [TestClass]
    public class GmailModelTests
    {
        [TestMethod]
        public void InternalDateTime_為零時回傳null()
        {
            var message = new GmailMessage { InternalDate = 0 };

            Assert.IsNull(message.InternalDateTime);
        }

        [TestMethod]
        public void InternalDateTime_換算成對應的時間點()
        {
            var message = new GmailMessage { InternalDate = 1725000000000L };

            Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(1725000000000L), message.InternalDateTime);
        }

        [TestMethod]
        public void GetHeader_不分大小寫且取第一個符合的標頭()
        {
            var message = new GmailMessage
            {
                Payload = new GmailMessagePart(),
            };
            message.Payload.Headers.Add(new GmailHeader { Name = "Subject", Value = "第一個" });
            message.Payload.Headers.Add(new GmailHeader { Name = "subject", Value = "第二個" });

            Assert.AreEqual("第一個", message.GetHeader("SUBJECT"));
        }

        [TestMethod]
        public void GetHeader_找不到時回傳null()
        {
            var message = new GmailMessage { Payload = new GmailMessagePart() };

            Assert.IsNull(message.GetHeader("List-Unsubscribe"));
        }

        [TestMethod]
        public void GetHeader_Payload為null時回傳null()
        {
            var message = new GmailMessage();

            Assert.IsNull(message.GetHeader("Subject"));
        }

        [TestMethod]
        public void GetHeader_名稱為空時回傳null()
        {
            var part = new GmailMessagePart();
            part.Headers.Add(new GmailHeader { Name = "From", Value = "sender@example.com" });

            Assert.IsNull(part.GetHeader(null));
            Assert.IsNull(part.GetHeader(string.Empty));
        }

        [TestMethod]
        public void DecodeData_Data為null時回傳null()
        {
            var body = new GmailMessagePartBody();

            Assert.IsNull(body.DecodeData());
        }

        [TestMethod]
        public void DecodeData_解出base64url內容()
        {
            var body = new GmailMessagePartBody { Data = "aGVsbG8" };

            Assert.AreEqual("hello", Encoding.UTF8.GetString(body.DecodeData()));
        }

        [TestMethod]
        public void 清單屬性預設為空清單而非null()
        {
            Assert.IsNotNull(new GmailMessage().LabelIds);
            Assert.IsNotNull(new GmailMessageList().Messages);
            Assert.IsNotNull(new GmailMessagePart().Headers);
            Assert.IsNotNull(new GmailMessagePart().Parts);
            Assert.IsNotNull(new GmailHistoryList().History);
            Assert.IsNotNull(new GmailHistoryRecord().MessagesAdded);
            Assert.IsNotNull(new GmailHistoryRecord().MessagesDeleted);
            Assert.IsNotNull(new GmailHistoryRecord().LabelsAdded);
            Assert.IsNotNull(new GmailHistoryRecord().LabelsRemoved);
            Assert.IsNotNull(new GmailHistoryRecord().Messages);
            Assert.IsNotNull(new GmailHistoryLabelChange().LabelIds);
            Assert.IsNotNull(new GmailAttachment().Data);
        }

        [TestMethod]
        public void 反序列化_字串型態的數字欄位可以讀成數值()
        {
            var message = GmailJson.Deserialize<GmailMessage>(
                "{\"id\":\"m1\",\"internalDate\":\"1725000000000\",\"sizeEstimate\":\"4096\",\"historyId\":\"12345\"}");

            Assert.AreEqual(1725000000000L, message.InternalDate);
            Assert.AreEqual(4096, message.SizeEstimate);
            Assert.AreEqual("12345", message.HistoryId);
        }

        [TestMethod]
        public void 反序列化_欄位缺漏時維持預設值與空清單()
        {
            var message = GmailJson.Deserialize<GmailMessage>("{\"id\":\"m1\"}");

            Assert.IsNull(message.Snippet);
            Assert.IsNull(message.Payload);
            Assert.AreEqual(0, message.InternalDate);
            Assert.IsNotNull(message.LabelIds);
            Assert.IsEmpty(message.LabelIds);
        }

        [TestMethod]
        public void 反序列化_巢狀MIME結構()
        {
            var message = GmailJson.Deserialize<GmailMessage>(
                "{\"payload\":{\"mimeType\":\"multipart/alternative\",\"parts\":[" +
                "{\"partId\":\"0\",\"mimeType\":\"text/plain\",\"body\":{\"size\":\"5\",\"data\":\"aGVsbG8\"}}," +
                "{\"partId\":\"1\",\"mimeType\":\"text/html\",\"filename\":\"\",\"body\":{\"attachmentId\":\"a1\",\"size\":\"99\"}}]}}");

            Assert.AreEqual(2, message.Payload.Parts.Count);
            Assert.AreEqual("text/plain", message.Payload.Parts[0].MimeType);
            Assert.AreEqual("hello", Encoding.UTF8.GetString(message.Payload.Parts[0].Body.DecodeData()));
            Assert.AreEqual("a1", message.Payload.Parts[1].Body.AttachmentId);
            Assert.AreEqual(99, message.Payload.Parts[1].Body.Size);
        }

        [TestMethod]
        public void 序列化_null欄位不會寫進JSON()
        {
            var json = GmailJson.Serialize(new GmailLabelColor { BackgroundColor = "#ffffff" });

            Assert.AreEqual("{\"backgroundColor\":\"#ffffff\"}", json);
        }
    }
}
