using System;
using Microsoft.Extensions.Configuration;

namespace Ozakboy.Gmail.OAuth
{
    /// <summary>
    /// Google Cloud OAuth 用戶端憑證。兩個欄位都是必填,缺一在建構 <see cref="GoogleOAuthClient"/> 時就會失敗。
    /// The Google Cloud OAuth client credentials. Both fields are required; a missing one fails when <see cref="GoogleOAuthClient"/> is constructed.
    /// </summary>
    /// <remarks>
    /// 憑證請從環境變數或密鑰保管服務取得,不要寫進原始碼或版本控制。
    /// Read the credentials from environment variables or a secret store; never hard-code them or commit them.
    /// </remarks>
    public class GoogleOAuthOptions
    {
        /// <summary>
        /// 綁定組態時預設使用的區段名稱。
        /// The configuration section name used by default.
        /// </summary>
        private const string DefaultSectionName = "GoogleOAuth";

        /// <summary>
        /// OAuth 用戶端識別碼。
        /// The OAuth client id.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// OAuth 用戶端密鑰。
        /// The OAuth client secret.
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// 從組態指定區段綁出設定。這是本套件唯一會碰到 <see cref="IConfiguration"/> 的地方,
        /// 從環境變數讀密鑰的宿主可以略過它、直接 new 出設定物件。
        /// Binds the options from a configuration section. This is the only place the package touches <see cref="IConfiguration"/>;
        /// hosts that read secrets from environment variables can skip it and construct the options directly.
        /// </summary>
        /// <param name="configuration">組態來源,null 時擲出例外。The configuration source; null throws.</param>
        /// <param name="sectionName">區段名稱,預設為 "GoogleOAuth"。The section name; defaults to "GoogleOAuth".</param>
        /// <returns>綁定結果;區段不存在時回傳空設定(留給建構子擲出明確錯誤)。The bound options; an empty instance when the section is missing, so the constructor can raise a clear error.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="configuration"/> 為 null 時擲出。Thrown when <paramref name="configuration"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="sectionName"/> 為 null 或空白時擲出。Thrown when <paramref name="sectionName"/> is null or blank.</exception>
        public static GoogleOAuthOptions FromConfiguration(IConfiguration configuration, string sectionName = DefaultSectionName)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (string.IsNullOrWhiteSpace(sectionName))
                throw new ArgumentException("區段名稱不可為 null 或空白。The section name cannot be null or blank.", nameof(sectionName));

            return configuration.GetSection(sectionName).Get<GoogleOAuthOptions>() ?? new GoogleOAuthOptions();
        }
    }
}
