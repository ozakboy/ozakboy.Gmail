using System.Collections.Generic;

namespace Ozakboy.Gmail.Core
{
    /// <summary>
    /// MIME 區段樹的走訪工具:把 Gmail 已經切好的區段攤平成深度優先的清單。
    /// Helpers for walking the MIME part tree, flattening the parts Gmail already split into a depth-first list.
    /// </summary>
    /// <remarks>
    /// 內部型別,不屬於公開 API;內文與附件的擷取都靠這裡取得一致的走訪順序。
    /// Internal type; not part of the public API surface. Body and attachment extraction share this traversal order.
    /// </remarks>
    internal static class MessagePartWalker
    {
        /// <summary>
        /// 以深度優先順序攤平區段樹,根區段本身也包含在內。
        /// Flattens the part tree depth-first, including the root part itself.
        /// </summary>
        /// <param name="root">根區段,可為 null。The root part; may be null.</param>
        /// <returns>攤平後的區段清單,永不為 null;<paramref name="root"/> 為 null 時為空清單。The flattened parts; never null, and empty when <paramref name="root"/> is null.</returns>
        internal static List<GmailMessagePart> Flatten(GmailMessagePart? root)
        {
            var result = new List<GmailMessagePart>();
            if (root != null)
                Visit(root, result);

            return result;
        }

        /// <summary>
        /// 走訪一個區段與它的所有子區段。
        /// Visits one part and all of its children.
        /// </summary>
        /// <param name="part">目前的區段。The current part.</param>
        /// <param name="result">收集結果的清單。The list collecting the result.</param>
        private static void Visit(GmailMessagePart part, List<GmailMessagePart> result)
        {
            result.Add(part);

            // Parts 預設是空清單,但反序列化後仍可能被塞成 null,一併防掉
            // Parts defaults to an empty list, yet deserialization can still leave it null; guard both.
            if (part.Parts == null)
                return;

            foreach (var child in part.Parts)
            {
                if (child != null)
                    Visit(child, result);
            }
        }
    }
}
