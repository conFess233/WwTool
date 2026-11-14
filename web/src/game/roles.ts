import { get, post, ApiError } from '../http';
import { t, language } from '../i18n';
import { me } from '../account/session';
import { GameError, type GuideLogin } from './gameAccount';
import { forgetGuide, loadGuide, saveGuide } from './oauthStore';

// 角色数据（官方攻略站）。攻略站接口只允许它自己的域名跨域访问，浏览器不能直连，由本站后端无状态转发：
// 游戏登录时把 access_token 交给后端换一次 Guide Token（后端不保存），令牌加密保存在本设备；
// 同步时带上令牌，后端转发请求并把角色快照落库。

export interface Attr { name: string; value: string }
export interface SkillLevel { imageUrl?: string; level: number; name?: string; type?: string }
export interface EchoSlot { cost: number; level?: number; mainStat?: string; subStat?: string }
export interface EchoSet { imageUrl?: string; name?: string }
export interface Role {
  id: string; name: string; star: number; limited: boolean; sequence: number; cardPictureUrl?: string; illustrationPictureUrl?: string;
  acquiredAt?: string; attributes: Attr[];
  weapon?: { id: string; name: string; star: number; fields: Attr[] };
  build: { skills: SkillLevel[]; echoes: EchoSlot[]; echoSets: EchoSet[]; mainEchoName?: string };
}
export interface EquippedWeapon { id: string; name: string; star: number; limited: boolean; ownerId: string; ownerName: string; acquiredAt?: string }
export interface RolesData { syncedAt?: string; roles: Role[]; weapons: EquippedWeapon[]; hasGachaRecords: boolean }

export const loadRoles = (uid: string, signal?: AbortSignal) =>
  get<RolesData>(`/api/uids/${uid}/roles?lang=${encodeURIComponent(language.value)}`, signal);

/** 游戏登录后立即调用：换取 Guide Token；remember 时按 UID 加密保存。返回令牌供本次同步使用。 */
export async function exchangeGuideToken(login: GuideLogin, uids: string[], remember: boolean) {
  const { token } = await post<{ token: string }>('/api/guide/token', { ...login, lang: language.value });
  login.accessToken = ''; // access_token 只经过后端这一次
  const userId = me.value?.id;
  if (remember && userId) for (const uid of uids) await saveGuide(userId, uid, login.cUid, token);
  return { token, cUid: login.cUid };
}

export async function hasGuide(uid: string) {
  const userId = me.value?.id;
  return !!userId && !!(await loadGuide(userId, uid));
}

/** 同步一个 UID 的角色。不传令牌时使用本设备保存的；令牌失效时删除并提示重新登录游戏账号。 */
export async function syncRoles(uid: string, guide?: { token: string; cUid: string }) {
  const userId = me.value?.id;
  if (!userId) throw new GameError(t('Web_SignInRequired'));
  const auth = guide ?? (await loadGuide(userId, uid));
  if (!auth) throw new GameError(t('Web_RolesNeedLogin'));
  try {
    return await post<{ roles: number }>(`/api/uids/${uid}/roles/sync`, { token: auth.token, cUid: auth.cUid, lang: language.value });
  } catch (e) {
    if (e instanceof ApiError && e.status === 401 && !guide) {
      await forgetGuide(userId, uid);
      throw new GameError(t('Web_RolesNeedLogin'));
    }
    throw e;
  }
}
