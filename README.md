<img src="https://raw.githubusercontent.com/ozakboy/ozakboy.Gmail/main/logo.png" width="112" align="right" alt="Ozakboy.Gmail" />

# Ozakboy.Gmail

**Gmail REST and Google OAuth in one thin async client. You keep the tokens; it keeps the HTTP.**

[English](README.md) | [繁體中文](README_zh-TW.md) · [Changelog](docs/en/changelog.md) · [API Reference](docs/en/api.md) · [NuGet](https://www.nuget.org/packages/Ozakboy.Gmail/)

A server that reads a support mailbox every five minutes, labels what it finds, trashes the ads and sends the odd reply. That job needs the Gmail API and a refresh token you store yourself — encrypted, per tenant, on your terms. It does not need the whole `Google.Apis` authentication stack deciding where tokens live. Ozakboy.Gmail is the client for that job: one `HttpClient`, one delegate that hands over an access token, and the Gmail REST surface you actually use.

🔑 &nbsp;·&nbsp;·&nbsp;·&nbsp; ✉

## Read a mailbox

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

Label it, trash it, or reply:

```csharp
await gmail.BatchModifyLabelsAsync(adIds, addLabelIds: new[] { adsLabelId }, removeLabelIds: null);
await gmail.TrashAsync(messageId);                       // recoverable for 30 days — there is no permanent delete here

var reply = new GmailOutgoingMessage { Subject = "Re: Quotation", HtmlBody = "<p>Attached.</p>", InReplyTo = originalMessageId };
reply.To.Add(new GmailAddress("customer@example.com", "Customer"));
reply.Attachments.Add(new GmailAttachmentContent { FileName = "quote.pdf", ContentType = "application/pdf", Content = pdfBytes });

GmailMessage sent = await gmail.SendAsync(reply, threadId: originalThreadId);
```

And decide what to do when Google says no:

```csharp
try
{
    GmailHistoryList history = await gmail.ListHistoryAsync(storedHistoryId);
    storedHistoryId = history.HistoryId!;
}
catch (GmailApiException ex) when (ex.IsHistoryExpired) { /* rescan by time window */ }
catch (GmailApiException ex) when (ex.IsUnauthorized)   { /* ask the user to re-authorize */ }
```

## Connect a mailbox

```csharp
using Ozakboy.Gmail.OAuth;

IGoogleOAuthClient oauth = new GoogleOAuthClient(httpClient, GoogleOAuthOptions.FromConfiguration(configuration));

// 1. send the user here
string url = oauth.BuildAuthorizationUrl(redirectUri, new[] { GmailScopes.GmailModify, GmailScopes.OpenId, GmailScopes.Email }, state);

// 2. on the callback
GoogleTokenResponse token = await oauth.ExchangeCodeAsync(code, redirectUri);
var who = GoogleIdTokenPayload.Parse(token.IdToken!);         // who.Subject, who.Email
// store token.RefreshToken (encrypted), token.AccessToken, token.ExpiresAt

// 3. later, before ExpiresAt
GoogleTokenResponse fresh = await oauth.RefreshAsync(refreshToken);
```

## What this is

A deliberately thin layer over two Google HTTP APIs:

- **`GmailClient`** — profile, list / get messages (metadata, full, or the raw RFC 822 bytes), `history.list` for incremental sync, modify / batch-modify labels, trash / untrash, report spam, labels CRUD, attachments, and `messages.send` — with a built-in RFC 822 writer (`GmailOutgoingMessage`) or your own bytes (`SendRawAsync`)
- **`GoogleOAuthClient`** — authorization URL, code exchange, refresh, revoke, and reading the `id_token` claims
- **`GmailApiException`** — every non-2xx response, with Google's error reason and four flags a sync loop needs: `IsUnauthorized`, `IsHistoryExpired`, `IsRateLimited`, `IsNotFound`

And what it is **not**: it does not store or encrypt tokens, does not watch Pub/Sub, does not delete permanently, does not speak IMAP or SMTP, does not cover other Google APIs, and does not classify mail. Those live in your application.

## Why it is different

**No `Google.Apis`, no opinions about tokens.** The client asks a `Func<CancellationToken, Task<string>>` for an access token before each request and never sees a refresh token. Encrypt them with your own key, scope them per tenant, rotate them on your schedule — the library has no cache to fight.

**`gmail.modify` is the only scope you need.** Sending goes through REST `messages.send` (the built-in writer produces the RFC 822 message, the client uploads it as `message/rfc822`), not SMTP. SMTP and IMAP with OAuth require the full `https://mail.google.com/` scope; this library never asks for it.

**Errors you can branch on.** Gmail's per-user quota comes back as HTTP 403, an expired `startHistoryId` as 404, a dead refresh token as 400 `invalid_grant`. You do not parse any of that: 429, 5xx and the rate-limit 403s are retried with exponential backoff (three times from one second, `Retry-After` honoured), and what still fails arrives as one exception type with the right flag set.

**No MIME library either.** Since 2.0.0 the package depends on nothing beyond the two `Microsoft.Extensions.Configuration` binding packages (and `System.Text.Json` on .NET Standard). `GmailOutgoingMessage` writes text + HTML bodies, attachments, `Reply-To`, `Bcc`, threading and custom headers itself; anything fancier, build with MimeKit in your own project and call `SendRawAsync`.

**Async all the way down.** No synchronous API, `ConfigureAwait(false)` on every await, `OperationCanceledException` never wrapped, models are plain classes with the Gmail REST field names.

## Install

```bash
dotnet add package Ozakboy.Gmail
```

Then follow [Getting Started](docs/en/getting-started.md) — it walks through the Google Cloud setup (Gmail API, consent screen, OAuth client), the authorization flow, a token provider that refreshes itself, and a first sync loop.

✉ &nbsp;·&nbsp;·&nbsp;·&nbsp; 🔑

## Compatibility

| Target framework | Supported |
|---|---|
| .NET 10.0 | ✅ |
| .NET 9.0 | ✅ |
| .NET 8.0 | ✅ |
| .NET Standard 2.1 | ✅ |
| .NET Standard 2.0 | ✅ |

.NET Standard 2.0 covers .NET Framework 4.6.1+, .NET Core 2.0+ and Mono/Xamarin/Unity.

Dependencies: `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.Configuration.Binder`, and `System.Text.Json` on .NET Standard only. No MIME library since 2.0.0 — see the [migration guide](docs/en/migration.md) if you are upgrading from 1.0.0.

## Documentation

| | |
|---|---|
| [Getting Started](docs/en/getting-started.md) | Google Cloud setup, OAuth flow, token provider, first sync |
| [Configuration](docs/en/configuration.md) | `GoogleOAuthOptions`, `GoogleAuthorizationUrlOptions`, `GmailClientOptions`, the refresh-token gotchas |
| [API Reference](docs/en/api.md) | Every public member, parameter, exception flag and null rule |
| [Migration](docs/en/migration.md) | 1.0.0 → 2.0.0 |
| [Changelog](docs/en/changelog.md) | Version history |

## License

MIT.

## Support

- [Report an issue](https://github.com/ozakboy/ozakboy.Gmail/issues)
- [Open a pull request](https://github.com/ozakboy/ozakboy.Gmail/pulls)
