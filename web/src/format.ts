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

/** 资料图标（catalog/Icons），缺失时回退到仓库默认分支的同名文件 */
export const iconUrl = (id: number | string) => `/catalog/icons/${id}.png`;
export function iconFallback(e: Event) {
  const img = e.target as HTMLImageElement;
  if (img.dataset.fallback) { img.style.visibility = 'hidden'; return; }
  img.dataset.fallback = '1';
  const file = img.src.split('/').pop();
  img.src = `https://raw.githubusercontent.com/conFess233/WwTool/HEAD/catalog/Icons/${file}`;
}

/** 卡池编号 → 桌面端文案键（src/UI/Pages/Pulls/PoolNames.cs） */
const poolKeys: Record<number, string> = {
  1: 'RoleEvent', 2: 'WeaponEvent', 3: 'RolePermanent', 4: 'WeaponPermanent', 5: 'Novice', 6: 'NoviceSelect',
  7: 'BeginnerChoiceGiveback', 8: 'RoleNewbie', 9: 'WeaponNewbie', 10: 'CharacterCollaboration', 11: 'WeaponCollaboration',
  12: 'CharacterMemoryJourney', 13: 'WeaponMemoryJourney',
};
export const poolTitleKey = (pool: number) => `Enum_CardPoolType_${poolKeys[pool] ?? 'RoleEvent'}`;
export const mainPools = [1, 2, 3, 4];
