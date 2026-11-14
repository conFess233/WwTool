import { ref, computed } from 'vue';
import { sessionState, currentUid } from '../account/session';
import { readPref, writePref } from '../prefs';
import { localUids } from './localGacha';

// 抽卡页面的数据来源：已登录且有绑定的 UID 时用云端；否则用当前浏览器里导入的本地记录。
export interface GachaSource {
  kind: 'cloud' | 'local';
  uid: string;
  updatedAt?: string;
}

const selectedLocal = ref<string | null>(readPref('localUid'));

export const gachaSource = computed<GachaSource | null>(() => {
  if (sessionState.value === 'signed-in' && currentUid.value)
    return { kind: 'cloud', uid: currentUid.value.uid, updatedAt: currentUid.value.gachaUpdatedAt };
  const list = localUids.value;
  if (!list.length) return null;
  const local = list.find(x => x.uid === selectedLocal.value) ?? list[0];
  return { kind: 'local', uid: local.uid, updatedAt: local.updatedAt };
});

export function selectLocal(uid: string) {
  selectedLocal.value = uid;
  writePref('localUid', uid);
}
