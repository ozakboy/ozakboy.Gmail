using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// Gmail batch 端點回應(multipart/mixed,每個部件是一段完整的 HTTP 回應)的解析器。
    /// The parser for the Gmail batch endpoint's response: multipart/mixed whose every part is a complete HTTP response.
    /// </summary>
    /// <remarks>
    /// 抽成獨立型別是為了能單獨做單元測試,不必真的送出 HTTP 請求。
    /// 這裡刻意不用 <c>MultipartMemoryStreamProvider</c> 之類的 API,netstandard2.0 沒有,而且格式固定、手寫更好掌握。
    /// Kept as a separate type so it can be unit tested without issuing real HTTP requests.
    /// It deliberately avoids APIs such as <c>MultipartMemoryStreamProvider</c>, which netstandard2.0 lacks, and the format is fixed enough to parse by hand.
    /// </remarks>
    internal static class BatchResponseParser
    {
        /// <summary>
        /// 內嵌 HTTP 回應狀態列的前綴。
        /// The prefix of the embedded HTTP response's status line.
        /// </summary>
        private const string HttpVersionPrefix = "HTTP/";

        /// <summary>
        /// 從回應的 Content-Type 取出 multipart 的 boundary。
        /// Reads the multipart boundary out of the response's Content-Type header.
        /// </summary>
        /// <param name="contentType">回應的 Content-Type 標頭值,可為 null。The response's Content-Type header value; may be null.</param>
        /// <param name="boundary">取出的 boundary,失敗時為空字串。The boundary that was found; an empty string on failure.</param>
        /// <returns>true 表示成功取出 boundary。true when a boundary was found.</returns>
        internal static bool TryGetBoundary(string? contentType, out string boundary)
        {
            boundary = string.Empty;
            if (string.IsNullOrEmpty(contentType))
                return false;

            var index = contentType!.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return false;

            var value = contentType.Substring(index + "boundary=".Length).Trim();

            // 參數可能以分號接續其他參數,先切掉
            // Other parameters may follow after a semicolon; cut them off first.
            var semicolon = value.IndexOf(';');
            if (semicolon >= 0)
                value = value.Substring(0, semicolon).Trim();

            // 引號是選用的,兩種寫法都要接受
            // The quotes are optional, so both spellings have to be accepted.
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                value = value.Substring(1, value.Length - 2);

            boundary = value;
            return boundary.Length > 0;
        }

        /// <summary>
        /// 依 boundary 切開 multipart 主體,並解析出每個部件內嵌的 HTTP 回應。
        /// Splits the multipart body on the boundary and parses the HTTP response embedded in every part.
        /// </summary>
        /// <param name="body">回應主體,可為 null。The response body; may be null.</param>
        /// <param name="boundary">multipart 的 boundary,可為 null。The multipart boundary; may be null.</param>
        /// <returns>子回應清單,依部件在回應中的順序排列;永不為 null。The sub-responses in the order they appeared; never null.</returns>
        internal static List<BatchSubResponse> Parse(string? body, string? boundary)
        {
            var result = new List<BatchSubResponse>();
            if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(boundary))
                return result;

            var delimiter = "--" + boundary;
            var index = body!.IndexOf(delimiter, StringComparison.Ordinal);

            while (index >= 0)
            {
                var start = index + delimiter.Length;

                // "--boundary--" 是結束分隔線,後面沒有部件了
                // "--boundary--" is the closing delimiter; no part follows it.
                if (start >= body.Length || (body[start] == '-' && start + 1 < body.Length && body[start + 1] == '-'))
                    break;

                var next = body.IndexOf(delimiter, start, StringComparison.Ordinal);
                var segment = next < 0 ? body.Substring(start) : body.Substring(start, next - start);

                var part = ParsePart(segment);
                if (part != null)
                    result.Add(part);

                if (next < 0)
                    break;

                index = next;
            }

            return result;
        }

        /// <summary>
        /// 解析單一部件:部件標頭(取 Content-ID)、空行,之後是一段完整的 HTTP 回應。
        /// Parses one part: the part headers (for Content-ID), a blank line, then a complete HTTP response.
        /// </summary>
        /// <param name="segment">部件內容(不含前後的分隔線)。The part content, without the surrounding delimiters.</param>
        /// <returns>子回應;結構不完整時為 null。The sub-response, or null when the part is malformed.</returns>
        private static BatchSubResponse? ParsePart(string segment)
        {
            var headerEnd = IndexOfBlankLine(segment, 0, out var blankLength);
            if (headerEnd < 0)
                return null;

            var partHeaders = segment.Substring(0, headerEnd);
            var inner = segment.Substring(headerEnd + blankLength);

            var contentId = TrimAngleBrackets(ReadHeader(partHeaders, "Content-ID"));

            // 內嵌回應:狀態列、標頭、空行、主體
            // The embedded response: status line, headers, blank line, body.
            var statusLineEnd = IndexOfLineBreak(inner, 0);
            if (statusLineEnd < 0)
                return new BatchSubResponse(ParseStatusCode(inner.Trim()), contentId, string.Empty);

            var statusCode = ParseStatusCode(inner.Substring(0, statusLineEnd));

            // 從狀態列的換行處開始找空行,這樣「狀態列後直接接主體」(完全沒有標頭)也讀得到主體
            // The search starts at the status line's own line break so a response with no headers at all still yields its body.
            var innerHeaderEnd = IndexOfBlankLine(inner, statusLineEnd, out var innerBlankLength);
            var innerBody = innerHeaderEnd < 0
                ? string.Empty
                : inner.Substring(innerHeaderEnd + innerBlankLength);

            return new BatchSubResponse(statusCode, contentId, innerBody.Trim());
        }

        /// <summary>
        /// 由狀態列("HTTP/1.1 200 OK")取出狀態碼。
        /// Reads the status code out of a status line ("HTTP/1.1 200 OK").
        /// </summary>
        /// <param name="statusLine">狀態列。The status line.</param>
        /// <returns>狀態碼;無法解析時為 0。The status code; 0 when it cannot be parsed.</returns>
        private static int ParseStatusCode(string statusLine)
        {
            var line = statusLine.Trim();
            if (!line.StartsWith(HttpVersionPrefix, StringComparison.OrdinalIgnoreCase))
                return 0;

            var space = line.IndexOf(' ');
            if (space < 0)
                return 0;

            var rest = line.Substring(space + 1).TrimStart();
            var end = rest.IndexOf(' ');
            var code = end < 0 ? rest : rest.Substring(0, end);

            return int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        /// <summary>
        /// 從標頭區塊中以不分大小寫的方式取出指定標頭的值。
        /// Reads one header value from a header block, ignoring case.
        /// </summary>
        /// <param name="headers">標頭區塊。The header block.</param>
        /// <param name="name">標頭名稱。The header name.</param>
        /// <returns>標頭值,找不到時為 null。The header value, or null when no header matches.</returns>
        private static string? ReadHeader(string headers, string name)
        {
            var lines = headers.Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                var colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;

                var headerName = line.Substring(0, colon).Trim();
                if (string.Equals(headerName, name, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(colon + 1).Trim();
            }

            return null;
        }

        /// <summary>
        /// 去掉字串前後的角括號(Content-ID 的慣用寫法)。
        /// Strips the surrounding angle brackets a Content-ID conventionally carries.
        /// </summary>
        /// <param name="value">原始值,可為 null。The raw value; may be null.</param>
        /// <returns>去掉角括號後的值;<paramref name="value"/> 為 null 時為 null。The value without brackets, or null when <paramref name="value"/> is null.</returns>
        private static string? TrimAngleBrackets(string? value)
        {
            if (value == null)
                return null;

            var trimmed = value.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '<' && trimmed[trimmed.Length - 1] == '>')
                return trimmed.Substring(1, trimmed.Length - 2);

            return trimmed;
        }

        /// <summary>
        /// 找出空行(標頭與主體的分界),同時支援 CRLF 與 LF 兩種換行。
        /// Finds the blank line separating headers from body, accepting both CRLF and LF line endings.
        /// </summary>
        /// <param name="text">來源字串。The source text.</param>
        /// <param name="startIndex">起始位置。Where to start looking.</param>
        /// <param name="length">分界字串的長度。The length of the separator that was found.</param>
        /// <returns>空行的起始位置;找不到時為 -1。The index of the blank line, or -1 when there is none.</returns>
        private static int IndexOfBlankLine(string text, int startIndex, out int length)
        {
            var crlf = text.IndexOf("\r\n\r\n", startIndex, StringComparison.Ordinal);
            var lf = text.IndexOf("\n\n", startIndex, StringComparison.Ordinal);

            if (crlf >= 0 && (lf < 0 || crlf <= lf))
            {
                length = 4;
                return crlf;
            }

            if (lf >= 0)
            {
                length = 2;
                return lf;
            }

            length = 0;
            return -1;
        }

        /// <summary>
        /// 找出下一個換行,同時支援 CRLF 與 LF。
        /// Finds the next line break, accepting both CRLF and LF.
        /// </summary>
        /// <param name="text">來源字串。The source text.</param>
        /// <param name="startIndex">起始位置。Where to start looking.</param>
        /// <returns>換行的起始位置(CRLF 時指向 '\r');找不到時為 -1。The index of the line break, pointing at the '\r' of a CRLF; -1 when there is none.</returns>
        private static int IndexOfLineBreak(string text, int startIndex)
        {
            var index = text.IndexOf('\n', startIndex);
            if (index < 0)
                return -1;

            return index > startIndex && text[index - 1] == '\r' ? index - 1 : index;
        }
    }
}
