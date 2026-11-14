import { post } from '../http';
import { t } from '../i18n';
import { me, refreshMe, maskUid } from '../account/session';
import { deleteLocal, loadLocal, plainPools } from './localGacha';

/**
 * 把本地记录上传到已绑定的 UID（后端按指纹去重合并）。UID 还没绑定到当前站点账号时提示先绑定：
 * 绑定需要抽卡链接或游戏账号登录，不能只凭本地记录认领。
 */
export async function uploadLocal(uid: string, removeAfter: boolean) {
  if (!me.value?.uids.some(u => u.uid === uid)) throw new Error(t('Web_UploadNeedBind', maskUid(uid)));
  const local = await loadLocal(uid);
  if (!local) return 0;
  const { added } = await post<{ added: number }>(`/api/uids/${uid}/gacha/upload`, { pools: plainPools(local) });
  await refreshMe();
  if (removeAfter) await deleteLocal(uid);
  return added;
}
