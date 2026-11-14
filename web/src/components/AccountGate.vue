<script setup lang="ts">
import { useRouter } from 'vue-router';
import { t } from '../i18n';
import { sessionState, currentUid } from '../account/session';
import SignInCard from '../pages/account/SignInCard.vue';
import EmptyState from './EmptyState.vue';

// 需要站点账号与 UID 的页面统一的状态层：未登录 → 登录卡；未绑定 UID → 空状态与下一步。
defineProps<{ icon?: string; hint?: string }>();
const router = useRouter();
</script>

<template>
  <div v-if="sessionState === 'unknown'" class="card"><span class="caption">{{ t('Web_Loading') }}</span></div>
  <SignInCard v-else-if="sessionState === 'signed-out'" compact />
  <EmptyState v-else-if="!currentUid" :title="t('Web_NeedUidTitle')" :hint="hint ?? t('Web_NeedUidHint')" :icon="icon">
    <button class="btn primary" @click="router.push('/account')">{{ t('Web_GoAccount') }}</button>
  </EmptyState>
  <slot v-else :uid="currentUid" />
</template>
