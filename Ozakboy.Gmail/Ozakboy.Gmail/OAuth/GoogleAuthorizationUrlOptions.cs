namespace Ozakboy.Gmail.OAuth
{
    /// <summary>
    /// 組授權網址時的可選參數。預設值是為「伺服器端保存 refresh token」的情境調校的。
    /// Optional parameters for the authorization URL. The defaults are tuned for a server-side app that stores a refresh token.
    /// </summary>
    /// <remarks>
    /// 沒有 access_type=offline,Google 不會給 refresh token;沒有 prompt=consent,已經授權過的使用者第二次授權也拿不到 refresh token。
    /// Without access_type=offline Google returns no refresh token, and without prompt=consent a user who already granted the app gets no refresh token on a second authorization.
    /// </remarks>
    public class GoogleAuthorizationUrlOptions
    {
        /// <summary>
        /// 存取類型,"offline" 才會拿到 refresh token;null 或空字串時不送這個參數。
        /// The access type; "offline" is what yields a refresh token. Null or empty omits the parameter.
        /// </summary>
        public string AccessType { get; set; } = "offline";

        /// <summary>
        /// 同意畫面提示方式,"consent" 會強制重新徵詢同意以取得 refresh token;null 時不送這個參數。
        /// The consent prompt; "consent" forces a refresh token on re-authorization. Null omits the parameter.
        /// </summary>
        public string? Prompt { get; set; } = "consent";

        /// <summary>
        /// 是否沿用先前已授權的範圍(漸進式授權),false 時不送這個參數。
        /// Whether to carry over previously granted scopes (incremental authorization); false omits the parameter.
        /// </summary>
        public bool IncludeGrantedScopes { get; set; } = true;

        /// <summary>
        /// 預先帶入的帳號提示,null 時不送這個參數。
        /// A hint that pre-fills the account chooser; null omits the parameter.
        /// </summary>
        public string? LoginHint { get; set; }
    }
}
