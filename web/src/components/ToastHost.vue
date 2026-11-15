<script setup lang="ts">
import { toasts } from './toast';
</script>

<template>
  <div class="toast-host" aria-live="polite">
    <TransitionGroup name="toast">
      <div v-for="t in toasts" :key="t.id" class="toast" :class="t.kind" role="status">
        <span class="dot" aria-hidden="true" />{{ t.text }}
      </div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.toast-host { position: fixed; top: 104px; right: 40px; z-index: 60; display: flex; flex-direction: column; gap: 8px; align-items: flex-end; pointer-events: none; }
.toast {
  display: flex; align-items: center; gap: 10px; max-width: 420px; padding: 10px 16px; border-radius: 999px;
  background: var(--surface-elevated); border: 1px solid var(--border); box-shadow: var(--shadow-menu); font-size: 13px; pointer-events: auto;
}
.dot { width: 8px; height: 8px; border-radius: 50%; background: var(--info); flex: none; }
.success .dot { background: var(--success); }
.error .dot { background: var(--danger); }
.toast-enter-active { transition: opacity var(--motion-standard) var(--ease-standard), transform var(--motion-standard) var(--ease-bounce); }
.toast-leave-active { transition: opacity var(--motion-fast) linear; }
.toast-enter-from { opacity: 0; transform: translateX(24px); }
.toast-leave-to { opacity: 0; }

/* 手机：在页头下方居中 */
@media (max-width: 768px) {
  .toast-host { top: calc(68px + env(safe-area-inset-top)); left: 12px; right: 12px; align-items: center; }
  .toast { max-width: 100%; border-radius: 18px; }
}
</style>
