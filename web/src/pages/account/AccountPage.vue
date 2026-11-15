<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { t } from '../../i18n';
import { me, sessionState, currentUid, selectUid, logout, refreshMe, maskUid, type MeUid } from '../../account/session';
import { del, patch, get, post } from '../../http';
import { localUids, refreshLocal, deleteLocal } from '../../gacha/localGacha';
import { selectLocal } from '../../gacha/source';
import { uploadLocal } from '../../gacha/upload';
import { useRouter } from 'vue-router';
import { hasOauth, forgetUid, forgetAll } from '../../game/oauthStore';
import { refreshProfile } from '../../game/profileSync';
import { toast, toastError } from '../../components/toast';
import { dateTime, iconUrl, iconFallback } from '../../format';
import SignInCard from './SignInCard.vue';
import BindUidDialog from './BindUidDialog.vue';
import GameLoginDialog from './GameLoginDialog.vue';
import ConfirmDialog from '../../components/ConfirmDialog.vue';
import Modal from '../../components/Modal.vue';
import RecoveryCodeDialog from './RecoveryCodeDialog.vue';
import StrokeIcon from '../../components/StrokeIcon.vue';
import { useFab } from '../../shell/fab';

// 账号数据（AccountsView.axaml）：站点账号卡 + UID 卡片网格 + 添加 UID 虚线框。
const binding = ref(false);
const refreshingGachaUid = ref<string | null>(null);
const gameLogin = ref(false);
const confirm = ref<{ title: string; message: string; action: () => Promise<unknown> } | null>(null);
const syncing = ref<string | null>(null);
const oauthSaved = ref<Record<string, boolean>>({});
const renaming = ref(false);
const newName = ref('');

async function loadOauthFlags() {
  if (!me.value) return;
  const flags: Record<string, boolean> = {};
  for (const u of me.value.uids) flags[u.uid] = await hasOauth(me.value.id, u.uid);
  oauthSaved.value = flags;
}
onMounted(loadOauthFlags);
watch(me, loadOauthFlags);

const serverName = (u: MeUid) => (u.region === 'china' ? t('Home_RegionCN') : t('Home_RegionGlobal'));

async function syncProfile(u: MeUid) {
  syncing.value = u.uid;
  try {
    await refreshProfile(u.uid);
    await refreshMe();
    toast(t('Web_ProfileSynced'), 'success');
  } catch (e) {
    toastError(e);
    await loadOauthFlags();
  } finally {
    syncing.value = null;
  }
}

function askUnbind(u: MeUid) {
  confirm.value = {
    title: t('Web_Unbind'),
    message: t('Web_UnbindConfirm', maskUid(u.uid)),
    action: async () => {
      await del(`/api/uids/${u.uid}`);
      if (me.value) await forgetUid(me.value.id, u.uid);
      await refreshMe();
    },
  };
}

function askDeleteAccount() {
  confirm.value = {
    title: t('Web_DeleteAccount'),
    message: t('Web_DeleteAccountConfirm'),
    action: async () => {
      const id = me.value?.id;
      await del('/api/me');
      if (id) await forgetAll(id);
      await refreshMe();
    },
  };
}

// ── 密码与恢复码 ──
const passwordDialog = ref<'change' | 'recovery' | null>(null);
const currentPassword = ref('');
const newPassword = ref('');
const passwordBusy = ref(false);
const passwordError = ref('');
const issuedCode = ref<string | null>(null);

function openPassword(kind: 'change' | 'recovery') {
  passwordDialog.value = kind;
  currentPassword.value = '';
  newPassword.value = '';
  passwordError.value = '';
}

async function submitPassword() {
  passwordBusy.value = true;
  passwordError.value = '';
  try {
    if (passwordDialog.value === 'change') {
      await post('/api/auth/password', { currentPassword: currentPassword.value, newPassword: newPassword.value });
      toast(t('Web_PasswordChanged'), 'success');
    } else {
      issuedCode.value = (await post<{ recoveryCode: string }>('/api/auth/recovery-code', { password: currentPassword.value })).recoveryCode;
    }
    passwordDialog.value = null;
  } catch (e) {
    passwordError.value = (e as Error).message;
  } finally {
    currentPassword.value = '';
    newPassword.value = '';
    passwordBusy.value = false;
  }
}

