using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ozakboy.Gmail.Core;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// Gmail REST API 用戶端。不依賴 Google.Apis,不保存權杖,所有網路方法都是非同步。
    /// The Gmail REST API client. It does not depend on Google.Apis, never stores tokens, and every network member is async.
    /// </summary>
    /// <remarks>
    /// 建構後不保有任何每次呼叫的狀態,可以安全註冊成 singleton;
    /// <see cref="HttpClient.BaseAddress"/> 會被忽略,一律使用絕對網址。
    /// The instance keeps no per-call state after construction and is safe as a singleton;
    /// <see cref="HttpClient.BaseAddress"/> is ignored because absolute URLs are always used.
    /// </remarks>
    public class GmailClient : IGmailClient
    {
        /// <summary>
        /// Gmail REST API 的主機位址(不含路徑)。
        /// The Gmail REST API host, without any path.
        /// </summary>
        private const string ApiHost = "https://gmail.googleapis.com";

        /// <summary>
        /// Gmail REST API 的路徑前綴(結尾為 users/)。
        /// The Gmail REST API path prefix, ending in users/.
        /// </summary>
        private const string ApiPathPrefix = "/gmail/v1/users/";

        /// <summary>
        /// Gmail REST API 的基底網址(結尾為 users/)。
        /// The Gmail REST API base URL, ending in users/.
        /// </summary>
        private const string ApiBaseUrl = ApiHost + ApiPathPrefix;

        /// <summary>
        /// 批次端點的路徑。
        /// The batch endpoint path.
        /// </summary>
        private const string BatchPath = "/batch/gmail/v1";

        /// <summary>
        /// 批次端點的網址。所有子請求都包在這一個 multipart/mixed 請求裡。
        /// The batch endpoint URL; every sub-request travels inside this single multipart/mixed request.
        /// </summary>
        private const string BatchUrl = ApiHost + BatchPath;

        /// <summary>
        /// 批次子回應的 Content-ID 前綴,後面接的數字對應送出的第幾個子請求。
        /// The Content-ID prefix of a batch sub-response; the number after it is the position of the matching sub-request.
        /// </summary>
        private const string BatchResponseItemPrefix = "response-item";

        /// <summary>
        /// 批次子請求的媒體類型。
        /// The media type of a batch sub-request.
        /// </summary>
        private const string HttpMediaType = "application/http";

        /// <summary>
        /// 批次每次 HTTP 請求可打包的郵件數上限。
        /// The largest number of messages one batch HTTP request may carry.
        /// </summary>
        private const int MaxBatchSize = 100;

        /// <summary>
        /// 寄信用的多段上傳基底網址(結尾為 users/)。
        /// The multipart upload base URL used for sending, ending in users/.
        /// </summary>
        private const string UploadBaseUrl = "https://gmail.googleapis.com/upload/gmail/v1/users/";

        /// <summary>
        /// JSON 請求主體的媒體類型。
        /// The media type of a JSON request body.
        /// </summary>
        private const string JsonMediaType = "application/json";

        /// <summary>
        /// HTTP 傳輸核心。
        /// The HTTP transport core.
        /// </summary>
        private readonly GmailHttp _http;

        /// <summary>
        /// 已做過 URL 轉義的 {userId} 路徑片段。
        /// The URL-escaped {userId} path segment.
        /// </summary>
        private readonly string _userId;

        /// <summary>
        /// 批次取信時每個 HTTP 請求打包幾封郵件。
        /// How many messages one batch HTTP request carries.
        /// </summary>
        private readonly int _batchSize;

        /// <summary>
        /// 以預設設定建立用戶端。
        /// Creates the client with the default options.
        /// </summary>
        /// <param name="httpClient">送出請求用的 HttpClient,本套件不負責釋放。The HttpClient used for every call; this library never disposes it.</param>
        /// <param name="accessTokenProvider">存取權杖提供者,每次呼叫(非每次重試)會被呼叫一次。The access token provider, invoked once per call and not per retry.</param>
        /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> 或 <paramref name="accessTokenProvider"/> 為 null 時擲出。Thrown when <paramref name="httpClient"/> or <paramref name="accessTokenProvider"/> is null.</exception>
        public GmailClient(HttpClient httpClient, Func<CancellationToken, Task<string>> accessTokenProvider)
            : this(httpClient, accessTokenProvider, null)
        {
        }

        /// <summary>
        /// 以指定設定建立用戶端。
        /// Creates the client with the given options.
        /// </summary>
        /// <param name="httpClient">送出請求用的 HttpClient,本套件不負責釋放。The HttpClient used for every call; this library never disposes it.</param>
        /// <param name="accessTokenProvider">存取權杖提供者,每次呼叫(非每次重試)會被呼叫一次。The access token provider, invoked once per call and not per retry.</param>
        /// <param name="options">重試與信箱設定,null 視同預設值;內容會在此複製一份。Retry and mailbox settings; null means the defaults, and the values are copied here.</param>
        /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> 或 <paramref name="accessTokenProvider"/> 為 null 時擲出。Thrown when <paramref name="httpClient"/> or <paramref name="accessTokenProvider"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> 的 MaxRetries 為負值、MaxRetryDelay 為負值,或 BatchSize 不在 1 到 100 之間時擲出。Thrown when MaxRetries or MaxRetryDelay on <paramref name="options"/> is negative, or BatchSize is outside 1 to 100.</exception>
        public GmailClient(HttpClient httpClient, Func<CancellationToken, Task<string>> accessTokenProvider, GmailClientOptions? options)
        {
            if (httpClient == null)
                throw new ArgumentNullException(nameof(httpClient));

            if (accessTokenProvider == null)
                throw new ArgumentNullException(nameof(accessTokenProvider));

            var maxRetries = options == null ? new GmailClientOptions().MaxRetries : options.MaxRetries;
            if (maxRetries < 0)
                throw new ArgumentOutOfRangeException(nameof(options), maxRetries, "MaxRetries 不可為負值。MaxRetries cannot be negative.");

            var maxRetryDelay = options == null ? new GmailClientOptions().MaxRetryDelay : options.MaxRetryDelay;
            if (maxRetryDelay.HasValue && maxRetryDelay.Value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(options), maxRetryDelay, "MaxRetryDelay 不可為負值。MaxRetryDelay cannot be negative.");

            var batchSize = options == null ? new GmailClientOptions().BatchSize : options.BatchSize;
            if (batchSize < 1 || batchSize > MaxBatchSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    batchSize,
                    "BatchSize 必須介於 1 與 100 之間。BatchSize has to be between 1 and 100.");
            }

            var retryBaseDelay = options == null ? new GmailClientOptions().RetryBaseDelay : options.RetryBaseDelay;
            var retryOnNetworkErrors = options != null && options.RetryOnNetworkErrors;
            var userId = options == null || string.IsNullOrWhiteSpace(options.UserId) ? "me" : options.UserId;

            _batchSize = batchSize;
            _userId = Uri.EscapeDataString(userId);
            _http = new GmailHttp(
                httpClient,
                accessTokenProvider,
                new RetryPolicy(maxRetries, retryBaseDelay, maxRetryDelay, retryOnNetworkErrors));
        }

        /// <inheritdoc />
        public Task<GmailProfile> GetProfileAsync(CancellationToken cancellationToken = default)
        {
            var url = BuildUrl("profile", null);
            return _http.SendForJsonAsync<GmailProfile>(() => new HttpRequestMessage(HttpMethod.Get, url), false, cancellationToken);
        }

        /// <inheritdoc />
        public Task<GmailMessageList> ListMessagesAsync(
            string? query = null,
            IEnumerable<string>? labelIds = null,
            int? maxResults = null,
            string? pageToken = null,
            bool includeSpamTrash = false,
            CancellationToken cancellationToken = default)
        {
            var parameters = new StringBuilder();
            AppendParameter(parameters, "q", query);
            AppendParameters(parameters, "labelIds", labelIds);
            AppendParameter(parameters, "maxResults", maxResults);
            AppendParameter(parameters, "pageToken", pageToken);

            // includeSpamTrash 只有 true 才送,維持與 Gmail 預設一致的最短查詢字串
            // includeSpamTrash is only sent when true, keeping the query string as short as Gmail's default behaviour allows.
            if (includeSpamTrash)
                AppendParameter(parameters, "includeSpamTrash", "true");

            var url = BuildUrl("messages", parameters);
            return _http.SendForJsonAsync<GmailMessageList>(() => new HttpRequestMessage(HttpMethod.Get, url), false, cancellationToken);
        }

        /// <inheritdoc />
        public Task<GmailMessage> GetMessageAsync(
            string id,
            GmailMessageFormat format = GmailMessageFormat.Full,
            IEnumerable<string>? metadataHeaders = null,
            CancellationToken cancellationToken = default)
        {
            RequireText(id, nameof(id));

            var url = BuildUrl("messages/" + Uri.EscapeDataString(id), BuildFormatParameters(format, metadataHeaders));
            return _http.SendForJsonAsync<GmailMessage>(() => new HttpRequestMessage(HttpMethod.Get, url), false, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<GmailBatchGetResult> BatchGetMessagesAsync(
            IEnumerable<string> ids,
            GmailMessageFormat format = GmailMessageFormat.Full,
            IEnumerable<string>? metadataHeaders = null,
            CancellationToken cancellationToken = default)
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            var messageIds = new List<string>(ids);
            var result = new GmailBatchGetResult();

            // 沒有任何郵件就不必送請求
            // Nothing to fetch means nothing to send.
            if (messageIds.Count == 0)
                return result;

            foreach (var messageId in messageIds)
            {
                if (string.IsNullOrWhiteSpace(messageId))
                {
                    throw new ArgumentException(
                        "郵件識別碼不可為 null 或空白。A message id cannot be null or blank.",
                        nameof(ids));
                }
            }

            // 查詢字串各段共用,先算一次,順便把 metadataHeaders 固化避免重複列舉
            // The query string is shared by every chunk, so it is computed once, which also materialises metadataHeaders instead of enumerating it repeatedly.
            var parameters = BuildFormatParameters(format, metadataHeaders);

            for (var offset = 0; offset < messageIds.Count; offset += _batchSize)
            {
                var chunk = messageIds.GetRange(offset, Math.Min(_batchSize, messageIds.Count - offset));
                await ExecuteBatchChunkAsync(chunk, parameters, result, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }

        /// <summary>
        /// 以 format=raw 取回郵件,回傳 base64url 解碼後的完整 RFC 822 位元組;可交給任何 MIME 函式庫解析。
        /// Fetches the message with format=raw and returns the base64url-decoded RFC 822 bytes, ready for any MIME parser.
        /// </summary>
        /// <param name="id">郵件識別碼,null 或空字串時擲出例外。The message id; null or empty throws.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>RFC 822 郵件位元組;Gmail 沒回傳 raw 時為空陣列。The RFC 822 bytes; an empty array when Gmail returned no raw field.</returns>
        /// <exception cref="ArgumentException"><paramref name="id"/> 為 null 或空白時擲出。Thrown when <paramref name="id"/> is null or blank.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        public async Task<byte[]> GetMessageRawAsync(string id, CancellationToken cancellationToken = default)
        {
            var message = await GetMessageAsync(id, GmailMessageFormat.Raw, null, cancellationToken).ConfigureAwait(false);
            return message.DecodeRaw() ?? Array.Empty<byte>();
        }

        /// <inheritdoc />
        public Task<GmailHistoryList> ListHistoryAsync(
            string startHistoryId,
            IEnumerable<GmailHistoryType>? historyTypes = null,
            string? pageToken = null,
            string? labelId = null,
            int? maxResults = null,
            CancellationToken cancellationToken = default)
        {
            RequireText(startHistoryId, nameof(startHistoryId));

            var parameters = new StringBuilder();
            AppendParameter(parameters, "startHistoryId", startHistoryId);

            if (historyTypes != null)
            {
                foreach (var historyType in historyTypes)
                    AppendParameter(parameters, "historyTypes", ToWire(historyType));
            }

            AppendParameter(parameters, "pageToken", pageToken);
            AppendParameter(parameters, "labelId", labelId);
            AppendParameter(parameters, "maxResults", maxResults);

            var url = BuildUrl("history", parameters);

            // 這裡的 true 讓 404 能被判定成「歷程已過期」,不必回頭猜路徑
            // The true here lets a 404 be recognised as expired history without guessing from the path.
            return _http.SendForJsonAsync<GmailHistoryList>(() => new HttpRequestMessage(HttpMethod.Get, url), true, cancellationToken);
        }

        /// <inheritdoc />
        public Task<GmailMessage> ModifyLabelsAsync(
            string id,
            IEnumerable<string>? addLabelIds,
            IEnumerable<string>? removeLabelIds,
            CancellationToken cancellationToken = default)
        {
            RequireText(id, nameof(id));

            var add = ToOptionalList(addLabelIds);
            var remove = ToOptionalList(removeLabelIds);
            if (add == null && remove == null)
            {
                throw new ArgumentException(
                    "addLabelIds 與 removeLabelIds 至少要有一個標籤。At least one label has to be supplied in addLabelIds or removeLabelIds.",
                    nameof(addLabelIds));
            }

            var body = GmailJson.Serialize(new GmailModifyLabelsRequest { AddLabelIds = add, RemoveLabelIds = remove });
            var url = BuildUrl("messages/" + Uri.EscapeDataString(id) + "/modify", null);

            return _http.SendForJsonAsync<GmailMessage>(() => CreateJsonRequest(HttpMethod.Post, url, body), false, cancellationToken);
        }

        /// <inheritdoc />
        public Task BatchModifyLabelsAsync(
            IEnumerable<string> ids,
            IEnumerable<string>? addLabelIds,
            IEnumerable<string>? removeLabelIds,
            CancellationToken cancellationToken = default)
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            var messageIds = new List<string>(ids);

            // 沒有任何郵件就不必送請求
            // Nothing to change means nothing to send.
            if (messageIds.Count == 0)
                return Task.CompletedTask;

            if (messageIds.Count > 1000)
            {
                throw new ArgumentException(
                    "Gmail 單次 batchModify 最多 1000 筆郵件。Gmail accepts at most 1000 message ids per batchModify call.",
                    nameof(ids));
            }

            var add = ToOptionalList(addLabelIds);
            var remove = ToOptionalList(removeLabelIds);
            if (add == null && remove == null)
            {
                throw new ArgumentException(
                    "addLabelIds 與 removeLabelIds 至少要有一個標籤。At least one label has to be supplied in addLabelIds or removeLabelIds.",
                    nameof(addLabelIds));
            }

            var body = GmailJson.Serialize(new GmailBatchModifyRequest { Ids = messageIds, AddLabelIds = add, RemoveLabelIds = remove });
            var url = BuildUrl("messages/batchModify", null);

            return _http.SendAsync(() => CreateJsonRequest(HttpMethod.Post, url, body), false, cancellationToken);
        }

        /// <inheritdoc />
        public Task<GmailMessage> TrashAsync(string id, CancellationToken cancellationToken = default)
        {
            RequireText(id, nameof(id));

            var url = BuildUrl("messages/" + Uri.EscapeDataString(id) + "/trash", null);
            return _http.SendForJsonAsync<GmailMessage>(() => new HttpRequestMessage(HttpMethod.Post, url), false, cancellationToken);
        }

        /// <inheritdoc />
        public Task<GmailMessage> UntrashAsync(string id, CancellationToken cancellationToken = default)
        {
            RequireText(id, nameof(id));

            var url = BuildUrl("messages/" + Uri.EscapeDataString(id) + "/untrash", null);
            return _http.SendForJsonAsync<GmailMessage>(() => new HttpRequestMessage(HttpMethod.Post, url), false, cancellationToken);
        }

        /// <inheritdoc />
        public Task<GmailMessage> ReportSpamAsync(string id, CancellationToken cancellationToken = default)
        {
            // Gmail 沒有專屬的檢舉端點,網頁版的做法就是移進 SPAM
            // Gmail has no dedicated report endpoint; moving the message into SPAM is what the web UI does.
            return ModifyLabelsAsync(
                id,
                new[] { GmailSystemLabels.Spam },
                new[] { GmailSystemLabels.Inbox },
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<GmailThread> GetThreadAsync(
            string id,
            GmailMessageFormat format = GmailMessageFormat.Full,
            IEnumerable<string>? metadataHeaders = null,
            CancellationToken cancellationToken = default)
        {
            RequireText(id, nameof(id));

            var url = BuildUrl("threads/" + Uri.EscapeDataString(id), BuildFormatParameters(format, metadataHeaders));
            return _http.SendForJsonAsync<GmailThread>(() => new HttpRequestMessage(HttpMethod.Get, url), false, cancellationToken);
        }

        /// <inheritdoc />
        public Task<GmailThread> ModifyThreadAsync(
            string id,
            IEnumerable<string>? addLabelIds,
            IEnumerable<string>? removeLabelIds,
            CancellationToken cancellationToken = default)
        {
            RequireText(id, nameof(id));

            var add = ToOptionalList(addLabelIds);
            var remove = ToOptionalList(removeLabelIds);
            if (add == null && remove == null)
            {
                throw new ArgumentException(
                    "addLabelIds 與 removeLabelIds 至少要有一個標籤。At least one label has to be supplied in addLabelIds or removeLabelIds.",
                    nameof(addLabelIds));
            }

            var body = GmailJson.Serialize(new GmailModifyLabelsRequest { AddLabelIds = add, RemoveLabelIds = remove });
            var url = BuildUrl("threads/" + Uri.EscapeDataString(id) + "/modify", null);

            return _http.SendForJsonAsync<GmailThread>(() => CreateJsonRequest(HttpMethod.Post, url, body), false, cancellationToken);
        }

        /// <inheritdoc />
        public Task<GmailThread> TrashThreadAsync(string id, CancellationToken cancellationToken = default)
        {
            RequireText(id, nameof(id));

            var url = BuildUrl("threads/" + Uri.EscapeDataString(id) + "/trash", null);
            return _http.SendForJsonAsync<GmailThread>(() => new HttpRequestMessage(HttpMethod.Post, url), false, cancellationToken);
        }

        /// <inheritdoc />
        public Task<GmailThread> UntrashThreadAsync(string id, CancellationToken cancellationToken = default)
        {
            RequireText(id, nameof(id));

            var url = BuildUrl("threads/" + Uri.EscapeDataString(id) + "/untrash", null);
            return _http.SendForJsonAsync<GmailThread>(() => new HttpRequestMessage(HttpMethod.Post, url), false, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<List<GmailLabel>> ListLabelsAsync(CancellationToken cancellationToken = default)
        {
            var url = BuildUrl("labels", null);
            var response = await _http
                .SendForJsonAsync<GmailLabelListResponse>(() => new HttpRequestMessage(HttpMethod.Get, url), false, cancellationToken)
                .ConfigureAwait(false);

            return response.Labels ?? new List<GmailLabel>();
        }

        /// <inheritdoc />
        public Task<GmailLabel> CreateLabelAsync(string name, GmailLabelOptions? options = null, CancellationToken cancellationToken = default)
        {
            RequireText(name, nameof(name));

            var body = GmailJson.Serialize(BuildLabelRequest(name, options));
            var url = BuildUrl("labels", null);

            return _http.SendForJsonAsync<GmailLabel>(() => CreateJsonRequest(HttpMethod.Post, url, body), false, cancellationToken);
        }

        /// <inheritdoc />
        public Task<GmailLabel> UpdateLabelAsync(string id, string? name = null, GmailLabelOptions? options = null, CancellationToken cancellationToken = default)
        {
            RequireText(id, nameof(id));

            var body = GmailJson.Serialize(BuildLabelRequest(name, options));
            var url = BuildUrl("labels/" + Uri.EscapeDataString(id), null);

            // netstandard2.0 沒有 HttpMethod.Patch,統一自行建立以維持各目標框架行為一致
            // netstandard2.0 has no HttpMethod.Patch, so the method is created by hand for consistent behaviour across targets.
            return _http.SendForJsonAsync<GmailLabel>(() => CreateJsonRequest(new HttpMethod("PATCH"), url, body), false, cancellationToken);
        }

        /// <inheritdoc />
        public Task DeleteLabelAsync(string id, CancellationToken cancellationToken = default)
        {
            RequireText(id, nameof(id));

            var url = BuildUrl("labels/" + Uri.EscapeDataString(id), null);
            return _http.SendAsync(() => new HttpRequestMessage(HttpMethod.Delete, url), false, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<GmailAttachment> GetAttachmentAsync(string messageId, string attachmentId, CancellationToken cancellationToken = default)
        {
            RequireText(messageId, nameof(messageId));
            RequireText(attachmentId, nameof(attachmentId));

            var url = BuildUrl(
                "messages/" + Uri.EscapeDataString(messageId) + "/attachments/" + Uri.EscapeDataString(attachmentId),
                null);

            var body = await _http
                .SendForJsonAsync<GmailAttachmentBody>(() => new HttpRequestMessage(HttpMethod.Get, url), false, cancellationToken)
                .ConfigureAwait(false);

            return new GmailAttachment
            {
                AttachmentId = body.AttachmentId,
                Size = body.Size,
                Data = body.Data == null ? Array.Empty<byte>() : Base64Url.Decode(body.Data),
            };
        }

        /// <inheritdoc />
        public async Task<long> DownloadAttachmentAsync(string messageId, string attachmentId, Stream destination, CancellationToken cancellationToken = default)
        {
            RequireText(messageId, nameof(messageId));
            RequireText(attachmentId, nameof(attachmentId));

            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            if (!destination.CanWrite)
                throw new ArgumentException("目標串流必須可寫入。The destination stream has to be writable.", nameof(destination));

            var attachment = await GetAttachmentAsync(messageId, attachmentId, cancellationToken).ConfigureAwait(false);

#if NETSTANDARD2_0
            await destination.WriteAsync(attachment.Data, 0, attachment.Data.Length, cancellationToken).ConfigureAwait(false);
#else
            await destination.WriteAsync(new ReadOnlyMemory<byte>(attachment.Data), cancellationToken).ConfigureAwait(false);
#endif

            // 串流刻意不關閉也不 Flush,由呼叫端決定何時收尾
            // The stream is deliberately neither closed nor flushed; that stays the caller's decision.
            return attachment.Data.LongLength;
        }

        /// <summary>
        /// 寄出郵件。郵件由內建組信器序列化成 RFC 822 後以 message/rfc822 多段上傳,因此只需要 gmail.modify 範圍,不必開 SMTP。
        /// Sends a message. The built-in writer serialises it to RFC 822 and uploads it as message/rfc822, so the gmail.modify scope is enough and no SMTP connection is needed.
        /// </summary>
        /// <param name="message">要寄出的郵件,null 時擲出例外。The message to send; null throws.</param>
        /// <param name="threadId">要併入的討論串識別碼,null 表示開新討論串。The thread to join; null starts a new thread.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>已寄出的郵件(含 Id、ThreadId 與 LabelIds)。The sent message, carrying Id, ThreadId and LabelIds.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> 為 null 時擲出。Thrown when <paramref name="message"/> is null.</exception>
        /// <exception cref="InvalidOperationException">郵件沒有收件人或標頭不合法時擲出(見 <see cref="GmailOutgoingMessage.ToRfc822Bytes"/>)。Thrown when the message has no recipients or an invalid header (see <see cref="GmailOutgoingMessage.ToRfc822Bytes"/>).</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        public Task<GmailMessage> SendAsync(GmailOutgoingMessage message, string? threadId = null, CancellationToken cancellationToken = default)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return SendRawAsync(message.ToRfc822Bytes(), threadId, cancellationToken);
        }

        /// <summary>
        /// 寄出呼叫端自行組好的 RFC 822 郵件位元組,原封不動以 message/rfc822 多段上傳。
        /// Sends caller-composed RFC 822 bytes, uploaded verbatim as message/rfc822.
        /// </summary>
        /// <param name="rfc822">完整的 RFC 822 郵件;null 或空陣列時擲出例外。The complete RFC 822 message; null or empty throws.</param>
        /// <param name="threadId">要併入的討論串識別碼,null 表示開新討論串。The thread to join; null starts a new thread.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>已寄出的郵件(含 Id、ThreadId 與 LabelIds)。The sent message, carrying Id, ThreadId and LabelIds.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rfc822"/> 為 null 時擲出。Thrown when <paramref name="rfc822"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="rfc822"/> 為空陣列時擲出。Thrown when <paramref name="rfc822"/> is empty.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        public Task<GmailMessage> SendRawAsync(byte[] rfc822, string? threadId = null, CancellationToken cancellationToken = default)
        {
            if (rfc822 == null)
                throw new ArgumentNullException(nameof(rfc822));

            if (rfc822.Length == 0)
                throw new ArgumentException("RFC 822 郵件內容不可為空。The RFC 822 message cannot be empty.", nameof(rfc822));

            // 位元組先算好,重試時 factory 才能重建同一份請求
            // The bytes are fixed up front so the factory can rebuild the very same request on retry.
            var metadata = GmailJson.Serialize(new GmailSendMetadata { ThreadId = threadId });
            var url = UploadBaseUrl + _userId + "/messages/send?uploadType=multipart";

            return _http.SendForJsonAsync<GmailMessage>(() => CreateSendRequest(url, metadata, rfc822), false, cancellationToken);
        }

        /// <summary>
        /// 送出一段批次請求並把子回應分類到成功與失敗兩邊。
        /// Sends one batch chunk and sorts its sub-responses into the successes and the failures.
        /// </summary>
        /// <param name="chunk">這一段要取的郵件識別碼。The message ids in this chunk.</param>
        /// <param name="parameters">共用的查詢字串緩衝區。The shared query string buffer.</param>
        /// <param name="result">累積結果的容器。The container accumulating the result.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>代表非同步作業的 <see cref="Task"/>。A <see cref="Task"/> representing the operation.</returns>
        /// <exception cref="GmailApiException">外層請求失敗,或回應不是可解析的 multipart 時擲出。Thrown when the outer request fails, or the response is not parsable multipart.</exception>
        private async Task ExecuteBatchChunkAsync(
            List<string> chunk,
            StringBuilder parameters,
            GmailBatchGetResult result,
            CancellationToken cancellationToken)
        {
            // 子請求路徑先算好,重試時 factory 才能重建同一份請求
            // The sub-request paths are fixed up front so the factory can rebuild the very same request on retry.
            var paths = new List<string>(chunk.Count);
            foreach (var messageId in chunk)
                paths.Add(BuildPath("messages/" + Uri.EscapeDataString(messageId), parameters));

            var raw = await _http.SendForRawAsync(() => CreateBatchRequest(paths), cancellationToken).ConfigureAwait(false);

            if (!BatchResponseParser.TryGetBoundary(raw.ContentType, out var boundary))
            {
                throw CreateBatchParseException(
                    "批次回應的 Content-Type 沒有 multipart boundary,無法解析。The batch response's Content-Type carries no multipart boundary, so it cannot be parsed.",
                    raw);
            }

            var parts = BatchResponseParser.Parse(raw.Body, boundary);
            if (parts.Count != chunk.Count)
            {
                throw CreateBatchParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "批次回應的部件數({0})與送出的郵件數({1})不符。The batch response has {0} parts but {1} messages were requested.",
                        parts.Count,
                        chunk.Count),
                    raw);
            }

            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var index = ResolveBatchIndex(part.ContentId, i, chunk.Count);

                if (part.StatusCode >= 200 && part.StatusCode <= 299)
                {
                    var message = string.IsNullOrWhiteSpace(part.Body)
                        ? new GmailMessage()
                        : GmailJson.Deserialize<GmailMessage>(part.Body) ?? new GmailMessage();

                    result.Messages.Add(message);
                    continue;
                }

                // 子部件的限流與 5xx 刻意不自動重試,交由呼叫端依 IsRateLimited 決定要不要重排
                // A rate-limited or 5xx sub-response is deliberately not retried here; IsRateLimited lets the caller decide whether to requeue it.
                var error = GoogleErrorBody.Parse(part.Body);
                result.Failures.Add(new GmailBatchFailure
                {
                    Id = chunk[index],
                    StatusCode = part.StatusCode,
                    Reason = error.Reason,
                    ErrorMessage = error.ErrorMessage,
                });
            }
        }

        /// <summary>
        /// 建立批次請求:multipart/mixed,每個部件是一段 application/http 的 GET 子請求。
        /// Builds the batch request: multipart/mixed whose every part is an application/http GET sub-request.
        /// </summary>
        /// <param name="paths">子請求的路徑加查詢字串。The path plus query string of every sub-request.</param>
        /// <returns>可送出的請求。The request, ready to be sent.</returns>
        private static HttpRequestMessage CreateBatchRequest(List<string> paths)
        {
            var content = new MultipartContent("mixed");

            for (var i = 0; i < paths.Count; i++)
            {
                // 子請求本身沒有 Authorization,權杖只掛在外層請求上
                // A sub-request carries no Authorization of its own; the token is attached to the outer request only.
                var part = new StringContent("GET " + paths[i] + " HTTP/1.1\r\n\r\n", Encoding.UTF8);
                part.Headers.ContentType = new MediaTypeHeaderValue(HttpMediaType);
                part.Headers.TryAddWithoutValidation(
                    "Content-ID",
                    "<item" + i.ToString(CultureInfo.InvariantCulture) + ">");

                content.Add(part);
            }

            return new HttpRequestMessage(HttpMethod.Post, BatchUrl) { Content = content };
        }

        /// <summary>
        /// 由部件的 Content-ID(<c>response-item{i}</c>)對回第幾個郵件識別碼;沒有或格式不符時退回部件順序。
        /// Maps a part's Content-ID (<c>response-item{i}</c>) back to a message id index, falling back to the part's own position when it is missing or malformed.
        /// </summary>
        /// <param name="contentId">部件的 Content-ID,可為 null。The part's Content-ID; may be null.</param>
        /// <param name="fallback">退回時使用的部件順序。The part position used as the fallback.</param>
        /// <param name="count">這一段的郵件數。How many messages this chunk holds.</param>
        /// <returns>郵件識別碼的索引。The index into the chunk's message ids.</returns>
        private static int ResolveBatchIndex(string? contentId, int fallback, int count)
        {
            if (contentId == null)
                return fallback;

            var trimmed = contentId.Trim();
            if (!trimmed.StartsWith(BatchResponseItemPrefix, StringComparison.OrdinalIgnoreCase))
                return fallback;

            var suffix = trimmed.Substring(BatchResponseItemPrefix.Length);
            if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < count)
                return index;

            return fallback;
        }

        /// <summary>
        /// 建立批次回應無法解析時的例外,狀態碼沿用外層回應。
        /// Builds the exception thrown when a batch response cannot be parsed; the status code is the outer response's.
        /// </summary>
        /// <param name="message">說明無法解析的原因。An explanation of why it could not be parsed.</param>
        /// <param name="raw">外層回應的原始內容。The raw content of the outer response.</param>
        /// <returns>組裝完成的例外。The assembled exception.</returns>
        private static GmailApiException CreateBatchParseException(string message, GmailRawResponse raw)
        {
            return new GmailApiException(
                raw.StatusCode,
                "batchParseError",
                message,
                string.IsNullOrEmpty(raw.Body) ? null : raw.Body,
                "POST",
                BatchPath,
                false);
        }

        /// <summary>
        /// 建立寄信用的多段上傳請求:第一段是 JSON 中繼資料,第二段是 message/rfc822 內容。
        /// Builds the multipart upload request used for sending: a JSON metadata part followed by the message/rfc822 content.
        /// </summary>
        /// <param name="url">上傳端點網址。The upload endpoint URL.</param>
        /// <param name="metadata">JSON 中繼資料。The JSON metadata.</param>
        /// <param name="rfc822">序列化後的郵件位元組。The serialised message bytes.</param>
        /// <returns>可送出的請求。The request, ready to be sent.</returns>
        private static HttpRequestMessage CreateSendRequest(string url, string metadata, byte[] rfc822)
        {
            var content = new MultipartContent("related");
            content.Add(new StringContent(metadata, Encoding.UTF8, JsonMediaType));

            var rawContent = new ByteArrayContent(rfc822);
            rawContent.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
            content.Add(rawContent);

            return new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        }

        /// <summary>
        /// 建立帶 JSON 主體的請求。
        /// Builds a request carrying a JSON body.
        /// </summary>
        /// <param name="method">HTTP 方法。The HTTP method.</param>
        /// <param name="url">請求網址。The request URL.</param>
        /// <param name="json">JSON 主體。The JSON body.</param>
        /// <returns>可送出的請求。The request, ready to be sent.</returns>
        private static HttpRequestMessage CreateJsonRequest(HttpMethod method, string url, string json)
        {
            return new HttpRequestMessage(method, url)
            {
                Content = new StringContent(json, Encoding.UTF8, JsonMediaType),
            };
        }

        /// <summary>
        /// 由名稱與選項組出標籤的請求主體,顏色只有在任一色值有設定時才寫入。
        /// Builds the label request body from the name and options; the colour object is only written when at least one colour is set.
        /// </summary>
        /// <param name="name">標籤名稱,null 表示不改名。The label name; null leaves it unchanged.</param>
        /// <param name="options">顯示方式與顏色,可為 null。Visibility and colours; may be null.</param>
        /// <returns>請求主體。The request body.</returns>
        private static GmailLabelWriteRequest BuildLabelRequest(string? name, GmailLabelOptions? options)
        {
            var request = new GmailLabelWriteRequest { Name = name };
            if (options == null)
                return request;

            request.LabelListVisibility = options.LabelListVisibility;
            request.MessageListVisibility = options.MessageListVisibility;

            if (options.BackgroundColor != null || options.TextColor != null)
            {
                request.Color = new GmailLabelColor
                {
                    BackgroundColor = options.BackgroundColor,
                    TextColor = options.TextColor,
                };
            }

            return request;
        }

        /// <summary>
        /// 組出 messages.get / threads.get 共用的查詢字串:format 一律送,metadataHeaders 只在 metadata 格式下送。
        /// Builds the query string shared by messages.get and threads.get: format is always sent, metadataHeaders only under the metadata format.
        /// </summary>
        /// <param name="format">要回傳多少內容。How much content to return.</param>
        /// <param name="metadataHeaders">要限制的標頭名稱,可為 null。The headers to limit the result to; may be null.</param>
        /// <returns>查詢字串緩衝區。The query string buffer.</returns>
        private static StringBuilder BuildFormatParameters(GmailMessageFormat format, IEnumerable<string>? metadataHeaders)
        {
            var parameters = new StringBuilder();
            AppendParameter(parameters, "format", ToWire(format));

            // metadataHeaders 只有 format=metadata 有意義,其他格式不送
            // metadataHeaders only means something with format=metadata, so it is not sent otherwise.
            if (format == GmailMessageFormat.Metadata)
                AppendParameters(parameters, "metadataHeaders", metadataHeaders);

            return parameters;
        }

        /// <summary>
        /// 把序列內容複製成清單,null 或空序列一律回傳 null(代表「不要送這個欄位」)。
        /// Copies a sequence into a list, returning null for a null or empty sequence so the field is left out of the request.
        /// </summary>
        /// <param name="values">來源序列,可為 null。The source sequence; may be null.</param>
        /// <returns>清單或 null。The list, or null.</returns>
        private static List<string>? ToOptionalList(IEnumerable<string>? values)
        {
            if (values == null)
                return null;

            var list = new List<string>(values);
            return list.Count == 0 ? null : list;
        }

        /// <summary>
        /// 檢查必填字串參數。
        /// Validates a required string argument.
        /// </summary>
        /// <param name="value">參數值。The argument value.</param>
        /// <param name="parameterName">參數名稱。The parameter name.</param>
        /// <exception cref="ArgumentException">值為 null 或空白時擲出。Thrown when the value is null or blank.</exception>
        private static void RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("必填參數不可為 null 或空白。The required argument cannot be null or blank.", parameterName);
        }

        /// <summary>
        /// 把郵件格式轉成 Gmail 查詢字串使用的字面值。
        /// Converts a message format into the literal Gmail expects on the wire.
        /// </summary>
        /// <param name="format">郵件格式。The message format.</param>
        /// <returns>查詢字串值。The query string value.</returns>
        private static string ToWire(GmailMessageFormat format)
        {
            switch (format)
            {
                case GmailMessageFormat.Metadata:
                    return "metadata";
                case GmailMessageFormat.Minimal:
                    return "minimal";
                case GmailMessageFormat.Raw:
                    return "raw";
                default:
                    return "full";
            }
        }

        /// <summary>
        /// 把歷程事件類型轉成 Gmail 查詢字串使用的字面值。
        /// Converts a history type into the literal Gmail expects on the wire.
        /// </summary>
        /// <param name="historyType">歷程事件類型。The history type.</param>
        /// <returns>查詢字串值。The query string value.</returns>
        private static string ToWire(GmailHistoryType historyType)
        {
            switch (historyType)
            {
                case GmailHistoryType.MessageDeleted:
                    return "messageDeleted";
                case GmailHistoryType.LabelAdded:
                    return "labelAdded";
                case GmailHistoryType.LabelRemoved:
                    return "labelRemoved";
                default:
                    return "messageAdded";
            }
        }

        /// <summary>
        /// 附加一個查詢參數,值為 null 或空字串時不附加。
        /// Appends one query parameter, skipping null or empty values.
        /// </summary>
        /// <param name="parameters">查詢字串緩衝區。The query string buffer.</param>
        /// <param name="name">參數名稱。The parameter name.</param>
        /// <param name="value">參數值,可為 null。The value; may be null.</param>
        private static void AppendParameter(StringBuilder parameters, string name, string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            parameters.Append(parameters.Length == 0 ? '?' : '&');
            parameters.Append(name);
            parameters.Append('=');
            parameters.Append(Uri.EscapeDataString(value));
        }

        /// <summary>
        /// 附加一個數值查詢參數,值為 null 時不附加。
        /// Appends one numeric query parameter, skipping a null value.
        /// </summary>
        /// <param name="parameters">查詢字串緩衝區。The query string buffer.</param>
        /// <param name="name">參數名稱。The parameter name.</param>
        /// <param name="value">參數值,可為 null。The value; may be null.</param>
        private static void AppendParameter(StringBuilder parameters, string name, int? value)
        {
            if (!value.HasValue)
                return;

            AppendParameter(parameters, name, value.Value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 以重複參數的形式附加多值查詢參數(Gmail 的多值語法),序列為 null 或空時不附加。
        /// Appends a multi-valued query parameter by repeating it, which is Gmail's multi-value syntax; a null or empty sequence appends nothing.
        /// </summary>
        /// <param name="parameters">查詢字串緩衝區。The query string buffer.</param>
        /// <param name="name">參數名稱。The parameter name.</param>
        /// <param name="values">參數值序列,可為 null。The values; may be null.</param>
        private static void AppendParameters(StringBuilder parameters, string name, IEnumerable<string>? values)
        {
            if (values == null)
                return;

            foreach (var value in values)
                AppendParameter(parameters, name, value);
        }

        /// <summary>
        /// 組出 Gmail API 的絕對網址。
        /// Builds an absolute Gmail API URL.
        /// </summary>
        /// <param name="relativePath">users/{userId}/ 之後的路徑。The path after users/{userId}/.</param>
        /// <param name="parameters">查詢字串緩衝區,可為 null。The query string buffer; may be null.</param>
        /// <returns>絕對網址。The absolute URL.</returns>
        private string BuildUrl(string relativePath, StringBuilder? parameters)
        {
            return ApiHost + BuildPath(relativePath, parameters);
        }

        /// <summary>
        /// 組出 Gmail API 的路徑加查詢字串(不含主機),batch 的子請求需要這種相對形式。
        /// Builds the Gmail API path plus query string without the host, which is the form a batch sub-request needs.
        /// </summary>
        /// <param name="relativePath">users/{userId}/ 之後的路徑。The path after users/{userId}/.</param>
        /// <param name="parameters">查詢字串緩衝區,可為 null。The query string buffer; may be null.</param>
        /// <returns>路徑加查詢字串。The path plus query string.</returns>
        private string BuildPath(string relativePath, StringBuilder? parameters)
        {
            var query = parameters == null ? string.Empty : parameters.ToString();
            return ApiPathPrefix + _userId + "/" + relativePath + query;
        }
    }
}
