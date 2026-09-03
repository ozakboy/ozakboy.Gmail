---
title: 快速開始
description: 安裝 Ozakboy.Gmail 1.0.0,用 Google OAuth 連接 Gmail 帳號,讀出第一批信。
---

# 快速開始

## 安裝

```bash
dotnet add package Ozakboy.Gmail --version 1.0.0
```

支援的目標框架:`netstandard2.0`、`netstandard2.1`、`net8.0`、`net9.0`、`net10.0`。

## 0. 先在 Google 那邊準備

寫任何程式之前,先到 [Google Cloud Console](https://console.cloud.google.com/) 建 OAuth 用戶端:

1. 為專案啟用 **Gmail API**。
2. 設定 **OAuth 同意畫面**。加入 `https://www.googleapis.com/auth/gmail.modify`(想從 `id_token` 拿使用者信箱與 id 的話再加 `openid`、`email`)。`gmail.modify` 是 *restricted* scope——應用程式在 **Testing** 狀態時最多 100 個列名測試者可用,且 refresh token 7 天後失效;要正式發佈得通過 Google 審查。
3. 建立 *Web application* 類型的 **OAuth 用戶端 ID**,把你的 callback 網址登記為授權的重新導向 URI,例如 `https://app.example.com/oauth/google/callback`。
4. client id 與 secret 不要進版控——放環境變數或你的機密存放區。

## 1. 設定 OAuth 用戶端

在 `appsettings.json` 加 `GoogleOAuth` 區段(或對應的 user secrets / 環境變數):

```json
{
  "GoogleOAuth": {
    "ClientId": "your-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-client-secret"
  }
}
```

```csharp
using Ozakboy.Gmail.OAuth;

var oauthOptions = GoogleOAuthOptions.FromConfiguration(configuration);   // 區段名稱預設 "GoogleOAuth"
```

值來自別處的話,直接手動建:

```csharp
var oauthOptions = new GoogleOAuthOptions
{
    ClientId     = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")!,
    ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")!,
};
```

任一值為空時 `GoogleOAuthClient` 建構子拋 `ArgumentException`,設定錯的宿主在啟動時就會失敗。

## 2. 註冊服務(ASP.NET Core)

```csharp
using Ozakboy.Gmail;
using Ozakboy.Gmail.OAuth;

builder.Services.AddSingleton(GoogleOAuthOptions.FromConfiguration(builder.Configuration));
builder.Services.AddHttpClient<IGoogleOAuthClient, GoogleOAuthClient>();
builder.Services.AddHttpClient("gmail");
```

`GmailClient` 需要一個「只屬於某一個信箱」的 access token 提供者,所以通常按帳號建立而不是直接注入:

```csharp
public sealed class GmailClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    public GmailClientFactory(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public IGmailClient Create(Func<CancellationToken, Task<string>> accessTokenProvider)
        => new GmailClient(_httpClientFactory.CreateClient("gmail"), accessTokenProvider);
}
```

Console 程式 `new HttpClient()` 一次重複用即可。

## 3. 連接信箱(authorization code 流程)

**把使用者送去 Google:**

```csharp
var state = /* 隨機值,存進 session、callback 時驗證 */;

var url = oauthClient.BuildAuthorizationUrl(
    redirectUri: "https://app.example.com/oauth/google/callback",
    scopes:      new[] { GmailScopes.GmailModify, GmailScopes.OpenId, GmailScopes.Email },
    state:       state);

return Redirect(url);
```

預設已帶 `access_type=offline` 與 `prompt=consent`,這是 Google 願意發 refresh token 的條件——同一個使用者*第二次*授權也一樣拿得到。

**callback 時用 code 換 token:**

```csharp
GoogleTokenResponse token = await oauthClient.ExchangeCodeAsync(code, "https://app.example.com/oauth/google/callback", ct);

// 剛剛是誰連進來?(不驗簽——只因為這個 id_token 是直接從 Google 拿到的才安全)
var identity = GoogleIdTokenPayload.Parse(token.IdToken!);
string googleUserId = identity.Subject!;
string email        = identity.Email!;

// 把需要的存起來——請加密:
//   token.RefreshToken   (Google 沒發的話為 null;見 設定 → 「沒拿到 refresh token?」)
//   token.AccessToken、token.ExpiresAt
```

## 4. 提供 access token

`GmailClient` 每個請求呼叫一次你的提供者。典型寫法是回傳快取的 access token,在 `ExpiresAt` 前一點續期:

```csharp
Func<CancellationToken, Task<string>> provider = async ct =>
{
    var account = await store.LoadAsync(accountId, ct);
    if (account.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        return account.AccessToken;

    var refreshed = await oauthClient.RefreshAsync(account.RefreshToken, ct);   // 回應的 RefreshToken 為 null——繼續用你手上的
    await store.SaveAccessTokenAsync(accountId, refreshed.AccessToken!, refreshed.ExpiresAt, ct);
    return refreshed.AccessToken!;
};

IGmailClient gmail = new GmailClient(httpClient, provider);
```

`RefreshAsync` 拋出 `IsUnauthorized == true` 的 `GmailApiException` 代表 refresh token 本身已被撤銷或過期——把帳號標成需要重新授權,再走一次第 3 步。

## 5. 讀信箱

```csharp
// 現在在哪?第一次回填前先把 HistoryId 存起來。
GmailProfile profile = await gmail.GetProfileAsync(ct);
Console.WriteLine($"{profile.EmailAddress} — {profile.MessagesTotal} 封,historyId {profile.HistoryId}");

// 最近 30 天,一次 100 封
string? pageToken = null;
do
{
    GmailMessageList page = await gmail.ListMessagesAsync("newer_than:30d", maxResults: 100, pageToken: pageToken, cancellationToken: ct);

    foreach (GmailMessageRef reference in page.Messages)
    {
        GmailMessage message = await gmail.GetMessageAsync(
            reference.Id!,
            GmailMessageFormat.Metadata,
            new[] { "From", "Subject", "List-Unsubscribe", "Authentication-Results" },
            ct);

        Console.WriteLine($"{message.InternalDateTime:u}  {message.GetHeader("From")}  {message.GetHeader("Subject")}");
    }

    pageToken = page.NextPageToken;
} while (pageToken != null);
```

## 6. 增量同步

```csharp
try
{
    GmailHistoryList history = await gmail.ListHistoryAsync(
        storedHistoryId,
        new[] { GmailHistoryType.MessageAdded, GmailHistoryType.MessageDeleted, GmailHistoryType.LabelAdded, GmailHistoryType.LabelRemoved },
        cancellationToken: ct);

    foreach (GmailHistoryRecord record in history.History)
        foreach (GmailHistoryMessageChange added in record.MessagesAdded)
            await HandleNewMessageAsync(added.Message!.Id!, ct);

    storedHistoryId = history.HistoryId!;   // 存起來給下一輪
}
catch (GmailApiException ex) when (ex.IsHistoryExpired)
{
    // Gmail 已經沒有那段歷史——改用時間窗口重掃,再從 GetProfileAsync 拿新的 HistoryId
}
```

## 7. 標籤、垃圾桶、寄信

```csharp
GmailLabel label = await gmail.CreateLabelAsync("Sower/Ads", cancellationToken: ct);

await gmail.BatchModifyLabelsAsync(adIds, addLabelIds: new[] { label.Id! }, removeLabelIds: null, ct);
await gmail.TrashAsync(messageId, ct);          // 30 天內可救;沒有永久刪除

var reply = new MimeMessage();
reply.From.Add(new MailboxAddress("客服", "support@example.com"));
reply.To.Add(new MailboxAddress("客戶", "customer@example.com"));
reply.Subject = "Re: 報價確認";
reply.InReplyTo = originalMessageIdHeader;      // 來自 GetHeader("Message-ID")
reply.References.Add(originalMessageIdHeader);
reply.Body = new TextPart("html") { Text = "<p>附件如附。</p>" };

GmailMessage sent = await gmail.SendAsync(reply, threadId: originalThreadId, ct);
```

Gmail 會把 `From` 換成已授權的信箱(或已驗證的 send-as 別名),你填的地址主要只剩顯示名稱的作用。

## 錯誤處理

```csharp
try
{
    await gmail.GetMessageAsync(id, cancellationToken: ct);
}
catch (GmailApiException ex) when (ex.IsUnauthorized)   { /* 帳號重新授權 */ }
catch (GmailApiException ex) when (ex.IsNotFound)       { /* 列出到抓取之間被刪了——跳過 */ }
catch (GmailApiException ex) when (ex.IsRateLimited)    { /* client 已經重試過;在 job 層級再退避 */ }
catch (GmailApiException ex)                            { logger.LogError(ex, "{Method} {Path} → {Status} {Reason}", ex.RequestMethod, ex.RequestPath, ex.StatusCode, ex.Reason); }
```

所有非 2xx 回應都是 `GmailApiException`;429 與 5xx 會先指數退避重試三次才丟到你手上。旗標、重試規則與 null 行為的完整清單見 [API 文件](./api.md)。

## 下一步

- [設定](./configuration.md)——每個選項,以及「沒拿到 refresh token」「7 天失效」兩個坑
- [API 文件](./api.md)——每個成員、參數與例外
- [版本紀錄](./changelog.md)
