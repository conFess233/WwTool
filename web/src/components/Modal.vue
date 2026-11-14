<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, nextTick } from 'vue';

// 对话框（IDialogService 的窗口内覆盖层）：遮罩淡入，内容小幅缩放上移进入；Escape 关闭，焦点圈定在对话框内。
const props = defineProps<{ title: string; width?: number; busy?: boolean }>();
const emit = defineEmits<{ close: [] }>();
const dialog = ref<HTMLElement>();
let previous: Element | null = null;

function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape' && !props.busy) emit('close');
  if (e.key === 'Tab' && dialog.value) {
    const items = [...dialog.value.querySelectorAll<HTMLElement>('button, input, textarea, select, a[href], [tabindex]:not([tabindex="-1"])')].filter(el => !el.hasAttribute('disabled'));
    if (!items.length) return;
    const first = items[0], last = items[items.length - 1];
    if (e.shiftKey && document.activeElement === first) { last.focus(); e.preventDefault(); }
    else if (!e.shiftKey && document.activeElement === last) { first.focus(); e.preventDefault(); }
  }
}

onMounted(async () => {
  previous = document.activeElement;
  document.addEventListener('keydown', onKey);
  await nextTick();
  dialog.value?.querySelector<HTMLElement>('[autofocus], input, textarea, button.primary, button')?.focus();
});
onBeforeUnmount(() => {
  document.removeEventListener('keydown', onKey);
  (previous as HTMLElement | null)?.focus?.();
});
</script>

<template>
  <Teleport to="body">
    <div class="scrim" @mousedown.self="!busy && emit('close')">
      <div ref="dialog" class="dialog" role="dialog" aria-modal="true" :aria-label="title" :style="{ width: `${width ?? 480}px` }">
        <h2 class="dialog-title">{{ title }}</h2>
        <slot />
        <div v-if="$slots.actions" class="actions"><slot name="actions" /></div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
@keyframes scrim-in { from { opacity: 0; } }
@keyframes dialog-in { from { opacity: 0; transform: translateY(12px) scale(0.97); } }
.scrim { position: fixed; inset: 0; z-index: 50; display: grid; place-items: center; background: var(--scrim); animation: scrim-in var(--motion-standard) var(--ease-standard); }
.dialog {
  max-width: calc(100vw - 32px); max-height: calc(100vh - 48px); overflow: auto;
  background: var(--surface-elevated); border: 1px solid var(--border); border-radius: var(--radius-dialog); box-shadow: var(--shadow-dialog);
  padding: 22px 24px; animation: dialog-in var(--motion-relaxed) var(--ease-standard);
}
.dialog-title { font-size: 18px; font-weight: 700; margin: 0 0 12px; }
.actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 20px; }
:global(.reduced-motion) .dialog { animation: fade-in var(--motion-fast) linear; }
</style>
