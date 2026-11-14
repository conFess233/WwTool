import { ref } from 'vue';

// 非阻断提示（IUIStateService 的 Toast）：最多同时 3 条，相同消息合并。错误提示不包含敏感信息。
export interface Toast { id: number; text: string; kind: 'info' | 'success' | 'error' }

export const toasts = ref<Toast[]>([]);
let seq = 0;

export function toast(text: string, kind: Toast['kind'] = 'info', ms = 3600) {
  if (toasts.value.some(t => t.text === text)) return;
  const item = { id: ++seq, text, kind };
  toasts.value = [...toasts.value.slice(-2), item];
  window.setTimeout(() => (toasts.value = toasts.value.filter(t => t.id !== item.id)), ms);
}

export const toastError = (e: unknown) => toast((e as Error)?.message || '操作失败。', 'error', 5200);
