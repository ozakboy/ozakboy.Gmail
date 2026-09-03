using System.Collections.Generic;

namespace Ozakboy.Gmail
{
    /// <summary>
    /// 批次取信的結果:成功取回的郵件與逐筆的失敗紀錄。
    /// The result of a batch get: the messages that came back plus the per-item failures.
    /// </summary>
    /// <remarks>
    /// 兩個清單永不為 null。<see cref="Messages"/> 的順序是 Gmail 回應中子部件的順序,
    /// 與傳入的識別碼順序未必一致,需要對應時請自行以 <see cref="GmailMessage.Id"/> 建索引。
    /// Neither list is ever null. <see cref="Messages"/> follows the order of the sub-responses Gmail returned,
    /// which need not match the order of the ids passed in; index by <see cref="GmailMessage.Id"/> when the mapping matters.
    /// </remarks>
    public class GmailBatchGetResult
    {
        /// <summary>
        /// 成功取回的郵件,永不為 null。
        /// The messages that were fetched successfully; never null.
        /// </summary>
        public List<GmailMessage> Messages { get; } = new List<GmailMessage>();

        /// <summary>
        /// 失敗的子回應,永不為 null;整批都成功時為空清單。
        /// The failed sub-responses; never null, and empty when every item succeeded.
        /// </summary>
        public List<GmailBatchFailure> Failures { get; } = new List<GmailBatchFailure>();
    }
}
