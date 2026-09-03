namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// messages.send 多段上傳中的第一段:郵件中繼資料。threadId 為 null 時序列化成空物件,代表開新討論串。
    /// The first part of the messages.send multipart upload: the message metadata. A null threadId serialises to an empty object, which starts a new thread.
    /// </summary>
    internal sealed class GmailSendMetadata
    {
        /// <summary>
        /// 要併入的既有討論串識別碼。
        /// The id of the existing thread to join.
        /// </summary>
        public string? ThreadId { get; set; }
    }
}
