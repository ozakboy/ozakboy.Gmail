using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ozakboy.Gmail.OAuth;
using Ozakboy.Gmail.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// GoogleAccessTokenProvider 的測試:快取、提前續期、並發收斂、回呼與失敗處理。
    /// 權杖端點一律由 RecordingHandler 假造,不會連到 Google。
    /// </summary>
    [TestClass]
    public class GoogleAccessTokenProviderTests
    {
        private const string ClientId = "client-id.apps.googleusercontent.com";
        private const string ClientSecret = "client-secret";
        private const string RefreshToken = "refresh-token";

        [TestMethod]
        public async Task GetAccessTokenAsync_沒有快取_會呼叫權杖端點續期()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(TokenJson("at-1", 3600));
            var provider = CreateProvider(handler);

            var token = await provider.GetAccessTokenAsync();

            Assert.AreEqual("at-1", token);
            Assert.AreEqual(1, handler.RequestCount);
            Assert.AreEqual("https://oauth2.googleapis.com/token", handler.LastRequest.Url);
            Assert.AreEqual("at-1", provider.CurrentAccessToken);
            Assert.IsNotNull(provider.ExpiresAt);
        }

        [TestMethod]
        public async Task GetAccessTokenAsync_第二次呼叫_直接用快取不再打權杖端點()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(TokenJson("at-1", 3600));
            var provider = CreateProvider(handler);

            await provider.GetAccessTokenAsync();
            var second = await provider.GetAccessTokenAsync();

            Assert.AreEqual("at-1", second);
            Assert.AreEqual(1, handler.RequestCount);
        }

        [TestMethod]
        public async Task GetAccessTokenAsync_快取已進入緩衝區間_提前續期()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(TokenJson("at-new", 3600));

            // 還有 1 分鐘到期,但緩衝設 2 分鐘,應該視為要續期
            var provider = CreateProvider(handler, new GoogleAccessTokenProviderOptions
            {
                InitialAccessToken = "at-old",
                InitialExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1),
                RefreshSkew = TimeSpan.FromMinutes(2),
            });

            var token = await provider.GetAccessTokenAsync();

            Assert.AreEqual("at-new", token);
            Assert.AreEqual(1, handler.RequestCount);
        }

        [TestMethod]
        public async Task GetAccessTokenAsync_初始權杖尚未到期_完全不送請求()
        {
            var handler = new RecordingHandler();
            var provider = CreateProvider(handler, new GoogleAccessTokenProviderOptions
            {
                InitialAccessToken = "at-initial",
                InitialExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            });

            var token = await provider.GetAccessTokenAsync();

            Assert.AreEqual("at-initial", token);
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public async Task GetAccessTokenAsync_只給初始權杖沒給到期時間_視同沒有快取()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(TokenJson("at-1", 3600));
            var provider = CreateProvider(handler, new GoogleAccessTokenProviderOptions
            {
                InitialAccessToken = "at-initial",
            });

            Assert.IsNull(provider.CurrentAccessToken);
            Assert.AreEqual("at-1", await provider.GetAccessTokenAsync());
            Assert.AreEqual(1, handler.RequestCount);
        }

        [TestMethod]
        public async Task GetAccessTokenAsync_只給到期時間沒給權杖_視同沒有快取()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(TokenJson("at-1", 3600));
            var provider = CreateProvider(handler, new GoogleAccessTokenProviderOptions
            {
                InitialExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            });

            Assert.IsNull(provider.ExpiresAt);
            Assert.AreEqual("at-1", await provider.GetAccessTokenAsync());
            Assert.AreEqual(1, handler.RequestCount);
        }

        [TestMethod]
        public async Task GetAccessTokenAsync_二十個並發呼叫_只續期一次()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(TokenJson("at-1", 3600));
            var provider = CreateProvider(handler);

            var tasks = new List<Task<string>>();
            for (var i = 0; i < 20; i++)
                tasks.Add(Task.Run(() => provider.GetAccessTokenAsync()));

            var tokens = await Task.WhenAll(tasks);

            Assert.AreEqual(1, handler.RequestCount);
            foreach (var token in tokens)
            {
                Assert.AreEqual("at-1", token);
            }
        }

        [TestMethod]
        public async Task GetAccessTokenAsync_續期成功_OnRefreshed收到權杖回應()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(TokenJson("at-1", 1800));

            GoogleTokenResponse received = null;
            var provider = CreateProvider(handler, new GoogleAccessTokenProviderOptions
            {
                OnRefreshed = (response, _) =>
                {
                    received = response;
                    return Task.CompletedTask;
                },
            });

            await provider.GetAccessTokenAsync();

            Assert.IsNotNull(received);
            Assert.AreEqual("at-1", received.AccessToken);
            Assert.AreEqual(1800, received.ExpiresIn);
        }

        [TestMethod]
        public async Task Invalidate_清掉快取後強制續期()
        {
            var handler = new RecordingHandler { RepeatLastResponse = false };
            handler.EnqueueJson(TokenJson("at-1", 3600));
            handler.EnqueueJson(TokenJson("at-2", 3600));
            var provider = CreateProvider(handler);

            Assert.AreEqual("at-1", await provider.GetAccessTokenAsync());

            provider.Invalidate();
            Assert.IsNull(provider.CurrentAccessToken);

            Assert.AreEqual("at-2", await provider.GetAccessTokenAsync());
            Assert.AreEqual(2, handler.RequestCount);
        }

        [TestMethod]
        public async Task GetAccessTokenAsync_續期失敗_原樣拋出且快取不變()
        {
            var handler = new RecordingHandler();
            handler.EnqueueError(
                HttpStatusCode.BadRequest,
                "{\"error\":\"invalid_grant\",\"error_description\":\"Token has been expired or revoked.\"}");

            var expired = DateTimeOffset.UtcNow.AddMinutes(-5);
            var provider = CreateProvider(handler, new GoogleAccessTokenProviderOptions
            {
                InitialAccessToken = "at-old",
                InitialExpiresAt = expired,
            });

            var exception = await Assert.ThrowsExactlyAsync<GmailApiException>(() => provider.GetAccessTokenAsync());

            Assert.IsTrue(exception.IsUnauthorized);
            Assert.AreEqual("invalid_grant", exception.Reason);
            Assert.AreEqual("at-old", provider.CurrentAccessToken);
            Assert.AreEqual(expired, provider.ExpiresAt);
        }

        [TestMethod]
        public async Task GetAccessTokenAsync_回應沒有access_token_拋出InvalidOperationException()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson("{\"expires_in\":3600,\"token_type\":\"Bearer\"}");
            var provider = CreateProvider(handler);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => provider.GetAccessTokenAsync());
            Assert.IsNull(provider.CurrentAccessToken);
        }

        [TestMethod]
        public async Task GetAccessTokenAsync_取消權杖已取消_不會送出請求()
        {
            var handler = new RecordingHandler();
            handler.EnqueueJson(TokenJson("at-1", 3600));
            var provider = CreateProvider(handler);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => provider.GetAccessTokenAsync(cancellation.Token));
            Assert.AreEqual(0, handler.RequestCount);
        }

        [TestMethod]
        public void 建構子_OAuth用戶端為null_拋出ArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new GoogleAccessTokenProvider(null, RefreshToken));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void 建構子_更新權杖為空_拋出ArgumentException(string refreshToken)
        {
            var client = CreateOAuthClient(new RecordingHandler());

            Assert.ThrowsExactly<ArgumentException>(() => new GoogleAccessTokenProvider(client, refreshToken));
        }

        [TestMethod]
        public void 建構子_RefreshSkew為負值_拋出ArgumentOutOfRangeException()
        {
            var client = CreateOAuthClient(new RecordingHandler());
            var options = new GoogleAccessTokenProviderOptions { RefreshSkew = TimeSpan.FromSeconds(-1) };

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GoogleAccessTokenProvider(client, RefreshToken, options));
        }

        [TestMethod]
        public async Task GetAccessTokenAsync_可直接當成GmailClient的權杖提供者()
        {
            var tokenHandler = new RecordingHandler();
            tokenHandler.EnqueueJson(TokenJson("at-1", 3600));
            var provider = CreateProvider(tokenHandler);

            var gmailHandler = new RecordingHandler();
            gmailHandler.EnqueueJson("{\"emailAddress\":\"user@example.com\"}");
            var client = GmailTestFactory.CreateClient(gmailHandler, accessTokenProvider: provider.GetAccessTokenAsync);

            await client.GetProfileAsync();

            Assert.AreEqual("Bearer at-1", gmailHandler.LastRequest.Authorization);
        }

        /// <summary>用假 handler 建立 provider。</summary>
        private static GoogleAccessTokenProvider CreateProvider(RecordingHandler handler, GoogleAccessTokenProviderOptions options = null)
        {
            return new GoogleAccessTokenProvider(CreateOAuthClient(handler), RefreshToken, options);
        }

        /// <summary>用假 handler 建立 OAuth 用戶端,重試不等待。</summary>
        private static GoogleOAuthClient CreateOAuthClient(RecordingHandler handler)
        {
            return new GoogleOAuthClient(
                handler.CreateClient(),
                new GoogleOAuthOptions { ClientId = ClientId, ClientSecret = ClientSecret },
                GmailTestFactory.NoDelayOptions(maxRetries: 0));
        }

        /// <summary>組出權杖端點的回應內容。</summary>
        private static string TokenJson(string accessToken, int expiresIn)
        {
            return "{\"access_token\":\"" + accessToken +
                "\",\"expires_in\":" + expiresIn.ToString(CultureInfo.InvariantCulture) +
                ",\"scope\":\"https://www.googleapis.com/auth/gmail.modify\",\"token_type\":\"Bearer\"}";
        }
    }
}
