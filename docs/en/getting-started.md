---
title: Getting Started
description: Install Ozakboy.Gmail 1.0.0, connect a Gmail account with Google OAuth, and read your first messages.
---

# Getting Started

## Install

```bash
dotnet add package Ozakboy.Gmail --version 1.0.0
```

Supported target frameworks: `netstandard2.0`, `netstandard2.1`, `net8.0`, `net9.0`, `net10.0`.

## 0. What you need from Google

Before any code runs you need an OAuth client in [Google Cloud Console](https://console.cloud.google.com/):

1. Enable the **Gmail API** for the project.
2. Configure the **OAuth consent screen**. Add `https://www.googleapis.com/auth/gmail.modify` (plus `openid` and `email` if you want the user's address and id from the `id_token`). `gmail.modify` is a *restricted* scope — while the app is in **Testing** status it works for up to 100 listed test users and refresh tokens expire after 7 days; publishing requires Google's verification.
3. Create an **OAuth client ID** of type *Web application* and register your callback URL as an authorized redirect URI, for example `https://app.example.com/oauth/google/callback`.
4. Keep the client id and secret out of source control — environment variables or your secret store.

## 1. Configure the OAuth client

Either add a `GoogleOAuth` section to `appsettings.json` (or user secrets / environment variables that map to it):

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

var oauthOptions = GoogleOAuthOptions.FromConfiguration(configuration);   // section name defaults to "GoogleOAuth"
```

Or build the options by hand when the values come from somewhere else:

```csharp
var oauthOptions = new GoogleOAuthOptions
{
    ClientId     = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")!,
    ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")!,
};
```

The constructor of `GoogleOAuthClient` throws `ArgumentException` when either value is empty, so a misconfigured host fails at startup.

## 2. Register the clients (ASP.NET Core)

```csharp
using Ozakboy.Gmail;
using Ozakboy.Gmail.OAuth;

builder.Services.AddSingleton(GoogleOAuthOptions.FromConfiguration(builder.Configuration));
builder.Services.AddHttpClient<IGoogleOAuthClient, GoogleOAuthClient>();
builder.Services.AddHttpClient("gmail");
```

`GmailClient` needs an access-token provider that is specific to *one* mailbox, so it is usually created per account rather than injected directly:

```csharp
public sealed class GmailClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    public GmailClientFactory(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public IGmailClient Create(Func<CancellationToken, Task<string>> accessTokenProvider)
        => new GmailClient(_httpClientFactory.CreateClient("gmail"), accessTokenProvider);
}
```

Console apps can simply `new HttpClient()` once and reuse it.

## 3. Connect a mailbox (authorization code flow)

**Send the user to Google:**

```csharp
var state = /* a random value you store in the session and verify on the callback */;

var url = oauthClient.BuildAuthorizationUrl(
    redirectUri: "https://app.example.com/oauth/google/callback",
    scopes:      new[] { GmailScopes.GmailModify, GmailScopes.OpenId, GmailScopes.Email },
    state:       state);

return Redirect(url);
```

The defaults already include `access_type=offline` and `prompt=consent`, which is what makes Google issue a refresh token — including on a *second* authorization by the same user.

**On the callback, exchange the code:**

```csharp
GoogleTokenResponse token = await oauthClient.ExchangeCodeAsync(code, "https://app.example.com/oauth/google/callback", ct);

// Who just connected? (no signature check — safe only because this id_token came straight from Google)
var identity = GoogleIdTokenPayload.Parse(token.IdToken!);
string googleUserId = identity.Subject!;
string email        = identity.Email!;

// Persist what you need — encrypted, please:
//   token.RefreshToken   (null if Google did not issue one; see Configuration → "No refresh token?")
//   token.AccessToken, token.ExpiresAt
```

## 4. Provide an access token

`GmailClient` calls your provider once per request. A typical provider returns the cached access token and refreshes it a little before `ExpiresAt`:

```csharp
Func<CancellationToken, Task<string>> provider = async ct =>
{
    var account = await store.LoadAsync(accountId, ct);
    if (account.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        return account.AccessToken;

    var refreshed = await oauthClient.RefreshAsync(account.RefreshToken, ct);   // RefreshToken in the response is null — keep yours
    await store.SaveAccessTokenAsync(accountId, refreshed.AccessToken!, refreshed.ExpiresAt, ct);
    return refreshed.AccessToken!;
};

IGmailClient gmail = new GmailClient(httpClient, provider);
```

If `RefreshAsync` throws `GmailApiException` with `IsUnauthorized == true`, the refresh token itself has been revoked or has expired — mark the account as needing re-authorization and send the user through step 3 again.

## 5. Read the mailbox

```csharp
// Where are we? Store HistoryId before the first backfill.
GmailProfile profile = await gmail.GetProfileAsync(ct);
Console.WriteLine($"{profile.EmailAddress} — {profile.MessagesTotal} messages, historyId {profile.HistoryId}");

// Last 30 days, 100 at a time
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

## 6. Sync incrementally

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

    storedHistoryId = history.HistoryId!;   // persist for the next pass
}
catch (GmailApiException ex) when (ex.IsHistoryExpired)
{
    // Gmail no longer has that history — rescan by time window, then take a fresh HistoryId from GetProfileAsync
}
```

## 7. Label, trash, send

```csharp
GmailLabel label = await gmail.CreateLabelAsync("Sower/Ads", cancellationToken: ct);

await gmail.BatchModifyLabelsAsync(adIds, addLabelIds: new[] { label.Id! }, removeLabelIds: null, ct);
await gmail.TrashAsync(messageId, ct);          // recoverable for 30 days; there is no permanent delete

var reply = new MimeMessage();
reply.From.Add(new MailboxAddress("Support", "support@example.com"));
reply.To.Add(new MailboxAddress("Customer", "customer@example.com"));
reply.Subject = "Re: Quotation";
reply.InReplyTo = originalMessageIdHeader;      // from GetHeader("Message-ID")
reply.References.Add(originalMessageIdHeader);
reply.Body = new TextPart("html") { Text = "<p>Attached.</p>" };

GmailMessage sent = await gmail.SendAsync(reply, threadId: originalThreadId, ct);
```

Gmail replaces `From` with the authenticated mailbox (or a verified send-as alias), so the address you put there mostly serves as the display name.

## Handling errors

```csharp
try
{
    await gmail.GetMessageAsync(id, cancellationToken: ct);
}
catch (GmailApiException ex) when (ex.IsUnauthorized)   { /* re-authorize the account */ }
catch (GmailApiException ex) when (ex.IsNotFound)       { /* deleted between list and get — skip */ }
catch (GmailApiException ex) when (ex.IsRateLimited)    { /* the client already retried; back off at the job level */ }
catch (GmailApiException ex)                            { logger.LogError(ex, "{Method} {Path} → {Status} {Reason}", ex.RequestMethod, ex.RequestPath, ex.StatusCode, ex.Reason); }
```

Every non-2xx response is a `GmailApiException`; 429 and 5xx are retried three times with exponential backoff before you see one. The full list of flags, retry rules and null behaviour is in the [API Reference](./api.md).

## Next steps

- [Configuration](./configuration.md) — every option, plus the "no refresh token" and "7-day expiry" gotchas
- [API Reference](./api.md) — every member, parameter and exception
- [Changelog](./changelog.md)
