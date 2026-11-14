import { ref, watchEffect } from 'vue';
import meta from './generated/themes.json';
import { readPref, writePref } from './prefs';

// 与桌面端 ThemeService 相同的两个独立维度：明暗预设 + 强调色（FollowTheme 表示用预设自带的强调色）。
// 顺序与桌面端设置页一致
export const presets = ['WarmWhite', 'DeepBlue', 'SoftGreen', 'SoftPink', 'BlackGold'].filter(p => p in meta.presets);
export const accents = ['FollowTheme', ...['Blue', 'Green', 'Orange', 'Purple', 'Red', 'Yellow'].filter(a => a in meta.accents)];
export const presetSwatch = (name: string) => (meta.presets as Record<string, { background: string; border: string }>)[name];
export const accentSwatch = (name: string) => (meta.accents as Record<string, string>)[name];

export const preset = ref(pick(readPref('preset'), presets, 'WarmWhite'));
export const accent = ref(pick(readPref('accent'), accents, 'FollowTheme'));
export const reducedMotion = ref(readPref('reducedMotion') === '1');

const systemReduced = window.matchMedia('(prefers-reduced-motion: reduce)');
export const effectiveReduced = ref(reducedMotion.value || systemReduced.matches);
systemReduced.addEventListener('change', () => (effectiveReduced.value = reducedMotion.value || systemReduced.matches));

function pick(value: string | null, allowed: string[], fallback: string) {
  return value && allowed.includes(value) ? value : fallback;
}

export function isDark(name = preset.value) {
  return (meta.presets as Record<string, { dark: boolean }>)[name]?.dark ?? false;
}

watchEffect(() => {
  const root = document.documentElement;
  root.dataset.preset = preset.value;
  if (accent.value === 'FollowTheme') delete root.dataset.accent;
  else root.dataset.accent = accent.value;
  effectiveReduced.value = reducedMotion.value || systemReduced.matches;
  root.classList.toggle('reduced-motion', effectiveReduced.value);
});

/**
 * 切换外观（动效「主题切换」）：旧画面从 origin 处挖出一个 700ms 内扩大到覆盖全屏的圆，露出新主题。
 * 桌面端用合成器截图实现，这里用 View Transitions；不支持或减少动画时直接切换。
 */
export function applyTheme(change: () => void, origin?: { x: number; y: number }) {
  const doc = document as Document & { startViewTransition?: (cb: () => void) => { ready: Promise<void> } };
  if (!doc.startViewTransition || effectiveReduced.value || !origin) {
    change();
    persist();
    return;
  }
  const transition = doc.startViewTransition(() => {
    change();
    persist();
  });
  const { x, y } = origin;
  const radius = Math.hypot(Math.max(x, innerWidth - x), Math.max(y, innerHeight - y));
  transition.ready.then(() => {
    // 新画面叠在旧画面上方，按扩大的圆逐步露出：观感与桌面端“旧截图被挖出扩大的圆”相同
    document.documentElement.animate(
      { clipPath: [`circle(0px at ${x}px ${y}px)`, `circle(${radius}px at ${x}px ${y}px)`] },
      { duration: 700, easing: 'cubic-bezier(0.2, 0.8, 0.2, 1)', pseudoElement: '::view-transition-new(root)' },
    );
  });
}

function persist() {
  writePref('preset', preset.value);
  writePref('accent', accent.value);
  writePref('reducedMotion', reducedMotion.value ? '1' : '0');
}

export function setReducedMotion(value: boolean) {
  reducedMotion.value = value;
  persist();
}
