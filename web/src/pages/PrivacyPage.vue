<script setup lang="ts">
import { t } from '../i18n';
</script>

<template>
  <div class="stagger">
    <section class="card privacy" style="--i: 0">
      <h2 class="section-title">{{ t('Web_Privacy') }}</h2>
      <p>{{ t('Web_PrivacyBody1') }}</p>
      <p>{{ t('Web_PrivacyBody2') }}</p>
      <p>{{ t('Web_PrivacyBody3') }}</p>
      <p class="caption">{{ t('Web_Disclaimer') }}</p>
    </section>
  </div>
</template>

<style scoped>
.privacy { max-width: 760px; line-height: 1.8; }
.privacy p { margin: 12px 0 0; color: var(--text-secondary); }
</style>
