<script setup lang="ts">
import { computed } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { t } from '../i18n';
import { sessionState, currentUid, maskUid } from '../account/session';
import { iconUrl, iconFallback } from '../format';

// 账号胶囊（MainWindow.axaml 的 AccountCapsule）：头像、昵称、等级；切换账号时昵称上滚换新。
const router = useRouter();
const route = useRoute();
const on = computed(() => route.name === 'account');
const name = computed(() => (currentUid.value ? currentUid.value.name || maskUid(currentUid.value.uid) : ''));
</script>

<template>
  <button class="capsule" :class="{ on }" :aria-label="t('Nav_AccountData')" @click="router.push('/account')">
    <span class="avatar">
      <img v-if="currentUid?.headPhoto" :key="currentUid.headPhoto" :src="iconUrl(currentUid.headPhoto)" alt="" @error="iconFallback" />
      <img v-else src="/icons/Account.png" alt="" class="placeholder" />
    </span>
    <span class="roll">
      <Transition name="roll">
        <span v-if="sessionState === 'signed-in' && currentUid" :key="currentUid.uid" class="text">{{ name }}</span>
        <span v-else-if="sessionState === 'signed-in'" key="none" class="text muted">{{ t('Account_None') }}</span>
        <span v-else key="out" class="text muted">{{ t('Web_SignIn') }}</span>
      </Transition>
    </span>
    <span v-if="currentUid?.level" class="level">LV.{{ currentUid.level }}</span>
    <svg class="chevron" viewBox="0 0 24 24" aria-hidden="true"><path d="M6 9l6 6 6-6" /></svg>
  </button>
</template>

<style scoped>
.capsule {
  justify-self: end; display: flex; align-items: center; gap: 10px; height: 44px; padding: 0 14px 0 6px; border-radius: 999px; cursor: pointer;
  background: var(--surface); border: 1px solid var(--border); box-shadow: var(--shadow-pill);
  transition: border-color var(--motion-fast) var(--ease-standard), background-color var(--motion-fast) var(--ease-standard);
}
.capsule:hover, .capsule.on { border-color: var(--accent-line); }
.capsule.on { background: var(--accent-container); }
.avatar { width: 32px; height: 32px; border-radius: 50%; overflow: hidden; background: #efe7d8; flex: none; }
.avatar img { width: 100%; height: 100%; object-fit: cover; }
.avatar .placeholder { opacity: 0.6; padding: 6px; }
.roll { display: grid; overflow: hidden; height: 20px; max-width: 160px; }
.roll > * { grid-area: 1 / 1; }
.text { font-size: 13px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; line-height: 20px; }
.muted { color: var(--text-muted); }
.level { font-size: 13px; font-weight: 600; color: var(--accent-text); }
.chevron { width: 14px; height: 14px; fill: none; stroke: var(--text-muted); stroke-width: 2.4; stroke-linecap: round; stroke-linejoin: round; }
.roll-enter-active, .roll-leave-active { transition: transform 480ms var(--ease-standard), opacity 480ms var(--ease-standard); }
.roll-enter-from { transform: translateY(100%); opacity: 0; }
.roll-leave-to { transform: translateY(-100%); opacity: 0; }
</style>
