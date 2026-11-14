<script setup lang="ts">
import { ref } from 'vue';
import Modal from '../../components/Modal.vue';
import { t } from '../../i18n';
import { refreshMe, selectUid } from '../../account/session';
import { loginAndSync } from '../../game/profileSync';
import { toast } from '../../components/toast';

// 添加游戏账号（LoginDialogView）：邮箱密码只在浏览器内直连库洛，提交后立即清空。
const emit = defineEmits<{ close: []; done: [] }>();
const email = ref('');
const password = ref('');
const remember = ref(true);
const busy = ref(false);
const error = ref('');
const captcha = ref(false);

async function submit() {
  busy.value = true;
  error.value = '';
  captcha.value = false;
  const pwd = password.value;
  password.value = ''; // 读出后立即清空输入框
  try {
    const { synced, transferred, rolesFailed } = await loginAndSync(email.value.trim(), pwd, remember.value, () => (captcha.value = true));
    await refreshMe();
    if (synced.length) {
      selectUid(synced[0]);
      toast(t('Web_GameLinked', synced.length), 'success');
    }
    if (transferred.length) toast(t('Web_Transferred'), 'info', 6000);
    if (rolesFailed) toast(t('Web_RolesSyncFailed'), 'error', 6000);
    emit('done');
    emit('close');
  } catch (e) {
    error.value = (e as Error).message;
  } finally {
    busy.value = false;
    captcha.value = false;
  }
}
</script>

<template>
  <Modal :title="t('Web_GameLogin')" :busy="busy" :width="460" @close="emit('close')">
    <form autocomplete="off" @submit.prevent="submit">
      <p class="note">{{ t('Web_GameLoginHint') }}</p>
      <p class="caption global">{{ t('Web_GlobalOnly') }}</p>
      <label class="field">
        <span class="field-label">{{ t('Login_Email') }}</span>
        <input v-model="email" class="input" type="email" autocomplete="username" autofocus required />
      </label>
      <label class="field">
        <span class="field-label">{{ t('Login_Password') }}</span>
        <input v-model="password" class="input" type="password" autocomplete="current-password" required />
      </label>
      <label class="check">
        <input v-model="remember" type="checkbox" />
        <span>{{ t('Web_RememberOauth') }}<br /><span class="caption">{{ t('Web_RememberOauthHint') }}</span></span>
      </label>
      <p v-if="captcha" class="caption captcha">{{ t('Web_CaptchaHint') }}</p>
      <p v-if="busy && !captcha" class="caption captcha">{{ t('Web_ProfileSyncing') }}</p>
      <p v-if="error" class="form-error" role="alert">{{ error }}</p>
      <div class="dialog-actions">
        <button type="button" class="btn" :disabled="busy" @click="emit('close')">{{ t('Common_Cancel') }}</button>
        <button type="submit" class="btn primary" :disabled="busy || !email.trim()">{{ t('Web_GameLoginButton') }}</button>
      </div>
    </form>
  </Modal>
</template>

<style scoped>
.note { margin: 0 0 6px; padding: 10px 12px; border-radius: var(--radius-control); background: var(--accent-container); color: var(--accent-text); font-size: 12.5px; line-height: 1.7; }
.global { margin: 0 0 14px; }
.captcha { margin: 12px 0 0; }
.dialog-actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 18px; }
</style>
