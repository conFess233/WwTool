import { post } from '../http';
import { t } from '../i18n';
import { me, maskUid } from '../account/session';
import { GameError, listRoles, loginWithEmail, queryRoleDetail, type GameRole } from './gameAccount';
import { forgetOauth, loadOauth, saveOauth } from './oauthStore';
import { exchangeGuideToken, syncRoles } from './roles';

// 账号资料同步：浏览器用 oauthCode 直连库洛取资料，再把不含凭据的原始响应交给后端映射落库。

const body = (role: GameRole, roleDetail: string) => ({ region: role.region, playerInfo: role.playerInfo, roleDetail });

/**
 * 登录游戏账号，把其中每个 UID 绑定到当前站点账号（已属于其他站点账号时转移过来）并同步资料。
 * 后端按浏览器上报的资料绑定，不接触任何凭据。返回绑定的 UID 与发生转移的 UID。
 */
export async function loginAndSync(email: string, password: string, remember: boolean, onCaptcha?: () => void) {
  const userId = me.value?.id;
  if (!userId) throw new GameError(t('Web_SignInRequired'));
  const { oauthCode, guide } = await loginWithEmail(email, password, onCaptcha);
  const roles = await listRoles(oauthCode);
  const synced: string[] = [];
  const transferred: string[] = [];
  for (const role of roles) {
    const result = await post<{ uid: string; transferred: boolean }>('/api/uids/claim', body(role, await queryRoleDetail(oauthCode, role)));
    if (remember) await saveOauth(userId, role.uid, role.region, oauthCode);
    synced.push(result.uid);
    if (result.transferred) transferred.push(maskUid(result.uid));
  }
  // 角色数据：换一次 Guide Token 并同步每个 UID；失败不影响账号资料的绑定与同步
  let rolesFailed = false;
  try {
    const auth = await exchangeGuideToken(guide, synced, remember);
    for (const uid of synced) await syncRoles(uid, auth);
  } catch {
    rolesFailed = true;
  } finally {
    guide.accessToken = '';
  }
  return { synced, transferred, rolesFailed };
}

/** 用本设备保存的授权刷新一个 UID 的资料。授权失效时删除本地授权并抛出提示。 */
export async function refreshProfile(uid: string) {
  const userId = me.value?.id;
  if (!userId) throw new GameError(t('Web_SignInRequired'));
  const saved = await loadOauth(userId, uid);
  if (!saved) throw new GameError(t('Web_NoOauth'));
  let roles: GameRole[];
  try {
    roles = await listRoles(saved.oauthCode);
  } catch {
    await forgetOauth(userId, uid);
    throw new GameError(t('Web_OauthExpired'));
  }
  const role = roles.find(r => r.uid === uid);
  if (!role) throw new GameError(t('Web_UidNotInAccount', maskUid(uid)));
  await post(`/api/uids/${role.uid}/profile`, body(role, await queryRoleDetail(saved.oauthCode, role)));
}
