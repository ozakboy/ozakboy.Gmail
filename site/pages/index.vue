<script setup>
useHead({ title: 'Ozakboy.Gmail — Gmail REST and OAuth for .NET' })
const localePath = useLocalePath()

const features = [
  { key: 'noGoogleApis', icon: '🔑' },
  { key: 'oneScope', icon: '📮' },
  { key: 'errors', icon: '🧭' },
  { key: 'asyncOnly', icon: '⚡' },
  { key: 'targets', icon: '📦' },
  { key: 'narrow', icon: '✂️' },
]
</script>

<template>
  <div>
    <!-- Hero -->
    <section class="bg-gradient-to-br from-brand-900 via-brand-700 to-brand-500 text-white">
      <div class="max-w-6xl mx-auto px-4 py-20 sm:py-28 text-center">
        <h1 class="text-5xl sm:text-7xl font-extrabold tracking-tight">
          {{ $t('home.hero.title') }}
        </h1>
        <p class="mt-5 text-xl sm:text-2xl font-semibold text-white">
          {{ $t('home.hero.tagline') }}
        </p>
        <p class="mt-5 text-base sm:text-lg text-brand-50 max-w-3xl mx-auto leading-relaxed">
          {{ $t('home.hero.subtitle') }}
        </p>
        <p class="mt-8 text-brand-100 tracking-[0.6em]" aria-hidden="true">🔑 · · · ✉</p>
        <div class="mt-8 flex justify-center gap-3 flex-wrap">
          <NuxtLink
            :to="localePath('/docs/getting-started')"
            class="bg-white text-brand-900 px-6 py-3 rounded font-semibold hover:bg-brand-50 transition"
          >
            {{ $t('home.hero.install') }}
          </NuxtLink>
          <a
            href="https://github.com/ozakboy/ozakboy.Gmail"
            target="_blank"
            rel="noopener"
            class="bg-brand-900/40 text-white px-6 py-3 rounded font-semibold hover:bg-brand-900/60 border border-white/30 transition"
          >
            {{ $t('home.hero.github') }}
          </a>
        </div>
      </div>
    </section>

    <!-- Features -->
    <section class="max-w-6xl mx-auto px-4 py-16 sm:py-20">
      <h2 class="text-3xl sm:text-4xl font-bold text-center mb-12">
        {{ $t('home.features.title') }}
      </h2>
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
        <div
          v-for="f in features"
          :key="f.key"
          class="bg-white border border-slate-200 rounded-lg p-6 hover:shadow-lg hover:border-brand-500 transition"
        >
          <div class="text-3xl mb-3">{{ f.icon }}</div>
          <h3 class="font-bold text-lg mb-2">
            {{ $t(`home.features.items.${f.key}.title`) }}
          </h3>
          <p class="text-slate-600 text-sm leading-relaxed">
            {{ $t(`home.features.items.${f.key}.desc`) }}
          </p>
        </div>
      </div>
    </section>

    <!-- Quick Start -->
    <section class="bg-white py-16 sm:py-20 border-t border-slate-200">
      <div class="max-w-4xl mx-auto px-4">
        <h2 class="text-3xl sm:text-4xl font-bold text-center mb-12">
          {{ $t('home.quickstart.title') }}
        </h2>
        <div class="space-y-8">
          <div>
            <h3 class="font-semibold text-lg mb-3">{{ $t('home.quickstart.step1') }}</h3>
            <pre class="bg-slate-900 text-slate-100 rounded p-4 text-sm overflow-x-auto"><code>dotnet add package Ozakboy.Gmail</code></pre>
          </div>
          <div>
            <h3 class="font-semibold text-lg mb-3">{{ $t('home.quickstart.step2') }}</h3>
            <pre class="bg-slate-900 text-slate-100 rounded p-4 text-sm overflow-x-auto"><code>{
  "GoogleOAuth": {
    "ClientId": "&lt;your-client-id&gt;.apps.googleusercontent.com",
    "ClientSecret": "&lt;your-client-secret&gt;"
  }
}</code></pre>
          </div>
          <div>
            <h3 class="font-semibold text-lg mb-3">{{ $t('home.quickstart.step3') }}</h3>
            <pre class="bg-slate-900 text-slate-100 rounded p-4 text-sm overflow-x-auto"><code>using Ozakboy.Gmail;
using Ozakboy.Gmail.OAuth;

var oauth = new GoogleOAuthClient(httpClient, GoogleOAuthOptions.FromConfiguration(configuration));

// 導使用者到這個網址同意授權 / send the user here to consent
var authorizeUrl = oauth.BuildAuthorizationUrl(
    "https://example.com/oauth/callback",
    new[] { GmailScopes.GmailModify },
    state);

// callback 拿 code 換 token,refresh token 自己加密存 DB / you store the refresh token
var token = await oauth.ExchangeCodeAsync(code, "https://example.com/oauth/callback", cancellationToken);

// client 只跟你要一個 access token / the client only ever asks for an access token
var gmail = new GmailClient(httpClient, ct => Task.FromResult(accessToken));

var profile = await gmail.GetProfileAsync(cancellationToken);
Console.WriteLine(profile.EmailAddress);   // 印出信箱地址就算接通了 / it works

var list = await gmail.ListMessagesAsync("newer_than:30d", maxResults: 100, cancellationToken: cancellationToken);

foreach (var item in list.Messages)
{
    var message = await gmail.GetMessageAsync(
        item.Id,
        GmailMessageFormat.Metadata,
        new[] { "From", "Subject", "List-Unsubscribe" },
        cancellationToken);

    if (message.GetHeader("List-Unsubscribe") != null)
        await gmail.TrashAsync(item.Id, cancellationToken);   // 30 天內救得回來 / recoverable for 30 days
}

// 增量同步的錯誤分支就是兩個旗標 / two flags cover the sync loop
try
{
    var history = await gmail.ListHistoryAsync(savedHistoryId, cancellationToken: cancellationToken);
    // ... 依 history.History 更新自己這邊的資料
}
catch (GmailApiException ex) when (ex.IsHistoryExpired)
{
    // 歷史太舊被 Gmail 丟了:改用時間區間重掃 / rescan by time window
}
catch (GmailApiException ex) when (ex.IsUnauthorized)
{
    // refresh token 死了:標記帳號待重新授權 / stop until the user re-consents
}</code></pre>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>
