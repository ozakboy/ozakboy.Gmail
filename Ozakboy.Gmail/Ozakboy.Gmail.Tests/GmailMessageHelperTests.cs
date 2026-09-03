using System.Collections.Generic;
using System.Text;
using Ozakboy.Gmail.Core;
using Xunit;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// GmailMessage 內文與附件擷取的測試。全部在記憶體中組出 Gmail 會回傳的 MIME 結構,不連網。
    /// </summary>
    public class GmailMessageHelperTests
    {
        [Fact]
        public void GetTextBody_單部件郵件_解出內文()
        {
            var message = new GmailMessage
            {
                Payload = TextPart("text/plain", "純文字內文"),
            };

            Assert.Equal("純文字內文", message.GetTextBody());
            Assert.Null(message.GetHtmlBody());
        }

        [Fact]
        public void GetHtmlBody_單部件HTML郵件_解出內文()
        {
            var message = new GmailMessage
            {
                Payload = TextPart("text/html", "<p>你好</p>"),
            };

            Assert.Equal("<p>你好</p>", message.GetHtmlBody());
            Assert.Null(message.GetTextBody());
        }

        [Fact]
        public void GetTextBody_大小寫不同的MimeType也認得()
        {
            var message = new GmailMessage
            {
                Payload = TextPart("TEXT/Plain", "大小寫"),
            };

            Assert.Equal("大小寫", message.GetTextBody());
        }

        [Fact]
        public void GetTextBody與GetHtmlBody_alternative結構_各自取到對應區段()
        {
            var message = new GmailMessage
            {
                Payload = Container(
                    "multipart/alternative",
                    TextPart("text/plain", "文字版"),
                    TextPart("text/html", "<b>HTML 版</b>")),
            };

            Assert.Equal("文字版", message.GetTextBody());
            Assert.Equal("<b>HTML 版</b>", message.GetHtmlBody());
        }

        [Fact]
        public void GetTextBody與GetHtmlBody_mixed包alternative_仍能往下找到內文()
        {
            var message = new GmailMessage
            {
                Payload = Container(
                    "multipart/mixed",
                    Container(
                        "multipart/alternative",
                        TextPart("text/plain", "巢狀文字"),
                        TextPart("text/html", "<i>巢狀 HTML</i>")),
                    AttachmentPart("report.pdf", "application/pdf", "att-1", 2048)),
            };

            Assert.Equal("巢狀文字", message.GetTextBody());
            Assert.Equal("<i>巢狀 HTML</i>", message.GetHtmlBody());
        }

        [Fact]
        public void GetTextBody_帶檔名的text_plain是附件_不算內文()
        {
            var attached = TextPart("text/plain", "這是附件內容");
            attached.Filename = "note.txt";

            var message = new GmailMessage
            {
                Payload = Container("multipart/mixed", attached, TextPart("text/plain", "這才是內文")),
            };

            Assert.Equal("這才是內文", message.GetTextBody());
        }

        [Fact]
        public void GetTextBody_只有帶檔名的text_plain_回傳null()
        {
            var attached = TextPart("text/plain", "只有附件");
            attached.Filename = "note.txt";

            var message = new GmailMessage
            {
                Payload = Container("multipart/mixed", attached),
            };

            Assert.Null(message.GetTextBody());
        }

        [Fact]
        public void GetTextBody_區段沒有Data_跳過繼續往下找()
        {
            var empty = new GmailMessagePart { MimeType = "text/plain", Filename = string.Empty };

            var message = new GmailMessage
            {
                Payload = Container("multipart/mixed", empty, TextPart("text/plain", "後面這個才有內容")),
            };

            Assert.Equal("後面這個才有內容", message.GetTextBody());
        }

        [Fact]
        public void GetTextBody_Payload為null_回傳null()
        {
            var message = new GmailMessage();

            Assert.Null(message.GetTextBody());
            Assert.Null(message.GetHtmlBody());
        }

        [Fact]
        public void GetTextBody_中文內容以UTF8正確解碼()
        {
            const string Content = "測試中文與 emoji 🙂,還有換行\r\n第二行";
            var message = new GmailMessage { Payload = TextPart("text/plain", Content) };

            Assert.Equal(Content, message.GetTextBody());
        }

        [Fact]
        public void GetAttachments_Payload為null_回傳空清單而非null()
        {
            var message = new GmailMessage();
            var attachments = message.GetAttachments();

            Assert.NotNull(attachments);
            Assert.Empty(attachments);
        }

        [Fact]
        public void GetAttachments_收集檔名附件與內嵌圖片_並跳過內文與容器()
        {
            var inline = new GmailMessagePart
            {
                PartId = "1.2",
                MimeType = "image/png",
                Filename = "logo.png",
                Headers = new List<GmailHeader>
                {
                    new GmailHeader { Name = "Content-Type", Value = "image/png" },
                    new GmailHeader { Name = "content-id", Value = "<logo@example.com>" },
                },
                Body = new GmailMessagePartBody { Size = 3, Data = Encode("PNG") },
            };

            var message = new GmailMessage
            {
                Payload = Container(
                    "multipart/mixed",
                    Container("multipart/alternative", TextPart("text/plain", "內文")),
                    inline,
                    AttachmentPart("report.pdf", "application/pdf", "att-1", 2048)),
            };

            var attachments = message.GetAttachments();

            Assert.Equal(2, attachments.Count);

            Assert.Equal("logo.png", attachments[0].FileName);
            Assert.Equal("image/png", attachments[0].MimeType);
            Assert.Equal("1.2", attachments[0].PartId);
            Assert.Equal(3, attachments[0].Size);
            Assert.Equal("logo@example.com", attachments[0].ContentId);
            Assert.Null(attachments[0].AttachmentId);
            Assert.Same(inline, attachments[0].Part);

            // 小附件沒有 attachmentId,內容直接在 Part.Body.Data
            Assert.Equal("PNG", Encoding.UTF8.GetString(attachments[0].Part.Body.DecodeData()));

            Assert.Equal("report.pdf", attachments[1].FileName);
            Assert.Equal("att-1", attachments[1].AttachmentId);
            Assert.Equal(2048, attachments[1].Size);
            Assert.Null(attachments[1].ContentId);
        }

        [Fact]
        public void GetAttachments_沒有檔名但有attachmentId_也算附件()
        {
            var nameless = new GmailMessagePart
            {
                PartId = "2",
                MimeType = "application/octet-stream",
                Filename = string.Empty,
                Body = new GmailMessagePartBody { AttachmentId = "att-9", Size = 10 },
            };

            var message = new GmailMessage { Payload = Container("multipart/mixed", nameless) };
            var attachments = message.GetAttachments();

            Assert.Single(attachments);
            Assert.Equal("att-9", attachments[0].AttachmentId);
        }

        [Fact]
        public void GetAttachments_單部件純文字郵件_沒有附件()
        {
            var message = new GmailMessage { Payload = TextPart("text/plain", "只有內文") };

            Assert.Empty(message.GetAttachments());
        }

        /// <summary>組出一個帶 base64url 內容的文字區段(非附件,Filename 為空字串)。</summary>
        private static GmailMessagePart TextPart(string mimeType, string content)
        {
            return new GmailMessagePart
            {
                PartId = "0",
                MimeType = mimeType,
                Filename = string.Empty,
                Body = new GmailMessagePartBody
                {
                    Size = Encoding.UTF8.GetByteCount(content),
                    Data = Encode(content),
                },
            };
        }

        /// <summary>組出一個 multipart 容器區段。</summary>
        private static GmailMessagePart Container(string mimeType, params GmailMessagePart[] children)
        {
            return new GmailMessagePart
            {
                PartId = string.Empty,
                MimeType = mimeType,
                Filename = string.Empty,
                Parts = new List<GmailMessagePart>(children),
            };
        }

        /// <summary>組出一個需另外下載的附件區段。</summary>
        private static GmailMessagePart AttachmentPart(string fileName, string mimeType, string attachmentId, long size)
        {
            return new GmailMessagePart
            {
                PartId = "3",
                MimeType = mimeType,
                Filename = fileName,
                Body = new GmailMessagePartBody { AttachmentId = attachmentId, Size = size },
            };
        }

        /// <summary>把字串以 UTF-8 轉成 base64url,模擬 Gmail 回傳的內容。</summary>
        private static string Encode(string content)
        {
            return Base64Url.Encode(Encoding.UTF8.GetBytes(content));
        }
    }
}
