namespace Ozakboy.Gmail
{
    /// <summary>
    /// Gmail 內建的系統標籤識別碼,避免呼叫端到處手寫字串。
    /// The built-in Gmail system label ids, so callers do not have to hand-write the strings.
    /// </summary>
    public static class GmailSystemLabels
    {
        /// <summary>收件匣。The inbox.</summary>
        public const string Inbox = "INBOX";

        /// <summary>垃圾郵件。Spam.</summary>
        public const string Spam = "SPAM";

        /// <summary>垃圾桶。Trash.</summary>
        public const string Trash = "TRASH";

        /// <summary>未讀。Unread.</summary>
        public const string Unread = "UNREAD";

        /// <summary>已加星號。Starred.</summary>
        public const string Starred = "STARRED";

        /// <summary>重要。Important.</summary>
        public const string Important = "IMPORTANT";

        /// <summary>寄件備份。Sent mail.</summary>
        public const string Sent = "SENT";

        /// <summary>草稿。Drafts.</summary>
        public const string Draft = "DRAFT";

        /// <summary>「個人」分頁。The Primary category.</summary>
        public const string CategoryPersonal = "CATEGORY_PERSONAL";

        /// <summary>「社交」分頁。The Social category.</summary>
        public const string CategorySocial = "CATEGORY_SOCIAL";

        /// <summary>「促銷內容」分頁。The Promotions category.</summary>
        public const string CategoryPromotions = "CATEGORY_PROMOTIONS";

        /// <summary>「最新快訊」分頁。The Updates category.</summary>
        public const string CategoryUpdates = "CATEGORY_UPDATES";

        /// <summary>「論壇」分頁。The Forums category.</summary>
        public const string CategoryForums = "CATEGORY_FORUMS";
    }
}
