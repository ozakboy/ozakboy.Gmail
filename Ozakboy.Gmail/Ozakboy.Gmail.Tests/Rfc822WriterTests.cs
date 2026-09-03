using System;
using System.Linq;
using System.Text;
using Xunit;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// 組信器輸出的純文字層級檢查:換行、行長、encoded-word 長度、標頭順序、base64 行寬。
    /// 這些是 RFC 5322 / 2045 / 2047 的硬規則,不靠解析器而是直接看原文。
    /// </summary>
    public class Rfc822WriterTests
    {
        private static string Render(GmailOutgoingMessage message)
        {
            return Encoding.UTF8.GetString(message.ToRfc822Bytes());
        }

        private static GmailOutgoingMessage Basic()
        {
            var message = new GmailOutgoingMessage
            {
                From = new GmailAddress("sender@example.com", "Sender"),
                Subject = "Hello",
                TextBody = "plain body",
            };
            message.To.Add(new GmailAddress("to@example.com"));
            return message;
        }

        private static string[] Lines(string text)
        {
            return text.Split(new[] { "\r\n" }, StringSplitOptions.None);
        }

        private static string HeaderBlock(string text)
        {
            return text.Substring(0, text.IndexOf("\r\n\r\n", StringComparison.Ordinal));
        }

        [Fact]
        public void 全部以CRLF換行_沒有裸LF或裸CR()
        {
            var message = Basic();
            message.HtmlBody = "<p>line1\nline2</p>";
            message.Attachments.Add(new GmailAttachmentContent { FileName = "a.bin", Content = Enumerable.Range(0, 5000).Select(i => (byte)i).ToArray() });

            var text = Render(message);
            var withoutCrLf = text.Replace("\r\n", string.Empty);

            Assert.DoesNotContain("\n", withoutCrLf, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", withoutCrLf, StringComparison.Ordinal);
        }

        [Fact]
        public void base64行長不超過76()
        {
            var message = Basic();
            message.TextBody = string.Concat(Enumerable.Repeat("很長的中文內文。", 200));
            message.Attachments.Add(new GmailAttachmentContent { FileName = "a.bin", Content = Enumerable.Range(0, 9000).Select(i => (byte)(i * 7)).ToArray() });

            foreach (var line in Lines(Render(message)))
                Assert.True(line.Length <= 998, "行長超過 998:" + line.Length);

            var bodyLines = Lines(Render(message)).Where(l => l.Length > 0 && !l.StartsWith("--", StringComparison.Ordinal) && l.IndexOf(':') < 0);
            foreach (var line in bodyLines)
                Assert.True(line.Length <= 76, "base64 行長超過 76:" + line.Length);
        }

        [Fact]
        public void 長中文主旨_每行不超過78且每個encoded_word不超過75()
        {
            var message = Basic();
            message.Subject = string.Concat(Enumerable.Repeat("測試主旨", 30));   // 120 個中文字

            var header = HeaderBlock(Render(message));
            var subjectLines = Lines(header).SkipWhile(l => !l.StartsWith("Subject:", StringComparison.Ordinal))
                .TakeWhile((l, i) => i == 0 || l.StartsWith(" ", StringComparison.Ordinal)).ToList();

            Assert.True(subjectLines.Count > 3, "長主旨應被折成多行");
            foreach (var line in subjectLines)
            {
                Assert.True(line.Length <= 78, "主旨行超過 78:" + line);
                var word = line.Trim().Replace("Subject: ", string.Empty);
                Assert.StartsWith("=?utf-8?B?", word, StringComparison.Ordinal);
                Assert.EndsWith("?=", word, StringComparison.Ordinal);
                Assert.True(word.Length <= 75, "encoded-word 超過 75:" + word.Length);
            }
        }

        [Fact]
        public void emoji主旨_encoded_word切割不會拆開代理對()
        {
            var message = Basic();
            message.Subject = string.Concat(Enumerable.Repeat("🚀", 40));

            var header = HeaderBlock(Render(message));
            var words = Lines(header).Where(l => l.Contains("=?utf-8?B?")).Select(l => l.Trim().Replace("Subject: ", string.Empty));

            var decoded = new StringBuilder();
            foreach (var word in words)
            {
                var payload = word.Substring("=?utf-8?B?".Length, word.Length - "=?utf-8?B?".Length - 2);
                var bytes = Convert.FromBase64String(payload);
                // 每個 word 單獨解碼都必須是合法 UTF-8(沒有被切在字元中間)
                var piece = new UTF8Encoding(false, true).GetString(bytes);
                decoded.Append(piece);
            }

            Assert.Equal(message.Subject, decoded.ToString());
        }

        [Fact]
        public void 標頭順序_From_To_Cc_Bcc_ReplyTo_Subject_InReplyTo_References_額外_MIMEVersion_ContentType()
        {
            var message = Basic();
            message.Cc.Add(new GmailAddress("cc@example.com"));
            message.Bcc.Add(new GmailAddress("bcc@example.com"));
            message.ReplyTo = new GmailAddress("reply@example.com");
            message.InReplyTo = "<a@example.com>";
            message.References.Add("<a@example.com>");
            message.Headers.Add(new GmailHeader { Name = "X-Sower-Digest", Value = "1" });

            var names = Lines(HeaderBlock(Render(message)))
                .Where(l => !l.StartsWith(" ", StringComparison.Ordinal))
                .Select(l => l.Substring(0, l.IndexOf(':')))
                .ToArray();

            Assert.Equal(
                new[] { "From", "To", "Cc", "Bcc", "Reply-To", "Subject", "In-Reply-To", "References", "X-Sower-Digest", "MIME-Version", "Content-Type", "Content-Transfer-Encoding" },
                names);
        }

        [Fact]
        public void 不寫Date與MessageID()
        {
            var header = HeaderBlock(Render(Basic()));

            Assert.DoesNotContain("Date:", header, StringComparison.Ordinal);
            Assert.DoesNotContain("Message-ID:", header, StringComparison.Ordinal);
        }

        [Fact]
        public void 純ASCII主旨與顯示名_原樣寫出不編碼()
        {
            var text = Render(Basic());

            Assert.Contains("Subject: Hello\r\n", text, StringComparison.Ordinal);
            Assert.Contains("From: Sender <sender@example.com>\r\n", text, StringComparison.Ordinal);
            Assert.Contains("To: to@example.com\r\n", text, StringComparison.Ordinal);
        }

        [Fact]
        public void 內文一律base64與utf8()
        {
            var text = Render(Basic());

            Assert.Contains("Content-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n", text, StringComparison.Ordinal);
            Assert.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes("plain body")), text, StringComparison.Ordinal);
        }

        [Fact]
        public void 有附件時最外層是multipart_mixed且alternative在內層()
        {
            var message = Basic();
            message.HtmlBody = "<p>x</p>";
            message.Attachments.Add(new GmailAttachmentContent { FileName = "a.txt", ContentType = "text/plain", Content = new byte[] { 65 } });

            var text = Render(message);
            var mixedIndex = text.IndexOf("Content-Type: multipart/mixed; boundary=\"", StringComparison.Ordinal);
            var alternativeIndex = text.IndexOf("Content-Type: multipart/alternative; boundary=\"", StringComparison.Ordinal);
            var attachmentIndex = text.IndexOf("Content-Disposition: attachment; filename=\"a.txt\"", StringComparison.Ordinal);

            Assert.True(mixedIndex >= 0 && alternativeIndex > mixedIndex && attachmentIndex > alternativeIndex);
            Assert.Contains("Content-Type: text/plain; name=\"a.txt\"", text, StringComparison.Ordinal);
            Assert.EndsWith("--\r\n", text, StringComparison.Ordinal);
        }

        [Fact]
        public void 中文檔名_以encoded_word寫進name與filename參數()
        {
            var message = Basic();
            message.Attachments.Add(new GmailAttachmentContent { FileName = "報價單.pdf", ContentType = "application/pdf", Content = new byte[] { 1 } });

            var text = Render(message);

            Assert.Contains("filename=\"=?utf-8?B?", text, StringComparison.Ordinal);
            Assert.Contains("name=\"=?utf-8?B?", text, StringComparison.Ordinal);
        }

        [Fact]
        public void 每次序列化_boundary都不同且mixed與alternative不共用()
        {
            var message = Basic();
            message.HtmlBody = "<p>x</p>";
            message.Attachments.Add(new GmailAttachmentContent { FileName = "a.txt", Content = new byte[] { 65 } });

            var first = Render(message);
            var second = Render(message);

            var boundaries = first.Split(new[] { "boundary=\"" }, StringSplitOptions.None).Skip(1).Select(s => s.Substring(0, s.IndexOf('"'))).ToArray();
            Assert.Equal(2, boundaries.Length);
            Assert.NotEqual(boundaries[0], boundaries[1]);
            Assert.NotEqual(first, second);
        }
    }
}
