import { ref, computed } from 'vue';
import { get, post, ApiError } from '../http';
import { readPref, writePref } from '../prefs';

// 站点会话与当前 UID。站点账号与游戏账号无关。

export interface MeUid {
  uid: string;
  region: 'global' | 'china';
  /** 最近一次绑定方式：抽卡链接或游戏账号登录 */
  method: 'gacha' | 'game' | '';
  boundAt: string;
  lastVerifiedAt: string;
  name?: string;
  level?: number;
  headPhoto?: number;
  gachaUpdatedAt?: string;
  profileUpdatedAt?: string;
}

export interface Me {
  id: string;
  userName: string;
  displayName: string;
  createdAt: string;
  uids: MeUid[];
  notices: { id: string; maskedUid: string; at: string }[];
}

export const me = ref<Me | null>(null);
export const sessionState = ref<'unknown' | 'signed-out' | 'signed-in'>('unknown');
const selectedUid = ref<string | null>(readPref('uid'));

export const currentUid = computed<MeUid | null>(() => {
  const list = me.value?.uids ?? [];
  return list.find(u => u.uid === selectedUid.value) ?? list[0] ?? null;
});

export function selectUid(uid: string) {
  selectedUid.value = uid;
  writePref('uid', uid);
}

export async function refreshMe() {
  try {
    me.value = await get<Me>('/api/me');
    sessionState.value = 'signed-in';
  } catch (e) {
    if (e instanceof ApiError && e.status === 401) {
      me.value = null;
      sessionState.value = 'signed-out';
    } else {
      throw e;
    }
  }
}

export async function logout() {
  await post('/api/auth/logout');
  me.value = null;
  sessionState.value = 'signed-out';
}

/** UID 尾号脱敏：1234****，与后端 GachaImportService.Mask 一致 */
export const maskUid = (uid: string) => (uid.length <= 4 ? '****' : `${uid.slice(0, -4)}****`);
