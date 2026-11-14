import { ref } from 'vue';
import { locale } from './i18n';

// 显示格式（本地时间）
const pad = (n: number) => String(n).padStart(2, '0');

export function dateTime(iso?: string | null) {
  if (!iso) return '';
  const d = new Date(iso);
  return `${d.getFullYear()}/${pad(d.getMonth() + 1)}/${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function date(iso?: string | number | null) {
  if (iso === undefined || iso === null || iso === '') return '';
  const d = typeof iso === 'number' ? new Date(iso) : new Date(iso);
  return `${d.getFullYear()}/${pad(d.getMonth() + 1)}/${pad(d.getDate())}`;
}

export const num = (v: number | null | undefined, digits = 0) =>
  v === null || v === undefined ? '--' : v.toLocaleString(locale.value, { maximumFractionDigits: digits, minimumFractionDigits: digits });

// 资料仓库（与桌面端「同步游戏资料」同源）：图标与资料由浏览器直接加载，不经本站服务器。
// 首选 GitHub；请求失败或超时时改用 jsDelivr 镜像，并在本次会话里一直用镜像（GitHub 在部分地区很慢或打不开）。
const CATALOG_SOURCES = [
  'https://raw.githubusercontent.com/conFess233/WwTool/HEAD/catalog',
  'https://cdn.jsdelivr.net/gh/conFess233/WwTool@master/catalog',
];
const MIRROR_KEY = 'wwtool.catalogMirror';
const CATALOG_TIMEOUT = 6000;

function initialSource() {
  try {
    return sessionStorage.getItem(MIRROR_KEY) === '1' ? 1 : 0;
  } catch {
    return 0;
  }
}
const catalogSource = ref(initialSource());

function useMirror() {
  if (catalogSource.value === 1) return;
  catalogSource.value = 1;
  try {
    sessionStorage.setItem(MIRROR_KEY, '1');
  } catch {
    // 存储不可用：本页内仍然改用镜像
  }
}

/** 资料图标（catalog/Icons） */
export const iconUrl = (id: number | string) => `${CATALOG_SOURCES[catalogSource.value]}/Icons/${id}.png`;

/** 图标加载失败：还在用 GitHub 时换镜像重试一次；镜像也没有（资料里还没有的新物品）就隐藏，保留占位底色 */
export function iconFallback(e: Event) {
  const img = e.target as HTMLImageElement;
  if (!img.dataset.retried && img.src.startsWith(CATALOG_SOURCES[0])) {
    img.dataset.retried = '1';
    useMirror();
    img.src = CATALOG_SOURCES[1] + img.src.slice(CATALOG_SOURCES[0].length);
    return;
  }
  img.style.visibility = 'hidden';
}

/** 读取资料数据（catalog/Data/{name}.json）：当前来源失败或超时就换另一个 */
export async function fetchCatalog<T = unknown>(name: string): Promise<T> {
  const order = catalogSource.value === 1 ? [1] : [0, 1];
  let last: unknown;
  for (const i of order) {
    try {
      const r = await fetch(`${CATALOG_SOURCES[i]}/Data/${name}.json`, { signal: AbortSignal.timeout(CATALOG_TIMEOUT) });
      if (!r.ok) throw new Error(`HTTP ${r.status}`);
      const data = (await r.json()) as T;
      if (i === 1) useMirror();
      return data;
    } catch (e) {
      last = e;
    }
  }
  throw last;
}

/** 卡池编号 → 桌面端文案键（src/UI/Pages/Pulls/PoolNames.cs） */
const poolKeys: Record<number, string> = {
  1: 'RoleEvent', 2: 'WeaponEvent', 3: 'RolePermanent', 4: 'WeaponPermanent', 5: 'Novice', 6: 'NoviceSelect',
  7: 'BeginnerChoiceGiveback', 8: 'RoleNewbie', 9: 'WeaponNewbie', 10: 'CharacterCollaboration', 11: 'WeaponCollaboration',
  12: 'CharacterMemoryJourney', 13: 'WeaponMemoryJourney',
};
export const poolTitleKey = (pool: number) => `Enum_CardPoolType_${poolKeys[pool] ?? 'RoleEvent'}`;
export const mainPools = [1, 2, 3, 4];
