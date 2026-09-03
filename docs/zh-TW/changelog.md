---
title: 版本紀錄
description: Ozakboy.Gmail 所有重要變更。
---

# 版本紀錄

**Ozakboy.Gmail** 套件所有重要變更都記錄在此。
版本號遵循[語意化版本](https://semver.org/lang/zh-TW/)。

---

## [1.0.0] - 2026-09-03

> 首次發佈。薄的、只提供非同步 API 的 Gmail REST 客戶端,外加 Google OAuth 2.0 token 端點——不依賴 `Google.Apis`、不存 token、不走 SMTP。寄信經 `messages.send`,所以單一 `gmail.modify` scope 就夠。

### 新增功能

- **`GmailClient` / `IGmailClient`**——`GetProfileAsync`、`ListMessagesAsync`、`GetMessageAsync`、`GetMessageRawAsync`(回 MimeKit `MimeMessage`)、`ListHistoryAsync`、`ModifyLabelsAsync`、`BatchModifyLabelsAsync`、`TrashAsync`、`UntrashAsync`、`ReportSpamAsync`、`ListLabelsAsync`、`CreateLabelAsync`、`UpdateLabelAsync`、`DeleteLabelAsync`、`GetAttachmentAsync`、`DownloadAttachmentAsync`、`SendAsync`。client 吃一個 `HttpClient` 與一個取 access token 的委派(`Func<CancellationToken, Task<string>>`);token 怎麼快取、續期、儲存全由呼叫端決定。
- **`SendAsync(MimeMessage, threadId)`** 以 `message/rfc822` 經 multipart 上傳端點寄出(35 MB 上限),回信可帶 `threadId`。
- **`GoogleOAuthClient` / `IGoogleOAuthClient`**——`BuildAuthorizationUrl`(預設 `access_type=offline` + `prompt=consent`,確保拿到 refresh token)、`ExchangeCodeAsync`、`RefreshAsync`、`RevokeAsync`。`GoogleOAuthOptions.FromConfiguration` 從 `IConfiguration` 區段綁定 `ClientId` / `ClientSecret`。
- **`GoogleIdTokenPayload.Parse`** 從剛由 Google 拿到的 `id_token` 讀出 `sub`、`email` 與其他標準 claim。不驗簽,文件已明寫。
- **`GmailApiException`**:所有非 2xx 回應都是它,帶 `StatusCode`、Google 的 `Reason` 與 `ErrorMessage`、原始 `ResponseBody`、請求方法與路徑,以及 `IsUnauthorized`、`IsHistoryExpired`、`IsRateLimited`、`IsNotFound` 四個旗標,同步迴圈不用自己解析錯誤 JSON 就能決定要重新授權、時間窗口重掃還是退避。
- **指數退避重試**:429、所有 5xx、以及 Gmail 以 403 回的 `rateLimitExceeded` / `userRateLimitExceeded`——預設 1 秒起跳重試 3 次、尊重 `Retry-After`,可用 `GmailClientOptions` 調整。
- **對齊 Gmail REST 欄位名的模型**:`GmailProfile`、`GmailMessage`、`GmailMessagePart`、`GmailMessagePartBody`、`GmailHeader`、`GmailMessageList`、`GmailMessageRef`、`GmailHistoryList`、`GmailHistoryRecord`、`GmailHistoryMessageChange`、`GmailHistoryLabelChange`、`GmailLabel`、`GmailLabelColor`、`GmailLabelOptions`、`GmailAttachment`,加上 `GmailScopes` / `GmailSystemLabels` 常數與 `GmailMessageFormat` / `GmailHistoryType` 列舉。
- **多目標框架**:`netstandard2.0`、`netstandard2.1`、`net8.0`、`net9.0`、`net10.0`。

### 技術改進

- 只做 async;內部所有 `await` 皆 `ConfigureAwait(false)`;`OperationCanceledException` 絕不包裝。
- xUnit 測試專案(`Ozakboy.Gmail.Tests`)以可記錄的 `HttpMessageHandler` 離線驗證公開契約:每個方法的請求形狀、回應映射、重試與錯誤旗標行為、參數驗證、OAuth 請求 body 與 id_token 解析。沒有任何測試連 Google。
- 手動測試 console(`Ozakboy.Gmail.TEST`):在 `appsettings.json` 填入真實憑證後,換 token 並讀取信箱 profile、最新幾封信與標籤。刻意設計成唯讀。
