---
title: API Reference
description: Complete public API of Ozakboy.Gmail 2.0.0 — GmailClient, GoogleOAuthClient, models, options and the GmailApiException error contract.
---

# API Reference

> Source of truth: [`Ozakboy.Gmail/Ozakboy.Gmail/`](https://github.com/ozakboy/ozakboy.Gmail/tree/main/Ozakboy.Gmail/Ozakboy.Gmail). The generated XML documentation ships inside the NuGet package.

Ozakboy.Gmail is a thin, async-only client for the [Gmail REST API](https://developers.google.com/workspace/gmail/api/reference/rest) plus the Google OAuth 2.0 token endpoints. It does **not** depend on `Google.Apis.*`, does not store tokens, does not open SMTP or IMAP connections, and (since 2.0.0) pulls in no MIME library — outgoing mail is composed by a built-in RFC 822 writer or handed over as raw bytes. Sending goes through `messages.send`, so the single `gmail.modify` scope is enough.

Items marked **(beyond the draft)** were not in the original 1.0.0 package specification and were added because a first consumer needs them. Members marked **(2.0.0)** replaced the MimeKit-typed members of 1.0.0 — see [Migration](./migration.md).

## Namespaces

| Namespace | Contents |
|---|---|
| `Ozakboy.Gmail` | `IGmailClient`, `GmailClient`, `GmailClientOptions`, `GmailApiException`, `GmailScopes`, `GmailSystemLabels`, `GmailOutgoingMessage`, `GmailAddress`, `GmailAttachmentContent`, and every `Gmail*` model |
| `Ozakboy.Gmail.OAuth` | `IGoogleOAuthClient`, `GoogleOAuthClient`, `GoogleOAuthOptions`, `GoogleAuthorizationUrlOptions`, `GoogleTokenResponse`, `GoogleIdTokenPayload` |
| `Ozakboy.Gmail.Core` | Internal HTTP / JSON / base64url plumbing. Not part of the public API — do not take a dependency on it. |

## Conventions that apply everywhere

- **Async only.** Every network member returns `Task` / `Task<T>`, ends in `Async`, and takes a trailing `CancellationToken cancellationToken = default`. There are no synchronous counterparts.
- **`ConfigureAwait(false)`** on every internal `await`; the library never captures a synchronization context.
- **Cancellation is never wrapped.** `OperationCanceledException` propagates unchanged.
- **Non-2xx is always `GmailApiException`** — see [Exceptions](#exceptions). Network-level failures (`HttpRequestException`) propagate unchanged and are not retried.
- **User id is always `me`** (overridable through `GmailClientOptions.UserId`).
- **Models are plain classes** with public get/set properties, property names aligned to the Gmail REST field names in PascalCase. Absent JSON fields stay `null`; list-typed properties are never `null` (they default to an empty list).
- **Ids are strings.** `historyId`, `internalDate` and similar int64/uint64 fields that Gmail serialises as strings are exposed as `string` (`HistoryId`) or `long` (`InternalDate`, `Size*`) exactly as documented below.
- `null` for an optional argument means "do not send that parameter".

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

    Task<byte[]> GetMessageRawAsync(string id, CancellationToken cancellationToken = default);                                                         // (2.0.0: byte[] instead of MimeMessage)

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
    Task<GmailLabel> UpdateLabelAsync(string id, string? name = null, GmailLabelOptions? options = null, CancellationToken cancellationToken = default);   // (beyond the draft)
    Task DeleteLabelAsync(string id, CancellationToken cancellationToken = default);                                                                    // (beyond the draft)

    Task<GmailAttachment> GetAttachmentAsync(string messageId, string attachmentId, CancellationToken cancellationToken = default);
    Task<long> DownloadAttachmentAsync(string messageId, string attachmentId, Stream destination, CancellationToken cancellationToken = default);

    Task<GmailMessage> SendAsync(GmailOutgoingMessage message, string? threadId = null, CancellationToken cancellationToken = default);   // (2.0.0: GmailOutgoingMessage instead of MimeMessage)
    Task<GmailMessage> SendRawAsync(byte[] rfc822, string? threadId = null, CancellationToken cancellationToken = default);            // (2.0.0)
}

public class GmailClient : IGmailClient
{
    public GmailClient(HttpClient httpClient, Func<CancellationToken, Task<string>> accessTokenProvider);
    public GmailClient(HttpClient httpClient, Func<CancellationToken, Task<string>> accessTokenProvider, GmailClientOptions? options);
}
```

`IGmailClient` **(beyond the draft)** exists so the consumer can register the client in DI and substitute a mock in tests. `GmailClient` is the only implementation.

### 1.1 Constructors

| Parameter | Description |
|---|---|
| `httpClient` | The `HttpClient` used for every call. The client does not dispose it. `BaseAddress` is ignored — absolute Gmail URLs are always used, so a plain `new HttpClient()` or an `IHttpClientFactory` typed client both work. |
| `accessTokenProvider` | Called **once per HTTP request** (not per retry) to obtain a bearer access token. Caching, refreshing and persisting the token are entirely the caller's job. A `null` provider throws `ArgumentNullException`; a provider that returns `null` or an empty string causes `InvalidOperationException` at call time. |
| `options` | See [`GmailClientOptions`](#12-gmailclientoptions). `null` is treated as defaults. |

The instance holds no per-call state, so it is safe to register as a singleton.

### 1.2 `GmailClientOptions`

```csharp
public class GmailClientOptions
{
    public int      MaxRetries     { get; set; } = 3;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    public string   UserId         { get; set; } = "me";
}
```

| Member | Description |
|---|---|
| `MaxRetries` | How many times a request is **re-sent** after a retryable failure (HTTP 429, any 5xx, or 403 with reason `rateLimitExceeded` / `userRateLimitExceeded`). `3` means up to four attempts in total. `0` disables retries. Negative values throw `ArgumentOutOfRangeException` when the client is constructed. |
| `RetryBaseDelay` | Delay before the first retry; doubled on every subsequent retry (1s → 2s → 4s with the defaults). When the response carries a `Retry-After` header, that value is used instead of the computed delay for that attempt. `TimeSpan.Zero` makes tests run without waiting. |
| `UserId` | The `{userId}` path segment. Leave as `me` unless you are a domain-wide-delegated service account. |

Retries apply to every Gmail call **and** to the OAuth token / revoke calls. A request is never retried after a 401, 404, 400 or any other non-retryable status.

### 1.3 Methods

| Method | REST call | Returns / notes |
|---|---|---|
| `GetProfileAsync` | `GET users/{userId}/profile` | `GmailProfile` — the mailbox address and the **current `HistoryId`**, which is what you store before the first backfill so incremental sync can start from it later. |
| `ListMessagesAsync` | `GET users/{userId}/messages` | `GmailMessageList` with `Messages` (id + threadId only), `NextPageToken`, `ResultSizeEstimate`. `query` is Gmail search syntax (`newer_than:30d`); `maxResults` is capped by Gmail at 500. Pass `NextPageToken` back as `pageToken` to continue. |
| `GetMessageAsync` | `GET users/{userId}/messages/{id}` | `GmailMessage`. `format` selects how much is returned (see [`GmailMessageFormat`](#25-enumerations)). `metadataHeaders` is only sent with `Metadata` and limits which headers come back. |
| `GetMessageRawAsync` **(2.0.0)** | `GET …/messages/{id}?format=raw` | The complete RFC 822 message as **decoded bytes** (empty array when Gmail returned no `raw`). Parse it with any MIME library, or skip parsing altogether: `GetMessageAsync(id, GmailMessageFormat.Full)` returns the same message already split into parts by Gmail. |
| `ListHistoryAsync` | `GET users/{userId}/history` | `GmailHistoryList`. `startHistoryId` must be a history id you obtained earlier. When Gmail has discarded that history (HTTP 404), the exception has `IsHistoryExpired == true` — fall back to a time-window rescan. |
| `ModifyLabelsAsync` | `POST …/messages/{id}/modify` | The updated `GmailMessage`. Both lists may be `null` or empty; at least one label must be supplied or `ArgumentException` is thrown. |
| `BatchModifyLabelsAsync` | `POST …/messages/batchModify` | Completes with no result (Gmail returns 204). Gmail accepts at most **1000 ids** per call; more than that throws `ArgumentException` before any request is sent. `ids` empty is a no-op that sends nothing. |
| `TrashAsync` / `UntrashAsync` | `POST …/messages/{id}/trash` / `untrash` | The updated `GmailMessage`. Trash is recoverable for 30 days; there is deliberately **no permanent delete** in this package. |
| `ReportSpamAsync` | `POST …/messages/{id}/modify` | Convenience for `ModifyLabelsAsync(id, add: ["SPAM"], remove: ["INBOX"])`. Gmail has no dedicated "report spam" endpoint; moving into `SPAM` is what the web UI does. |
| `ListLabelsAsync` | `GET users/{userId}/labels` | All labels, system and user. The list endpoint does not include message counts; call `GetLabelAsync`-equivalents yourself if you need them (not provided in 1.0.0). |
| `CreateLabelAsync` | `POST users/{userId}/labels` | The created `GmailLabel`. Nested labels use `/` in the name (`Sower/Ads`); the parent must exist or Gmail returns 400. Creating a duplicate name returns 409 → `GmailApiException` with `StatusCode == 409`. |
| `UpdateLabelAsync` **(beyond the draft)** | `PATCH users/{userId}/labels/{id}` | Renames and/or changes visibility / colour. `name == null` keeps the current name; `options == null` keeps the current visibility and colour. |
| `DeleteLabelAsync` **(beyond the draft)** | `DELETE users/{userId}/labels/{id}` | Deletes a **user** label; the label is removed from every message it was applied to. System labels cannot be deleted (Gmail returns 400). |
| `GetAttachmentAsync` | `GET …/messages/{messageId}/attachments/{attachmentId}` | `GmailAttachment` with the **already-decoded** bytes in `Data` and Gmail's `Size`. Gmail returns attachments as base64url inside a JSON body, so the whole attachment is buffered in memory once — there is no true streaming on the wire. |
| `DownloadAttachmentAsync` | same | Decodes and writes the bytes into `destination`, returns the number of bytes written. Meant for proxying a download into an HTTP response without touching disk. `destination` must be writable; the stream is **not** closed or flushed for you. |
| `SendAsync` **(2.0.0)** | `POST upload/gmail/v1/users/{userId}/messages/send?uploadType=multipart` | The sent `GmailMessage` (`Id`, `ThreadId`, `LabelIds`). The [`GmailOutgoingMessage`](#27-outgoing-mail--gmailoutgoingmessage-gmailaddress-gmailattachmentcontent) is serialised by the built-in RFC 822 writer (`ToRfc822Bytes()`) and uploaded as `message/rfc822` in a multipart request, so the 35 MB upload limit applies rather than the JSON `raw` limit. `threadId` makes the message part of an existing thread — for a reply, also set `InReplyTo` and `References`, or Gmail will not thread it. |
| `SendRawAsync` **(2.0.0)** | same | Same upload, but you supply the RFC 822 bytes yourself — from MimeKit, MailKit, `System.Net.Mail` or anything else. `rfc822` `null` → `ArgumentNullException`, empty → `ArgumentException`. |

**`SendAsync` and the sender address.** Gmail rewrites `From` to the authenticated mailbox (or one of its verified send-as aliases). A `From` that is not verified does not fail — it is silently replaced.

---

## 2. Models (`Ozakboy.Gmail`)

All models are plain classes with public get/set properties. Only the fields Gmail actually returns for the methods above are modelled; anything else Gmail sends is ignored.

### 2.1 Profile, message references, lists

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
    public List<GmailMessageRef> Messages           { get; set; }   // never null
    public string?               NextPageToken      { get; set; }
    public long?                 ResultSizeEstimate { get; set; }
}
```

### 2.2 `GmailMessage` and MIME parts

```csharp
public class GmailMessage
{
    public string?           Id           { get; set; }
    public string?           ThreadId     { get; set; }
    public List<string>      LabelIds     { get; set; }   // never null
    public string?           Snippet      { get; set; }
    public string?           HistoryId    { get; set; }
    public long              InternalDate { get; set; }   // epoch milliseconds, 0 when absent
    public long              SizeEstimate { get; set; }
    public GmailMessagePart? Payload      { get; set; }   // null for format=Minimal
    public string?           Raw          { get; set; }   // base64url, only for format=Raw

    public DateTimeOffset? InternalDateTime { get; }       // InternalDate converted; null when 0
    public string? GetHeader(string name);                  // case-insensitive lookup in Payload.Headers; null when absent
    public byte[]? DecodeRaw();                              // (2.0.0) Raw base64url-decoded; null when Raw is null
}

public class GmailMessagePart
{
    public string?                PartId   { get; set; }
    public string?                MimeType { get; set; }
    public string?                Filename { get; set; }
    public List<GmailHeader>      Headers  { get; set; }   // never null
    public GmailMessagePartBody?  Body     { get; set; }
    public List<GmailMessagePart> Parts    { get; set; }   // never null

    public string? GetHeader(string name);                  // case-insensitive; first match; null when absent
}

public class GmailHeader
{
    public string? Name  { get; set; }
    public string? Value { get; set; }
}

public class GmailMessagePartBody
{
    public string? AttachmentId { get; set; }   // set when the content is not inline
    public long    Size         { get; set; }
    public string? Data         { get; set; }   // base64url, inline content only

    public byte[]? DecodeData();                 // null when Data is null
}
```

`GetHeader` is the helper you will reach for most: `message.GetHeader("List-Unsubscribe")`, `message.GetHeader("Authentication-Results")`. It only searches the top-level payload headers — exactly what `format=Metadata` returns.

### 2.3 History

```csharp
public class GmailHistoryList
{
    public List<GmailHistoryRecord> History       { get; set; }   // never null; empty when nothing changed
    public string?                  NextPageToken { get; set; }
    public string?                  HistoryId     { get; set; }   // the mailbox's current history id — store it after each successful pass
}

public class GmailHistoryRecord
{
    public string?                       Id              { get; set; }
    public List<GmailMessageRef>         Messages        { get; set; }   // never null
    public List<GmailHistoryMessageChange> MessagesAdded   { get; set; }   // never null
    public List<GmailHistoryMessageChange> MessagesDeleted { get; set; }   // never null
    public List<GmailHistoryLabelChange>   LabelsAdded     { get; set; }   // never null
    public List<GmailHistoryLabelChange>   LabelsRemoved   { get; set; }   // never null
}

public class GmailHistoryMessageChange
{
    public GmailMessage? Message { get; set; }   // only Id, ThreadId and LabelIds are populated by Gmail
}

public class GmailHistoryLabelChange
{
    public GmailMessage? Message  { get; set; }
    public List<string>  LabelIds { get; set; }  // never null
}
```

### 2.4 Labels and attachments

```csharp
public class GmailLabel
{
    public string?          Id                    { get; set; }
    public string?          Name                  { get; set; }
    public string?          Type                  { get; set; }   // "system" | "user"
    public string?          MessageListVisibility { get; set; }   // "show" | "hide"
    public string?          LabelListVisibility   { get; set; }   // "labelShow" | "labelShowIfUnread" | "labelHide"
    public long?            MessagesTotal         { get; set; }   // only on labels.get / labels.create responses
    public long?            MessagesUnread        { get; set; }
    public long?            ThreadsTotal          { get; set; }
    public long?            ThreadsUnread         { get; set; }
    public GmailLabelColor? Color                 { get; set; }
}

public class GmailLabelColor
{
    public string? BackgroundColor { get; set; }   // "#rrggbb", must be one of Gmail's allowed palette values
    public string? TextColor       { get; set; }
}

public class GmailLabelOptions
{
    public string? LabelListVisibility   { get; set; }   // null = Gmail default ("labelShow")
    public string? MessageListVisibility { get; set; }   // null = Gmail default ("show")
    public string? BackgroundColor       { get; set; }   // both colours must be set together or Gmail returns 400
    public string? TextColor             { get; set; }
}

public class GmailAttachment
{
    public string? AttachmentId { get; set; }
    public long    Size         { get; set; }
    public byte[]  Data         { get; set; }   // decoded; never null (empty array when Gmail returned no data)
}
```

### 2.5 Enumerations

```csharp
public enum GmailMessageFormat { Full, Metadata, Minimal, Raw }
public enum GmailHistoryType   { MessageAdded, MessageDeleted, LabelAdded, LabelRemoved }
```

Serialised to the wire as `full` / `metadata` / `minimal` / `raw` and `messageAdded` / `messageDeleted` / `labelAdded` / `labelRemoved`.

### 2.6 Constants

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

Only the scopes this package is designed for are listed. `mail.google.com` is intentionally absent.

### 2.7 Outgoing mail — `GmailOutgoingMessage`, `GmailAddress`, `GmailAttachmentContent`

Added in 2.0.0, replacing the MimeKit `MimeMessage` parameter of `SendAsync`.

```csharp
public class GmailAddress
{
    public GmailAddress(string address);                 // Name = null
    public GmailAddress(string address, string? name);
    public string  Address { get; }                      // must contain '@', no whitespace / CR / LF → ArgumentException
    public string? Name    { get; }                      // display name; blank is normalised to null; CR / LF → ArgumentException
    public override string ToString();                   // "Name <address>" or "address" — for display only, not RFC 2047 encoded
}

public class GmailAttachmentContent
{
    public string FileName    { get; set; } = "";                          // blank → "attachment"
    public string ContentType { get; set; } = "application/octet-stream"; // blank → application/octet-stream
    public byte[] Content     { get; set; } = Array.Empty<byte>();        // never null (null is stored as an empty array)
}

public class GmailOutgoingMessage
{
    public GmailAddress?                From        { get; set; }   // null → no From header; Gmail fills in the authenticated mailbox
    public List<GmailAddress>           To          { get; }        // never null
    public List<GmailAddress>           Cc          { get; }        // never null
    public List<GmailAddress>           Bcc         { get; }        // never null; Gmail delivers to Bcc recipients listed in the header
    public GmailAddress?                ReplyTo     { get; set; }
    public string?                      Subject     { get; set; }   // null / empty → no Subject header
    public string?                      TextBody    { get; set; }
    public string?                      HtmlBody    { get; set; }
    public List<GmailAttachmentContent> Attachments { get; }        // never null
    public string?                      InReplyTo   { get; set; }   // Message-ID of the original; angle brackets added when missing
    public List<string>                 References  { get; }        // Message-IDs; angle brackets added when missing
    public List<GmailHeader>            Headers     { get; }        // extra headers (X-…); clashing with a generated header → InvalidOperationException

    public byte[] ToRfc822Bytes();                                  // the exact bytes SendAsync uploads
}
```

**What `ToRfc822Bytes()` produces**

| Situation | Structure |
|---|---|
| Only `TextBody` or only `HtmlBody` | a single `text/plain` or `text/html` part, `charset=utf-8`, base64 |
| Both bodies | `multipart/alternative` — text first, HTML second |
| Neither body | an empty `text/plain` part |
| Any attachment | the body (single part or alternative) wrapped in `multipart/mixed`, each attachment as `Content-Disposition: attachment`, base64 |

- Headers are written in this order: `From`, `To`, `Cc`, `Bcc`, `Reply-To`, `Subject`, `In-Reply-To`, `References`, your extra `Headers`, `MIME-Version`, `Content-Type`. `Date` and `Message-ID` are **not** written — Gmail assigns them.
- Non-ASCII display names, subjects, extra header values and file names are RFC 2047 `=?utf-8?B?…?=` encoded and folded so no line exceeds the RFC 5322 limits. ASCII values are written verbatim.
- Bodies and attachments are always base64 (76-character lines, CRLF). There is no quoted-printable and no 8-bit mode.
- Not supported: inline images (`cid:` / `multipart/related`), S/MIME, nested `message/rfc822` parts, custom transfer encodings. For those, compose the message with a MIME library and use `SendRawAsync`.

**Throws** (`ToRfc822Bytes()` and therefore `SendAsync`): `InvalidOperationException` when `To`, `Cc` and `Bcc` are all empty, when an extra header clashes with a generated one (case-insensitive), or when `InReplyTo` / `References` / a header value contains CR or LF.

---

## 3. `IGoogleOAuthClient` / `GoogleOAuthClient` (`Ozakboy.Gmail.OAuth`)

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
    public GoogleOAuthClient(HttpClient httpClient, GoogleOAuthOptions options, GmailClientOptions? clientOptions);   // retry settings only
}
```

`IGoogleOAuthClient` **(beyond the draft)** — same DI / mocking rationale as `IGmailClient`.

### 3.1 `GoogleOAuthOptions`

```csharp
public class GoogleOAuthOptions
{
    public string ClientId     { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    public static GoogleOAuthOptions FromConfiguration(IConfiguration configuration, string sectionName = "GoogleOAuth");
}
```

`ClientId` and `ClientSecret` come from your Google Cloud OAuth client. Both are required — the constructor throws `ArgumentException` when either is empty, so a misconfigured host fails at startup rather than on the first login. `FromConfiguration` binds a section of that name (defaults to `GoogleOAuth`) via `Microsoft.Extensions.Configuration.Binder`; it is the only place this package touches `IConfiguration`, so hosts that read secrets from environment variables can skip it and `new` the options directly.

### 3.2 Methods

| Method | Endpoint | Notes |
|---|---|---|
| `BuildAuthorizationUrl` | `https://accounts.google.com/o/oauth2/v2/auth` | Pure function, no network. Emits `client_id`, `redirect_uri`, `response_type=code`, `scope` (space-joined), `state`, plus the options below. `state` is required — pass a value you can verify on the callback (CSRF protection is the caller's job). |
| `ExchangeCodeAsync` | `POST https://oauth2.googleapis.com/token` (`grant_type=authorization_code`) | Returns access + refresh token. `redirectUri` must be byte-for-byte the one used in the authorization URL. |
| `RefreshAsync` | `POST https://oauth2.googleapis.com/token` (`grant_type=refresh_token`) | Returns a new access token. `RefreshToken` in the response is **`null`** — Google does not rotate refresh tokens on refresh; keep the one you have. |
| `RevokeAsync` | `POST https://oauth2.googleapis.com/revoke` | Accepts either an access or a refresh token; revoking either invalidates the whole grant. Google returns 200 on success and 400 (`invalid_token`) when the token is already invalid — that 400 **is** surfaced as `GmailApiException`, so catch it if "already revoked" is fine for you. |

### 3.3 `GoogleAuthorizationUrlOptions`

```csharp
public class GoogleAuthorizationUrlOptions
{
    public string  AccessType           { get; set; } = "offline";   // "offline" is what yields a refresh token
    public string? Prompt               { get; set; } = "consent";   // "consent" forces a refresh token on re-authorization; null omits the parameter
    public bool    IncludeGrantedScopes { get; set; } = true;        // incremental authorization
    public string? LoginHint            { get; set; }                // pre-fills the account chooser
}
```

Defaults are tuned for a server-side app that stores a refresh token: without `access_type=offline` Google returns no refresh token, and without `prompt=consent` a user who already granted the app once gets **no refresh token on the second authorization**.

### 3.4 `GoogleTokenResponse`

```csharp
public class GoogleTokenResponse
{
    public string? AccessToken  { get; set; }
    public string? RefreshToken { get; set; }   // null on RefreshAsync, and on ExchangeCodeAsync when Google did not issue one
    public int     ExpiresIn    { get; set; }   // seconds
    public string? Scope        { get; set; }   // space-separated, as granted (may be fewer than requested)
    public string? TokenType    { get; set; }   // "Bearer"
    public string? IdToken      { get; set; }   // present when "openid" was requested

    public DateTimeOffset IssuedAt  { get; set; }   // set by the client when the response is received (UTC)
    public DateTimeOffset ExpiresAt { get; }        // IssuedAt + ExpiresIn
    public string[] GetScopes();                     // Scope split on spaces; empty array when Scope is null
}
```

`ExpiresAt` is what you persist and compare against; refresh a minute or two early rather than on the dot.

### 3.5 `GoogleIdTokenPayload` **(beyond the draft)**

```csharp
public class GoogleIdTokenPayload
{
    public string?         Subject       { get; set; }   // "sub" — the stable Google user id
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

`Parse` base64url-decodes the JWT payload segment and reads the standard claims. **It does not verify the signature.** That is safe only for a token you just received directly from Google's token endpoint over TLS (the `IdToken` of a `GoogleTokenResponse`) — do not use it to validate a token handed to you by a browser or a third party. A `null` or blank token throws `ArgumentException`; anything else that is not a JWT throws `FormatException`.

---

## Exceptions

| Exception | When |
|---|---|
| `GmailApiException` | Any HTTP response outside 2xx from Gmail **or** the Google OAuth endpoints, after retries are exhausted. |
| `OperationCanceledException` | The `CancellationToken` was cancelled. Propagates unwrapped, even mid-retry-delay. |
| `HttpRequestException` | Network / DNS / TLS failure before a response was received. Propagates unwrapped and is not retried. |
| `ArgumentNullException` / `ArgumentException` / `ArgumentOutOfRangeException` | Invalid arguments, thrown **before** any request is sent (null ids, empty label lists, more than 1000 batch ids, negative `MaxRetries`, empty `ClientId`…). |
| `InvalidOperationException` | `accessTokenProvider` returned `null` or an empty string; or `GmailOutgoingMessage.ToRfc822Bytes()` / `SendAsync` found no recipients, a clashing extra header, or CR / LF in a header value. |
| `FormatException` | `GoogleIdTokenPayload.Parse` received something that is not a JWT. |

### `GmailApiException`

```csharp
public class GmailApiException : Exception
{
    public int     StatusCode       { get; }   // HTTP status
    public string? Reason           { get; }   // Gmail: error.errors[0].reason (e.g. "notFound", "rateLimitExceeded"); OAuth: "error" (e.g. "invalid_grant")
    public string? ErrorMessage     { get; }   // Gmail: error.message; OAuth: error_description
    public string? ResponseBody     { get; }   // raw body, for logging; null when empty or unreadable
    public string? RequestMethod    { get; }   // "GET", "POST" …
    public string? RequestPath      { get; }   // path + query, no host
    public bool    IsUnauthorized   { get; }   // 401, or OAuth "invalid_grant" / "invalid_token" → re-authorization is needed
    public bool    IsHistoryExpired { get; }   // 404 from ListHistoryAsync → the startHistoryId is too old, rescan by time window
    public bool    IsRateLimited    { get; }   // 429, or 403 with reason rateLimitExceeded / userRateLimitExceeded
    public bool    IsNotFound       { get; }   // 404 that is not IsHistoryExpired (message deleted, label gone…)
}
```

`Message` is `"Gmail API {METHOD} {path} failed with {status} ({reason}): {errorMessage}"` — human-readable, not a parsing contract.

**How the flags are meant to be used by a sync loop:**

- `IsUnauthorized` → mark the account as needing re-authorization and stop calling until the user re-consents. A refreshed access token would not help: for Gmail 401 the provider already supplied its best token, and for OAuth `invalid_grant` the refresh token itself is dead.
- `IsHistoryExpired` → discard the stored history id, rescan with `ListMessagesAsync("newer_than:…")`, then store the `HistoryId` from `GetProfileAsync`.
- `IsRateLimited` → the client already retried `MaxRetries` times with backoff; back off at the job level.
- `IsNotFound` from `GetMessageAsync` → the message was deleted between listing and fetching; skip it.

### Retry policy

| Status | Retried | Notes |
|---|---|---|
| 429 | yes | honours `Retry-After` |
| 500, 502, 503, 504 (any 5xx) | yes | honours `Retry-After` when present |
| 403 with reason `rateLimitExceeded` / `userRateLimitExceeded` | yes | Gmail's per-user quota errors come back as 403, not 429 |
| 401, 400, 403 (other reasons), 404, 409, 412 … | no | surfaced immediately |
| `HttpRequestException` | no | surfaced immediately |

Delays are `RetryBaseDelay × 2^(attempt-1)`, so 1s, 2s, 4s by default. The `accessTokenProvider` is **not** called again for a retry; the same token is reused.

---

## Null handling summary

| Input | Behaviour |
|---|---|
| Optional query argument is `null` (`query`, `labelIds`, `maxResults`, `pageToken`, `historyTypes`, `labelId`, `metadataHeaders`) | Parameter omitted from the request |
| `labelIds` / `historyTypes` / `metadataHeaders` is an empty sequence | Parameter omitted from the request |
| `addLabelIds` **and** `removeLabelIds` both `null` / empty (`ModifyLabelsAsync`, `BatchModifyLabelsAsync`) | `ArgumentException` |
| `ids` empty (`BatchModifyLabelsAsync`) | No request sent, completes immediately |
| `threadId` is `null` (`SendAsync`) | New thread |
| `options` is `null` (`CreateLabelAsync`, `UpdateLabelAsync`, `BuildAuthorizationUrl`, `GmailClient`) | Defaults |
| `name` is `null` (`UpdateLabelAsync`) | Name unchanged |
| `Prompt` is `null` (`GoogleAuthorizationUrlOptions`) | `prompt` parameter omitted |
| Any required string argument (`id`, `messageId`, `attachmentId`, `name`, `code`, `refreshToken`, `token`, `redirectUri`, `state`, `startHistoryId`) is `null` or empty | `ArgumentException` |
| `message` (`SendAsync`), `rfc822` (`SendRawAsync`), `destination` (`DownloadAttachmentAsync`) or `scopes` (`BuildAuthorizationUrl`) is `null` | `ArgumentNullException` |
| `rfc822` is empty (`SendRawAsync`) | `ArgumentException` |
| `GmailOutgoingMessage.From` is `null` | No `From` header; Gmail fills in the authenticated mailbox |
| `GmailOutgoingMessage.Subject` is `null` or empty | No `Subject` header |
| `GmailOutgoingMessage.TextBody` and `HtmlBody` both `null` | An empty `text/plain` part |
| `GmailAttachmentContent.FileName` blank / `ContentType` blank / `Content` null | `attachment` / `application/octet-stream` / empty array |
| `GmailMessage.Raw` is `null` | `DecodeRaw()` returns `null`; `GetMessageRawAsync` returns an empty array |
| `scopes` is an empty sequence (`BuildAuthorizationUrl`) | `ArgumentException` |
| `destination` is not writable (`DownloadAttachmentAsync`) | `ArgumentException` |
| `GmailClientOptions.UserId` is `null` or blank | `me` |
| JSON field absent in a Gmail response | Property stays `null` / `0`; list properties stay empty |

---

## Thread safety

`GmailClient` and `GoogleOAuthClient` keep no mutable state after construction and can be used concurrently from any number of threads, subject to the same rule as `HttpClient` itself. The `accessTokenProvider` you supply may be called concurrently — make it thread-safe if it refreshes.

---

## See also

- [Getting Started](./getting-started.md)
- [Configuration](./configuration.md)
- [Migration](./migration.md)
- [Changelog](./changelog.md)
