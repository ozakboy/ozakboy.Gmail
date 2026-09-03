---
title: Changelog
description: All notable changes to Ozakboy.Gmail.
---

# Changelog

All notable changes to the **Ozakboy.Gmail** package are recorded here.
Version numbers follow [Semantic Versioning](https://semver.org/).

---

## [2.1.0] - 2026-09-04

> Faster backfill, thread endpoints, body / attachment helpers, a ready-made access-token provider and finer retry control. **No breaking changes.**

### Added

- **`BatchGetMessagesAsync(ids, format, metadataHeaders)`** — fetches many messages through the Gmail batch endpoint (`multipart/mixed`), up to `GmailClientOptions.BatchSize` (default 50) per HTTP request, chunked automatically. Returns `GmailBatchGetResult` with `Messages` and per-id `Failures` (`IsNotFound`, `IsRateLimited`) instead of failing the whole batch when one message was deleted in the meantime.
- **Thread endpoints** — `GetThreadAsync`, `ModifyThreadAsync`, `TrashThreadAsync`, `UntrashThreadAsync`, with the `GmailThread` model.
- **`GmailMessage.GetTextBody()` / `GetHtmlBody()`** — walk the MIME parts Gmail already split and return the first non-attachment body, decoded. **`GmailMessage.GetAttachments()`** — every attachment part as `GmailAttachmentInfo` (part id, file name, MIME type, size, attachment id, `Content-ID`, the original part).
- **`GoogleAccessTokenProvider`** (`Ozakboy.Gmail.OAuth`) — caches the access token, refreshes it ahead of expiry (`RefreshSkew`, default 2 minutes), serialises concurrent refreshes so twenty callers produce one token request, and reports each refresh through `OnRefreshed` for persistence. Pass `provider.GetAccessTokenAsync` to `GmailClient`.
- **`GmailApiException.RetryAfter`** — the `Retry-After` value of the final failed response, for job-level backoff.
- **`GmailClientOptions.MaxRetryDelay`** (default 60 s) — a backoff or `Retry-After` longer than this is not waited for; the exception is thrown immediately with `RetryAfter` set. **`GmailClientOptions.RetryOnNetworkErrors`** (default `false`) — retry `HttpRequestException` with the same backoff; the last one still propagates unwrapped.

> **Note for `IGmailClient` implementers:** the interface gained five members (`BatchGetMessagesAsync`, `GetThreadAsync`, `ModifyThreadAsync`, `TrashThreadAsync`, `UntrashThreadAsync`). Code that only *uses* `GmailClient` / `IGmailClient` is unaffected; a hand-written fake or mock that *implements* the interface needs the new members. Released as a Minor because the package has no external implementers yet.

### Fixed

- **.NET Framework: `NullReferenceException` on 204 No Content.** `HttpResponseMessage.Content` can be `null` on .NET Framework (it is always an empty content on .NET Core), so `BatchModifyLabelsAsync`, `DeleteLabelAsync` and `RevokeAsync` crashed on `net48` and other netstandard2.0 hosts. Found by the new `net48` test target.

### Technical

- The test project now targets `net10.0` **and** `net48`; every test runs against the `netstandard2.0` build as well.
- GitHub Actions workflow `build-test.yml`: five-TFM Release build with warnings as errors and both test targets on every push to `main` and every pull request.

---

## [2.0.0] - 2026-09-03

> **MimeKit is no longer a dependency.** Outgoing mail is composed by a built-in RFC 822 writer or handed over as raw bytes; raw messages come back as bytes. **Breaking**: two signatures changed — see the [migration guide](./migration.md). Everything else is identical to 1.0.0.

### Added

- **`GmailOutgoingMessage`** — a small outgoing-mail model (`From`, `To`, `Cc`, `Bcc`, `ReplyTo`, `Subject`, `TextBody`, `HtmlBody`, `Attachments`, `InReplyTo`, `References`, extra `Headers`) with `ToRfc822Bytes()`: a built-in RFC 822 / MIME writer — UTF-8, base64 bodies and attachments, RFC 2047 encoded headers, `multipart/alternative` for text + HTML, `multipart/mixed` for attachments.
- **`GmailAddress`** (address + optional display name, validated) and **`GmailAttachmentContent`** (`FileName`, `ContentType`, `byte[] Content`) as the building blocks of `GmailOutgoingMessage`.
- **`SendRawAsync(byte[] rfc822, threadId)`** — send a message composed by any MIME library of your choice; the bytes are uploaded verbatim.
- **`GmailMessage.DecodeRaw()`** — the base64url-decoded RFC 822 bytes of a message fetched with `GmailMessageFormat.Raw`.

### Changed (breaking)

- **`SendAsync(MimeMessage, threadId)` → `SendAsync(GmailOutgoingMessage, threadId)`.** Build the message with `GmailOutgoingMessage`, or keep MimeKit in your own project and call `SendRawAsync` with the serialised bytes.
- **`GetMessageRawAsync(id)` now returns `Task<byte[]>`** instead of `Task<MimeMessage>`. Parse with any MIME library, or use `GetMessageAsync(id, GmailMessageFormat.Full)`, which Gmail has already split into parts.
- **The package no longer references `MimeKit`.** Projects that used MimeKit types through the transitive reference must add the package themselves.

### Technical

- The RFC 822 writer is verified in the test project by parsing its output with MimeKit (test-only dependency): addresses and display names, non-ASCII subjects and file names round-trip, `multipart/alternative` + `multipart/mixed` structure, attachment bytes, header folding and line-length limits.

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
