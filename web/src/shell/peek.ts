import { onMounted, onBeforeUnmount, watch, type Ref } from 'vue';
import { peekTransparency, backdropImage } from './backdrop';

// 空白处查看背景（MainWindow.axaml.cs）：指针在空白处停留 350ms 后，内容层淡出到 1 - 透明度，露出背景图。
// 空白 = 背景层、内容区外圈，或内容区里只有透明的布局容器；落在卡片、按钮、文字、图片等可见组件上都不算。
const VISIBLE_TAGS = new Set(['BUTTON', 'A', 'INPUT', 'SELECT', 'TEXTAREA', 'IMG', 'SVG', 'VIDEO', 'CANVAS', 'LABEL', 'H1', 'H2', 'H3', 'P', 'SPAN']);

function isVisibleComponent(el: Element) {
  if (VISIBLE_TAGS.has(el.tagName.toUpperCase())) return true;
  for (const n of el.childNodes) if (n.nodeType === Node.TEXT_NODE && n.textContent?.trim()) return true;
  const cs = getComputedStyle(el);
  if (cs.backgroundImage !== 'none') return true;
  const bg = cs.backgroundColor;
  if (bg && bg !== 'transparent' && !/rgba?\(.*,\s*0\)$/.test(bg) && !bg.endsWith(' / 0)')) return true;
  return parseFloat(cs.borderTopWidth) > 0 || parseFloat(cs.borderLeftWidth) > 0;
}

function isEmpty(target: EventTarget | null, frame: HTMLElement) {
  if (!(target instanceof Element)) return true;
  if (target === document.body || target === document.documentElement) return true;
  if (!frame.contains(target)) return false; // 悬浮按钮、提示、对话框等其他层
  for (let el: Element | null = target; el && el !== frame; el = el.parentElement) {
    if (isVisibleComponent(el)) return false;
  }
  return true;
}

export function usePeek(frame: Ref<HTMLElement | null>) {
  let timer = 0;
  let peeking = false;

  function set(on: boolean) {
    clearTimeout(timer);
    timer = 0;
    const el = frame.value;
    if (!el) return;
    const opacity = on ? 1 - peekTransparency.value / 100 : 1;
    peeking = on && opacity < 1;
    el.style.opacity = peeking ? String(opacity) : '';
  }

  function onMove(e: PointerEvent) {
    const el = frame.value;
    if (!el || peekTransparency.value <= 0 || !backdropImage.value || e.buttons) return set(false);
    if (!isEmpty(e.target, el)) return set(false);
    // 稍作停留再淡出，从一张卡片移到另一张时不会闪动
    if (!peeking && !timer) timer = window.setTimeout(() => set(true), 350);
  }
  const onLeave = () => set(false);

  watch(peekTransparency, () => peeking && set(true));
  onMounted(() => {
    window.addEventListener('pointermove', onMove, { passive: true });
    document.documentElement.addEventListener('pointerleave', onLeave);
    window.addEventListener('blur', onLeave);
  });
  onBeforeUnmount(() => {
    window.removeEventListener('pointermove', onMove);
    document.documentElement.removeEventListener('pointerleave', onLeave);
    window.removeEventListener('blur', onLeave);
    clearTimeout(timer);
  });
}
