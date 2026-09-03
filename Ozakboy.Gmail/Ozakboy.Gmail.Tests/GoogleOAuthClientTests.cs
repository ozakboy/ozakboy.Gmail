using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Ozakboy.Gmail.OAuth;
using Ozakboy.Gmail.Tests.TestSupport;
using Xunit;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// Google OAuth 用戶端測試:授權網址組裝、權杖交換與更新、撤銷,以及錯誤映射。
    /// </summary>
    public class GoogleOAuthClientTests
    {
        private const string ClientId = "client-id.apps.googleusercontent.com";
        private const string ClientSecret = "client-secret";
        private const string RedirectUri = "https://app.example.com/oauth/callback";
        private const string TokenUrl = "https://oauth2.googleapis.com/token";
        private const string RevokeUrl = "https://oauth2.googleapis.com/revoke";

        private static GoogleOAuthClient CreateClient(RecordingHandler handler)
        {
            return new GoogleOAuthClient(
                handler.CreateClient(),
                new GoogleOAuthOptions { ClientId = ClientId, ClientSecret = ClientSecret },
                GmailTestFactory.NoDelayOptions());
        }

        [Fact]
        public void BuildAuthorizationUrl_預設選項_含offline與consent與漸進式授權()
        {
            var handler = new RecordingHandler();
            var client = CreateClient(handler);

            var url = client.BuildAuthorizationUrl(RedirectUri, new[] { GmailScopes.GmailModify, GmailScopes.OpenId }, "state-123");

            Assert.Equal(
                "https://accounts.google.com/o/oauth2/v2/auth" +
                "?client_id=client-id.apps.googleusercontent.com" +
                "&redirect_uri=https%3A%2F%2Fapp.example.com%2Foauth%2Fcallback" +
                "&response_type=code" +
                "&scope=https%3A%2F%2Fwww.googleapis.com%2Fauth%2Fgmail.modify%20openid" +
                "&state=state-123" +
                "&access_type=offline" +
                "&prompt=consent" +
                "&include_granted_scopes=true",
                url);
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public void BuildAuthorizationUrl_Prompt為null_不送prompt參數()
        {
            var client = CreateClient(new RecordingHandler());

            var url = client.BuildAuthorizationUrl(
                RedirectUri,
                new[] { GmailScopes.Email },
                "s",
                new GoogleAuthorizationUrlOptions { Prompt = null });

            Assert.DoesNotContain("prompt=", url, StringComparison.Ordinal);
            Assert.Contains("access_type=offline", url, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAuthorizationUrl_關閉漸進式授權_不送include_granted_scopes()
        {
            var client = CreateClient(new RecordingHandler());

            var url = client.BuildAuthorizationUrl(
                RedirectUri,
                new[] { GmailScopes.Email },
                "s",
                new GoogleAuthorizationUrlOptions { IncludeGrantedScopes = false });

            Assert.DoesNotContain("include_granted_scopes", url, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAuthorizationUrl_有LoginHint_會被轉義後帶上()
        {
            var client = CreateClient(new RecordingHandler());

            var url = client.BuildAuthorizationUrl(
                RedirectUri,
                new[] { GmailScopes.Email },
                "s",
                new GoogleAuthorizationUrlOptions { LoginHint = "user@example.com" });

            Assert.Contains("login_hint=user%40example.com", url, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void BuildAuthorizationUrl_導回網址為空_拋出ArgumentException(string redirectUri)
        {
            var client = CreateClient(new RecordingHandler());

            Assert.Throws<ArgumentException>(() => client.BuildAuthorizationUrl(redirectUri, new[] { GmailScopes.Email }, "s"));
        }

        [Fact]
        public void BuildAuthorizationUrl_狀態值為空_拋出ArgumentException()
        {
            var client = CreateClient(new RecordingHandler());

            Assert.Throws<ArgumentException>(() => client.BuildAuthorizationUrl(RedirectUri, new[] { GmailScopes.Email }, " "));
        }

        [Fact]
        public void BuildAuthorizationUrl_範圍為null_拋出ArgumentNullException()
        {
            var client = CreateClient(new RecordingHandler());

            Assert.Throws<ArgumentNullException>(() => client.BuildAuthorizationUrl(RedirectUri, null, "s"));
        }

        [Fact]
        public void BuildAuthorizationUrl_範圍為空序列_拋出ArgumentException()
        {
            var client = CreateClient(new RecordingHandler());

            Assert.Throws<ArgumentException>(() => client.BuildAuthorizationUrl(RedirectUri, Array.Empty<string>(), "s"));
        }

        [Fact]
        public async Task ExchangeCodeAsync_送出授權碼表單並解析回應()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(
                "{\"access_token\":\"ya29.token\",\"expires_in\":3599,\"refresh_token\":\"1//refresh\"," +
                "\"scope\":\"https://www.googleapis.com/auth/gmail.modify openid\",\"token_type\":\"Bearer\",\"id_token\":\"a.b.c\"}");
            var client = CreateClient(handler);

            var before = DateTimeOffset.UtcNow;
            var token = await client.ExchangeCodeAsync("auth-code", RedirectUri);

            Assert.Equal("POST", handler.LastRequest.Method);
            Assert.Equal(TokenUrl, handler.LastRequest.Url);
            Assert.Null(handler.LastRequest.Authorization);
            Assert.Contains("grant_type=authorization_code", handler.LastRequest.Body, StringComparison.Ordinal);
            Assert.Contains("code=auth-code", handler.LastRequest.Body, StringComparison.Ordinal);
            Assert.Contains("client_secret=client-secret", handler.LastRequest.Body, StringComparison.Ordinal);
            Assert.Contains("redirect_uri=https%3A%2F%2Fapp.example.com%2Foauth%2Fcallback", handler.LastRequest.Body, StringComparison.Ordinal);

            Assert.Equal("ya29.token", token.AccessToken);
            Assert.Equal("1//refresh", token.RefreshToken);
            Assert.Equal(3599, token.ExpiresIn);
            Assert.Equal("Bearer", token.TokenType);
            Assert.Equal("a.b.c", token.IdToken);
            Assert.True(token.IssuedAt >= before);
            Assert.Equal(token.IssuedAt.AddSeconds(3599), token.ExpiresAt);
            Assert.Equal(new[] { "https://www.googleapis.com/auth/gmail.modify", "openid" }, token.GetScopes());
        }

        [Fact]
        public async Task RefreshAsync_送出更新權杖表單且回應的RefreshToken為null()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"access_token\":\"ya29.new\",\"expires_in\":3599,\"scope\":\"openid\",\"token_type\":\"Bearer\"}");
            var client = CreateClient(handler);

            var token = await client.RefreshAsync("1//refresh");

            Assert.Equal(TokenUrl, handler.LastRequest.Url);
            Assert.Contains("grant_type=refresh_token", handler.LastRequest.Body, StringComparison.Ordinal);
            Assert.Contains("refresh_token=1%2F%2Frefresh", handler.LastRequest.Body, StringComparison.Ordinal);
            Assert.Equal("ya29.new", token.AccessToken);
            Assert.Null(token.RefreshToken);
        }

        [Fact]
        public async Task RevokeAsync_送出token表單()
        {
            var handler = new RecordingHandler();
            handler.EnqueueEmpty(HttpStatusCode.OK);
            var client = CreateClient(handler);

            await client.RevokeAsync("ya29.token");

            Assert.Equal("POST", handler.LastRequest.Method);
            Assert.Equal(RevokeUrl, handler.LastRequest.Url);
            Assert.Equal("token=ya29.token", handler.LastRequest.Body);
        }

        [Fact]
        public async Task RevokeAsync_已失效的權杖_400invalid_token會拋出且IsUnauthorized為真()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.BadRequest, "{\"error\":\"invalid_token\",\"error_description\":\"Token expired or revoked\"}");
            var client = CreateClient(handler);

            var exception = await Assert.ThrowsAsync<GmailApiException>(() => client.RevokeAsync("ya29.token"));

            Assert.Equal(400, exception.StatusCode);
            Assert.Equal("invalid_token", exception.Reason);
            Assert.Equal("Token expired or revoked", exception.ErrorMessage);
            Assert.True(exception.IsUnauthorized);
            Assert.Equal(1, handler.RequestCount);
        }

        [Fact]
        public async Task RefreshAsync_invalid_grant_IsUnauthorized為真()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\",\"error_description\":\"Token has been expired or revoked.\"}");
            var client = CreateClient(handler);

            var exception = await Assert.ThrowsAsync<GmailApiException>(() => client.RefreshAsync("1//dead"));

            Assert.True(exception.IsUnauthorized);
            Assert.Equal("invalid_grant", exception.Reason);
        }

        [Fact]
        public async Task 權杖端點的5xx也會重試()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(HttpStatusCode.InternalServerError, "{}");
            handler.EnqueueJson("{\"access_token\":\"ya29.new\",\"expires_in\":10}");
            var client = CreateClient(handler);

            var token = await client.RefreshAsync("1//refresh");

            Assert.Equal(2, handler.RequestCount);
            Assert.Equal("ya29.new", token.AccessToken);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public async Task ExchangeCodeAsync_授權碼為空_拋出ArgumentException(string code)
        {
            var handler = new RecordingHandler();
            var client = CreateClient(handler);

            await Assert.ThrowsAsync<ArgumentException>(() => client.ExchangeCodeAsync(code, RedirectUri));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task RefreshAsync_更新權杖為空_拋出ArgumentException()
        {
            var handler = new RecordingHandler();
            var client = CreateClient(handler);

            await Assert.ThrowsAsync<ArgumentException>(() => client.RefreshAsync(null));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task RevokeAsync_權杖為空_拋出ArgumentException()
        {
            var handler = new RecordingHandler();
            var client = CreateClient(handler);

            await Assert.ThrowsAsync<ArgumentException>(() => client.RevokeAsync(""));
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public void 建構子_ClientId為空_拋出ArgumentException()
        {
            using var httpClient = new HttpClient();

            Assert.Throws<ArgumentException>(() => new GoogleOAuthClient(httpClient, new GoogleOAuthOptions { ClientSecret = ClientSecret }));
        }

        [Fact]
        public void 建構子_ClientSecret為空_拋出ArgumentException()
        {
            using var httpClient = new HttpClient();

            Assert.Throws<ArgumentException>(() => new GoogleOAuthClient(httpClient, new GoogleOAuthOptions { ClientId = ClientId }));
        }

        [Fact]
        public void 建構子_options為null_拋出ArgumentNullException()
        {
            using var httpClient = new HttpClient();

            Assert.Throws<ArgumentNullException>(() => new GoogleOAuthClient(httpClient, null));
        }

        [Fact]
        public void 建構子_MaxRetries為負值_拋出ArgumentOutOfRangeException()
        {
            using var httpClient = new HttpClient();
            var options = new GoogleOAuthOptions { ClientId = ClientId, ClientSecret = ClientSecret };

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GoogleOAuthClient(httpClient, options, new GmailClientOptions { MaxRetries = -1 }));
        }

        [Fact]
        public void FromConfiguration_綁定預設區段()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "GoogleOAuth:ClientId", ClientId },
                    { "GoogleOAuth:ClientSecret", ClientSecret },
                })
                .Build();

            var options = GoogleOAuthOptions.FromConfiguration(configuration);

            Assert.Equal(ClientId, options.ClientId);
            Assert.Equal(ClientSecret, options.ClientSecret);
        }

        [Fact]
        public void FromConfiguration_綁定自訂區段()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Google:ClientId", ClientId },
                    { "Google:ClientSecret", ClientSecret },
                })
                .Build();

            var options = GoogleOAuthOptions.FromConfiguration(configuration, "Google");

            Assert.Equal(ClientId, options.ClientId);
        }

        [Fact]
        public void FromConfiguration_區段不存在_回傳空設定()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()).Build();

            var options = GoogleOAuthOptions.FromConfiguration(configuration);

            Assert.Equal(string.Empty, options.ClientId);
            Assert.Equal(string.Empty, options.ClientSecret);
        }

        [Fact]
        public void FromConfiguration_組態為null_拋出ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => GoogleOAuthOptions.FromConfiguration(null));
        }

        [Fact]
        public void GetScopes_Scope為null時回傳空陣列()
        {
            Assert.Empty(new GoogleTokenResponse().GetScopes());
        }

        [Fact]
        public void GetScopes_忽略多餘空白()
        {
            var response = new GoogleTokenResponse { Scope = "openid  email " };

            Assert.Equal(new[] { "openid", "email" }, response.GetScopes());
        }
    }
}
