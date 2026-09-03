using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// Gmail REST API 的用戶端契約。只有非同步方法,存取權杖由呼叫端提供的委派供應。
    /// The Gmail REST API client contract. Async only; the access token comes from a caller supplied delegate.
    /// </summary>
    /// <remarks>
    /// 這個介面的存在是為了讓消費端能註冊到 DI 容器,並在測試時替換成假物件;正式實作只有 <see cref="GmailClient"/>。
    /// The interface exists so consumers can register the client in DI and substitute a fake in tests; <see cref="GmailClient"/> is the only implementation.
    /// </remarks>
    public interface IGmailClient
    {
        /// <summary>
        /// 取得信箱基本資料,含目前的歷程識別碼。
        /// Gets the mailbox profile, including its current history id.
        /// </summary>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>信箱基本資料。The mailbox profile.</returns>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<GmailProfile> GetProfileAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 列出符合條件的郵件參考(只含 Id 與 ThreadId)。
        /// Lists message references (Id and ThreadId only) matching the given criteria.
        /// </summary>
        /// <param name="query">Gmail 搜尋語法(例如 "newer_than:30d"),null 表示不送這個參數。Gmail search syntax ("newer_than:30d"); null omits the parameter.</param>
        /// <param name="labelIds">要篩選的標籤識別碼,null 或空序列表示不送這個參數。Label ids to filter by; null or an empty sequence omits the parameter.</param>
        /// <param name="maxResults">單頁筆數上限,Gmail 上限為 500;null 表示不送這個參數。The page size, capped by Gmail at 500; null omits the parameter.</param>
        /// <param name="pageToken">續抓用的頁籤,null 表示不送這個參數。The paging token; null omits the parameter.</param>
        /// <param name="includeSpamTrash">是否納入垃圾郵件與垃圾桶,false 時不送這個參數。Whether to include spam and trash; false omits the parameter.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>郵件參考清單與分頁資訊。The page of message references plus paging information.</returns>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<GmailMessageList> ListMessagesAsync(
            string? query = null,
            IEnumerable<string>? labelIds = null,
            int? maxResults = null,
            string? pageToken = null,
            bool includeSpamTrash = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得單封郵件。
        /// Gets a single message.
        /// </summary>
        /// <param name="id">郵件識別碼,null 或空字串時擲出例外。The message id; null or empty throws.</param>
        /// <param name="format">要回傳多少內容,預設為完整內容。How much content to return; defaults to the full message.</param>
        /// <param name="metadataHeaders">只在 <see cref="GmailMessageFormat.Metadata"/> 下送出,用來限制回傳哪些標頭。Only sent with <see cref="GmailMessageFormat.Metadata"/>, limiting which headers come back.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>郵件內容。The message.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="id"/> 為 null 或空白時擲出。Thrown when <paramref name="id"/> is null or blank.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<GmailMessage> GetMessageAsync(
            string id,
            GmailMessageFormat format = GmailMessageFormat.Full,
            IEnumerable<string>? metadataHeaders = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 以 format=raw 取回郵件,回傳 base64url 解碼後的完整 RFC 822 位元組;可交給任何 MIME 函式庫解析。
        /// Fetches the message with format=raw and returns the base64url-decoded RFC 822 bytes, ready for any MIME parser.
        /// </summary>
        /// <param name="id">郵件識別碼,null 或空字串時擲出例外。The message id; null or empty throws.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>RFC 822 郵件位元組;Gmail 沒回傳 raw 時為空陣列。The RFC 822 bytes; an empty array when Gmail returned no raw field.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="id"/> 為 null 或空白時擲出。Thrown when <paramref name="id"/> is null or blank.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<byte[]> GetMessageRawAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 取回指定歷程識別碼之後的信箱變更,用於增量同步。
        /// Lists the mailbox changes after a given history id, for incremental sync.
        /// </summary>
        /// <param name="startHistoryId">起始歷程識別碼,null 或空字串時擲出例外。The starting history id; null or empty throws.</param>
        /// <param name="historyTypes">要取回的事件類型,null 或空序列表示不送這個參數。The event types to fetch; null or an empty sequence omits the parameter.</param>
        /// <param name="pageToken">續抓用的頁籤,null 表示不送這個參數。The paging token; null omits the parameter.</param>
        /// <param name="labelId">只回傳與該標籤相關的變更,null 表示不送這個參數。Restricts the changes to one label; null omits the parameter.</param>
        /// <param name="maxResults">單頁筆數上限,null 表示不送這個參數。The page size; null omits the parameter.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>變更紀錄與分頁資訊。The history records plus paging information.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="startHistoryId"/> 為 null 或空白時擲出。Thrown when <paramref name="startHistoryId"/> is null or blank.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出;歷程已被 Gmail 清掉時 <see cref="GmailApiException.IsHistoryExpired"/> 為 true。Thrown on a non-2xx status; <see cref="GmailApiException.IsHistoryExpired"/> is true when Gmail has discarded that history.</exception>
        Task<GmailHistoryList> ListHistoryAsync(
            string startHistoryId,
            IEnumerable<GmailHistoryType>? historyTypes = null,
            string? pageToken = null,
            string? labelId = null,
            int? maxResults = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 修改單封郵件的標籤。
        /// Changes the labels on a single message.
        /// </summary>
        /// <param name="id">郵件識別碼,null 或空字串時擲出例外。The message id; null or empty throws.</param>
        /// <param name="addLabelIds">要加上的標籤,可為 null 或空序列。The labels to add; may be null or empty.</param>
        /// <param name="removeLabelIds">要移除的標籤,可為 null 或空序列。The labels to remove; may be null or empty.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>更新後的郵件。The updated message.</returns>
        /// <exception cref="System.ArgumentException">識別碼為空白,或兩個標籤清單都是 null / 空序列時擲出。Thrown when the id is blank, or both label lists are null or empty.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<GmailMessage> ModifyLabelsAsync(
            string id,
            IEnumerable<string>? addLabelIds,
            IEnumerable<string>? removeLabelIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 一次修改多封郵件的標籤,Gmail 單次最多 1000 筆。
        /// Changes the labels on many messages at once; Gmail accepts at most 1000 per call.
        /// </summary>
        /// <param name="ids">要套用變更的郵件識別碼;空序列時不送任何請求。The message ids; an empty sequence sends nothing.</param>
        /// <param name="addLabelIds">要加上的標籤,可為 null 或空序列。The labels to add; may be null or empty.</param>
        /// <param name="removeLabelIds">要移除的標籤,可為 null 或空序列。The labels to remove; may be null or empty.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>代表非同步作業的 <see cref="Task"/>,Gmail 成功時回 204 不帶內容。A <see cref="Task"/> representing the operation; Gmail answers 204 with no content.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="ids"/> 為 null 時擲出。Thrown when <paramref name="ids"/> is null.</exception>
        /// <exception cref="System.ArgumentException">識別碼超過 1000 筆,或兩個標籤清單都是 null / 空序列時擲出。Thrown when there are more than 1000 ids, or both label lists are null or empty.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task BatchModifyLabelsAsync(
            IEnumerable<string> ids,
            IEnumerable<string>? addLabelIds,
            IEnumerable<string>? removeLabelIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 把郵件移到垃圾桶(30 天內可還原)。
        /// Moves a message to the trash; it is recoverable for 30 days.
        /// </summary>
        /// <param name="id">郵件識別碼,null 或空字串時擲出例外。The message id; null or empty throws.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>更新後的郵件。The updated message.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="id"/> 為 null 或空白時擲出。Thrown when <paramref name="id"/> is null or blank.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<GmailMessage> TrashAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 把郵件從垃圾桶還原。
        /// Restores a message from the trash.
        /// </summary>
        /// <param name="id">郵件識別碼,null 或空字串時擲出例外。The message id; null or empty throws.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>更新後的郵件。The updated message.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="id"/> 為 null 或空白時擲出。Thrown when <paramref name="id"/> is null or blank.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<GmailMessage> UntrashAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 回報垃圾郵件:加上 SPAM 標籤並移除 INBOX,等同 Gmail 網頁版的「回報垃圾郵件」。
        /// Reports spam by adding SPAM and removing INBOX, which is what the Gmail web UI does.
        /// </summary>
        /// <param name="id">郵件識別碼,null 或空字串時擲出例外。The message id; null or empty throws.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>更新後的郵件。The updated message.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="id"/> 為 null 或空白時擲出。Thrown when <paramref name="id"/> is null or blank.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<GmailMessage> ReportSpamAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 列出信箱中的所有標籤(系統與使用者標籤都包含)。
        /// Lists every label in the mailbox, both system and user labels.
        /// </summary>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>標籤清單,永不為 null。The labels; never null.</returns>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<List<GmailLabel>> ListLabelsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 建立使用者標籤。巢狀標籤用 '/' 分隔,上層標籤必須已存在。
        /// Creates a user label. Nested labels use '/' and the parent has to exist already.
        /// </summary>
        /// <param name="name">標籤名稱,null 或空白時擲出例外。The label name; null or blank throws.</param>
        /// <param name="options">顯示方式與顏色,null 表示全部採用 Gmail 預設。Visibility and colours; null means Gmail's defaults.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>建立完成的標籤。The created label.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="name"/> 為 null 或空白時擲出。Thrown when <paramref name="name"/> is null or blank.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出;同名標籤已存在時狀態碼為 409。Thrown on a non-2xx status; a duplicate name yields status 409.</exception>
        Task<GmailLabel> CreateLabelAsync(string name, GmailLabelOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新標籤的名稱、顯示方式或顏色。未指定的欄位維持原值。
        /// Updates a label's name, visibility or colours. Anything not supplied keeps its current value.
        /// </summary>
        /// <param name="id">標籤識別碼,null 或空白時擲出例外。The label id; null or blank throws.</param>
        /// <param name="name">新名稱,null 表示不改名。The new name; null keeps the current name.</param>
        /// <param name="options">新的顯示方式與顏色,null 表示都不改。The new visibility and colours; null keeps them all.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>更新後的標籤。The updated label.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="id"/> 為 null 或空白時擲出。Thrown when <paramref name="id"/> is null or blank.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<GmailLabel> UpdateLabelAsync(string id, string? name = null, GmailLabelOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 刪除使用者標籤,該標籤會從所有套用過的郵件上移除。系統標籤無法刪除(Gmail 回 400)。
        /// Deletes a user label; it is removed from every message it was applied to. System labels cannot be deleted (Gmail answers 400).
        /// </summary>
        /// <param name="id">標籤識別碼,null 或空白時擲出例外。The label id; null or blank throws.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>代表非同步作業的 <see cref="Task"/>。A <see cref="Task"/> representing the operation.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="id"/> 為 null 或空白時擲出。Thrown when <paramref name="id"/> is null or blank.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task DeleteLabelAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得附件,內容已從 base64url 解碼。
        /// Gets an attachment with its content already decoded from base64url.
        /// </summary>
        /// <param name="messageId">郵件識別碼,null 或空白時擲出例外。The message id; null or blank throws.</param>
        /// <param name="attachmentId">附件識別碼,null 或空白時擲出例外。The attachment id; null or blank throws.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>附件內容。The attachment.</returns>
        /// <exception cref="System.ArgumentException">任一識別碼為 null 或空白時擲出。Thrown when either id is null or blank.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<GmailAttachment> GetAttachmentAsync(string messageId, string attachmentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得附件並直接寫入指定串流,適合把下載轉送到 HTTP 回應而不落地。
        /// Downloads an attachment straight into a stream, which suits proxying it into an HTTP response without touching disk.
        /// </summary>
        /// <param name="messageId">郵件識別碼,null 或空白時擲出例外。The message id; null or blank throws.</param>
        /// <param name="attachmentId">附件識別碼,null 或空白時擲出例外。The attachment id; null or blank throws.</param>
        /// <param name="destination">目標串流,必須可寫;本方法不會關閉或清空它。The destination stream; it has to be writable and is neither closed nor flushed here.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>實際寫入的位元組數。The number of bytes written.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="destination"/> 為 null 時擲出。Thrown when <paramref name="destination"/> is null.</exception>
        /// <exception cref="System.ArgumentException">任一識別碼為 null 或空白,或串流不可寫時擲出。Thrown when either id is null or blank, or the stream is not writable.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<long> DownloadAttachmentAsync(string messageId, string attachmentId, Stream destination, CancellationToken cancellationToken = default);

        /// <summary>
        /// 寄出郵件。郵件由內建組信器序列化成 RFC 822 後以 message/rfc822 多段上傳,因此只需要 gmail.modify 範圍,不必開 SMTP。
        /// Sends a message. The built-in writer serialises it to RFC 822 and uploads it as message/rfc822, so the gmail.modify scope is enough and no SMTP connection is needed.
        /// </summary>
        /// <param name="message">要寄出的郵件,null 時擲出例外。The message to send; null throws.</param>
        /// <param name="threadId">要併入的討論串識別碼,null 表示開新討論串。The thread to join; null starts a new thread.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>已寄出的郵件(含 Id、ThreadId 與 LabelIds)。The sent message, carrying Id, ThreadId and LabelIds.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> 為 null 時擲出。Thrown when <paramref name="message"/> is null.</exception>
        /// <exception cref="System.InvalidOperationException">郵件沒有收件人或標頭不合法時擲出(見 <see cref="GmailOutgoingMessage.ToRfc822Bytes"/>)。Thrown when the message has no recipients or an invalid header (see <see cref="GmailOutgoingMessage.ToRfc822Bytes"/>).</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<GmailMessage> SendAsync(GmailOutgoingMessage message, string? threadId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 寄出呼叫端自行組好的 RFC 822 郵件位元組(MimeKit、MailKit、System.Net.Mail 或任何工具產生皆可),原封不動以 message/rfc822 多段上傳。
        /// Sends RFC 822 bytes composed by the caller (MimeKit, MailKit, System.Net.Mail or anything else), uploaded verbatim as message/rfc822.
        /// </summary>
        /// <param name="rfc822">完整的 RFC 822 郵件;null 或空陣列時擲出例外。The complete RFC 822 message; null or empty throws.</param>
        /// <param name="threadId">要併入的討論串識別碼,null 表示開新討論串。The thread to join; null starts a new thread.</param>
        /// <param name="cancellationToken">取消權杖。Cancellation token.</param>
        /// <returns>已寄出的郵件(含 Id、ThreadId 與 LabelIds)。The sent message, carrying Id, ThreadId and LabelIds.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="rfc822"/> 為 null 時擲出。Thrown when <paramref name="rfc822"/> is null.</exception>
        /// <exception cref="System.ArgumentException"><paramref name="rfc822"/> 為空陣列時擲出。Thrown when <paramref name="rfc822"/> is empty.</exception>
        /// <exception cref="GmailApiException">Gmail 回傳非 2xx 時擲出。Thrown when Gmail answers with a non-2xx status.</exception>
        Task<GmailMessage> SendRawAsync(byte[] rfc822, string? threadId = null, CancellationToken cancellationToken = default);
    }
}
