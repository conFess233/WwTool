<script setup lang="ts">
import { onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { t } from '../i18n';
import AccountGate from './AccountGate.vue';
import EmptyState from './EmptyState.vue';
import { profile, profileState, loadProfile } from '../game/profile';

// 需要账号资料的页面（首页、探索、摩托）：在 AccountGate 之上再判断是否已同步资料。
defineProps<{ icon?: string }>();
const router = useRouter();
onMounted(loadProfile);
</script>

<template>
  <AccountGate :icon="icon">
    <div v-if="profileState === 'loading' || profileState === 'idle'" class="card"><span class="caption">{{ t('Web_Loading') }}</span></div>
    <EmptyState v-else-if="profileState === 'failed'" danger :title="t('Web_LoadFailed')" :icon="icon">
      <button class="btn" @click="loadProfile">{{ t('Common_Retry') }}</button>
    </EmptyState>
    <template v-else-if="!profile?.hasProfile || !profile.snapshot">
      <slot name="empty" />
      <EmptyState :title="t('Web_NeedProfileTitle')" :hint="t('Web_NeedProfileHint')" :icon="icon">
        <button class="btn primary" @click="router.push('/account')">{{ t('Web_GoAccount') }}</button>
      </EmptyState>
    </template>
    <slot v-else :profile="profile" :snapshot="profile.snapshot" />
  </AccountGate>
</template>
