// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-01-01',
  devtools: { enabled: true },

  // Nuxt site config (used by sitemap module to prepend absolute URL)
  // 注意:url 只能放 domain,baseURL 在 app.baseURL 處理(GitHub Pages project site 規則)
  site: {
    url: 'https://ozakboy.github.io',
    name: 'Ozakboy.Gmail',
    description:
      'Gmail REST and Google OAuth in one thin async client for .NET. Read mail, apply labels, move to trash and send through messages.send — no Google.Apis dependency, and the package never stores your tokens.',
  },

  app: {
    baseURL: '/ozakboy.Gmail/',
    head: {
      title: 'Ozakboy.Gmail — Gmail REST and OAuth for .NET',
      htmlAttrs: { lang: 'zh-TW' },
      meta: [
        { charset: 'utf-8' },
        { name: 'viewport', content: 'width=device-width, initial-scale=1' },
        {
          name: 'description',
          content:
            'Ozakboy.Gmail — read Gmail, apply labels, move mail to trash and send from a .NET server. Async-only client over the Gmail REST API and Google OAuth 2.0. You keep the tokens; it keeps the HTTP.',
        },
        {
          name: 'keywords',
          content:
            'Ozakboy.Gmail, Gmail API, .NET Gmail client, csharp gmail, Google OAuth, gmail.modify, messages.send, MimeKit, dotnet, NuGet',
        },
        { name: 'author', content: 'ozakboy' },

        // Search engine verification
        {
          name: 'google-site-verification',
          content: '7B6Z2O-JFfFD6I0jeayWg1SFeDWKZmf4RwSVGbQHmVk',
        },
        { name: 'msvalidate.01', content: '4928B4223346F74DB53D9754C37164AB' },

        // Open Graph (Facebook / LinkedIn / general social)
        { property: 'og:type', content: 'website' },
        { property: 'og:site_name', content: 'Ozakboy.Gmail' },
        { property: 'og:title', content: 'Ozakboy.Gmail — Gmail REST and OAuth for .NET' },
        {
          property: 'og:description',
          content:
            'Gmail REST and Google OAuth in one thin async client. You keep the tokens; it keeps the HTTP. Sending goes through messages.send, so the gmail.modify scope is enough.',
        },
        { property: 'og:url', content: 'https://ozakboy.github.io/ozakboy.Gmail/' },
        { property: 'og:image', content: 'https://ozakboy.github.io/ozakboy.Gmail/logo.png' },
        { property: 'og:image:alt', content: 'Ozakboy.Gmail logo' },
        { property: 'og:locale', content: 'zh_TW' },
        { property: 'og:locale:alternate', content: 'en_US' },

        // Twitter Card
        { name: 'twitter:card', content: 'summary' },
        { name: 'twitter:title', content: 'Ozakboy.Gmail — Gmail REST and OAuth for .NET' },
        {
          name: 'twitter:description',
          content:
            'Gmail REST and Google OAuth in one thin async client. You keep the tokens; it keeps the HTTP.',
        },
        { name: 'twitter:image', content: 'https://ozakboy.github.io/ozakboy.Gmail/logo.png' },
      ],
      link: [
        { rel: 'icon', type: 'image/png', href: '/ozakboy.Gmail/logo.png' },
        { rel: 'apple-touch-icon', href: '/ozakboy.Gmail/logo.png' },
        { rel: 'canonical', href: 'https://ozakboy.github.io/ozakboy.Gmail/' },
      ],
    },
  },

  modules: [
    '@nuxtjs/i18n',
    '@nuxtjs/tailwindcss',
    '@nuxt/content',
    '@nuxtjs/sitemap',
    // @nuxtjs/robots 不適用(Project Pages baseURL 與 robots.txt root 衝突),
    // 改用 site/public/robots.txt 靜態檔
  ],

  // @nuxt/content 預設讀 site/content/。內容由 scripts/sync-docs.mjs 自動從 ../docs/ 同步進來。
  content: {
    build: {
      markdown: {
        toc: { depth: 3 },
        highlight: {
          theme: 'github-light',
        },
      },
    },
  },

  i18n: {
    baseUrl: 'https://ozakboy.github.io',
    locales: [
      { code: 'zh-TW', name: '繁體中文', file: 'zh-TW.json' },
      { code: 'en', name: 'English', file: 'en.json' },
    ],
    defaultLocale: 'zh-TW',
    strategy: 'prefix_except_default',
    langDir: 'locales/',
    detectBrowserLanguage: {
      useCookie: true,
      cookieKey: 'i18n_redirected',
      redirectOn: 'root',
    },
  },

  // Sitemap: 自動產 sitemap.xml,部署後位於 https://ozakboy.github.io/ozakboy.Gmail/sitemap.xml
  // 自動掃描 pages/ 內路由 + i18n locale 變體
  sitemap: {
    autoLastmod: true,
  },

  // GitHub Pages 部署:使用內建 preset,自動產生 404.html / .nojekyll
  nitro: {
    preset: 'github_pages',
  },

  ssr: true,
})
