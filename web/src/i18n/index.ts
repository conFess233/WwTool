import { ref, computed } from 'vue';
import zhHans from '../generated/i18n/zh-Hans.json';
import en from '../generated/i18n/en.json';
import ja from '../generated/i18n/ja.json';
import { readPref, writePref } from '../prefs';

// 语言资源来自 src/Core/Local/Language（与桌面端同源）+ src/i18n 的 Web 专属文案，键集合在生成时校验一致。
const tables: Record<Language, Record<string, string>> = { 'zh-Hans': zhHans, en, ja };
export type Language = 'zh-Hans' | 'en' | 'ja';
export const languages: Language[] = ['zh-Hans', 'en', 'ja'];

function initial(): Language {
  const saved = readPref('language');
  if (saved && (languages as string[]).includes(saved)) return saved as Language;
  const nav = navigator.language.toLowerCase();
  if (nav.startsWith('ja')) return 'ja';
  if (nav.startsWith('zh')) return 'zh-Hans';
  return 'en';
}

export const language = ref<Language>(initial());

export function setLanguage(value: Language) {
  language.value = value;
  writePref('language', value);
  document.documentElement.lang = value === 'zh-Hans' ? 'zh-CN' : value;
}
setLanguage(language.value);

/** 取文案，支持桌面端的 {0} {1} 占位符。缺失时回退中文，再回退键名。 */
export function t(key: string, ...args: (string | number)[]): string {
  const text = tables[language.value][key] ?? tables['zh-Hans'][key] ?? key;
  return args.length ? text.replace(/\{(\d+)\}/g, (_, i) => String(args[Number(i)] ?? '')) : text;
}

export const locale = computed(() => (language.value === 'zh-Hans' ? 'zh-CN' : language.value));
