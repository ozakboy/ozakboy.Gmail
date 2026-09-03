using System;
using System.Collections.Generic;
using System.Text;

namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// 內建的 RFC 822 / MIME 組信器:把 <see cref="GmailOutgoingMessage"/> 寫成 Gmail 可上傳的郵件原文,不依賴任何 MIME 函式庫。
    /// The built-in RFC 822 / MIME writer: turns a <see cref="GmailOutgoingMessage"/> into the raw message Gmail accepts, with no MIME library involved.
    /// </summary>
    /// <remarks>
    /// 設計取捨:內文與附件一律 base64(避開 quoted-printable 的行長規則),非 ASCII 標頭一律 RFC 2047 B 編碼,換行一律 CRLF。
    /// Design choices: bodies and attachments are always base64 (sidestepping quoted-printable's line rules), non-ASCII headers are always RFC 2047 B-encoded, and every line break is CRLF.
    /// </remarks>
    internal static class Rfc822Writer
    {
        /// <summary>
        /// 郵件的換行字串。
        /// The line terminator of a mail message.
        /// </summary>
        private const string CrLf = "\r\n";

        /// <summary>
        /// 單一 encoded-word 的長度上限(RFC 2047 規定 75)。
        /// The maximum length of one encoded-word (75 per RFC 2047).
        /// </summary>
        private const int MaxEncodedWordLength = 75;

        /// <summary>
        /// 一個 encoded-word 最多能裝的原始位元組數:75 - 12(前後綴)= 63 個 base64 字元,對應 45 個位元組。
        /// The most raw bytes one encoded-word can hold: 75 - 12 (affixes) = 63 base64 characters, which is 45 bytes.
        /// </summary>
        private const int MaxBytesPerEncodedWord = 45;

        /// <summary>
        /// 標頭行的建議長度上限(RFC 5322 的 78)。
        /// The preferred maximum header line length (78 per RFC 5322).
        /// </summary>
        private const int PreferredLineLength = 78;

        /// <summary>
        /// RFC 2047 B 編碼的前綴。
        /// The RFC 2047 B-encoding prefix.
        /// </summary>
        private const string EncodedWordPrefix = "=?utf-8?B?";

        /// <summary>
        /// RFC 2047 encoded-word 的結尾。
        /// The RFC 2047 encoded-word suffix.
        /// </summary>
        private const string EncodedWordSuffix = "?=";

        /// <summary>
        /// 組信器自己會產生、因此不允許呼叫端以額外標頭覆寫的標頭名稱。
        /// Header names the writer produces itself and therefore refuses as extra headers.
        /// </summary>
        private static readonly string[] ReservedHeaders =
        {
            "From", "To", "Cc", "Bcc", "Reply-To", "Subject", "In-Reply-To", "References",
            "MIME-Version", "Content-Type", "Content-Transfer-Encoding", "Date", "Message-ID",
        };

        /// <summary>
        /// 把郵件序列化成 UTF-8 位元組。
        /// Serialises the message into UTF-8 bytes.
        /// </summary>
        /// <param name="message">要序列化的郵件。The message to serialise.</param>
        /// <returns>RFC 822 郵件內容。The RFC 822 message.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> 為 null 時擲出。Thrown when <paramref name="message"/> is null.</exception>
        /// <exception cref="InvalidOperationException">沒有收件人、額外標頭不合法或與內建標頭衝突、討論串識別碼或標頭值含換行時擲出。Thrown when there are no recipients, an extra header is invalid or clashes with a built-in one, or a thread id / header value contains a line break.</exception>
        internal static byte[] Write(GmailOutgoingMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (message.To.Count == 0 && message.Cc.Count == 0 && message.Bcc.Count == 0)
                throw new InvalidOperationException("郵件至少要有一位收件人(To、Cc 或 Bcc)。A message needs at least one recipient in To, Cc or Bcc.");

            var builder = new StringBuilder();

            if (message.From != null)
                AppendAddressHeader(builder, "From", new[] { message.From });

            if (message.To.Count > 0)
                AppendAddressHeader(builder, "To", message.To);

            if (message.Cc.Count > 0)
                AppendAddressHeader(builder, "Cc", message.Cc);

            if (message.Bcc.Count > 0)
                AppendAddressHeader(builder, "Bcc", message.Bcc);

            if (message.ReplyTo != null)
                AppendAddressHeader(builder, "Reply-To", new[] { message.ReplyTo });

            if (!string.IsNullOrEmpty(message.Subject))
                AppendTextHeader(builder, "Subject", message.Subject!);

            if (!string.IsNullOrWhiteSpace(message.InReplyTo))
                AppendTextHeader(builder, "In-Reply-To", NormalizeMessageId(message.InReplyTo!));

            if (message.References.Count > 0)
            {
                var ids = new List<string>(message.References.Count);
                foreach (var reference in message.References)
                {
                    if (!string.IsNullOrWhiteSpace(reference))
                        ids.Add(NormalizeMessageId(reference));
                }

                if (ids.Count > 0)
                    AppendTextHeader(builder, "References", string.Join(" ", ids));
            }

            foreach (var header in message.Headers)
                AppendExtraHeader(builder, header);

            builder.Append("MIME-Version: 1.0").Append(CrLf);
            AppendBody(builder, message);

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        /// <summary>
        /// 寫出最外層的內容:有附件時包成 multipart/mixed,否則直接是內文部件。
        /// Writes the outermost content: multipart/mixed when there are attachments, otherwise the body part itself.
        /// </summary>
        /// <param name="builder">輸出緩衝。The output buffer.</param>
        /// <param name="message">郵件。The message.</param>
        private static void AppendBody(StringBuilder builder, GmailOutgoingMessage message)
        {
            if (message.Attachments.Count == 0)
            {
                AppendContentPart(builder, message);
                return;
            }

            var boundary = NewBoundary();
            builder.Append("Content-Type: multipart/mixed; boundary=\"").Append(boundary).Append('"').Append(CrLf);
            builder.Append(CrLf);

            builder.Append("--").Append(boundary).Append(CrLf);
            AppendContentPart(builder, message);

            foreach (var attachment in message.Attachments)
            {
                if (attachment == null)
                    continue;

                builder.Append("--").Append(boundary).Append(CrLf);
                AppendAttachment(builder, attachment);
            }

            builder.Append("--").Append(boundary).Append("--").Append(CrLf);
        }

        /// <summary>
        /// 寫出內文部件:純文字、HTML,或兩者組成的 multipart/alternative。
        /// Writes the body part: plain text, HTML, or both wrapped in multipart/alternative.
        /// </summary>
        /// <param name="builder">輸出緩衝。The output buffer.</param>
        /// <param name="message">郵件。The message.</param>
        private static void AppendContentPart(StringBuilder builder, GmailOutgoingMessage message)
        {
            var hasText = message.TextBody != null;
            var hasHtml = message.HtmlBody != null;

            if (hasText && hasHtml)
            {
                var boundary = NewBoundary();
                builder.Append("Content-Type: multipart/alternative; boundary=\"").Append(boundary).Append('"').Append(CrLf);
                builder.Append(CrLf);

                builder.Append("--").Append(boundary).Append(CrLf);
                AppendTextPart(builder, "text/plain", message.TextBody!);

                builder.Append("--").Append(boundary).Append(CrLf);
                AppendTextPart(builder, "text/html", message.HtmlBody!);

                builder.Append("--").Append(boundary).Append("--").Append(CrLf);
                return;
            }

            if (hasHtml)
            {
                AppendTextPart(builder, "text/html", message.HtmlBody!);
                return;
            }

            AppendTextPart(builder, "text/plain", message.TextBody ?? string.Empty);
        }

        /// <summary>
        /// 寫出一個 base64 編碼的文字部件。
        /// Writes one base64-encoded text part.
        /// </summary>
        /// <param name="builder">輸出緩衝。The output buffer.</param>
        /// <param name="mediaType">text/plain 或 text/html。text/plain or text/html.</param>
        /// <param name="text">內文。The body text.</param>
        private static void AppendTextPart(StringBuilder builder, string mediaType, string text)
        {
            builder.Append("Content-Type: ").Append(mediaType).Append("; charset=utf-8").Append(CrLf);
            builder.Append("Content-Transfer-Encoding: base64").Append(CrLf);
            builder.Append(CrLf);
            AppendBase64(builder, Encoding.UTF8.GetBytes(text));
        }

        /// <summary>
        /// 寫出一個附件部件。
        /// Writes one attachment part.
        /// </summary>
        /// <param name="builder">輸出緩衝。The output buffer.</param>
        /// <param name="attachment">附件。The attachment.</param>
        private static void AppendAttachment(StringBuilder builder, GmailAttachmentContent attachment)
        {
            var fileName = string.IsNullOrWhiteSpace(attachment.FileName) ? "attachment" : attachment.FileName.Trim();
            var contentType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType.Trim();
            var quotedName = QuoteParameterValue(fileName);

            builder.Append("Content-Type: ").Append(contentType).Append("; name=").Append(quotedName).Append(CrLf);
            builder.Append("Content-Disposition: attachment; filename=").Append(quotedName).Append(CrLf);
            builder.Append("Content-Transfer-Encoding: base64").Append(CrLf);
            builder.Append(CrLf);
            AppendBase64(builder, attachment.Content);
        }

        /// <summary>
        /// 以每 76 字元一行的 base64 寫出位元組,結尾補一個換行。
        /// Writes bytes as base64 wrapped at 76 characters, followed by a line break.
        /// </summary>
        /// <param name="builder">輸出緩衝。The output buffer.</param>
        /// <param name="bytes">要編碼的位元組。The bytes to encode.</param>
        private static void AppendBase64(StringBuilder builder, byte[] bytes)
        {
            if (bytes.Length > 0)
                builder.Append(Convert.ToBase64String(bytes, 0, bytes.Length, Base64FormattingOptions.InsertLineBreaks));

            builder.Append(CrLf);
        }

        /// <summary>
        /// 寫出地址類標頭,多個地址以逗號分隔,過長時在地址之間折行。
        /// Writes an address header, comma-separating the addresses and folding between them when the line gets long.
        /// </summary>
        /// <param name="builder">輸出緩衝。The output buffer.</param>
        /// <param name="name">標頭名稱。The header name.</param>
        /// <param name="addresses">地址清單。The addresses.</param>
        private static void AppendAddressHeader(StringBuilder builder, string name, IEnumerable<GmailAddress> addresses)
        {
            builder.Append(name).Append(':');
            var lineLength = name.Length + 1;
            var first = true;

            foreach (var address in addresses)
            {
                if (address == null)
                    continue;

                var formatted = FormatAddress(address);

                if (first)
                {
                    builder.Append(' ').Append(formatted);
                    lineLength += 1 + formatted.Length;
                    first = false;
                    continue;
                }

                builder.Append(',');
                lineLength += 1;

                if (lineLength + 1 + formatted.Length > PreferredLineLength)
                {
                    builder.Append(CrLf).Append(' ').Append(formatted);
                    lineLength = 1 + formatted.Length;
                }
                else
                {
                    builder.Append(' ').Append(formatted);
                    lineLength += 1 + formatted.Length;
                }
            }

            builder.Append(CrLf);
        }

        /// <summary>
        /// 把一個地址寫成標頭用的形式:純 ASCII 名稱直接寫或加引號,非 ASCII 名稱用 RFC 2047 編碼。
        /// Formats one address for a header: ASCII names are written bare or quoted, non-ASCII names are RFC 2047 encoded.
        /// </summary>
        /// <param name="address">地址。The address.</param>
        /// <returns>標頭用字串。The header form.</returns>
        private static string FormatAddress(GmailAddress address)
        {
            if (address.Name == null)
                return address.Address;

            string phrase;
            if (!IsAscii(address.Name))
                phrase = string.Join(" ", EncodeWords(address.Name));
            else if (NeedsQuoting(address.Name))
                phrase = "\"" + address.Name.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            else
                phrase = address.Name;

            return phrase + " <" + address.Address + ">";
        }

        /// <summary>
        /// 寫出文字類標頭:ASCII 在空白處折行,非 ASCII 切成多個 encoded-word 各自一行。
        /// Writes a text header: ASCII is folded at spaces, non-ASCII becomes one encoded-word per continuation line.
        /// </summary>
        /// <param name="builder">輸出緩衝。The output buffer.</param>
        /// <param name="name">標頭名稱。The header name.</param>
        /// <param name="value">標頭值。The header value.</param>
        private static void AppendTextHeader(StringBuilder builder, string name, string value)
        {
            builder.Append(name).Append(':');

            if (IsAscii(value))
            {
                AppendFoldedAscii(builder, name.Length + 1, value);
            }
            else
            {
                // 第一個 encoded-word 與標頭名同一行,所以要扣掉「名稱: 」佔的長度;放不下就整個換到續行
                // The first encoded-word shares the line with the header name, so its budget shrinks by "Name: "; when nothing fits it moves to a continuation line.
                var firstLineRoom = PreferredLineLength - 2 - (name.Length + 2) - EncodedWordPrefix.Length - EncodedWordSuffix.Length;
                var firstMaxBytes = Math.Min(MaxBytesPerEncodedWord, firstLineRoom / 4 * 3);
                var first = true;

                foreach (var word in EncodeWords(value, firstMaxBytes > 0 ? firstMaxBytes : MaxBytesPerEncodedWord))
                {
                    builder.Append(first && firstMaxBytes > 0 ? " " : CrLf + " ").Append(word);
                    first = false;
                }
            }

            builder.Append(CrLf);
        }

        /// <summary>
        /// 把 ASCII 標頭值寫出並在空白處折行,讓每行盡量不超過 78 字元。
        /// Writes an ASCII header value folded at spaces so lines stay within 78 characters where possible.
        /// </summary>
        /// <param name="builder">輸出緩衝。The output buffer.</param>
        /// <param name="usedLength">目前這行已用的長度。The length already used on the current line.</param>
        /// <param name="value">標頭值。The header value.</param>
        private static void AppendFoldedAscii(StringBuilder builder, int usedLength, string value)
        {
            var lineLength = usedLength;
            var tokens = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0)
                return;

            foreach (var token in tokens)
            {
                if (lineLength > usedLength && lineLength + 1 + token.Length > PreferredLineLength)
                {
                    builder.Append(CrLf).Append(' ').Append(token);
                    lineLength = 1 + token.Length;
                }
                else
                {
                    builder.Append(' ').Append(token);
                    lineLength += 1 + token.Length;
                }
            }
        }

        /// <summary>
        /// 驗證並寫出呼叫端提供的額外標頭。
        /// Validates and writes a caller-supplied extra header.
        /// </summary>
        /// <param name="builder">輸出緩衝。The output buffer.</param>
        /// <param name="header">額外標頭。The extra header.</param>
        /// <exception cref="InvalidOperationException">名稱不合法、與內建標頭衝突,或值含換行時擲出。Thrown when the name is invalid or reserved, or the value contains a line break.</exception>
        private static void AppendExtraHeader(StringBuilder builder, GmailHeader header)
        {
            if (header == null)
                return;

            var name = header.Name == null ? string.Empty : header.Name.Trim();
            if (name.Length == 0 || !IsValidHeaderName(name))
                throw new InvalidOperationException("額外標頭名稱不合法:「" + name + "」。只允許可列印 ASCII 且不含冒號與空白。Invalid extra header name \"" + name + "\": only printable ASCII without colons or whitespace is allowed.");

            foreach (var reserved in ReservedHeaders)
            {
                if (string.Equals(reserved, name, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("額外標頭「" + name + "」與組信器自動產生的標頭衝突,請改用對應的屬性。Extra header \"" + name + "\" clashes with a header the writer generates; use the corresponding property instead.");
            }

            var value = header.Value ?? string.Empty;
            if (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
                throw new InvalidOperationException("額外標頭「" + name + "」的值不可含換行字元。The value of extra header \"" + name + "\" cannot contain line breaks.");

            AppendTextHeader(builder, name, value);
        }

        /// <summary>
        /// 把文字切成多個 RFC 2047 B 編碼字,切割點落在完整字元(含代理對)邊界上。
        /// Splits text into RFC 2047 B-encoded words, cutting only on whole-character (surrogate-pair aware) boundaries.
        /// </summary>
        /// <param name="text">要編碼的文字。The text to encode.</param>
        /// <param name="firstMaxBytes">第一個 encoded-word 最多裝幾個位元組(與標頭名同行時要縮短);之後的一律 45。The byte budget of the first encoded-word (smaller when it shares a line with the header name); later words always get 45.</param>
        /// <returns>encoded-word 清單,每個不超過 75 字元。The encoded-words, none longer than 75 characters.</returns>
        private static List<string> EncodeWords(string text, int firstMaxBytes = MaxBytesPerEncodedWord)
        {
            var words = new List<string>();
            var chunk = new StringBuilder();
            var chunkBytes = 0;
            var index = 0;

            while (index < text.Length)
            {
                var length = char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
                var piece = text.Substring(index, length);
                var pieceBytes = Encoding.UTF8.GetByteCount(piece);
                var limit = words.Count == 0 ? firstMaxBytes : MaxBytesPerEncodedWord;

                if (chunkBytes + pieceBytes > limit && chunk.Length > 0)
                {
                    words.Add(EncodeWord(chunk.ToString()));
                    chunk.Clear();
                    chunkBytes = 0;
                }

                chunk.Append(piece);
                chunkBytes += pieceBytes;
                index += length;
            }

            if (chunk.Length > 0)
                words.Add(EncodeWord(chunk.ToString()));

            return words;
        }

        /// <summary>
        /// 把一小段文字編成單一 encoded-word。
        /// Encodes one short piece of text as a single encoded-word.
        /// </summary>
        /// <param name="text">文字,UTF-8 後不超過 45 位元組。The text; at most 45 bytes in UTF-8.</param>
        /// <returns>encoded-word。The encoded-word.</returns>
        private static string EncodeWord(string text)
        {
            var word = EncodedWordPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(text)) + EncodedWordSuffix;
            return word.Length <= MaxEncodedWordLength ? word : throw new InvalidOperationException("encoded-word 超過 75 字元,這是組信器的內部錯誤。An encoded-word exceeded 75 characters; this is a writer bug.");
        }

        /// <summary>
        /// 把附件檔名寫成加引號的參數值;非 ASCII 或含引號時改用 RFC 2047 編碼再加引號。
        /// Quotes an attachment file name as a parameter value; non-ASCII or quote-bearing names are RFC 2047 encoded first.
        /// </summary>
        /// <param name="value">檔名。The file name.</param>
        /// <returns>加引號的參數值。The quoted parameter value.</returns>
        private static string QuoteParameterValue(string value)
        {
            if (IsAscii(value) && value.IndexOf('"') < 0 && value.IndexOf('\\') < 0 && value.IndexOf('\r') < 0 && value.IndexOf('\n') < 0)
                return "\"" + value + "\"";

            return "\"" + string.Join(" ", EncodeWords(value)) + "\"";
        }

        /// <summary>
        /// 正規化 Message-ID:去除前後空白、補上角括號;含換行時擲出。
        /// Normalises a Message-ID: trims it and adds angle brackets; throws when it contains a line break.
        /// </summary>
        /// <param name="value">Message-ID。The Message-ID.</param>
        /// <returns>含角括號的 Message-ID。The Message-ID with angle brackets.</returns>
        /// <exception cref="InvalidOperationException">含換行字元時擲出。Thrown when the value contains a line break.</exception>
        private static string NormalizeMessageId(string value)
        {
            var id = value.Trim();
            if (id.IndexOf('\r') >= 0 || id.IndexOf('\n') >= 0 || id.IndexOf(' ') >= 0)
                throw new InvalidOperationException("Message-ID 不可含空白或換行字元:「" + id + "」。A Message-ID cannot contain whitespace or line breaks: \"" + id + "\".");

            if (!id.StartsWith("<", StringComparison.Ordinal))
                id = "<" + id;

            if (!id.EndsWith(">", StringComparison.Ordinal))
                id += ">";

            return id;
        }

        /// <summary>
        /// 產生一個不會與內容碰撞的 multipart 邊界字串。
        /// Produces a multipart boundary that will not collide with the content.
        /// </summary>
        /// <returns>邊界字串。The boundary.</returns>
        private static string NewBoundary()
        {
            return "=_Ozakboy.Gmail_" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 判斷字串是否全為 ASCII。
        /// Tells whether a string is pure ASCII.
        /// </summary>
        /// <param name="value">字串。The string.</param>
        /// <returns>true 表示全為 ASCII。true when every character is ASCII.</returns>
        private static bool IsAscii(string value)
        {
            foreach (var c in value)
            {
                if (c > 127)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 判斷 ASCII 顯示名稱是否含 RFC 5322 的特殊字元而需要加引號。
        /// Tells whether an ASCII display name contains RFC 5322 specials and therefore needs quoting.
        /// </summary>
        /// <param name="value">顯示名稱。The display name.</param>
        /// <returns>true 表示需要加引號。true when quoting is needed.</returns>
        private static bool NeedsQuoting(string value)
        {
            foreach (var c in value)
            {
                if (c < 32 || c == 127 || "()<>[]:;@\\,.\"".IndexOf(c) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 判斷標頭名稱是否符合 RFC 5322 的 field-name(可列印 ASCII、不含冒號)。
        /// Tells whether a header name is a valid RFC 5322 field-name (printable ASCII, no colon).
        /// </summary>
        /// <param name="name">標頭名稱。The header name.</param>
        /// <returns>true 表示合法。true when valid.</returns>
        private static bool IsValidHeaderName(string name)
        {
            foreach (var c in name)
            {
                if (c < 33 || c > 126 || c == ':')
                    return false;
            }

            return true;
        }
    }
}
