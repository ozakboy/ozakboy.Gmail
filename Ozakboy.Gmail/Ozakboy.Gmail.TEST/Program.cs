// Ozakboy.Gmail 手動測試 console。
// 只做讀取類操作:看信箱資料、列最近五封信的寄件者與主旨、數一數標籤,不寄信、不改標籤、不動垃圾桶。
// 憑證請填在 appsettings.json,不要寫進程式碼,也不要提交到版本控制。

using System.Text;
using Microsoft.Extensions.Configuration;
using Ozakboy.Gmail;
using Ozakboy.Gmail.OAuth;

Console.OutputEncoding = Encoding.UTF8;

// 佔位符:設定檔還沒填就直接印說明結束,避免拿佔位符去打 Google
const string ClientIdPlaceholder = "你的 Client ID";
const string ClientSecretPlaceholder = "你的 Client Secret";
const string RefreshTokenPlaceholder = "你的 refresh token";

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var oauthOptions = GoogleOAuthOptions.FromConfiguration(configuration);
var refreshToken = configuration["RefreshToken"] ?? string.Empty;

if (string.IsNullOrWhiteSpace(oauthOptions.ClientId)
    || string.IsNullOrWhiteSpace(oauthOptions.ClientSecret)
    || string.IsNullOrWhiteSpace(refreshToken)
    || oauthOptions.ClientId == ClientIdPlaceholder
    || oauthOptions.ClientSecret == ClientSecretPlaceholder
    || refreshToken == RefreshTokenPlaceholder)
{
    Console.WriteLine("appsettings.json 還是佔位符,請先填入自己的憑證:");
    Console.WriteLine("  1. 到 Google Cloud Console 建立 OAuth 用戶端(類型:網頁應用程式),取得 ClientId 與 ClientSecret。");
    Console.WriteLine("  2. 把 ClientId / ClientSecret 填進 appsettings.json 的 GoogleOAuth 區段。");
    Console.WriteLine("  3. 依本檔最下方註解掉的「第一次授權」流程取得 refresh token,填進 RefreshToken。");
    Console.WriteLine("  4. 重新執行本程式。");
    return;
}

using var httpClient = new HttpClient();

// 先用 refresh token 換一把存取權杖
var oauthClient = new GoogleOAuthClient(httpClient, oauthOptions);
GoogleTokenResponse token;

try
{
    token = await oauthClient.RefreshAsync(refreshToken);
}
catch (GmailApiException ex)
{
    Console.WriteLine($"換取存取權杖失敗:{ex.Message}");
    if (ex.IsUnauthorized)
        Console.WriteLine("refresh token 已失效,請重新跑一次授權流程。");

    return;
}

Console.WriteLine($"取得存取權杖,到期時間:{token.ExpiresAt:yyyy-MM-dd HH:mm:ss}(UTC)");
Console.WriteLine($"授權範圍:{string.Join(", ", token.GetScopes())}");
Console.WriteLine();

// 存取權杖直接回傳給 Gmail 用戶端;正式環境請自行做快取與提前更新
var accessToken = token.AccessToken ?? string.Empty;
var gmail = new GmailClient(httpClient, _ => Task.FromResult(accessToken));

try
{
    var profile = await gmail.GetProfileAsync();
    Console.WriteLine($"信箱:{profile.EmailAddress}");
    Console.WriteLine($"目前 historyId:{profile.HistoryId}(增量同步要存下來的就是這個值)");
    Console.WriteLine($"郵件總數:{profile.MessagesTotal}、討論串總數:{profile.ThreadsTotal}");
    Console.WriteLine();

    var list = await gmail.ListMessagesAsync(maxResults: 5);
    Console.WriteLine($"最近 {list.Messages.Count} 封郵件:");

    foreach (var reference in list.Messages)
    {
        if (string.IsNullOrEmpty(reference.Id))
            continue;

        var message = await gmail.GetMessageAsync(
            reference.Id,
            GmailMessageFormat.Metadata,
            new[] { "From", "Subject", "Date" });

        Console.WriteLine($"  - [{message.Id}] {message.GetHeader("Date")}");
        Console.WriteLine($"    寄件者:{message.GetHeader("From")}");
        Console.WriteLine($"    主旨  :{message.GetHeader("Subject")}");
    }

    Console.WriteLine();

    var labels = await gmail.ListLabelsAsync();
    Console.WriteLine($"標籤共 {labels.Count} 個(系統 + 使用者)。");
}
catch (GmailApiException ex)
{
    Console.WriteLine($"呼叫 Gmail API 失敗:{ex.Message}");
    Console.WriteLine($"狀態碼:{ex.StatusCode}、原因:{ex.Reason}");
    Console.WriteLine($"旗標:未授權={ex.IsUnauthorized}、限流={ex.IsRateLimited}、找不到={ex.IsNotFound}");
}

// ── 第一次授權(取得 refresh token)的示範 ───────────────────────────────
// 下面這段平常不用跑;第一次要拿 refresh token 時,把註解拿掉執行一次即可。
// 流程:印出授權網址 → 自己貼到瀏覽器完成同意 → 從導回網址的 code 參數複製授權碼 → 貼回主控台。
//
// var redirectUri = "http://localhost:5000/oauth/callback";   // 必須與 Google Cloud Console 設定的完全一致
// var state = Guid.NewGuid().ToString("N");                   // 回呼時要自行驗證,防 CSRF
//
// var authorizationUrl = oauthClient.BuildAuthorizationUrl(
//     redirectUri,
//     new[] { GmailScopes.GmailModify, GmailScopes.OpenId, GmailScopes.Email },
//     state);
//
// Console.WriteLine("請用瀏覽器打開下面這個網址完成授權:");
// Console.WriteLine(authorizationUrl);
// Console.Write("完成後把網址列上的 code 參數貼進來:");
// var code = Console.ReadLine() ?? string.Empty;
//
// var issued = await oauthClient.ExchangeCodeAsync(code, redirectUri);
// Console.WriteLine($"refresh token:{issued.RefreshToken}");   // 存進 appsettings.json 的 RefreshToken
// Console.WriteLine($"access token 到期:{issued.ExpiresAt:u}");
//
// if (!string.IsNullOrEmpty(issued.IdToken))
// {
//     var payload = GoogleIdTokenPayload.Parse(issued.IdToken);
//     Console.WriteLine($"授權帳號:{payload.Email}(sub={payload.Subject})");
// }
