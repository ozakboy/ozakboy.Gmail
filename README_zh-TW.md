<img src="https://raw.githubusercontent.com/ozakboy/ozakboy.Gmail/main/logo.png" width="112" align="right" alt="Ozakboy.Gmail" />

# Ozakboy.Gmail

**Gmail REST 與 Google OAuth 合一的薄客戶端。token 你自己管,HTTP 交給它。**

[English](README.md) | [繁體中文](README_zh-TW.md) · [版本紀錄](docs/zh-TW/changelog.md) · [API 文件](docs/zh-TW/api.md) · [NuGet](https://www.nuget.org/packages/Ozakboy.Gmail/)

一台伺服器每五分鐘讀一次客服信箱,把信貼上標籤、廣告丟垃圾桶、偶爾回一封信。這件事需要 Gmail API,以及一個你自己保管的 refresh token——加密、分租戶、照你的規矩。它不需要整套 `Google.Apis` 認證堆疊來決定 token 該放哪。Ozakboy.Gmail 就是為這件事寫的客戶端:一個 `HttpClient`、一個交出 access token 的委派,加上你真的會用到的那些 Gmail REST 端點。

🔑 &nbsp;·&nbsp;·&nbsp;·&nbsp; ✉

## 讀信箱

```csharp
using Ozakboy.Gmail;

IGmailClient gmail = new GmailClient(httpClient, ct => Task.FromResult(accessToken));

GmailProfile profile = await gmail.GetProfileAsync();
GmailMessageList page = await gmail.ListMessagesAsync("newer_than:30d", maxResults: 100);

foreach (GmailMessageRef reference in page.Messages)
{
    GmailMessage message = await gmail.GetMessageAsync(
        reference.Id!,
        GmailMessageFormat.Metadata,
        new[] { "From", "Subject", "List-Unsubscribe" });

    Console.WriteLine($"{message.GetHeader("From")}  {message.GetHeader("Subject")}");
}
```

貼標籤、丟垃圾桶、回信:

```csharp
await gmail.BatchModifyLabelsAsync(adIds, addLabelIds: new[] { adsLabelId }, removeLabelIds: null);
await gmail.TrashAsync(messageId);                       // 30 天內可救——這裡沒有永久刪除

var reply = new MimeMessage { Subject = "Re: 報價確認" };
reply.To.Add(new MailboxAddress("客戶", "customer@example.com"));
reply.InReplyTo = originalMessageId;
reply.Body = new TextPart("html") { Text = "<p>附件如附。</p>" };

GmailMessage sent = await gmail.SendAsync(reply, threadId: originalThreadId);
```

Google 說不的時候,你決定怎麼辦:

```csharp
try
{
    GmailHistoryList history = await gmail.ListHistoryAsync(storedHistoryId);
    storedHistoryId = history.HistoryId!;
}
catch (GmailApiException ex) when (ex.IsHistoryExpired) { /* 改用時間窗口重掃 */ }
catch (GmailApiException ex) when (ex.IsUnauthorized)   { /* 請使用者重新授權 */ }
```

## 連接信箱

```csharp
using Ozakboy.Gmail.OAuth;

IGoogleOAuthClient oauth = new GoogleOAuthClient(httpClient, GoogleOAuthOptions.FromConfiguration(configuration));

// 1. 把使用者送去這裡
string url = oauth.BuildAuthorizationUrl(redirectUri, new[] { GmailScopes.GmailModify, GmailScopes.OpenId, GmailScopes.Email }, state);

// 2. callback 時
GoogleTokenResponse token = await oauth.ExchangeCodeAsync(code, redirectUri);
var who = GoogleIdTokenPayload.Parse(token.IdToken!);         // who.Subject、who.Email
// 存 token.RefreshToken(加密)、token.AccessToken、token.ExpiresAt

// 3. 之後,在 ExpiresAt 之前
GoogleTokenResponse fresh = await oauth.RefreshAsync(refreshToken);
```

## 這是什麼

刻意做薄的一層,包兩組 Google HTTP API:

- **`GmailClient`**——profile、列出 / 讀取信件(metadata、full,或 raw 解析成 MimeKit `MimeMessage`)、`history.list` 增量同步、修改 / 批次修改標籤、垃圾桶進出、回報垃圾信、標籤 CRUD、附件、`messages.send`
- **`GoogleOAuthClient`**——授權網址、code 換 token、續期、撤銷、讀 `id_token` 的 claim
- **`GmailApiException`**——所有非 2xx 回應,帶 Google 的錯誤 reason 與同步迴圈需要的四個旗標:`IsUnauthorized`、`IsHistoryExpired`、`IsRateLimited`、`IsNotFound`

它**不是**什麼:不存、不加密 token;不做 Pub/Sub 推播;不永久刪除;不講 IMAP / SMTP;不包 Gmail 以外的 Google API;不做分類。這些都在你的應用程式裡。

## 哪裡不一樣

**沒有 `Google.Apis`,對 token 沒有意見。** client 在每個請求前向一個 `Func<CancellationToken, Task<string>>` 要 access token,從頭到尾看不到 refresh token。用你自己的金鑰加密、按租戶隔離、照你的節奏輪替——套件裡沒有一個快取會跟你打架。

**只要 `gmail.modify` 一個 scope。** 寄信走 REST `messages.send`(MimeKit 組 `MimeMessage`,client 以 `message/rfc822` 上傳),不走 SMTP。SMTP 與 IMAP 配 OAuth 一律要 `https://mail.google.com/` 完整權限;本套件從不申請它。

**錯誤可以直接分支。** Gmail 的每使用者配額回 HTTP 403、`startHistoryId` 過期回 404、refresh token 死了回 400 `invalid_grant`。這些你都不用解析:429、5xx 與配額型 403 會先指數退避重試(1 秒起跳三次,尊重 `Retry-After`),還是失敗的以同一種例外型別、對的旗標交到你手上。

**從頭到尾 async。** 沒有同步 API,每個 await 都 `ConfigureAwait(false)`,`OperationCanceledException` 絕不包裝,模型是對齊 Gmail REST 欄位名的 plain class。

## 安裝

```bash
dotnet add package Ozakboy.Gmail
```

接著看[快速開始](docs/zh-TW/getting-started.md)——從 Google Cloud 設定(Gmail API、同意畫面、OAuth 用戶端)、授權流程、會自己續期的 token 提供者,到第一個同步迴圈。

✉ &nbsp;·&nbsp;·&nbsp;·&nbsp; 🔑

## 相容性

| 目標框架 | 支援 |
|---|---|
| .NET 10.0 | ✅ |
| .NET 9.0 | ✅ |
| .NET 8.0 | ✅ |
| .NET Standard 2.1 | ✅ |
| .NET Standard 2.0 | ✅ |

.NET Standard 2.0 涵蓋 .NET Framework 4.6.1+、.NET Core 2.0+ 與 Mono/Xamarin/Unity。

相依:`MimeKit`、`Microsoft.Extensions.Configuration.Abstractions`、`Microsoft.Extensions.Configuration.Binder`,以及僅 .NET Standard 需要的 `System.Text.Json`。

## 文件

| | |
|---|---|
| [快速開始](docs/zh-TW/getting-started.md) | Google Cloud 設定、OAuth 流程、token 提供者、第一次同步 |
| [設定](docs/zh-TW/configuration.md) | `GoogleOAuthOptions`、`GoogleAuthorizationUrlOptions`、`GmailClientOptions`、refresh token 的坑 |
| [API 文件](docs/zh-TW/api.md) | 每個公開成員、參數、例外旗標與 null 規則 |
| [版本紀錄](docs/zh-TW/changelog.md) | 版本歷史 |

## 授權

MIT。

## 支援

- [回報問題](https://github.com/ozakboy/ozakboy.Gmail/issues)
- [發 pull request](https://github.com/ozakboy/ozakboy.Gmail/pulls)
