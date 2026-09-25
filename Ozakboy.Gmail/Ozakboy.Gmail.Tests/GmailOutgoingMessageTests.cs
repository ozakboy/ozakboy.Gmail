using System;
using System.IO;
using System.Linq;
using System.Text;
using MimeKit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// 內建組信器的反向驗證:用 MimeKit(僅測試相依)解析 ToRfc822Bytes() 的輸出,確認每個欄位都能 round-trip。
    /// 主函式庫本身不依賴 MimeKit,這裡只是拿業界標準解析器當裁判。
    /// </summary>
    [TestClass]
    public class GmailOutgoingMessageTests
    {
        private static MimeMessage Parse(GmailOutgoingMessage message)
        {
            var bytes = message.ToRfc822Bytes();
            using var stream = new MemoryStream(bytes, false);
            return MimeMessage.Load(stream);
        }

        private static GmailOutgoingMessage Basic()
        {
            var message = new GmailOutgoingMessage
            {
                From = new GmailAddress("sender@example.com", "Sender"),
                Subject = "Hello",
                TextBody = "plain body",
            };
            message.To.Add(new GmailAddress("to@example.com", "To Person"));
            return message;
        }

        [TestMethod]
        public void 基本欄位_From_To_Subject_純文字內文_round_trip()
        {
            var parsed = Parse(Basic());

            var from = Assert.IsExactInstanceOfType<MailboxAddress>(Assert.ContainsSingle(parsed.From));
            Assert.AreEqual("sender@example.com", from.Address);
            Assert.AreEqual("Sender", from.Name);

            var to = Assert.IsExactInstanceOfType<MailboxAddress>(Assert.ContainsSingle(parsed.To));
            Assert.AreEqual("to@example.com", to.Address);
            Assert.AreEqual("To Person", to.Name);

            Assert.AreEqual("Hello", parsed.Subject);
            Assert.AreEqual("plain body", parsed.TextBody);
            Assert.IsNull(parsed.HtmlBody);
            Assert.IsEmpty(parsed.Attachments);
        }

        [TestMethod]
        public void 中文顯示名與中文主旨_round_trip()
        {
            var message = new GmailOutgoingMessage
            {
                From = new GmailAddress("sender@example.com", "韓滷工作室 客服"),
                Subject = "Re: 報價確認——附件如附,請查收(含 emoji 🚀)",
                TextBody = "中文內文",
            };
            message.To.Add(new GmailAddress("to@example.com", "客戶 王小明"));
            message.Cc.Add(new GmailAddress("cc@example.com", "副本 李小華"));

            var parsed = Parse(message);

            Assert.AreEqual("韓滷工作室 客服", ((MailboxAddress)parsed.From[0]).Name);
            Assert.AreEqual("客戶 王小明", ((MailboxAddress)parsed.To[0]).Name);
            Assert.AreEqual("副本 李小華", ((MailboxAddress)parsed.Cc[0]).Name);
            Assert.AreEqual("Re: 報價確認——附件如附,請查收(含 emoji 🚀)", parsed.Subject);
            Assert.AreEqual("中文內文", parsed.TextBody);
        }

        [TestMethod]
        public void 超長中文主旨_折成多個encoded_word後仍能還原()
        {
            var subject = string.Concat(Enumerable.Repeat("很長的中文主旨測試", 12));   // 108 個中文字
            var message = Basic();
            message.Subject = subject;

            var parsed = Parse(message);

            Assert.AreEqual(subject, parsed.Subject);
        }

        [TestMethod]
        public void ASCII顯示名含特殊字元_會加引號且能還原()
        {
            var message = Basic();
            message.To.Clear();
            message.To.Add(new GmailAddress("to@example.com", "Smith, John (Sales) \"JS\""));

            var parsed = Parse(message);

            var to = Assert.IsExactInstanceOfType<MailboxAddress>(Assert.ContainsSingle(parsed.To));
            Assert.AreEqual("to@example.com", to.Address);
            Assert.AreEqual("Smith, John (Sales) \"JS\"", to.Name);
        }

        [TestMethod]
        public void Bcc_ReplyTo_多收件者_皆寫入標頭()
        {
            var message = Basic();
            message.To.Add(new GmailAddress("second@example.com"));
            message.Bcc.Add(new GmailAddress("hidden@example.com", "Hidden"));
            message.ReplyTo = new GmailAddress("reply@example.com", "Reply Here");

            var parsed = Parse(message);

            Assert.AreEqual(2, parsed.To.Count);
            Assert.AreEqual("second@example.com", ((MailboxAddress)parsed.To[1]).Address);
            Assert.AreEqual(string.Empty, ((MailboxAddress)parsed.To[1]).Name);
            Assert.AreEqual("hidden@example.com", ((MailboxAddress)Assert.ContainsSingle(parsed.Bcc)).Address);
            var replyTo = Assert.IsExactInstanceOfType<MailboxAddress>(Assert.ContainsSingle(parsed.ReplyTo));
            Assert.AreEqual("reply@example.com", replyTo.Address);
            Assert.AreEqual("Reply Here", replyTo.Name);
        }

        [TestMethod]
        public void 很多收件者_地址標頭折行後仍全部解析得到()
        {
            var message = Basic();
            message.To.Clear();
            for (var i = 0; i < 20; i++)
                message.To.Add(new GmailAddress("recipient" + i + "@example.com", "Recipient Number " + i));

            var parsed = Parse(message);

            Assert.AreEqual(20, parsed.To.Count);
            Assert.AreEqual("recipient19@example.com", ((MailboxAddress)parsed.To[19]).Address);
            Assert.AreEqual("Recipient Number 19", ((MailboxAddress)parsed.To[19]).Name);
        }

        [TestMethod]
        public void 只有HTML內文_是單一text_html部件()
        {
            var message = Basic();
            message.TextBody = null;
            message.HtmlBody = "<p>只有 HTML</p>";

            var parsed = Parse(message);

            Assert.IsNull(parsed.TextBody);
            Assert.AreEqual("<p>只有 HTML</p>", parsed.HtmlBody);
            var part = Assert.IsExactInstanceOfType<TextPart>(parsed.Body);
            Assert.IsTrue(part.IsHtml);
        }

        [TestMethod]
        public void 文字與HTML都有_是multipart_alternative且兩者皆可取得()
        {
            var message = Basic();
            message.HtmlBody = "<p>plain body</p>";

            var parsed = Parse(message);

            var alternative = Assert.IsExactInstanceOfType<MultipartAlternative>(parsed.Body);
            Assert.AreEqual(2, alternative.Count);
            Assert.IsTrue(((TextPart)alternative[0]).IsPlain);
            Assert.IsTrue(((TextPart)alternative[1]).IsHtml);
            Assert.AreEqual("plain body", parsed.TextBody);
            Assert.AreEqual("<p>plain body</p>", parsed.HtmlBody);
        }

        [TestMethod]
        public void 兩種內文都沒有_是空的text_plain部件()
        {
            var message = Basic();
            message.TextBody = null;

            var parsed = Parse(message);

            var part = Assert.IsExactInstanceOfType<TextPart>(parsed.Body);
            Assert.IsTrue(part.IsPlain);
            Assert.AreEqual(string.Empty, parsed.TextBody);
        }

        [TestMethod]
        public void 附件_是multipart_mixed且檔名型別內容皆正確()
        {
            var content = new byte[] { 0x00, 0x01, 0xFF, 0x7F, 0x80, 0x0A, 0x0D, 0x25, 0x50, 0x44, 0x46 };
            var message = Basic();
            message.HtmlBody = "<p>html</p>";
            message.Attachments.Add(new GmailAttachmentContent { FileName = "quote.pdf", ContentType = "application/pdf", Content = content });
            message.Attachments.Add(new GmailAttachmentContent { FileName = "報價單 v2.pdf", ContentType = "application/pdf", Content = content });

            var parsed = Parse(message);

            Assert.IsExactInstanceOfType<Multipart>(parsed.Body);
            Assert.AreEqual("mixed", ((Multipart)parsed.Body).ContentType.MediaSubtype);
            Assert.AreEqual("plain body", parsed.TextBody);
            Assert.AreEqual("<p>html</p>", parsed.HtmlBody);

            var attachments = parsed.Attachments.OfType<MimePart>().ToList();
            Assert.AreEqual(2, attachments.Count);
            Assert.AreEqual("quote.pdf", attachments[0].FileName);
            Assert.AreEqual("報價單 v2.pdf", attachments[1].FileName);
            Assert.AreEqual("application/pdf", attachments[0].ContentType.MimeType);

            foreach (var attachment in attachments)
            {
                using var decoded = new MemoryStream();
                attachment.Content.DecodeTo(decoded);
                CollectionAssert.AreEqual(content, decoded.ToArray());
            }
        }

        [TestMethod]
        public void 附件檔名與型別空白_退回attachment與octet_stream()
        {
            var message = Basic();
            message.Attachments.Add(new GmailAttachmentContent { FileName = " ", ContentType = "", Content = new byte[] { 1, 2, 3 } });

            var parsed = Parse(message);

            var attachment = Assert.IsExactInstanceOfType<MimePart>(Assert.ContainsSingle(parsed.Attachments));
            Assert.AreEqual("attachment", attachment.FileName);
            Assert.AreEqual("application/octet-stream", attachment.ContentType.MimeType);
        }

        [TestMethod]
        public void 大附件_base64後仍完整還原()
        {
            var random = new Random(42);
            var content = new byte[300_000];
            random.NextBytes(content);
            var message = Basic();
            message.Attachments.Add(new GmailAttachmentContent { FileName = "blob.bin", Content = content });

            var parsed = Parse(message);

            var attachment = Assert.IsExactInstanceOfType<MimePart>(Assert.ContainsSingle(parsed.Attachments));
            using var decoded = new MemoryStream();
            attachment.Content.DecodeTo(decoded);
            CollectionAssert.AreEqual(content, decoded.ToArray());
        }

        [TestMethod]
        public void InReplyTo與References_缺角括號會自動補上()
        {
            var message = Basic();
            message.InReplyTo = "abc123@mail.example.com";
            message.References.Add("<first@mail.example.com>");
            message.References.Add("second@mail.example.com");

            var parsed = Parse(message);

            Assert.AreEqual("abc123@mail.example.com", parsed.InReplyTo);
            CollectionAssert.AreEqual(new[] { "first@mail.example.com", "second@mail.example.com" }, parsed.References.ToArray());
        }

        [TestMethod]
        public void 額外標頭_ASCII與非ASCII值皆可還原()
        {
            var message = Basic();
            message.Headers.Add(new GmailHeader { Name = "X-Sower-Digest", Value = "1" });
            message.Headers.Add(new GmailHeader { Name = "X-Note", Value = "中文備註 note" });

            var parsed = Parse(message);

            Assert.AreEqual("1", parsed.Headers["X-Sower-Digest"]);
            Assert.AreEqual("中文備註 note", parsed.Headers["X-Note"]);
        }

        [TestMethod]
        public void 沒有From_MimeKit解析出的From為空()
        {
            var message = Basic();
            message.From = null;

            var parsed = Parse(message);

            Assert.IsEmpty(parsed.From);
            Assert.ContainsSingle(parsed.To);
        }

        [TestMethod]
        public void 沒有Subject_不寫Subject標頭()
        {
            var message = Basic();
            message.Subject = null;

            var bytes = message.ToRfc822Bytes();
            var text = Encoding.UTF8.GetString(bytes);

            Assert.DoesNotContain("Subject:", text, StringComparison.Ordinal);
            Assert.IsNull(Parse(message).Subject);
        }

        [TestMethod]
        public void 沒有任何收件人_拋InvalidOperationException()
        {
            var message = new GmailOutgoingMessage { Subject = "x", TextBody = "y" };

            Assert.ThrowsExactly<InvalidOperationException>(() => message.ToRfc822Bytes());
        }

        [TestMethod]
        public void 只有Bcc_也算有收件人()
        {
            var message = new GmailOutgoingMessage { TextBody = "y" };
            message.Bcc.Add(new GmailAddress("hidden@example.com"));

            var parsed = Parse(message);

            Assert.ContainsSingle(parsed.Bcc);
        }

        [TestMethod]
        [DataRow("Subject")]
        [DataRow("content-type")]
        [DataRow("MIME-Version")]
        [DataRow("Date")]
        [DataRow("Message-ID")]
        public void 額外標頭與內建標頭同名_拋InvalidOperationException(string name)
        {
            var message = Basic();
            message.Headers.Add(new GmailHeader { Name = name, Value = "x" });

            Assert.ThrowsExactly<InvalidOperationException>(() => message.ToRfc822Bytes());
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("X Bad")]
        [DataRow("X:Bad")]
        [DataRow("X-中文")]
        public void 額外標頭名稱不合法_拋InvalidOperationException(string name)
        {
            var message = Basic();
            message.Headers.Add(new GmailHeader { Name = name, Value = "x" });

            Assert.ThrowsExactly<InvalidOperationException>(() => message.ToRfc822Bytes());
        }

        [TestMethod]
        public void 額外標頭值含換行_拋InvalidOperationException()
        {
            var message = Basic();
            message.Headers.Add(new GmailHeader { Name = "X-Bad", Value = "a\r\nBcc: evil@example.com" });

            Assert.ThrowsExactly<InvalidOperationException>(() => message.ToRfc822Bytes());
        }

        [TestMethod]
        public void InReplyTo含換行_拋InvalidOperationException()
        {
            var message = Basic();
            message.InReplyTo = "<a@b>\r\nX: y";

            Assert.ThrowsExactly<InvalidOperationException>(() => message.ToRfc822Bytes());
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("no-at-sign")]
        [DataRow("@example.com")]
        [DataRow("user@")]
        [DataRow("user name@example.com")]
        [DataRow("user@example.com\r\nBcc: x@example.com")]
        public void GmailAddress_地址不合法_拋ArgumentException(string address)
        {
            Assert.ThrowsExactly<ArgumentException>(() => new GmailAddress(address));
        }

        [TestMethod]
        public void GmailAddress_顯示名含換行_拋ArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new GmailAddress("user@example.com", "a\nb"));
        }

        [TestMethod]
        public void GmailAddress_空白顯示名視為null並修剪地址()
        {
            var address = new GmailAddress("  user@example.com ", "   ");

            Assert.AreEqual("user@example.com", address.Address);
            Assert.IsNull(address.Name);
            Assert.AreEqual("user@example.com", address.ToString());
            Assert.AreEqual("Name <user@example.com>", new GmailAddress("user@example.com", "Name").ToString());
        }

        [TestMethod]
        public void GmailAttachmentContent_Content設null_存成空陣列()
        {
            var attachment = new GmailAttachmentContent { Content = null };

            Assert.IsNotNull(attachment.Content);
            Assert.IsEmpty(attachment.Content);
        }

        [TestMethod]
        public void 清單屬性預設非null()
        {
            var message = new GmailOutgoingMessage();

            Assert.IsNotNull(message.To);
            Assert.IsNotNull(message.Cc);
            Assert.IsNotNull(message.Bcc);
            Assert.IsNotNull(message.Attachments);
            Assert.IsNotNull(message.References);
            Assert.IsNotNull(message.Headers);
        }
    }
}
