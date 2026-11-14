import { useRouter } from 'vue-router';
import { t } from '../i18n';
import { me, currentUid } from '../account/session';
import { hasOauth } from './oauthStore';
import { refreshProfile } from './profileSync';
import { loadProfile } from './profile';
import { toast, toastError } from '../components/toast';
import { useFab } from '../shell/fab';

let busy = false;

/** 用本地保存的 OauthCode 刷新当前 UID 的账号资料 */
export async function syncCurrentProfile() {
  const uid = currentUid.value?.uid;
  if (!uid || !me.value || busy) return;
  if (!(await hasOauth(me.value.id, uid))) return toast(t('Web_NoOauth'), 'info');
  busy = true;
  try {
    await refreshProfile(uid);
    await loadProfile();
    toast(t('Web_ProfileSynced'), 'success');
  } catch (e) {
    toastError(e);
  } finally {
    busy = false;
  }
}

/** 探索、摩托等资料页的悬浮按钮：刷新数据、返回 */
export function useProfileFab() {
  const router = useRouter();
  useFab(() => ({
    actions: [
      { key: 'refresh', title: t('Fab_RefreshData'), icon: 'Sync', run: syncCurrentProfile },
      { key: 'back', title: t('Fab_Back'), icon: 'Back', run: () => router.back() },
    ],
  }));
}
