<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { t } from '../i18n';
import { sessionState } from '../account/session';
import { gachaSource, selectLocal } from '../gacha/source';
import { localUids } from '../gacha/localGacha';
import { uploadLocal } from '../gacha/upload';
import { toast, toastError } from './toast';
import EmptyState from './EmptyState.vue';
import BindUidDialog from '../pages/account/BindUidDialog.vue';

// 抽卡页面的状态层：有云端 UID 时直接显示；未登录或未绑定时可以导入本地记录（只存当前浏览器）。
defineProps<{ icon?: string }>();
const router = useRouter();
const importing = ref(false);
const uploading = ref(false);

async function upload(uid: string) {
  uploading.value = true;
  try {
    toast(t('Web_UploadDone', await uploadLocal(uid, true)), 'success');
  } catch (e) {
    toastError(e);
  } finally {
    uploading.value = false;
  }
}
</script>

<template>
  <div v-if="sessionState === 'unknown'" class="card"><span class="caption">{{ t('Web_Loading') }}</span></div>
  <EmptyState v-else-if="!gachaSource" :title="t('Web_ImportLocal')" :hint="t('Web_ImportLocalHint')" :icon="icon">
    <div class="actions">
      <button class="btn primary" @click="importing = true">{{ t('Web_ImportLocal') }}</button>
      <button class="btn" @click="router.push('/account')">{{ sessionState === 'signed-in' ? t('Web_GoAccount') : t('Web_SignInToSync') }}</button>
    </div>
  </EmptyState>
  <template v-else>
    <!-- 本地数据：提示只在此浏览器，并提供切换、上传或登录 -->
    <div v-if="gachaSource.kind === 'local'" class="local-bar">
      <span class="tag accent">{{ t('Web_LocalBadge') }}</span>
      <select v-if="localUids.length > 1" class="chip select" :value="gachaSource.uid" @change="selectLocal(($event.target as HTMLSelectElement).value)">
        <option v-for="u in localUids" :key="u.uid" :value="u.uid">UID {{ u.uid }}</option>
      </select>
      <span v-else class="caption">UID {{ gachaSource.uid }}</span>
      <span class="spacer" />
      <button v-if="sessionState === 'signed-in'" class="btn small" :disabled="uploading" @click="upload(gachaSource.uid)">{{ t('Web_UploadCloud') }}</button>
      <button v-else class="btn small" @click="router.push('/account')">{{ t('Web_SignInToSync') }}</button>
    </div>
    <slot :source="gachaSource" :import-local="() => (importing = true)" />
  </template>
  <BindUidDialog v-if="importing" local @close="importing = false" />
</template>

<style scoped>
.actions { display: flex; gap: 10px; justify-content: center; }
.local-bar { display: flex; align-items: center; gap: 10px; margin-bottom: 14px; padding: 8px 14px; border-radius: var(--radius-control); background: var(--surface); border: 1px solid var(--border); }
.spacer { flex: 1; }
</style>
