---
title: Changelog
description: All notable changes to Ozakboy.Gmail.
---

# Changelog

All notable changes to the **Ozakboy.Gmail** package are recorded here.
Version numbers follow [Semantic Versioning](https://semver.org/).

---

## [1.0.0] - 2026-09-03

> First release. A thin, async-only client for the Gmail REST API and the Google OAuth 2.0 token endpoints — no `Google.Apis` dependency, no token storage, no SMTP. Sending goes through `messages.send`, so the single `gmail.modify` scope is enough.

### Added

- **`GmailClient` / `IGmailClient`** — `GetProfileAsync`, `ListMessagesAsync`, `GetMessageAsync`, `GetMessageRawAsync` (returns a MimeKit `MimeMessage`), `ListHistoryAsync`, `ModifyLabelsAsync`, `BatchModifyLabelsAsync`, `TrashAsync`, `UntrashAsync`, `ReportSpamAsync`, `ListLabelsAsync`, `CreateLabelAsync`, `UpdateLabelAsync`, `DeleteLabelAsync`, `GetAttachmentAsync`, `DownloadAttachmentAsync`, `SendAsync`. The client takes an `HttpClient` and an access-token provider (`Func<CancellationToken, Task<string>>`); how tokens are cached, refreshed and persisted is entirely up to the caller.
- **`SendAsync(MimeMessage, threadId)`** uploads the MIME message as `message/rfc822` through the multipart upload endpoint (35 MB limit), with an optional `threadId` for replies.
- **`GoogleOAuthClient` / `IGoogleOAuthClient`** — `BuildAuthorizationUrl` (defaults to `access_type=offline` and `prompt=consent` so a refresh token is issued), `ExchangeCodeAsync`, `RefreshAsync`, `RevokeAsync`. `GoogleOAuthOptions.FromConfiguration` binds `ClientId` / `ClientSecret` from an `IConfiguration` section.
- **`GoogleIdTokenPayload.Parse`** reads `sub`, `email` and the other standard claims from an `id_token` received directly from Google. It does not verify the signature and is documented as such.
- **`GmailApiException`** for every non-2xx response, carrying `StatusCode`, Google's `Reason` and `ErrorMessage`, the raw `ResponseBody`, the request method and path, and the flags `IsUnauthorized`, `IsHistoryExpired`, `IsRateLimited`, `IsNotFound` so a sync loop can decide between re-authorization, a time-window rescan and backing off without parsing error JSON.
- **Retry with exponential backoff** for 429, every 5xx and Gmail's 403 `rateLimitExceeded` / `userRateLimitExceeded` — 3 retries from 1 s by default, `Retry-After` honoured, configurable through `GmailClientOptions`.
- **Models aligned to the Gmail REST field names**: `GmailProfile`, `GmailMessage`, `GmailMessagePart`, `GmailMessagePartBody`, `GmailHeader`, `GmailMessageList`, `GmailMessageRef`, `GmailHistoryList`, `GmailHistoryRecord`, `GmailHistoryMessageChange`, `GmailHistoryLabelChange`, `GmailLabel`, `GmailLabelColor`, `GmailLabelOptions`, `GmailAttachment`, plus the `GmailScopes` and `GmailSystemLabels` constants and the `GmailMessageFormat` / `GmailHistoryType` enumerations.
- **Multi-targeting**: `netstandard2.0`, `netstandard2.1`, `net8.0`, `net9.0`, `net10.0`.

### Technical

- Async only; every internal `await` uses `ConfigureAwait(false)`; `OperationCanceledException` is never wrapped.
- xUnit test project (`Ozakboy.Gmail.Tests`) covering the public contract offline against a recording `HttpMessageHandler`: request shapes for every method, response mapping, retry and error-flag behaviour, argument validation, OAuth request bodies and id-token parsing. No test contacts Google.
- Manual console (`Ozakboy.Gmail.TEST`) that refreshes a token and reads a mailbox's profile, latest messages and labels once real credentials are filled into `appsettings.json`. Read-only by design.
