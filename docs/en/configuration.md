---
title: Configuration
description: Every option of Ozakboy.Gmail 2.0.0 — GoogleOAuthOptions, GoogleAuthorizationUrlOptions, GmailClientOptions — plus the Google-side settings and gotchas.
---

# Configuration

Ozakboy.Gmail has three option classes. Only one of them (`GoogleOAuthOptions`) holds secrets; the other two are behaviour knobs with sensible defaults.

## `GoogleOAuthOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `ClientId` | `string` | `""` | The OAuth client id from Google Cloud Console (`….apps.googleusercontent.com`). Required. |
| `ClientSecret` | `string` | `""` | The matching client secret. Required. Treat it like a password. |

### From `appsettings.json`

```json
{
  "GoogleOAuth": {
    "ClientId": "your-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-client-secret"
  }
}
```

```csharp
var options = GoogleOAuthOptions.FromConfiguration(configuration);                 // reads "GoogleOAuth"
var options = GoogleOAuthOptions.FromConfiguration(configuration, "Google:OAuth");  // any section path
```

`FromConfiguration` is a thin wrapper over `configuration.GetSection(name).Get<GoogleOAuthOptions>()`. A missing section yields empty strings, and `new GoogleOAuthClient(httpClient, options)` then throws `ArgumentException` — deliberately, so the error surfaces at startup.

### From environment variables

.NET's configuration providers map `GoogleOAuth__ClientId` / `GoogleOAuth__ClientSecret` (double underscore) into the same section, so `FromConfiguration` keeps working. Or skip `IConfiguration` altogether:

```csharp
var options = new GoogleOAuthOptions
{
    ClientId     = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")!,
    ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")!,
};
```

Never commit the secret, never log it, and rotate it in Google Cloud Console if it leaks.

## `GoogleAuthorizationUrlOptions`

Passed to `BuildAuthorizationUrl`; `null` uses the defaults.

| Property | Type | Default | Description |
|---|---|---|---|
| `AccessType` | `string` | `"offline"` | `offline` asks Google for a refresh token. `online` gives an access token only — useless for a server that syncs unattended. |
| `Prompt` | `string?` | `"consent"` | `consent` always shows the consent screen and always returns a refresh token. `null` omits the parameter; `select_account` / `none` are also valid Google values. |
| `IncludeGrantedScopes` | `bool` | `true` | Sends `include_granted_scopes=true` (`false` omits the parameter, which is Google's default) so a later authorization with more scopes keeps the ones already granted (incremental authorization). |
| `LoginHint` | `string?` | `null` | An email address to pre-select in Google's account chooser. Useful when the user is re-authorizing a mailbox you already know. |

### No refresh token?

The number one OAuth surprise: `ExchangeCodeAsync` returns `RefreshToken == null`. Google only issues a refresh token when **both** `access_type=offline` is present **and** the user is granting for the first time — unless `prompt=consent` forces it. The defaults set both, so with default options you always get one. If you changed `Prompt` and lost the refresh token, the user has to revoke the app at [myaccount.google.com/permissions](https://myaccount.google.com/permissions) or you call `RevokeAsync` and send them through the flow again with `Prompt = "consent"`.

### 7-day expiry while in Testing

While your Google Cloud project's OAuth consent screen is in **Testing** status and uses a restricted scope such as `gmail.modify`, every refresh token expires **7 days** after issue. `RefreshAsync` then throws `GmailApiException` with `IsUnauthorized == true` (`invalid_grant`). This is Google policy, not a bug — handle it by marking the account as needing re-authorization. Moving the app to **In production** (which requires verification for restricted scopes) removes the limit.

## `GmailClientOptions`

Passed to `GmailClient` and (retry settings only) to `GoogleOAuthClient`; `null` uses the defaults. The values are copied at construction time — changing the options object afterwards has no effect on an existing client.

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxRetries` | `int` | `3` | Retries after a retryable failure (429, any 5xx, 403 `rateLimitExceeded` / `userRateLimitExceeded`). `0` disables retries. Negative throws `ArgumentOutOfRangeException`. |
| `RetryBaseDelay` | `TimeSpan` | 1 second | First retry delay; doubled per retry (1 s, 2 s, 4 s). A `Retry-After` response header overrides the computed delay for that attempt. Set to `TimeSpan.Zero` in tests. |
| `UserId` | `string` | `"me"` | The `{userId}` path segment of every Gmail URL. `me` means the authorized user. Only change it for domain-wide delegation. |

```csharp
var gmail = new GmailClient(httpClient, provider, new GmailClientOptions
{
    MaxRetries     = 5,
    RetryBaseDelay = TimeSpan.FromSeconds(2),
});
```

### What is *not* configurable

- **Endpoints.** Gmail and OAuth URLs are fixed. Tests inject a fake `HttpMessageHandler` into the `HttpClient` instead.
- **Timeouts.** Set `HttpClient.Timeout` on the client you pass in; the library does not override it. Remember that a request retried three times can take up to `4 × Timeout + 7 s` in the worst case.
- **JSON serialization.** Internal.

## The `HttpClient` you pass in

- The library never disposes it and ignores `BaseAddress`. Any `DefaultRequestHeaders` you set are sent; the `Authorization` header is set per request and overrides yours.
- One `HttpClient` can back many `GmailClient` instances for different mailboxes — the token is per request, not per client.
- With `IHttpClientFactory`, a named client (`AddHttpClient("gmail")`) is the simplest fit because `GmailClient` also needs the per-mailbox token provider, which DI cannot supply on its own.

## Google Cloud checklist

| Setting | Value |
|---|---|
| API | Gmail API enabled |
| OAuth client type | Web application |
| Authorized redirect URI | exactly the `redirectUri` you pass to `BuildAuthorizationUrl` / `ExchangeCodeAsync` |
| Scopes on the consent screen | `https://www.googleapis.com/auth/gmail.modify` (+ `openid`, `email` for `id_token`) |
| Publishing status | Testing: ≤ 100 test users, 7-day refresh tokens. Production: requires restricted-scope verification (CASA assessment) |

## See also

- [Getting Started](./getting-started.md)
- [API Reference](./api.md)
