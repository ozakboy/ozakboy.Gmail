namespace Ozakboy.Gmail
{
    /// <summary>
    /// 本套件會用到的 OAuth 授權範圍。刻意不提供 mail.google.com 這種全權限範圍。
    /// The OAuth scopes this package is designed for. The all-powerful mail.google.com scope is deliberately absent.
    /// </summary>
    public static class GmailScopes
    {
        /// <summary>
        /// 讀取、修改標籤與寄信(透過 messages.send)所需的範圍。
        /// The scope needed to read messages, change labels and send mail through messages.send.
        /// </summary>
        public const string GmailModify = "https://www.googleapis.com/auth/gmail.modify";

        /// <summary>
        /// 要求 Google 回傳 id_token 的範圍。
        /// The scope that makes Google return an id_token.
        /// </summary>
        public const string OpenId = "openid";

        /// <summary>
        /// 取得使用者信箱位址的範圍。
        /// The scope that grants access to the user's email address.
        /// </summary>
        public const string Email = "email";
    }
}
