using System;
using System.Text;
using Ozakboy.Gmail.Core;
using Ozakboy.Gmail.OAuth;
using Xunit;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// id_token 酬載解析測試。這裡只驗證解碼與宣告讀取,不涉及簽章驗證(本套件刻意不做)。
    /// </summary>
    public class GoogleIdTokenPayloadTests
    {
        private static string CreateToken(string payloadJson)
        {
            var header = Base64Url.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
            var payload = Base64Url.Encode(Encoding.UTF8.GetBytes(payloadJson));
            return header + "." + payload + ".c2lnbmF0dXJl";
        }

        [Fact]
        public void Parse_讀出標準宣告()
        {
            var token = CreateToken(
                "{\"iss\":\"https://accounts.google.com\",\"sub\":\"1234567890\",\"aud\":\"client-id\"," +
                "\"email\":\"user@example.com\",\"email_verified\":true,\"name\":\"測試使用者\"," +
                "\"picture\":\"https://example.com/a.png\",\"hd\":\"example.com\",\"iat\":1725000000,\"exp\":1725003600}");

            var payload = GoogleIdTokenPayload.Parse(token);

            Assert.Equal("1234567890", payload.Subject);
            Assert.Equal("user@example.com", payload.Email);
            Assert.True(payload.EmailVerified);
            Assert.Equal("測試使用者", payload.Name);
            Assert.Equal("https://example.com/a.png", payload.Picture);
            Assert.Equal("example.com", payload.HostedDomain);
            Assert.Equal("https://accounts.google.com", payload.Issuer);
            Assert.Equal("client-id", payload.Audience);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1725000000), payload.IssuedAt);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1725003600), payload.ExpiresAt);
        }

        [Fact]
        public void Parse_aud為陣列時取第一個()
        {
            var token = CreateToken("{\"aud\":[\"first-client\",\"second-client\"]}");

            var payload = GoogleIdTokenPayload.Parse(token);

            Assert.Equal("first-client", payload.Audience);
        }

        [Fact]
        public void Parse_email_verified為字串true也視為已驗證()
        {
            var token = CreateToken("{\"email_verified\":\"true\"}");

            Assert.True(GoogleIdTokenPayload.Parse(token).EmailVerified);
        }

        [Fact]
        public void Parse_email_verified缺漏或為false時為未驗證()
        {
            Assert.False(GoogleIdTokenPayload.Parse(CreateToken("{}")).EmailVerified);
            Assert.False(GoogleIdTokenPayload.Parse(CreateToken("{\"email_verified\":false}")).EmailVerified);
            Assert.False(GoogleIdTokenPayload.Parse(CreateToken("{\"email_verified\":\"false\"}")).EmailVerified);
        }

        [Fact]
        public void Parse_時間宣告以字串傳回時也能解析()
        {
            var token = CreateToken("{\"iat\":\"1725000000\",\"exp\":\"1725003600\"}");

            var payload = GoogleIdTokenPayload.Parse(token);

            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1725000000), payload.IssuedAt);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1725003600), payload.ExpiresAt);
        }

        [Fact]
        public void Parse_缺少時間宣告時為null()
        {
            var payload = GoogleIdTokenPayload.Parse(CreateToken("{\"sub\":\"1\"}"));

            Assert.Null(payload.IssuedAt);
            Assert.Null(payload.ExpiresAt);
        }

        [Fact]
        public void Parse_不是JWT_拋出FormatException()
        {
            Assert.Throws<FormatException>(() => GoogleIdTokenPayload.Parse("not-a-jwt"));
        }

        [Fact]
        public void Parse_酬載不是JSON_拋出FormatException()
        {
            var token = "aGVhZGVy." + Base64Url.Encode(Encoding.UTF8.GetBytes("這不是 JSON")) + ".c2ln";

            Assert.Throws<FormatException>(() => GoogleIdTokenPayload.Parse(token));
        }

        [Fact]
        public void Parse_酬載不是base64url_拋出FormatException()
        {
            Assert.Throws<FormatException>(() => GoogleIdTokenPayload.Parse("aGVhZGVy.@@@@.c2ln"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Parse_內容為空_拋出ArgumentException(string idToken)
        {
            Assert.Throws<ArgumentException>(() => GoogleIdTokenPayload.Parse(idToken));
        }
    }
}