// ── 本地抽卡记录（未登录时导入，只在此浏览器） ──
const router = useRouter();
const removeAfterUpload = ref(true);
const uploading = ref<string | null>(null);
const importingLocal = ref(false);
onMounted(refreshLocal);

async function upload(uid: string) {
  uploading.value = uid;
  try {
    toast(t('Web_UploadDone', await uploadLocal(uid, removeAfterUpload.value)), 'success');
  } catch (e) {
    toastError(e);
  } finally {
    uploading.value = null;
  }
}

function viewLocal(uid: string) {
  selectLocal(uid);
  router.push('/pulls');
}

function askDeleteLocal(uid: string) {
  confirm.value = { title: t('Web_DeleteLocal'), message: t('Web_DeleteLocalConfirm', maskUid(uid)), action: () => deleteLocal(uid) };
}

const regionName = (region: string) => (region === 'china' ? t('Home_RegionCN') : t('Home_RegionGlobal'));

async function saveName() {
  try {
    await patch('/api/me', { displayName: newName.value });
    renaming.value = false;
    await refreshMe();
  } catch (e) {
    toastError(e);
  }
}

async function exportData() {
  try {
    const data = await get<unknown>('/api/me/export');
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `wwtool-web-export-${new Date().toISOString().slice(0, 10)}.json`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  } catch (e) {
    toastError(e);
  }
}

async function signOut() {
  await logout();
}

async function dismissNotices() {
  await del('/api/me/notices');
  await refreshMe();
}

const summary = computed(() => t('Web_UidCount', me.value?.uids.length ?? 0));
const refreshingList = ref(false);
async function refreshList() {
  refreshingList.value = true;
  try {
    await refreshMe();
    await loadOauthFlags();
  } catch (e) {
    toastError(e);
  } finally {
    refreshingList.value = false;
  }
}

// 悬浮按钮（AccountsView）：刷新、添加游戏账号、按抽卡链接绑定、返回
useFab(() => (sessionState.value === 'signed-in' ? {
  actions: [
    { key: 'refresh', title: t('Index_RefreshAccountList'), icon: 'Sync', run: refreshList },
    { key: 'game', title: t('Dialog_AddAccount'), icon: 'User', run: () => (gameLogin.value = true) },
    { key: 'bind', title: t('Web_BindUid'), icon: 'Link', run: () => (binding.value = true) },
    { key: 'back', title: t('Fab_Back'), icon: 'Back', run: () => router.back() },
  ],
} : null));
</script>

