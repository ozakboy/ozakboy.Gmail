---
title: 升級指南
description: Ozakboy.Gmail 從 1.0.0 升到 2.0.0——MimeKit 拔除、SendAsync 改吃 GmailOutgoingMessage、GetMessageRawAsync 改回 bytes。
---

# 升級指南

## 1.0.0 → 2.0.0

2.0.0 移除 **MimeKit** 相依。只有兩個成員因為簽章用到 MimeKit 型別而改變;其他所有方法、模型、`GmailApiException`、重試策略、OAuth client 一律不動。

| 1.0.0 | 2.0.0 |
|---|---|
| `Task<GmailMessage> SendAsync(MimeMessage message, string? threadId, ct)` | `Task<GmailMessage> SendAsync(GmailOutgoingMessage message, string? threadId, ct)` |
| — | `Task<GmailMessage> SendRawAsync(byte[] rfc822, string? threadId, ct)` |
| `Task<MimeMessage> GetMessageRawAsync(string id, ct)` | `Task<byte[]> GetMessageRawAsync(string id, ct)` |
| — | `byte[]? GmailMessage.DecodeRaw()` |
| 套件相依 `MimeKit` | 相依圖裡沒有任何 MIME 函式庫 |

### 寄信:兩條路挑一條

**路線 A——用內建組信器。** 把 `MimeMessage` 換成 `GmailOutgoingMessage`:

```csharp
// 1.0.0
var reply = new MimeMessage();
reply.From.Add(new MailboxAddress("客服", "support@example.com"));
reply.To.Add(new MailboxAddress("客戶", "customer@example.com"));
reply.Subject = "Re: 報價確認";
reply.InReplyTo = originalMessageId;
reply.References.Add(originalMessageId);
reply.Body = new TextPart("html") { Text = "<p>附件如附。</p>" };
await gmail.SendAsync(reply, threadId);

// 2.0.0
var reply = new GmailOutgoingMessage
{
    From      = new GmailAddress("support@example.com", "客服"),
    Subject   = "Re: 報價確認",
    HtmlBody  = "<p>附件如附。</p>",
    InReplyTo = originalMessageId,
};
reply.To.Add(new GmailAddress("customer@example.com", "客戶"));
reply.References.Add(originalMessageId);
reply.Attachments.Add(new GmailAttachmentContent { FileName = "報價單.pdf", ContentType = "application/pdf", Content = pdfBytes });
await gmail.SendAsync(reply, threadId);
```

組信器涵蓋純文字與 HTML 內文(兩者都有時 `multipart/alternative`)、`byte[]` 附件、`Reply-To`、`Bcc`、討論串標頭與額外標頭。**不做**內嵌圖片(`cid:`)、S/MIME、巢狀郵件——需要這些走路線 B。

**路線 B——在你自己的專案保留 MimeKit(或任何 MIME 函式庫)。** 在*你的*應用程式加 `MimeKit`,照原本方式組信,把 bytes 交出去:

```csharp
using var buffer = new MemoryStream();
await mimeMessage.WriteToAsync(buffer, ct);
await gmail.SendRawAsync(buffer.ToArray(), threadId, ct);
```

`SendRawAsync` 把 bytes 原封不動上傳,和 1.0.0 序列化 `MimeMessage` 之後做的事完全一樣。

### 讀原始信

`GetMessageRawAsync` 現在回解碼後的 RFC 822 bytes,不再回解析好的 `MimeMessage`:

```csharp
// 1.0.0
MimeMessage parsed = await gmail.GetMessageRawAsync(id, ct);

// 2.0.0——用你選的函式庫解析
byte[] rfc822 = await gmail.GetMessageRawAsync(id, ct);
MimeMessage parsed = await MimeMessage.LoadAsync(new MemoryStream(rfc822), ct);   // 你專案裡的 MimeKit
```

大多數呼叫端根本不需要 MIME 解析器:`GetMessageAsync(id, GmailMessageFormat.Full)` 回的信已經由 Gmail 拆好 part,標頭已解碼、內文是 base64url(`GmailMessagePartBody.DecodeData()`)。

### 遞移相依

如果你的專案沒直接參考 `MimeKit` 卻用了它的型別,那是因為 Ozakboy.Gmail 1.0.0 遞移帶進來的。升級後請明確加 `MimeKit`,或改用內建型別。

### 其他都沒變

方法名、參數名、`GmailApiException` 旗標、重試規則、`GoogleOAuthClient` 與 1.0.0 的每個模型完全相同。升 2.0.0 只因為兩個簽章變了。

## 延伸閱讀

- [API 文件](./api.md)——`GmailOutgoingMessage`、`GmailAddress`、`GmailAttachmentContent`、`SendRawAsync`
- [版本紀錄](./changelog.md)
