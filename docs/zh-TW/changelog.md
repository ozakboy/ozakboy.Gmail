---
title: 版本紀錄
description: Ozakboy.Gmail 所有重要變更。
---

# 版本紀錄

**Ozakboy.Gmail** 套件所有重要變更都記錄在此。
版本號遵循[語意化版本](https://semver.org/lang/zh-TW/)。

---

## [2.1.0] - 2026-09-04

> 回填更快、討論串端點、內文 / 附件 helper、現成的 access token 提供者、更細的重試控制。**沒有破壞性變更。**

### 新增功能

- **`BatchGetMessagesAsync(ids, format, metadataHeaders)`**——經 Gmail batch 端點(`multipart/mixed`)一次抓多封,每個 HTTP 請求最多 `GmailClientOptions.BatchSize`(預設 50)封、自動分段。回 `GmailBatchGetResult`:`Messages` 與逐 id 的 `Failures`(`IsNotFound`、`IsRateLimited`),中途有一封被刪不會讓整批失敗。
- **討論串端點**——`GetThreadAsync`、`ModifyThreadAsync`、`TrashThreadAsync`、`UntrashThreadAsync`,搭配 `GmailThread` 模型。
- **`GmailMessage.GetTextBody()` / `GetHtmlBody()`**——走訪 Gmail 已拆好的 MIME 部件,回第一個非附件內文、已解碼。**`GmailMessage.GetAttachments()`**——所有附件部件以 `GmailAttachmentInfo` 呈現(部件 id、檔名、MIME 型別、大小、attachment id、`Content-ID`、原始部件)。
- **`GoogleAccessTokenProvider`**(`Ozakboy.Gmail.OAuth`)——快取 access token、到期前續期(`RefreshSkew`,預設 2 分鐘)、續期序列化(二十個呼叫者只發一次 token 請求),每次續期經 `OnRefreshed` 回報供落庫。把 `provider.GetAccessTokenAsync` 交給 `GmailClient` 即可。
- **`GmailApiException.RetryAfter`**——最後一次失敗回應的 `Retry-After` 值,供 job 層退避。
- **`GmailClientOptions.MaxRetryDelay`**(預設 60 秒)——退避或 `Retry-After` 超過它就不等,立即拋例外並帶 `RetryAfter`。**`GmailClientOptions.RetryOnNetworkErrors`**(預設 `false`)——以同一套退避重試 `HttpRequestException`;用盡後最後一個仍原樣上拋。

> **給自行實作 `IGmailClient` 的人:** 介面多了五個成員(`BatchGetMessagesAsync`、`GetThreadAsync`、`ModifyThreadAsync`、`TrashThreadAsync`、`UntrashThreadAsync`)。只*使用* `GmailClient` / `IGmailClient` 的程式不受影響;手寫的 fake / mock 若*實作*了這個介面要補上新成員。因為套件目前還沒有外部實作者,所以以 Minor 發佈。

### 問題修正

- **.NET Framework 上 204 No Content 拋 `NullReferenceException`。** `HttpResponseMessage.Content` 在 .NET Framework 可能是 `null`(.NET Core 永遠是空內容),`BatchModifyLabelsAsync`、`DeleteLabelAsync`、`RevokeAsync` 在 `net48` 等 netstandard2.0 宿主會炸。由新加的 `net48` 測試目標抓到。

### 技術改進

- 測試專案改為 `net10.0` **與** `net48` 雙目標;每個測試也對 `netstandard2.0` 組建跑一遍。
- GitHub Actions workflow `build-test.yml`:push `main` 與每個 pull request 都跑五個 TFM 的 Release 建置(警告視為錯誤)與兩個測試目標。

---

## [2.0.0] - 2026-09-03

> **不再相依 MimeKit。** 寄出的信由內建 RFC 822 組信器組裝,或以 raw bytes 交入;原始信改以 bytes 回傳。**破壞性變更**:兩個簽章改了——見[升級指南](./migration.md)。其餘與 1.0.0 完全相同。

### 新增功能

- **`GmailOutgoingMessage`**——小型寄件模型(`From`、`To`、`Cc`、`Bcc`、`ReplyTo`、`Subject`、`TextBody`、`HtmlBody`、`Attachments`、`InReplyTo`、`References`、額外 `Headers`),附 `ToRfc822Bytes()`:內建 RFC 822 / MIME 組信器——UTF-8、base64 內文與附件、RFC 2047 標頭編碼、text + HTML 用 `multipart/alternative`、附件用 `multipart/mixed`。
- **`GmailAddress`**(地址 + 選填顯示名,含驗證)與 **`GmailAttachmentContent`**(`FileName`、`ContentType`、`byte[] Content`)作為 `GmailOutgoingMessage` 的零件。
- **`SendRawAsync(byte[] rfc822, threadId)`**——用任何 MIME 函式庫組好的信直接寄;bytes 原封不動上傳。
- **`GmailMessage.DecodeRaw()`**——以 `GmailMessageFormat.Raw` 取回的信,base64url 解碼後的 RFC 822 bytes。

### 破壞性變更

- **`SendAsync(MimeMessage, threadId)` → `SendAsync(GmailOutgoingMessage, threadId)`。** 用 `GmailOutgoingMessage` 組信,或在自己的專案保留 MimeKit、序列化後呼叫 `SendRawAsync`。
- **`GetMessageRawAsync(id)` 改回 `Task<byte[]>`**,不再回 `Task<MimeMessage>`。用任何 MIME 函式庫解析,或改用 `GetMessageAsync(id, GmailMessageFormat.Full)`——Gmail 已經拆好 part。
- **套件不再參考 `MimeKit`。** 靠遞移參考用到 MimeKit 型別的專案要自己加套件。

### 技術改進

- RFC 822 組信器在測試專案以 MimeKit(僅測試相依)解析輸出反向驗證:地址與顯示名、非 ASCII 主旨與檔名 round-trip、`multipart/alternative` + `multipart/mixed` 結構、附件 bytes、標頭折行與行長限制。

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
