using System;
using System.Text;
using Ozakboy.Gmail.Core;
using Ozakboy.Gmail.OAuth;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// id_token 酬載解析測試。這裡只驗證解碼與宣告讀取,不涉及簽章驗證(本套件刻意不做)。
    /// </summary>
    [TestClass]
    public class GoogleIdTokenPayloadTests
    {
        private static string CreateToken(string payloadJson)
        {
            var header = Base64Url.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
            var payload = Base64Url.Encode(Encoding.UTF8.GetBytes(payloadJson));
            return header + "." + payload + ".c2lnbmF0dXJl";
        }

        [TestMethod]
        public void Parse_讀出標準宣告()
        {
            var token = CreateToken(
                "{\"iss\":\"https://accounts.google.com\",\"sub\":\"1234567890\",\"aud\":\"client-id\"," +
                "\"email\":\"user@example.com\",\"email_verified\":true,\"name\":\"測試使用者\"," +
                "\"picture\":\"https://example.com/a.png\",\"hd\":\"example.com\",\"iat\":1725000000,\"exp\":1725003600}");

            var payload = GoogleIdTokenPayload.Parse(token);

            Assert.AreEqual("1234567890", payload.Subject);
            Assert.AreEqual("user@example.com", payload.Email);
            Assert.IsTrue(payload.EmailVerified);
            Assert.AreEqual("測試使用者", payload.Name);
            Assert.AreEqual("https://example.com/a.png", payload.Picture);
            Assert.AreEqual("example.com", payload.HostedDomain);
            Assert.AreEqual("https://accounts.google.com", payload.Issuer);
            Assert.AreEqual("client-id", payload.Audience);
            Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1725000000), payload.IssuedAt);
            Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1725003600), payload.ExpiresAt);
        }

        [TestMethod]
        public void Parse_aud為陣列時取第一個()
        {
            var token = CreateToken("{\"aud\":[\"first-client\",\"second-client\"]}");

            var payload = GoogleIdTokenPayload.Parse(token);

            Assert.AreEqual("first-client", payload.Audience);
        }

        [TestMethod]
        public void Parse_email_verified為字串true也視為已驗證()
        {
            var token = CreateToken("{\"email_verified\":\"true\"}");

            Assert.IsTrue(GoogleIdTokenPayload.Parse(token).EmailVerified);
        }

        [TestMethod]
        public void Parse_email_verified缺漏或為false時為未驗證()
        {
            Assert.IsFalse(GoogleIdTokenPayload.Parse(CreateToken("{}")).EmailVerified);
            Assert.IsFalse(GoogleIdTokenPayload.Parse(CreateToken("{\"email_verified\":false}")).EmailVerified);
            Assert.IsFalse(GoogleIdTokenPayload.Parse(CreateToken("{\"email_verified\":\"false\"}")).EmailVerified);
        }

        [TestMethod]
        public void Parse_時間宣告以字串傳回時也能解析()
        {
            var token = CreateToken("{\"iat\":\"1725000000\",\"exp\":\"1725003600\"}");

            var payload = GoogleIdTokenPayload.Parse(token);

            Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1725000000), payload.IssuedAt);
            Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1725003600), payload.ExpiresAt);
        }

        [TestMethod]
        public void Parse_缺少時間宣告時為null()
        {
            var payload = GoogleIdTokenPayload.Parse(CreateToken("{\"sub\":\"1\"}"));

            Assert.IsNull(payload.IssuedAt);
            Assert.IsNull(payload.ExpiresAt);
        }

        [TestMethod]
        public void Parse_不是JWT_拋出FormatException()
        {
            Assert.ThrowsExactly<FormatException>(() => GoogleIdTokenPayload.Parse("not-a-jwt"));
        }

        [TestMethod]
        public void Parse_酬載不是JSON_拋出FormatException()
        {
            var token = "aGVhZGVy." + Base64Url.Encode(Encoding.UTF8.GetBytes("這不是 JSON")) + ".c2ln";

            Assert.ThrowsExactly<FormatException>(() => GoogleIdTokenPayload.Parse(token));
        }

        [TestMethod]
        public void Parse_酬載不是base64url_拋出FormatException()
        {
            Assert.ThrowsExactly<FormatException>(() => GoogleIdTokenPayload.Parse("aGVhZGVy.@@@@.c2ln"));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void Parse_內容為空_拋出ArgumentException(string idToken)
        {
            Assert.ThrowsExactly<ArgumentException>(() => GoogleIdTokenPayload.Parse(idToken));
        }
    }
}
