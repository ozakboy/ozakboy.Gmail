<script setup lang="ts">
// JSON-LD 結構化資料 — 讓 Google 認得這是 SoftwareApplication
// 出現在 rich result / knowledge graph 機率提升
const jsonLd = {
  '@context': 'https://schema.org',
  '@type': 'SoftwareApplication',
  name: 'Ozakboy.Gmail',
  alternateName: ['Ozakboy.Gmail .NET Gmail API Client'],
  applicationCategory: 'DeveloperApplication',
  applicationSubCategory: 'Gmail API Client',
  operatingSystem: 'Cross-platform (.NET Standard 2.0+, .NET 8/9/10)',
  description:
    'A thin, async-only .NET client for the Gmail REST API and the Google OAuth 2.0 token endpoints. Read messages, apply and remove labels, move mail to trash, download attachments and send through messages.send. It does not depend on Google.Apis and never stores tokens — you supply an access token through a callback and keep the storage, encryption and tenant isolation on your side.',
  url: 'https://ozakboy.github.io/ozakboy.Gmail/',
  downloadUrl: 'https://www.nuget.org/packages/Ozakboy.Gmail/',
  image: 'https://ozakboy.github.io/ozakboy.Gmail/logo.png',
  author: {
    '@type': 'Person',
    name: 'ozakboy',
    url: 'https://github.com/ozakboy',
  },
  license: 'https://opensource.org/licenses/MIT',
  programmingLanguage: 'C#',
  offers: {
    '@type': 'Offer',
    price: '0',
    priceCurrency: 'USD',
  },
  softwareVersion: '2.0.0',
}

useHead({
  script: [
    {
      type: 'application/ld+json',
      innerHTML: JSON.stringify(jsonLd),
    },
  ],
})
</script>

<template>
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
</template>
