---
title: API 文件
description: Ozakboy.Gmail 2.0.0 完整公開 API——GmailClient、GoogleOAuthClient、模型、選項與 GmailApiException 錯誤契約。
---

# API 文件

> 以原始碼為準:[`Ozakboy.Gmail/Ozakboy.Gmail/`](https://github.com/ozakboy/ozakboy.Gmail/tree/main/Ozakboy.Gmail/Ozakboy.Gmail)。產生的 XML 文件檔隨 NuGet 套件一起發佈。

Ozakboy.Gmail 是一個薄的、只提供非同步 API 的 [Gmail REST API](https://developers.google.com/workspace/gmail/api/reference/rest) 客戶端,外加 Google OAuth 2.0 token 端點。**不依賴** `Google.Apis.*`、不儲存 token、不開 SMTP 或 IMAP 連線,(2.0.0 起)也不帶任何 MIME 函式庫——寄出的信由內建 RFC 822 組信器組裝或以 raw bytes 交入。寄信走 `messages.send`,所以單一 `gmail.modify` scope 就夠。

標示 **(草案外新增)** 的項目不在原本 1.0.0 的套件規格裡,是因為第一個使用者(Sower)會用到而補上。標示 **(2.0.0)** 的成員取代了 1.0.0 用 MimeKit 型別的成員——見[升級指南](./migration.md)。

## 命名空間

| 命名空間 | 內容 |
|---|---|
| `Ozakboy.Gmail` | `IGmailClient`、`GmailClient`、`GmailClientOptions`、`GmailApiException`、`GmailScopes`、`GmailSystemLabels`、`GmailOutgoingMessage`、`GmailAddress`、`GmailAttachmentContent` 與所有 `Gmail*` 模型 |
| `Ozakboy.Gmail.OAuth` | `IGoogleOAuthClient`、`GoogleOAuthClient`、`GoogleOAuthOptions`、`GoogleAuthorizationUrlOptions`、`GoogleTokenResponse`、`GoogleIdTokenPayload` |
| `Ozakboy.Gmail.Core` | 內部 HTTP / JSON / base64url 管線。**不屬於公開 API**,請勿依賴。 |

## 全套件通用慣例

- **只做 async。** 所有網路成員回 `Task` / `Task<T>`、以 `Async` 結尾、最後一個參數是 `CancellationToken cancellationToken = default`。沒有同步版。
- 內部每個 `await` 都 **`ConfigureAwait(false)`**;函式庫絕不捕捉 synchronization context。
- **取消例外不包裝。** `OperationCanceledException` 原樣往上拋。
- **非 2xx 一律 `GmailApiException`**——見[例外](#例外)。網路層失敗(`HttpRequestException`)原樣上拋、不重試。
- **userId 永遠是 `me`**(可用 `GmailClientOptions.UserId` 覆寫)。
- **模型都是 plain class**,public get/set 屬性,名稱對齊 Gmail REST 欄位(PascalCase)。JSON 沒有的欄位維持 `null`;List 型別屬性**永不為 null**(預設空清單)。
- **Id 都是字串。** `historyId`、`internalDate` 這類 Gmail 以字串序列化的 int64/uint64 欄位,依下文標示分別以 `string`(`HistoryId`)或 `long`(`InternalDate`、`Size*`)呈現。
- 選擇性參數傳 `null` 代表「不送這個參數」。

---

## 1. `IGmailClient` / `GmailClient`

```csharp
namespace Ozakboy.Gmail;

public interface IGmailClient
{
    Task<GmailProfile> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<GmailMessageList> ListMessagesAsync(
        string? query = null,
        IEnumerable<string>? labelIds = null,
        int? maxResults = null,
        string? pageToken = null,
        bool includeSpamTrash = false,
        CancellationToken cancellationToken = default);

    Task<GmailMessage> GetMessageAsync(
        string id,
        GmailMessageFormat format = GmailMessageFormat.Full,
        IEnumerable<string>? metadataHeaders = null,
        CancellationToken cancellationToken = default);

    Task<byte[]> GetMessageRawAsync(string id, CancellationToken cancellationToken = default);                                                         // (2.0.0:改回 byte[])

    Task<GmailHistoryList> ListHistoryAsync(
        string startHistoryId,
        IEnumerable<GmailHistoryType>? historyTypes = null,
        string? pageToken = null,
        string? labelId = null,
        int? maxResults = null,
        CancellationToken cancellationToken = default);

    Task<GmailMessage> ModifyLabelsAsync(
        string id,
        IEnumerable<string>? addLabelIds,
        IEnumerable<string>? removeLabelIds,
        CancellationToken cancellationToken = default);

    Task BatchModifyLabelsAsync(
        IEnumerable<string> ids,
        IEnumerable<string>? addLabelIds,
        IEnumerable<string>? removeLabelIds,
        CancellationToken cancellationToken = default);

    Task<GmailMessage> TrashAsync(string id, CancellationToken cancellationToken = default);
    Task<GmailMessage> UntrashAsync(string id, CancellationToken cancellationToken = default);
    Task<GmailMessage> ReportSpamAsync(string id, CancellationToken cancellationToken = default);

    Task<List<GmailLabel>> ListLabelsAsync(CancellationToken cancellationToken = default);
    Task<GmailLabel> CreateLabelAsync(string name, GmailLabelOptions? options = null, CancellationToken cancellationToken = default);
    Task<GmailLabel> UpdateLabelAsync(string id, string? name = null, GmailLabelOptions? options = null, CancellationToken cancellationToken = default);   // (草案外新增)
    Task DeleteLabelAsync(string id, CancellationToken cancellationToken = default);                                                                    // (草案外新增)

    Task<GmailAttachment> GetAttachmentAsync(string messageId, string attachmentId, CancellationToken cancellationToken = default);
    Task<long> DownloadAttachmentAsync(string messageId, string attachmentId, Stream destination, CancellationToken cancellationToken = default);

    Task<GmailMessage> SendAsync(GmailOutgoingMessage message, string? threadId = null, CancellationToken cancellationToken = default);   // (2.0.0:改吃 GmailOutgoingMessage)
    Task<GmailMessage> SendRawAsync(byte[] rfc822, string? threadId = null, CancellationToken cancellationToken = default);            // (2.0.0)
}

public class GmailClient : IGmailClient
{
    public GmailClient(HttpClient httpClient, Func<CancellationToken, Task<string>> accessTokenProvider);
    public GmailClient(HttpClient httpClient, Func<CancellationToken, Task<string>> accessTokenProvider, GmailClientOptions? options);
}
```

`IGmailClient` **(草案外新增)** 的用途是讓呼叫端註冊進 DI、測試時換成 mock。`GmailClient` 是唯一實作。

### 1.1 建構子

| 參數 | 說明 |
|---|---|
| `httpClient` | 所有呼叫共用的 `HttpClient`。本套件不會 Dispose 它。`BaseAddress` 會被忽略——一律使用 Gmail 絕對網址,所以 `new HttpClient()` 或 `IHttpClientFactory` 的 typed client 都可以。 |
| `accessTokenProvider` | **每個 HTTP 請求呼叫一次**(重試不會再呼叫)取得 bearer access token。快取、續期、儲存全是呼叫端的事。傳 `null` 拋 `ArgumentNullException`;provider 回 `null` 或空字串則在呼叫時拋 `InvalidOperationException`。 |
| `options` | 見 [`GmailClientOptions`](#12-gmailclientoptions)。`null` 視同預設值。 |

實例不保存任何呼叫間狀態,可安全註冊為 singleton。

### 1.2 `GmailClientOptions`

```csharp
public class GmailClientOptions
{
    public int      MaxRetries     { get; set; } = 3;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    public string   UserId         { get; set; } = "me";
}
```

| 成員 | 說明 |
|---|---|
| `MaxRetries` | 可重試失敗(HTTP 429、任何 5xx、或 reason 為 `rateLimitExceeded` / `userRateLimitExceeded` 的 403)後**重送**的次數。`3` 代表最多四次嘗試。`0` 關閉重試。負數在建構 client 時拋 `ArgumentOutOfRangeException`。 |
| `RetryBaseDelay` | 第一次重試前的等待;之後每次加倍(預設 1s → 2s → 4s)。回應帶 `Retry-After` 標頭時,該次改用標頭的值。設 `TimeSpan.Zero` 測試就不用等。 |
| `UserId` | 路徑裡的 `{userId}`。除非是 domain-wide delegation 的服務帳號,否則維持 `me`。 |

重試套用於所有 Gmail 呼叫**以及** OAuth token / revoke 呼叫。401、404、400 等不可重試狀態絕不重送。

### 1.3 方法

| 方法 | REST 呼叫 | 回傳 / 備註 |
|---|---|---|
| `GetProfileAsync` | `GET users/{userId}/profile` | `GmailProfile`——信箱地址與**目前的 `HistoryId`**,首次回填前先存起來,之後增量同步從它開始。 |
| `ListMessagesAsync` | `GET users/{userId}/messages` | `GmailMessageList`,含 `Messages`(只有 id + threadId)、`NextPageToken`、`ResultSizeEstimate`。`query` 是 Gmail 搜尋語法(`newer_than:30d`);`maxResults` 上限由 Gmail 定為 500。把 `NextPageToken` 傳回 `pageToken` 續抓。 |
| `GetMessageAsync` | `GET users/{userId}/messages/{id}` | `GmailMessage`。`format` 決定回多少(見 [`GmailMessageFormat`](#25-列舉))。`metadataHeaders` 只在 `Metadata` 時送出,限制回哪些標頭。 |
| `GetMessageRawAsync` **(2.0.0)** | `GET …/messages/{id}?format=raw` | 完整的 RFC 822 原文,**已解碼的 bytes**(Gmail 沒回 `raw` 時為空陣列)。用任何 MIME 函式庫解析,或乾脆不解析:`GetMessageAsync(id, GmailMessageFormat.Full)` 回的就是 Gmail 已拆好 part 的同一封信。 |
| `ListHistoryAsync` | `GET users/{userId}/history` | `GmailHistoryList`。`startHistoryId` 必須是先前取得的 history id。Gmail 已丟棄該段歷史時(HTTP 404),例外的 `IsHistoryExpired == true`——退回時間窗口重掃。 |
| `ModifyLabelsAsync` | `POST …/messages/{id}/modify` | 更新後的 `GmailMessage`。兩個清單可為 `null` 或空,但至少要有一個標籤,否則拋 `ArgumentException`。 |
| `BatchModifyLabelsAsync` | `POST …/messages/batchModify` | 無回傳值(Gmail 回 204)。Gmail 一次最多 **1000 個 id**,超過在送出前就拋 `ArgumentException`。`ids` 為空是 no-op、不送請求。 |
| `TrashAsync` / `UntrashAsync` | `POST …/messages/{id}/trash` / `untrash` | 更新後的 `GmailMessage`。垃圾桶 30 天內可救;本套件**刻意不提供永久刪除**。 |
| `ReportSpamAsync` | `POST …/messages/{id}/modify` | 等同 `ModifyLabelsAsync(id, add: ["SPAM"], remove: ["INBOX"])` 的便利方法。Gmail 沒有專門的「回報垃圾信」端點;移進 `SPAM` 就是網頁版做的事。 |
| `ListLabelsAsync` | `GET users/{userId}/labels` | 所有標籤(系統 + 使用者)。list 端點不含信件數量;需要數量請自行呼叫 labels.get(1.0.0 不提供)。 |
| `CreateLabelAsync` | `POST users/{userId}/labels` | 建立後的 `GmailLabel`。巢狀標籤用 `/`(`Sower/Ads`);父標籤必須存在否則 Gmail 回 400。重複名稱回 409 → `GmailApiException`、`StatusCode == 409`。 |
| `UpdateLabelAsync` **(草案外新增)** | `PATCH users/{userId}/labels/{id}` | 改名及/或改可見性、顏色。`name == null` 維持原名;`options == null` 維持原可見性與顏色。 |
| `DeleteLabelAsync` **(草案外新增)** | `DELETE users/{userId}/labels/{id}` | 刪除**使用者**標籤;所有貼過該標籤的信會被移除標籤。系統標籤不可刪(Gmail 回 400)。 |
| `GetAttachmentAsync` | `GET …/messages/{messageId}/attachments/{attachmentId}` | `GmailAttachment`,`Data` 是**已解碼**的位元組、`Size` 是 Gmail 回報的大小。Gmail 附件是 JSON 包 base64url,整包必須在記憶體緩衝一次——線路上沒有真正的串流。 |
| `DownloadAttachmentAsync` | 同上 | 解碼後寫進 `destination`,回寫入的位元組數。用途是把下載代理進 HTTP 回應、不落磁碟。`destination` 必須可寫;本套件**不會**幫你關閉或 flush。 |
| `SendAsync` **(2.0.0)** | `POST upload/gmail/v1/users/{userId}/messages/send?uploadType=multipart` | 寄出後的 `GmailMessage`(`Id`、`ThreadId`、`LabelIds`)。[`GmailOutgoingMessage`](#27-寄件模型gmailoutgoingmessagegmailaddressgmailattachmentcontent) 由內建 RFC 822 組信器序列化(`ToRfc822Bytes()`),以 `message/rfc822` 放進 multipart 上傳,適用 35 MB 上傳上限而非 JSON `raw` 的限制。`threadId` 讓信加入既有討論串——回信時請一併設 `InReplyTo` 與 `References`,否則 Gmail 不會串成同一串。 |
| `SendRawAsync` **(2.0.0)** | 同上 | 同樣的上傳,但 RFC 822 bytes 由你自己提供——MimeKit、MailKit、`System.Net.Mail` 或任何工具都行。`rfc822` 為 `null` → `ArgumentNullException`,空陣列 → `ArgumentException`。 |

**`SendAsync` 與寄件者地址。** Gmail 會把 `From` 改寫成已授權的信箱(或其已驗證的 send-as 別名)。未驗證的 `From` 不會失敗——會被靜默取代。

---

## 2. 模型(`Ozakboy.Gmail`)

所有模型都是 plain class,public get/set 屬性。只建模上述方法實際會回的欄位;Gmail 多送的一律忽略。

### 2.1 Profile、信件參照、清單

```csharp
public class GmailProfile
{
    public string? EmailAddress  { get; set; }
    public long    MessagesTotal { get; set; }
    public long    ThreadsTotal  { get; set; }
    public string? HistoryId     { get; set; }
}

public class GmailMessageRef
{
    public string? Id       { get; set; }
    public string? ThreadId { get; set; }
}

public class GmailMessageList
{
    public List<GmailMessageRef> Messages           { get; set; }   // 永不為 null
    public string?               NextPageToken      { get; set; }
    public long?                 ResultSizeEstimate { get; set; }
}
```

### 2.2 `GmailMessage` 與 MIME part

```csharp
public class GmailMessage
{
    public string?           Id           { get; set; }
    public string?           ThreadId     { get; set; }
    public List<string>      LabelIds     { get; set; }   // 永不為 null
    public string?           Snippet      { get; set; }
    public string?           HistoryId    { get; set; }
    public long              InternalDate { get; set; }   // epoch 毫秒,缺席時為 0
    public long              SizeEstimate { get; set; }
    public GmailMessagePart? Payload      { get; set; }   // format=Minimal 時為 null
    public string?           Raw          { get; set; }   // base64url,只在 format=Raw 時有值

    public DateTimeOffset? InternalDateTime { get; }       // InternalDate 轉換;0 時為 null
    public string? GetHeader(string name);                  // 在 Payload.Headers 不分大小寫查找;沒有回 null
    public byte[]? DecodeRaw();                              // (2.0.0) Raw 的 base64url 解碼;Raw 為 null 時回 null
}

public class GmailMessagePart
{
    public string?                PartId   { get; set; }
    public string?                MimeType { get; set; }
    public string?                Filename { get; set; }
    public List<GmailHeader>      Headers  { get; set; }   // 永不為 null
    public GmailMessagePartBody?  Body     { get; set; }
    public List<GmailMessagePart> Parts    { get; set; }   // 永不為 null

    public string? GetHeader(string name);                  // 不分大小寫;取第一個;沒有回 null
}

public class GmailHeader
{
    public string? Name  { get; set; }
    public string? Value { get; set; }
}

public class GmailMessagePartBody
{
    public string? AttachmentId { get; set; }   // 內容不是 inline 時才有
    public long    Size         { get; set; }
    public string? Data         { get; set; }   // base64url,只有 inline 內容才有

    public byte[]? DecodeData();                 // Data 為 null 時回 null
}
```

`GetHeader` 是你最常用到的輔助:`message.GetHeader("List-Unsubscribe")`、`message.GetHeader("Authentication-Results")`。它只搜最上層 payload 的標頭——正好就是 `format=Metadata` 回的東西。

### 2.3 History

```csharp
public class GmailHistoryList
{
    public List<GmailHistoryRecord> History       { get; set; }   // 永不為 null;沒有變動時為空
    public string?                  NextPageToken { get; set; }
    public string?                  HistoryId     { get; set; }   // 信箱目前的 history id——每次成功跑完就存起來
}

public class GmailHistoryRecord
{
    public string?                         Id              { get; set; }
    public List<GmailMessageRef>           Messages        { get; set; }   // 永不為 null
    public List<GmailHistoryMessageChange> MessagesAdded   { get; set; }   // 永不為 null
    public List<GmailHistoryMessageChange> MessagesDeleted { get; set; }   // 永不為 null
    public List<GmailHistoryLabelChange>   LabelsAdded     { get; set; }   // 永不為 null
    public List<GmailHistoryLabelChange>   LabelsRemoved   { get; set; }   // 永不為 null
}

public class GmailHistoryMessageChange
{
    public GmailMessage? Message { get; set; }   // Gmail 只填 Id、ThreadId、LabelIds
}

public class GmailHistoryLabelChange
{
    public GmailMessage? Message  { get; set; }
    public List<string>  LabelIds { get; set; }  // 永不為 null
}
```

### 2.4 標籤與附件

```csharp
public class GmailLabel
{
    public string?          Id                    { get; set; }
    public string?          Name                  { get; set; }
    public string?          Type                  { get; set; }   // "system" | "user"
    public string?          MessageListVisibility { get; set; }   // "show" | "hide"
    public string?          LabelListVisibility   { get; set; }   // "labelShow" | "labelShowIfUnread" | "labelHide"
    public long?            MessagesTotal         { get; set; }   // 只在 labels.get / labels.create 回應才有
    public long?            MessagesUnread        { get; set; }
    public long?            ThreadsTotal          { get; set; }
    public long?            ThreadsUnread         { get; set; }
    public GmailLabelColor? Color                 { get; set; }
}

public class GmailLabelColor
{
    public string? BackgroundColor { get; set; }   // "#rrggbb",必須是 Gmail 允許的調色盤值
    public string? TextColor       { get; set; }
}

public class GmailLabelOptions
{
    public string? LabelListVisibility   { get; set; }   // null = Gmail 預設("labelShow")
    public string? MessageListVisibility { get; set; }   // null = Gmail 預設("show")
    public string? BackgroundColor       { get; set; }   // 兩個顏色必須一起設,否則 Gmail 回 400
    public string? TextColor             { get; set; }
}

public class GmailAttachment
{
    public string? AttachmentId { get; set; }
    public long    Size         { get; set; }
    public byte[]  Data         { get; set; }   // 已解碼;永不為 null(Gmail 沒回資料時為空陣列)
}
```

### 2.5 列舉

```csharp
public enum GmailMessageFormat { Full, Metadata, Minimal, Raw }
public enum GmailHistoryType   { MessageAdded, MessageDeleted, LabelAdded, LabelRemoved }
```

上線路時序列化為 `full` / `metadata` / `minimal` / `raw` 與 `messageAdded` / `messageDeleted` / `labelAdded` / `labelRemoved`。

### 2.6 常數

```csharp
public static class GmailScopes
{
    public const string GmailModify = "https://www.googleapis.com/auth/gmail.modify";
    public const string OpenId      = "openid";
    public const string Email       = "email";
}

public static class GmailSystemLabels
{
    public const string Inbox = "INBOX", Spam = "SPAM", Trash = "TRASH", Unread = "UNREAD",
                        Starred = "STARRED", Important = "IMPORTANT", Sent = "SENT", Draft = "DRAFT",
                        CategoryPersonal = "CATEGORY_PERSONAL", CategorySocial = "CATEGORY_SOCIAL",
                        CategoryPromotions = "CATEGORY_PROMOTIONS", CategoryUpdates = "CATEGORY_UPDATES",
                        CategoryForums = "CATEGORY_FORUMS";
}
```

只列本套件設計上會用的 scope。`mail.google.com` 刻意不列。

### 2.7 寄件模型——`GmailOutgoingMessage`、`GmailAddress`、`GmailAttachmentContent`

2.0.0 新增,取代 `SendAsync` 原本的 MimeKit `MimeMessage` 參數。

```csharp
public class GmailAddress
{
    public GmailAddress(string address);                 // Name = null
    public GmailAddress(string address, string? name);
    public string  Address { get; }                      // 必須含 '@'、不含空白 / CR / LF → 否則 ArgumentException
    public string? Name    { get; }                      // 顯示名;空白視為 null;含 CR / LF → ArgumentException
    public override string ToString();                   // "Name <address>" 或 "address"——只供顯示,未做 RFC 2047 編碼
}

public class GmailAttachmentContent
{
    public string FileName    { get; set; } = "";                          // 空白 → "attachment"
    public string ContentType { get; set; } = "application/octet-stream"; // 空白 → application/octet-stream
    public byte[] Content     { get; set; } = Array.Empty<byte>();        // 永不為 null(設 null 存成空陣列)
}

public class GmailOutgoingMessage
{
    public GmailAddress?                From        { get; set; }   // null → 不寫 From 標頭,Gmail 會填已授權信箱
    public List<GmailAddress>           To          { get; }        // 永不為 null
    public List<GmailAddress>           Cc          { get; }        // 永不為 null
    public List<GmailAddress>           Bcc         { get; }        // 永不為 null;Gmail 會依標頭投遞給 Bcc 收件人
    public GmailAddress?                ReplyTo     { get; set; }
    public string?                      Subject     { get; set; }   // null / 空 → 不寫 Subject 標頭
    public string?                      TextBody    { get; set; }
    public string?                      HtmlBody    { get; set; }
    public List<GmailAttachmentContent> Attachments { get; }        // 永不為 null
    public string?                      InReplyTo   { get; set; }   // 原信的 Message-ID;缺角括號會自動補
    public List<string>                 References  { get; }        // Message-ID 清單;缺角括號會自動補
    public List<GmailHeader>            Headers     { get; }        // 額外標頭(X-…);與內建標頭同名 → InvalidOperationException

    public byte[] ToRfc822Bytes();                                  // SendAsync 實際上傳的 bytes
}
```

**`ToRfc822Bytes()` 的輸出**

| 情境 | 結構 |
|---|---|
| 只有 `TextBody` 或只有 `HtmlBody` | 單一 `text/plain` 或 `text/html` 部件,`charset=utf-8`,base64 |
| 兩種內文都有 | `multipart/alternative`——text 先、HTML 後 |
| 兩種都沒有 | 一個空的 `text/plain` 部件 |
| 有任何附件 | 內文(單部件或 alternative)包進 `multipart/mixed`,每個附件 `Content-Disposition: attachment`、base64 |

- 標頭順序:`From`、`To`、`Cc`、`Bcc`、`Reply-To`、`Subject`、`In-Reply-To`、`References`、你的額外 `Headers`、`MIME-Version`、`Content-Type`。**不寫** `Date` 與 `Message-ID`——Gmail 會指定。
- 非 ASCII 的顯示名、主旨、額外標頭值與檔名一律 RFC 2047 `=?utf-8?B?…?=` 編碼並折行,不會超過 RFC 5322 的行長限制。純 ASCII 的值原樣寫出。
- 內文與附件一律 base64(76 字元一行、CRLF)。沒有 quoted-printable、沒有 8-bit 模式。
- 不支援:內嵌圖片(`cid:` / `multipart/related`)、S/MIME、巢狀 `message/rfc822`、自訂傳輸編碼。需要這些請用 MIME 函式庫組信後走 `SendRawAsync`。

**拋出**(`ToRfc822Bytes()`,因此 `SendAsync` 也會):`To`、`Cc`、`Bcc` 全空、額外標頭與內建標頭同名(不分大小寫)、或 `InReplyTo` / `References` / 標頭值含 CR 或 LF 時拋 `InvalidOperationException`。

---

## 3. `IGoogleOAuthClient` / `GoogleOAuthClient`(`Ozakboy.Gmail.OAuth`)

```csharp
namespace Ozakboy.Gmail.OAuth;

public interface IGoogleOAuthClient
{
    string BuildAuthorizationUrl(string redirectUri, IEnumerable<string> scopes, string state, GoogleAuthorizationUrlOptions? options = null);
    Task<GoogleTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default);
    Task<GoogleTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task RevokeAsync(string token, CancellationToken cancellationToken = default);
}

public class GoogleOAuthClient : IGoogleOAuthClient
{
    public GoogleOAuthClient(HttpClient httpClient, GoogleOAuthOptions options);
    public GoogleOAuthClient(HttpClient httpClient, GoogleOAuthOptions options, GmailClientOptions? clientOptions);   // 只用到重試設定
}
```

`IGoogleOAuthClient` **(草案外新增)**——理由同 `IGmailClient`(DI 與 mock)。

### 3.1 `GoogleOAuthOptions`

```csharp
public class GoogleOAuthOptions
{
    public string ClientId     { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    public static GoogleOAuthOptions FromConfiguration(IConfiguration configuration, string sectionName = "GoogleOAuth");
}
```

`ClientId` 與 `ClientSecret` 來自你的 Google Cloud OAuth 用戶端。兩者皆必填——任一為空時建構子拋 `ArgumentException`,讓設定錯的宿主在啟動時就失敗,而不是第一次登入才炸。`FromConfiguration` 用 `Microsoft.Extensions.Configuration.Binder` 綁定該區段(預設 `GoogleOAuth`);這是本套件唯一碰 `IConfiguration` 的地方,從環境變數讀機密的宿主可以跳過它、直接 `new` options。

### 3.2 方法

| 方法 | 端點 | 備註 |
|---|---|---|
| `BuildAuthorizationUrl` | `https://accounts.google.com/o/oauth2/v2/auth` | 純函式、不連網。輸出 `client_id`、`redirect_uri`、`response_type=code`、`scope`(空白分隔)、`state`,加上下方 options。`state` 必填——請傳一個你在 callback 能驗證的值(CSRF 防護是呼叫端的事)。 |
| `ExchangeCodeAsync` | `POST https://oauth2.googleapis.com/token`(`grant_type=authorization_code`) | 回 access + refresh token。`redirectUri` 必須與授權網址用的逐字相同。 |
| `RefreshAsync` | `POST https://oauth2.googleapis.com/token`(`grant_type=refresh_token`) | 回新的 access token。回應的 `RefreshToken` 為 **`null`**——Google 續期時不會換發 refresh token,繼續用你手上那個。 |
| `RevokeAsync` | `POST https://oauth2.googleapis.com/revoke` | access 或 refresh token 皆可;撤銷任一個整個授權都失效。成功回 200,token 已無效時回 400(`invalid_token`)——那個 400 **會**以 `GmailApiException` 拋出,若「已經撤銷過」對你來說沒差就自行 catch。 |

### 3.3 `GoogleAuthorizationUrlOptions`

```csharp
public class GoogleAuthorizationUrlOptions
{
    public string  AccessType           { get; set; } = "offline";   // "offline" 才會拿到 refresh token
    public string? Prompt               { get; set; } = "consent";   // "consent" 強制重新授權也發 refresh token;null 就不送此參數
    public bool    IncludeGrantedScopes { get; set; } = true;        // 漸進式授權
    public string? LoginHint            { get; set; }                // 預填帳號選擇器
}
```

預設值是為「伺服器端儲存 refresh token 的應用程式」調的:沒有 `access_type=offline` Google 不會給 refresh token;沒有 `prompt=consent`,已經授權過一次的使用者**第二次授權不會拿到 refresh token**。

### 3.4 `GoogleTokenResponse`

```csharp
public class GoogleTokenResponse
{
    public string? AccessToken  { get; set; }
    public string? RefreshToken { get; set; }   // RefreshAsync 時為 null;ExchangeCodeAsync 時 Google 沒發也為 null
    public int     ExpiresIn    { get; set; }   // 秒
    public string? Scope        { get; set; }   // 空白分隔,實際授予的(可能比申請的少)
    public string? TokenType    { get; set; }   // "Bearer"
    public string? IdToken      { get; set; }   // 有申請 "openid" 才有

    public DateTimeOffset IssuedAt  { get; set; }   // 由 client 在收到回應時設定(UTC)
    public DateTimeOffset ExpiresAt { get; }        // IssuedAt + ExpiresIn
    public string[] GetScopes();                     // Scope 以空白切開;Scope 為 null 時回空陣列
}
```

`ExpiresAt` 是你該存下來比對的值;提前一兩分鐘續期,別掐在到期那一刻。

### 3.5 `GoogleIdTokenPayload` **(草案外新增)**

```csharp
public class GoogleIdTokenPayload
{
    public string?         Subject       { get; set; }   // "sub"——穩定的 Google 使用者 id
    public string?         Email         { get; set; }
    public bool            EmailVerified { get; set; }
    public string?         Name          { get; set; }
    public string?         Picture       { get; set; }
    public string?         HostedDomain  { get; set; }   // "hd"
    public string?         Issuer        { get; set; }   // "iss"
    public string?         Audience      { get; set; }   // "aud"
    public DateTimeOffset? IssuedAt      { get; set; }   // "iat"
    public DateTimeOffset? ExpiresAt     { get; set; }   // "exp"

    public static GoogleIdTokenPayload Parse(string idToken);
}
```

`Parse` 把 JWT 的 payload 段 base64url 解碼、讀出標準 claim。**不驗證簽章。** 這只對「你剛剛直接從 Google token 端點經 TLS 拿到的 token」(`GoogleTokenResponse.IdToken`)安全——不要拿它驗證瀏覽器或第三方交給你的 token。`null` 或空白拋 `ArgumentException`;其他不是 JWT 的輸入拋 `FormatException`。

---

## 例外

| 例外 | 時機 |
|---|---|
| `GmailApiException` | Gmail **或** Google OAuth 端點回任何非 2xx,且重試已用盡。 |
| `OperationCanceledException` | `CancellationToken` 被取消。原樣上拋,重試等待中也一樣。 |
| `HttpRequestException` | 收到回應前的網路 / DNS / TLS 失敗。原樣上拋、不重試。 |
| `ArgumentNullException` / `ArgumentException` / `ArgumentOutOfRangeException` | 參數無效,在**送出任何請求前**拋出(null id、空標籤清單、batch 超過 1000 個 id、`MaxRetries` 為負、`ClientId` 為空……)。 |
| `InvalidOperationException` | `accessTokenProvider` 回 `null` 或空字串;或 `GmailOutgoingMessage.ToRfc822Bytes()` / `SendAsync` 發現沒有收件人、額外標頭與內建標頭衝突、標頭值含 CR / LF。 |
| `FormatException` | `GoogleIdTokenPayload.Parse` 收到的不是 JWT。 |

### `GmailApiException`

```csharp
public class GmailApiException : Exception
{
    public int     StatusCode       { get; }   // HTTP 狀態碼
    public string? Reason           { get; }   // Gmail:error.errors[0].reason(如 "notFound"、"rateLimitExceeded");OAuth:"error"(如 "invalid_grant")
    public string? ErrorMessage     { get; }   // Gmail:error.message;OAuth:error_description
    public string? ResponseBody     { get; }   // 原始 body,供記 log;空或讀不到時為 null
    public string? RequestMethod    { get; }   // "GET"、"POST" …
    public string? RequestPath      { get; }   // path + query,不含 host
    public bool    IsUnauthorized   { get; }   // 401,或 OAuth "invalid_grant" / "invalid_token" → 需要重新授權
    public bool    IsHistoryExpired { get; }   // ListHistoryAsync 的 404 → startHistoryId 太舊,改用時間窗口重掃
    public bool    IsRateLimited    { get; }   // 429,或 reason 為 rateLimitExceeded / userRateLimitExceeded 的 403
    public bool    IsNotFound       { get; }   // 非 IsHistoryExpired 的 404(信被刪、標籤不在……)
}
```

`Message` 為 `"Gmail API {METHOD} {path} failed with {status} ({reason}): {errorMessage}"`——給人讀的,不是解析契約。

**同步迴圈該怎麼用這些旗標:**

- `IsUnauthorized` → 把帳號標成需要重新授權,使用者重新同意前停止呼叫。換新的 access token 沒用:Gmail 401 代表 provider 已經給了它手上最好的 token,OAuth `invalid_grant` 則代表 refresh token 本身已死。
- `IsHistoryExpired` → 丟掉存的 history id,用 `ListMessagesAsync("newer_than:…")` 重掃,再把 `GetProfileAsync` 的 `HistoryId` 存起來。
- `IsRateLimited` → client 已經退避重試 `MaxRetries` 次了;請在 job 層級再退避。
- `GetMessageAsync` 的 `IsNotFound` → 信在列出與抓取之間被刪了;跳過。

### 重試策略

| 狀態 | 重試 | 備註 |
|---|---|---|
| 429 | 是 | 尊重 `Retry-After` |
| 500、502、503、504(任何 5xx) | 是 | 有 `Retry-After` 時尊重 |
| reason 為 `rateLimitExceeded` / `userRateLimitExceeded` 的 403 | 是 | Gmail 的每使用者配額錯誤是回 403 不是 429 |
| 401、400、403(其他 reason)、404、409、412 … | 否 | 立即拋出 |
| `HttpRequestException` | 否 | 立即拋出 |

等待時間為 `RetryBaseDelay × 2^(attempt-1)`,預設 1s、2s、4s。重試**不會**再呼叫 `accessTokenProvider`,沿用同一個 token。

---

## null 行為總表

| 輸入 | 行為 |
|---|---|
| 選擇性查詢參數為 `null`(`query`、`labelIds`、`maxResults`、`pageToken`、`historyTypes`、`labelId`、`metadataHeaders`) | 請求中省略該參數 |
| `labelIds` / `historyTypes` / `metadataHeaders` 為空序列 | 請求中省略該參數 |
| `addLabelIds` **與** `removeLabelIds` 皆 `null` / 空(`ModifyLabelsAsync`、`BatchModifyLabelsAsync`) | `ArgumentException` |
| `ids` 為空(`BatchModifyLabelsAsync`) | 不送請求,立即完成 |
| `threadId` 為 `null`(`SendAsync`) | 新討論串 |
| `options` 為 `null`(`CreateLabelAsync`、`UpdateLabelAsync`、`BuildAuthorizationUrl`、`GmailClient`) | 預設值 |
| `name` 為 `null`(`UpdateLabelAsync`) | 名稱不變 |
| `Prompt` 為 `null`(`GoogleAuthorizationUrlOptions`) | 省略 `prompt` 參數 |
| 任何必填字串參數(`id`、`messageId`、`attachmentId`、`name`、`code`、`refreshToken`、`token`、`redirectUri`、`state`、`startHistoryId`)為 `null` 或空 | `ArgumentException` |
| `message`(`SendAsync`)、`rfc822`(`SendRawAsync`)、`destination`(`DownloadAttachmentAsync`)或 `scopes`(`BuildAuthorizationUrl`)為 `null` | `ArgumentNullException` |
| `rfc822` 為空陣列(`SendRawAsync`) | `ArgumentException` |
| `GmailOutgoingMessage.From` 為 `null` | 不寫 `From` 標頭,Gmail 填已授權信箱 |
| `GmailOutgoingMessage.Subject` 為 `null` 或空 | 不寫 `Subject` 標頭 |
| `GmailOutgoingMessage.TextBody` 與 `HtmlBody` 皆 `null` | 一個空的 `text/plain` 部件 |
| `GmailAttachmentContent.FileName` 空白 / `ContentType` 空白 / `Content` 為 null | `attachment` / `application/octet-stream` / 空陣列 |
| `GmailMessage.Raw` 為 `null` | `DecodeRaw()` 回 `null`;`GetMessageRawAsync` 回空陣列 |
| `scopes` 為空序列(`BuildAuthorizationUrl`) | `ArgumentException` |
| `destination` 不可寫(`DownloadAttachmentAsync`) | `ArgumentException` |
| `GmailClientOptions.UserId` 為 `null` 或空白 | `me` |
| Gmail 回應缺少某 JSON 欄位 | 屬性維持 `null` / `0`;List 屬性維持空 |

---

## 執行緒安全

`GmailClient` 與 `GoogleOAuthClient` 建構後沒有可變狀態,可被任意多執行緒同時使用,規則同 `HttpClient` 本身。你提供的 `accessTokenProvider` 可能被同時呼叫——若它會續期,請自行做到執行緒安全。

---

## 延伸閱讀

- [快速開始](./getting-started.md)
- [設定](./configuration.md)
- [升級指南](./migration.md)
- [版本紀錄](./changelog.md)
