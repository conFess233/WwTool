import DOMPurify from 'dompurify';
import { get } from '../http';
import { language } from '../i18n';

// encore.moe 解包数据的共用部分：元素颜色、富文本与角色详情接口（src/Web/Public/PublicFeeds.cs）。
export const ELEMENT_COLORS = ['#8F9BA6', '#3FA9F5', '#F0703C', '#A86BF0', '#2FCB91', '#E3C33A', '#D9488F'];
export const elementColor = (id: number) => ELEMENT_COLORS[id] ?? 'transparent';

/** 解包文本用 <span style="color:…"> 标出关键词；CSP 不允许内联样式，换成统一的强调色 class */
export function rich(html?: string | null) {
  const text = (html ?? '').replace(/\n/g, '<br>').replace(/<span\s+style="[^"]*color[^"]*"\s*>/gi, '<span class="hl">');
  return DOMPurify.sanitize(text, { ALLOWED_TAGS: ['b', 'strong', 'i', 'em', 'br', 'span', 'p'], ALLOWED_ATTR: ['class'] });
}

export interface EncoreStat { name: string; levels: { level: number; value: string }[] }
export interface EncoreSkill { type: string; name: string; description?: string; iconUrl?: string; attributes: { name: string; values: string[] }[] }
export interface EncoreCharacter {
  id: number; name: string; nickName?: string; introduction?: string; quality: number; elementId: number; elementName?: string;
  weaponTypeName?: string; portraitUrl?: string;
  tags: { name: string; description?: string; color?: string }[];
  skins: { id: number; name: string; portraitUrl?: string }[];
  profile: { key: string; value: string }[];
  stats: EncoreStat[];
  skills: EncoreSkill[];
  chain: { index: number; name: string; description?: string; iconUrl?: string }[];
  stories: { title: string; content: string }[];
  voices: { title: string; content: string }[];
}
export interface CharacterDetail { character: EncoreCharacter; pageUrl: string; fetchedAt: string }

export const loadCharacter = (id: number | string, signal?: AbortSignal) =>
  get<CharacterDetail>(`/api/public/characters/${id}?lang=${encodeURIComponent(language.value)}`, signal);

/** 同一等级突破前后取突破后的值 */
export const statAt = (s: EncoreStat, level: number) =>
  [...s.levels].reverse().find(l => l.level <= level)?.value ?? s.levels[0]?.value;
