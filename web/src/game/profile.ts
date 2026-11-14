import { ref, watch } from 'vue';
import { get } from '../http';
import { currentUid } from '../account/session';
import { language } from '../i18n';

// 当前 UID 的账号资料（后端保存的、由浏览器转交的快照，见 src/Web/Uids/UidEndpoints.cs 的 ProfileMapper）。
export interface ProfileBase {
  name: string; id: number; creatTime: number; activeDays: number; level: number; worldLevel: number; roleNum: number; soundBox: number;
  energy: number; maxEnergy: number; storeEnergy: number; storeEnergyRecoverTime: number; maxStoreEnergy: number; energyRecoverTime: number;
  liveness: number; livenessMaxCount: number; livenessUnlock: boolean; chapterId: number; weeklyInstCount: number;
  boxes?: Record<string, number>; basicBoxes?: Record<string, number>; phantomBoxes?: Record<string, number>; birthMon: number; birthDay: number;
}
export interface Motor {
  level: number; exp: number; nextExp: number;
  skins?: { id: number; quality: number }[]; stickers?: { id: number; quality: number; partId: number }[];
  decorations?: { id: number; quality: number; partId: number }[]; frames?: { id: number; quality: number }[];
  equipSkin?: { id: number; quality: number };
}
export interface Profile {
  uid: string;
  hasProfile: boolean;
  updatedAt?: string;
  account?: { name?: string; level: number; region?: string; headPhoto: number };
  snapshot?: {
    base?: ProfileBase;
    motor?: Motor;
    music?: { id: number; count: number; totalCount: number }[];
    battlePass?: { level: number; weekExp: number; weekMaxExp: number; isUnlock: boolean; isOpen: boolean; exp: number; expLimit: number };
  };
}

export const profile = ref<Profile | null>(null);
export const profileState = ref<'idle' | 'loading' | 'ready' | 'failed'>('idle');

export async function loadProfile() {
  const uid = currentUid.value?.uid;
  if (!uid) {
    profile.value = null;
    return;
  }
  if (profile.value?.uid !== uid) profileState.value = 'loading';
  try {
    profile.value = await get<Profile>(`/api/uids/${uid}/profile`);
    profileState.value = 'ready';
  } catch {
    profileState.value = 'failed';
  }
}
watch(() => [currentUid.value?.uid, currentUid.value?.profileUpdatedAt], loadProfile);

/** 秒或毫秒时间戳（与 HomeViewModel.FromTimestamp 一致） */
export const fromTimestamp = (v: number) => new Date(v > 20_000_000_000 ? v : v * 1000);

// ---- 资料目录：物品名称（catalog/Data，按需读取） ----
type CatalogItem = { type?: string; qualityLevel?: number; names?: Record<string, string> };
const catalogs = new Map<string, Promise<Record<string, CatalogItem>>>();
export const catalogVersion = ref(0);
const loaded: Record<string, Record<string, CatalogItem>> = {};

export function useCatalog(...files: string[]) {
  for (const f of files) {
    if (catalogs.has(f)) continue;
    const p = fetch(`/catalog/data/${f}.json`).then(r => r.json()).then(j => (j.items ?? {}) as Record<string, CatalogItem>);
    catalogs.set(f, p);
    p.then(items => { loaded[f] = items; catalogVersion.value++; }).catch(() => catalogs.delete(f));
  }
}

export function itemName(id: number | string): string {
  void catalogVersion.value; // 资料加载完成后触发重新计算
  for (const items of Object.values(loaded)) {
    const names = items[String(id)]?.names;
    if (names) return names[language.value] ?? names['zh-Hans'] ?? String(id);
  }
  return String(id);
}
