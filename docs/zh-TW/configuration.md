---
title: 設定
description: Ozakboy.Gmail 2.0.0 的每個選項——GoogleOAuthOptions、GoogleAuthorizationUrlOptions、GmailClientOptions——以及 Google 端的設定與常見的坑。
---

# 設定

Ozakboy.Gmail 有三個選項類別。只有 `GoogleOAuthOptions` 放機密;另外兩個是行為旋鈕,預設值就能用。

## `GoogleOAuthOptions`

| 屬性 | 型別 | 預設 | 說明 |
|---|---|---|---|
| `ClientId` | `string` | `""` | Google Cloud Console 的 OAuth 用戶端 id(`….apps.googleusercontent.com`)。必填。 |
| `ClientSecret` | `string` | `""` | 對應的用戶端密鑰。必填。當密碼看待。 |

### 從 `appsettings.json`

```json
{
  "GoogleOAuth": {
    "ClientId": "your-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-client-secret"
  }
}
```

```csharp
var options = GoogleOAuthOptions.FromConfiguration(configuration);                 // 讀 "GoogleOAuth"
var options = GoogleOAuthOptions.FromConfiguration(configuration, "Google:OAuth");  // 任意區段路徑
```

`FromConfiguration` 只是 `configuration.GetSection(name).Get<GoogleOAuthOptions>()` 的薄包裝。區段不存在會得到空字串,接著 `new GoogleOAuthClient(httpClient, options)` 拋 `ArgumentException`——刻意的,讓錯誤在啟動時就浮現。

### 從環境變數

.NET 的設定提供者會把 `GoogleOAuth__ClientId` / `GoogleOAuth__ClientSecret`(雙底線)對應到同一個區段,所以 `FromConfiguration` 照樣能用。或完全跳過 `IConfiguration`:

```csharp
var options = new GoogleOAuthOptions
{
    ClientId     = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")!,
    ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")!,
};
```

密鑰不進版控、不寫 log,外洩就到 Google Cloud Console 輪替。

## `GoogleAuthorizationUrlOptions`

傳給 `BuildAuthorizationUrl`;`null` 用預設值。

| 屬性 | 型別 | 預設 | 說明 |
|---|---|---|---|
| `AccessType` | `string` | `"offline"` | `offline` 向 Google 要 refresh token。`online` 只給 access token——對無人值守同步的伺服器沒用。 |
| `Prompt` | `string?` | `"consent"` | `consent` 每次都顯示同意畫面、每次都回 refresh token。`null` 不送此參數;`select_account` / `none` 也是 Google 合法值。 |
| `IncludeGrantedScopes` | `bool` | `true` | 送 `include_granted_scopes=true`(`false` 就不送此參數,等同 Google 預設),之後申請更多 scope 時保留已授予的(漸進式授權)。 |
| `LoginHint` | `string?` | `null` | 在 Google 帳號選擇器預選的信箱。使用者重新授權你已知的信箱時很好用。 |

### 沒拿到 refresh token?

OAuth 最常見的意外:`ExchangeCodeAsync` 回來 `RefreshToken == null`。Google 只在 **同時**滿足 `access_type=offline` **且**使用者第一次授權時才發 refresh token——除非 `prompt=consent` 強制。預設值兩者都設了,所以用預設一定拿得到。若你改了 `Prompt` 而弄丟 refresh token,得請使用者到 [myaccount.google.com/permissions](https://myaccount.google.com/permissions) 撤銷應用程式,或你呼叫 `RevokeAsync`,再用 `Prompt = "consent"` 讓他重走一次流程。

### Testing 狀態下的 7 天失效

Google Cloud 專案的 OAuth 同意畫面在 **Testing** 狀態且使用 `gmail.modify` 這類 restricted scope 時,每個 refresh token 發出後 **7 天**失效。之後 `RefreshAsync` 拋 `IsUnauthorized == true` 的 `GmailApiException`(`invalid_grant`)。這是 Google 政策不是 bug——把帳號標成需要重新授權即可。把應用程式改成 **In production**(restricted scope 需通過審查)就沒有這個限制。

## `GmailClientOptions`

傳給 `GmailClient`,也可傳給 `GoogleOAuthClient`(只用到重試設定);`null` 用預設值。建構時會**複製一份**——之後改 options 物件對既有 client 沒影響。

| 屬性 | 型別 | 預設 | 說明 |
|---|---|---|---|
| `MaxRetries` | `int` | `3` | 可重試失敗(429、任何 5xx、403 `rateLimitExceeded` / `userRateLimitExceeded`)後的重試次數。`0` 關閉重試。負數拋 `ArgumentOutOfRangeException`。 |
| `RetryBaseDelay` | `TimeSpan` | 1 秒 | 第一次重試的等待;之後每次加倍(1s、2s、4s)。回應有 `Retry-After` 標頭時該次改用它。測試設 `TimeSpan.Zero`。 |
| `UserId` | `string` | `"me"` | 所有 Gmail 網址的 `{userId}` 路徑段。`me` 代表已授權的使用者。只有 domain-wide delegation 才需要改。 |

```csharp
var gmail = new GmailClient(httpClient, provider, new GmailClientOptions
{
    MaxRetries     = 5,
    RetryBaseDelay = TimeSpan.FromSeconds(2),
});
```

### 不開放設定的東西

- **端點。** Gmail 與 OAuth 網址固定。測試改在 `HttpClient` 注入假的 `HttpMessageHandler`。
- **逾時。** 在你傳進來的 `HttpClient` 設 `Timeout`;本套件不覆寫。注意一個請求重試三次最壞可到 `4 × Timeout + 7 秒`。
- **JSON 序列化。** 內部實作。

## 你傳進來的 `HttpClient`

- 本套件不會 Dispose 它、也忽略 `BaseAddress`。你設的 `DefaultRequestHeaders` 會送出;`Authorization` 標頭每個請求另設、會蓋掉你的。
- 一個 `HttpClient` 可以給多個不同信箱的 `GmailClient` 共用——token 是每個請求給的,不是綁在 client 上。
- 用 `IHttpClientFactory` 時,具名 client(`AddHttpClient("gmail")`)最省事,因為 `GmailClient` 還需要每個信箱各自的 token 提供者,DI 自己給不出來。

## Google Cloud 檢查清單

| 設定 | 值 |
|---|---|
| API | 啟用 Gmail API |
| OAuth 用戶端類型 | Web application |
| 授權的重新導向 URI | 與你傳給 `BuildAuthorizationUrl` / `ExchangeCodeAsync` 的 `redirectUri` 完全一致 |
| 同意畫面的 scope | `https://www.googleapis.com/auth/gmail.modify`(要 `id_token` 再加 `openid`、`email`) |
| 發佈狀態 | Testing:≤ 100 個測試者、refresh token 7 天。Production:restricted scope 需通過審查(CASA 評估) |

## 延伸閱讀

- [快速開始](./getting-started.md)
- [API 文件](./api.md)
