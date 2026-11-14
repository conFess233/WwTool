<script setup lang="ts">
import { ref, computed } from 'vue';
import Modal from '../../components/Modal.vue';
import { t } from '../../i18n';
import { post } from '../../http';
import { refreshMe } from '../../account/session';
import RecoveryCodeDialog from './RecoveryCodeDialog.vue';

// 未登录时的入口卡片：用户名 + 密码登录 / 注册；忘记密码时用恢复码重设。
defineProps<{ compact?: boolean }>();
type Mode = 'login' | 'register';
const mode = ref<Mode>('login');
const userName = ref('');
const password = ref('');
const confirm = ref('');
const busy = ref(false);
const error = ref('');
const recovering = ref(false);
const recoveryCode = ref('');
const newPassword = ref('');
const issuedCode = ref<string | null>(null);

const mismatch = computed(() => mode.value === 'register' && confirm.value.length > 0 && confirm.value !== password.value);

function switchMode(next: Mode) {
  mode.value = next;
  error.value = '';
  confirm.value = '';
}

async function submit() {
  if (mode.value === 'register' && password.value !== confirm.value) {
    error.value = t('Web_PasswordMismatch');
    return;
  }
  busy.value = true;
  error.value = '';
  try {
    const body = { userName: userName.value.trim(), password: password.value };
    if (mode.value === 'login') {
      await post('/api/auth/login', body);
    } else {
      const result = await post<{ recoveryCode: string }>('/api/auth/register', body);
      issuedCode.value = result.recoveryCode;
    }
    password.value = '';
    confirm.value = '';
    // 注册后先展示恢复码，关闭后再进入账号页
    if (!issuedCode.value) await refreshMe();
  } catch (e) {
    error.value = (e as Error).message;
  } finally {
    busy.value = false;
  }
}

async function recover() {
  busy.value = true;
  error.value = '';
  try {
    const result = await post<{ recoveryCode: string }>('/api/auth/recover', {
      userName: userName.value.trim(), recoveryCode: recoveryCode.value.trim(), newPassword: newPassword.value,
    });
    recovering.value = false;
    recoveryCode.value = '';
    newPassword.value = '';
    issuedCode.value = result.recoveryCode;
  } catch (e) {
    error.value = (e as Error).message;
  } finally {
    busy.value = false;
  }
}

async function codeSaved() {
  issuedCode.value = null;
  await refreshMe();
}
</script>

<template>
  <section class="card sign-in" :class="{ compact }">
    <div class="badge" aria-hidden="true"><span class="icon" /></div>
    <h2 class="card-title">{{ compact ? t('Web_SignInRequiredTitle') : t('Web_SiteAccount') }}</h2>
    <p class="caption hint">{{ compact ? t('Web_SignInRequired') : t('Web_SiteAccountHint') }}</p>

    <div class="modes" role="tablist">
      <button class="chip small" role="tab" :aria-pressed="mode === 'login'" @click="switchMode('login')">{{ t('Web_SignIn') }}</button>
      <button class="chip small" role="tab" :aria-pressed="mode === 'register'" @click="switchMode('register')">{{ t('Web_SignUp') }}</button>
    </div>

    <form class="form" @submit.prevent="submit">
      <label class="field">
        <span class="field-label">{{ t('Web_UserName') }}</span>
        <input v-model="userName" class="input" maxlength="32" autocomplete="username" required />
        <span v-if="mode === 'register'" class="field-hint">{{ t('Web_UserNameHint') }}</span>
      </label>
      <label class="field">
        <span class="field-label">{{ t('Web_Password') }}</span>
        <input v-model="password" class="input" type="password" maxlength="128"
          :autocomplete="mode === 'login' ? 'current-password' : 'new-password'" required />
        <span v-if="mode === 'register'" class="field-hint">{{ t('Web_PasswordHint') }}</span>
      </label>
      <label v-if="mode === 'register'" class="field">
        <span class="field-label">{{ t('Web_PasswordConfirm') }}</span>
        <input v-model="confirm" class="input" type="password" maxlength="128" autocomplete="new-password" required />
        <span v-if="mismatch" class="field-hint error-text">{{ t('Web_PasswordMismatch') }}</span>
      </label>
      <p v-if="error && !recovering" class="form-error" role="alert">{{ error }}</p>
      <button class="btn primary submit" type="submit" :disabled="busy || !userName.trim() || !password">
        {{ mode === 'login' ? t('Web_SignIn') : t('Web_SignUp') }}
      </button>
      <button v-if="mode === 'login'" type="button" class="btn link small forgot" @click="recovering = true; error = ''">{{ t('Web_ForgotPassword') }}</button>
    </form>
    <RouterLink class="caption privacy" to="/privacy">{{ t('Web_Privacy') }}</RouterLink>

    <Modal v-if="recovering" :title="t('Web_RecoverTitle')" :busy="busy" :width="460" @close="recovering = false">
      <form @submit.prevent="recover">
        <p class="caption lead">{{ t('Web_RecoverHint') }}</p>
        <label class="field">
          <span class="field-label">{{ t('Web_UserName') }}</span>
          <input v-model="userName" class="input" maxlength="32" autocomplete="username" required />
        </label>
        <label class="field">
          <span class="field-label">{{ t('Web_RecoveryCode') }}</span>
          <input v-model="recoveryCode" class="input mono" maxlength="40" autocomplete="off" spellcheck="false" required />
        </label>
        <label class="field">
          <span class="field-label">{{ t('Web_NewPassword') }}</span>
          <input v-model="newPassword" class="input" type="password" maxlength="128" autocomplete="new-password" required />
          <span class="field-hint">{{ t('Web_PasswordHint') }}</span>
        </label>
        <p v-if="error" class="form-error" role="alert">{{ error }}</p>
        <div class="dialog-actions">
          <button type="button" class="btn" :disabled="busy" @click="recovering = false">{{ t('Common_Cancel') }}</button>
          <button type="submit" class="btn primary" :disabled="busy || !userName.trim() || !recoveryCode.trim() || !newPassword">{{ t('Web_ResetPassword') }}</button>
        </div>
      </form>
    </Modal>
    <RecoveryCodeDialog v-if="issuedCode" :code="issuedCode" @close="codeSaved" />
  </section>
</template>

<style scoped>
.sign-in { display: flex; flex-direction: column; align-items: center; text-align: center; gap: 8px; padding: 40px 22px; }
.badge { width: 76px; height: 76px; border-radius: 50%; display: grid; place-items: center; background: var(--accent-container); color: var(--accent-text); margin-bottom: 6px; }
.icon { width: 34px; height: 34px; background: currentColor; -webkit-mask: url(/icons/Account.png) center / contain no-repeat; mask: url(/icons/Account.png) center / contain no-repeat; }
.hint { max-width: 460px; margin: 0 0 8px; line-height: 1.7; }
.modes { display: flex; gap: 6px; margin-bottom: 6px; }
.form { width: 100%; max-width: 340px; text-align: left; display: flex; flex-direction: column; }
.submit { margin-top: 4px; }
.forgot { align-self: center; margin-top: 8px; }
.error-text { color: var(--danger); }
.privacy { margin-top: 8px; color: var(--text-muted); }
.lead { margin: 0 0 14px; line-height: 1.7; text-align: left; }
.dialog-actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 8px; }
</style>
