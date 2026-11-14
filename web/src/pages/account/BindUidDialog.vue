<script setup lang="ts">
import { ref } from 'vue';
import Modal from '../../components/Modal.vue';
import { t } from '../../i18n';
import { post } from '../../http';
import { refreshMe, selectUid, maskUid } from '../../account/session';
import { toast } from '../../components/toast';
import { linkScript } from '../../account/linkScript';
import { importWithLink } from '../../gacha/localGacha';
import { selectLocal } from '../../gacha/source';

// 绑定 UID / 更新抽卡记录：粘贴抽卡链接，后端拉取一次作为所有权证明并导入。链接不保存。
// local 模式（未登录）：后端只拉取并原样返回，记录合并进当前浏览器。
const props = defineProps<{ uid?: string; local?: boolean }>();
const emit = defineEmits<{ close: []; done: [uid: string] }>();
const url = ref('');
const busy = ref(false);
const error = ref('');
const copied = ref(false);

async function submit() {
  busy.value = true;
  error.value = '';
  try {
    if (props.local) {
      const result = await importWithLink(url.value.trim());
      url.value = '';
      selectLocal(result.uid);
      toast(t('Web_ImportDone', result.added), 'success');
      emit('done', result.uid);
      emit('close');
      return;
    }
    const path = props.uid ? `/api/uids/${props.uid}/gacha/refresh` : '/api/uids/verify';
    const result = await post<{ uid: string; added: number; transferred: boolean }>(path, { url: url.value.trim() });
    url.value = '';
    await refreshMe();
    selectUid(result.uid);
    toast(t('Web_ImportDone', result.added), 'success');
    if (result.transferred) toast(t('Web_Transferred'), 'info', 6000);
    emit('done', result.uid);
    emit('close');
  } catch (e) {
    error.value = (e as Error).message;
  } finally {
    busy.value = false;
  }
}

async function copyScript() {
  await navigator.clipboard.writeText(linkScript);
  copied.value = true;
  setTimeout(() => (copied.value = false), 2000);
}
</script>

<template>
  <Modal :title="local ? t('Web_ImportLocal') : uid ? `${t('Web_RefreshGacha')} · ${maskUid(uid)}` : t('Web_BindUid')" :busy="busy" :width="560" @close="emit('close')">
    <form @submit.prevent="submit">
      <p class="caption lead">{{ local ? t('Web_ImportLocalHint') : t('Web_BindUidHint') }}</p>
      <label class="field">
        <span class="field-label">{{ t('Web_GachaLink') }}</span>
        <textarea v-model="url" class="input" rows="3" spellcheck="false" autocomplete="off" autofocus required
          placeholder="https://aki-gm-resources-oversea.aki-game.net/aki/gacha/index.html#/record?…" />
      </label>
      <details class="howto">
        <summary>{{ t('Web_LinkHowTo') }}</summary>
        <p class="caption">{{ t('Web_LinkHowToHint') }}</p>
        <div class="code-box">{{ linkScript }}</div>
        <button type="button" class="btn small" @click="copyScript">{{ copied ? t('Web_Copied') : t('Web_CopyCommand') }}</button>
      </details>
      <p v-if="busy" class="caption">{{ t('Web_Verifying') }}</p>
      <p v-if="error" class="form-error" role="alert">{{ error }}</p>
      <div class="dialog-actions">
        <button type="button" class="btn" :disabled="busy" @click="emit('close')">{{ t('Common_Cancel') }}</button>
        <button type="submit" class="btn primary" :disabled="busy || !url.trim()">{{ local ? t('Web_ImportLocal') : t('Web_Verify') }}</button>
      </div>
    </form>
  </Modal>
</template>

<style scoped>
.lead { margin: 0 0 14px; line-height: 1.7; }
.howto { margin-bottom: 12px; font-size: 13px; }
.howto summary { cursor: pointer; color: var(--accent-text); margin-bottom: 8px; }
.howto p { margin: 0 0 8px; line-height: 1.6; }
.howto .btn { margin-top: 8px; }
.dialog-actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 8px; }
</style>