<template>
  <div class="account-page">
    <div v-if="sessionState === 'unknown'" class="card"><span class="caption">{{ t('Web_Loading') }}</span></div>
    <SignInCard v-else-if="sessionState === 'signed-out'" />

    <template v-else-if="me">
      <!-- 转移通知 -->
      <section v-if="me.notices.length" class="card notice">
        <div>
          <p v-for="n in me.notices" :key="n.id" class="notice-line">{{ t('Web_TransferNotice', n.maskedUid, dateTime(n.at)) }}</p>
        </div>
        <button class="btn small" @click="dismissNotices">{{ t('Web_Dismiss') }}</button>
      </section>

      <!-- 工具行 -->
      <div class="toolbar">
        <span class="label">{{ summary }}</span>
        <button class="btn refresh" :disabled="refreshingList" @click="refreshList">
          <StrokeIcon name="Sync" :size="16" :class="{ spinning: refreshingList }" />{{ t('Index_RefreshAccountList') }}
        </button>
      </div>

      <!-- UID 卡片 -->
      <div class="grid stagger">
        <article v-for="(u, i) in me.uids" :key="u.uid" class="card account" :class="{ current: currentUid?.uid === u.uid }" :style="{ '--i': i }">
          <div class="avatar">
            <img v-if="u.headPhoto" :src="iconUrl(u.headPhoto)" alt="" @error="iconFallback" />
            <span v-else class="initial">{{ (u.name ?? u.uid).slice(0, 1) }}</span>
          </div>
          <h3 class="name">{{ u.name || maskUid(u.uid) }}</h3>
          <span class="caption uid">UID {{ u.uid }}<template v-if="u.level"> · LV.{{ u.level }}</template></span>
          <div class="tag-slot">
            <span v-if="currentUid?.uid === u.uid" class="tag accent">{{ t('Accounts_CurrentTag') }}</span>
            <span v-if="u.method" class="tag">{{ u.method === 'game' ? t('Web_BoundByGame') : t('Web_BoundByGacha') }}</span>
          </div>
          <div class="kvs">
            <div class="kv"><span>{{ t('Index_Server') }}</span><span>{{ serverName(u) }}</span></div>
            <div class="kv"><span>{{ t('Web_GachaUpdated', '') }}</span><span>{{ dateTime(u.gachaUpdatedAt) || t('Web_Never') }}</span></div>
            <div class="kv"><span>{{ t('Web_ProfileUpdated', '') }}</span><span>{{ dateTime(u.profileUpdatedAt) || t('Web_Never') }}</span></div>
          </div>
          <div class="card-actions">
            <button class="btn small" @click="refreshingGachaUid = u.uid">{{ t('Web_RefreshGacha') }}</button>
            <button v-if="u.region === 'global'" class="btn small" :disabled="syncing === u.uid || !oauthSaved[u.uid]"
              :title="oauthSaved[u.uid] ? '' : t('Web_NoOauth')" @click="syncProfile(u)">
              {{ syncing === u.uid ? t('Web_ProfileSyncing') : t('Web_RefreshProfile') }}
            </button>
            <button v-if="oauthSaved[u.uid]" class="btn small link" @click="me && forgetUid(me.id, u.uid).then(loadOauthFlags)">{{ t('Web_ForgetOauth') }}</button>
          </div>
          <div class="bottom">
            <button v-if="currentUid?.uid !== u.uid" class="btn" @click="selectUid(u.uid)">{{ t('Accounts_SwitchTo') }}</button>
            <span v-else class="current-label">{{ t('Accounts_Current') }}</span>
            <button class="btn trash" :title="t('Web_Unbind')" :aria-label="t('Web_Unbind')" @click="askUnbind(u)"><StrokeIcon name="Trash" :size="16" /></button>
          </div>
        </article>

        <div class="add-card" :style="{ '--i': me.uids.length }">
          <span class="plus" aria-hidden="true"><StrokeIcon name="Plus" :size="26" :stroke="1.6" /></span>
          <strong>{{ t('Web_AddUidTitle') }}</strong>
          <span class="caption">{{ t('Web_AddUidHint') }}</span>
          <div class="add-actions">
            <button class="btn primary" @click="binding = true">{{ t('Web_BindUid') }}</button>
            <button class="btn" @click="gameLogin = true">{{ t('Web_GameLogin') }}</button>
          </div>
        </div>
      </div>

      <!-- 站点账号 -->
      <section class="card site">
        <div class="site-head">
          <div>
            <h2 class="section-title">{{ t('Web_SiteAccount') }}</h2>
            <div v-if="!renaming" class="site-name">{{ me.displayName }} <button class="btn link small" @click="renaming = true; newName = me.displayName">{{ t('Web_Rename') }}</button></div>
            <form v-else class="rename" @submit.prevent="saveName">
              <input v-model="newName" class="input" maxlength="64" autofocus />
              <button class="btn small primary" type="submit">{{ t('Web_Save') }}</button>
              <button class="btn small" type="button" @click="renaming = false">{{ t('Common_Cancel') }}</button>
            </form>
            <span class="caption">{{ t('Web_UserName') }} {{ me.userName }} · {{ t('Web_CreatedAt', dateTime(me.createdAt)) }}</span>
          </div>
          <button class="btn" @click="signOut">{{ t('Web_SignOut') }}</button>
        </div>

        <h3 class="sub">{{ t('Web_Security') }}</h3>
        <p class="caption">{{ t('Web_RecoverHint') }}</p>
        <div class="row">
          <button class="btn small" @click="openPassword('change')">{{ t('Web_ChangePassword') }}</button>
          <button class="btn small" @click="openPassword('recovery')">{{ t('Web_RegenerateRecovery') }}</button>
        </div>

        <h3 class="sub">{{ t('Web_Privacy') }}</h3>
        <p class="caption">{{ t('Web_ExportHint') }}</p>
        <div class="row">
          <button class="btn small" @click="exportData">{{ t('Web_ExportData') }}</button>
          <RouterLink class="btn small link" to="/privacy">{{ t('Web_Privacy') }}</RouterLink>
          <span class="spacer" />
          <button class="btn small danger" @click="askDeleteAccount">{{ t('Web_DeleteAccount') }}</button>
        </div>
      </section>
    </template>

    <!-- 本地抽卡记录：登录与否都显示 -->
    <section v-if="sessionState !== 'unknown'" class="card local">
      <div class="site-head">
        <div>
          <h2 class="section-title">{{ t('Web_LocalGacha') }}</h2>
          <span class="caption">{{ t('Web_LocalGachaHint') }}</span>
        </div>
        <button class="btn" @click="importingLocal = true">{{ t('Web_ImportLocal') }}</button>
      </div>
      <div v-for="u in localUids" :key="u.uid" class="local-row">
        <div class="local-info">
          <strong>UID {{ u.uid }}</strong>
          <span class="caption">{{ regionName(u.region) }} · {{ t('Web_LocalRecordCount', u.count, dateTime(u.updatedAt)) }}</span>
        </div>
        <button class="btn small" @click="viewLocal(u.uid)">{{ t('Nav_Statistics') }}</button>
        <button v-if="sessionState === 'signed-in'" class="btn small primary" :disabled="uploading === u.uid" @click="upload(u.uid)">{{ t('Web_UploadCloud') }}</button>
        <button class="btn small danger" @click="askDeleteLocal(u.uid)">{{ t('Web_DeleteLocal') }}</button>
      </div>
      <label v-if="sessionState === 'signed-in' && localUids.length" class="check">
        <input v-model="removeAfterUpload" type="checkbox" />
        <span>{{ t('Web_UploadRemoveLocal') }}</span>
      </label>
    </section>

    <BindUidDialog v-if="importingLocal" local @close="importingLocal = false" />
    <Modal v-if="passwordDialog" :title="passwordDialog === 'change' ? t('Web_ChangePassword') : t('Web_RegenerateRecovery')" :busy="passwordBusy" :width="420" @close="passwordDialog = null">
      <form @submit.prevent="submitPassword">
        <p v-if="passwordDialog === 'recovery'" class="caption">{{ t('Web_RegenerateRecoveryHint') }}</p>
        <label class="field">
          <span class="field-label">{{ t('Web_CurrentPassword') }}</span>
          <input v-model="currentPassword" class="input" type="password" maxlength="128" autocomplete="current-password" required />
        </label>
        <label v-if="passwordDialog === 'change'" class="field">
          <span class="field-label">{{ t('Web_NewPassword') }}</span>
          <input v-model="newPassword" class="input" type="password" maxlength="128" autocomplete="new-password" required />
          <span class="field-hint">{{ t('Web_PasswordHint') }}</span>
        </label>
        <p v-if="passwordError" class="form-error" role="alert">{{ passwordError }}</p>
        <div class="dialog-actions">
          <button type="button" class="btn" :disabled="passwordBusy" @click="passwordDialog = null">{{ t('Common_Cancel') }}</button>
          <button type="submit" class="btn primary" :disabled="passwordBusy || !currentPassword || (passwordDialog === 'change' && !newPassword)">{{ t('Web_Save') }}</button>
        </div>
      </form>
    </Modal>
    <RecoveryCodeDialog v-if="issuedCode" :code="issuedCode" @close="issuedCode = null" />
    <BindUidDialog v-if="binding" @close="binding = false" />
    <BindUidDialog v-if="refreshingGachaUid" :uid="refreshingGachaUid" @close="refreshingGachaUid = null" />
    <GameLoginDialog v-if="gameLogin" @close="gameLogin = false" @done="loadOauthFlags" />
    <ConfirmDialog v-if="confirm" v-bind="confirm" @close="confirm = null" />
  </div>
