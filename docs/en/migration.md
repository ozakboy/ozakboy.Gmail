---
title: Migration
description: Upgrading Ozakboy.Gmail from 1.0.0 to 2.0.0 — MimeKit is gone, SendAsync takes GmailOutgoingMessage, GetMessageRawAsync returns bytes.
---

# Migration

## 1.0.0 → 2.0.0

2.0.0 removes the **MimeKit** dependency. Two members changed shape because they exposed MimeKit types; everything else — every other method, the models, `GmailApiException`, the retry policy, the OAuth client — is untouched.

| 1.0.0 | 2.0.0 |
|---|---|
| `Task<GmailMessage> SendAsync(MimeMessage message, string? threadId, ct)` | `Task<GmailMessage> SendAsync(GmailOutgoingMessage message, string? threadId, ct)` |
| — | `Task<GmailMessage> SendRawAsync(byte[] rfc822, string? threadId, ct)` |
| `Task<MimeMessage> GetMessageRawAsync(string id, ct)` | `Task<byte[]> GetMessageRawAsync(string id, ct)` |
| — | `byte[]? GmailMessage.DecodeRaw()` |
| Package depends on `MimeKit` | No MIME library in the dependency graph |

### Sending: pick one of two paths

**Path A — use the built-in composer.** Replace the `MimeMessage` with a `GmailOutgoingMessage`:

```csharp
// 1.0.0
var reply = new MimeMessage();
reply.From.Add(new MailboxAddress("Support", "support@example.com"));
reply.To.Add(new MailboxAddress("Customer", "customer@example.com"));
reply.Subject = "Re: Quotation";
reply.InReplyTo = originalMessageId;
reply.References.Add(originalMessageId);
reply.Body = new TextPart("html") { Text = "<p>Attached.</p>" };
await gmail.SendAsync(reply, threadId);

// 2.0.0
var reply = new GmailOutgoingMessage
{
    From      = new GmailAddress("support@example.com", "Support"),
    Subject   = "Re: Quotation",
    HtmlBody  = "<p>Attached.</p>",
    InReplyTo = originalMessageId,
};
reply.To.Add(new GmailAddress("customer@example.com", "Customer"));
reply.References.Add(originalMessageId);
reply.Attachments.Add(new GmailAttachmentContent { FileName = "quote.pdf", ContentType = "application/pdf", Content = pdfBytes });
await gmail.SendAsync(reply, threadId);
```

The composer covers plain-text and HTML bodies (`multipart/alternative` when both are set), attachments as `byte[]`, `Reply-To`, `Bcc`, threading headers and extra headers. It does **not** do inline images (`cid:`), S/MIME or nested messages — for those use path B.

**Path B — keep MimeKit (or any MIME library) in your own project.** Add `MimeKit` to *your* application, build the message exactly as before, and hand over the bytes:

```csharp
using var buffer = new MemoryStream();
await mimeMessage.WriteToAsync(buffer, ct);
await gmail.SendRawAsync(buffer.ToArray(), threadId, ct);
```

`SendRawAsync` uploads the bytes verbatim, exactly as 1.0.0 did after serialising the `MimeMessage`.

### Reading raw messages

`GetMessageRawAsync` now returns the decoded RFC 822 bytes instead of a parsed `MimeMessage`:

```csharp
// 1.0.0
MimeMessage parsed = await gmail.GetMessageRawAsync(id, ct);

// 2.0.0 — parse with the library of your choice
byte[] rfc822 = await gmail.GetMessageRawAsync(id, ct);
MimeMessage parsed = await MimeMessage.LoadAsync(new MemoryStream(rfc822), ct);   // MimeKit in your project
```

Most callers do not need a MIME parser at all: `GetMessageAsync(id, GmailMessageFormat.Full)` returns the message already split into parts by Gmail, with decoded headers and base64url bodies (`GmailMessagePartBody.DecodeData()`).

### Transitive dependency

If your project used `MimeKit` types without referencing the package directly, it compiled only because Ozakboy.Gmail 1.0.0 pulled MimeKit in transitively. After upgrading, either add `MimeKit` explicitly or move to the built-in types.

### Nothing else changed

Method names, parameter names, `GmailApiException` flags, retry rules, `GoogleOAuthClient` and every model from 1.0.0 are identical. The version is 2.0.0 only because two signatures changed.

## See also

- [API Reference](./api.md) — `GmailOutgoingMessage`, `GmailAddress`, `GmailAttachmentContent`, `SendRawAsync`
- [Changelog](./changelog.md)