</template>

<style scoped>
.account-page { display: flex; flex-direction: column; gap: 18px; padding-bottom: 70px; }
.toolbar { display: flex; align-items: center; justify-content: space-between; height: 44px; }
.label { font-size: 14px; color: var(--text-muted); }
.refresh { display: inline-flex; align-items: center; gap: 8px; font-weight: 700; }
.spinning { animation: spin 0.9s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
.notice { display: flex; justify-content: space-between; align-items: center; gap: 16px; border-color: var(--accent-line); background: var(--accent-container); }
.notice-line { margin: 2px 0; font-size: 13px; color: var(--accent-text); }

.grid { display: grid; grid-template-columns: repeat(auto-fill, 352px); gap: 24px; }
.account { display: flex; flex-direction: column; align-items: stretch; transition: border-color var(--motion-fast) var(--ease-standard), box-shadow var(--motion-fast) var(--ease-standard), transform var(--motion-fast) var(--ease-standard); }
.account:hover { box-shadow: var(--shadow-hover); transform: translateY(-2px); }
.account.current { border: 2px solid var(--accent); background: color-mix(in srgb, var(--accent-container) 35%, var(--surface)); }
.kvs :deep(.kv), .kvs .kv { padding: 12px 0; border-top: 1px solid var(--divider); }
.trash { width: 48px; padding: 0; display: grid; place-items: center; }
.trash:hover { color: var(--danger); border-color: var(--danger-line); }
.avatar { width: 104px; height: 104px; border-radius: 50%; overflow: hidden; align-self: center; background: var(--surface-muted); display: grid; place-items: center; }
.avatar img { width: 100%; height: 100%; object-fit: cover; }
.initial { font-size: 30px; font-weight: 700; }
.name { font-size: 20px; font-weight: 700; text-align: center; margin: 14px 0 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.uid { text-align: center; margin-top: 4px; }
.tag-slot { height: 26px; margin-top: 8px; display: flex; justify-content: center; align-items: center; }
.kvs { margin-top: 10px; }
.card-actions { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 12px; }
.bottom { display: grid; grid-template-columns: 1fr auto; gap: 10px; margin-top: 16px; }
.current-label { display: grid; place-items: center; height: 44px; border-radius: var(--radius-control); background: var(--accent); color: var(--text-on-accent); font-weight: 600; }

.add-card {
  display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 12px; min-height: 420px; padding: 16px;
  border: 2px dashed var(--border-strong); border-radius: var(--radius-card); background: transparent; cursor: pointer; text-align: center;
  animation: card-in var(--motion-relaxed) var(--ease-standard) both; animation-delay: calc(var(--i, 0) * 70ms);
  transition: border-color var(--motion-fast) var(--ease-standard), background-color var(--motion-fast) var(--ease-standard);
}
.add-card:hover { border-color: var(--accent); background: var(--accent-container); }
.plus { width: 68px; height: 68px; border-radius: 50%; display: grid; place-items: center; background: var(--accent-container); color: var(--accent-text); }
.add-card .caption { line-height: 1.6; }
.add-card { cursor: default; }
.add-actions { display: flex; flex-direction: column; gap: 8px; width: 100%; max-width: 220px; margin-top: 6px; }
.local { display: flex; flex-direction: column; gap: 10px; }
.local-row { display: flex; align-items: center; gap: 8px; padding: 10px 12px; border-radius: var(--radius-inner); background: var(--surface-raised); }
.local-info { flex: 1; display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.check { display: flex; align-items: center; gap: 8px; font-size: 13px; }
.dialog-actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 8px; }

.site-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; }
.site-name { font-size: 18px; font-weight: 700; margin: 6px 0 2px; }
.rename { display: flex; gap: 8px; margin: 6px 0; }
.rename .input { width: 240px; }
.sub { font-size: 14px; font-weight: 700; margin: 22px 0 8px; }
.row { display: flex; align-items: center; gap: 8px; margin-top: 8px; }
.spacer { flex: 1; }
.site .caption { display: block; margin: 6px 0; line-height: 1.6; }

/* 手机：账号卡片铺满宽度 */
@media (max-width: 768px) {
  .account-page { gap: 12px; padding-bottom: 12px; }
  .toolbar { height: auto; flex-wrap: wrap; gap: 8px; }
  .notice { flex-direction: column; align-items: flex-start; gap: 10px; }
  .grid { grid-template-columns: minmax(0, 1fr); gap: 12px; }
  .account:hover { transform: none; }
  .avatar { width: 84px; height: 84px; }
  .add-card { min-height: 240px; }
  .site-head { flex-direction: column; gap: 10px; }
  .rename { flex-wrap: wrap; }
  .rename .input { width: auto; flex: 1; min-width: 0; }
  .row { flex-wrap: wrap; }
}
</style>
