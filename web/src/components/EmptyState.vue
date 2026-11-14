<script setup lang="ts">
// 空状态与异常（Controls/EmptyState.cs）：居中布局，76 圆形图标底（Accent Soft；danger 时 Danger Soft），标题 + 说明 + 可选操作。
defineProps<{ title: string; hint?: string; icon?: string; danger?: boolean }>();
</script>

<template>
  <div class="empty card">
    <div class="badge" :class="{ danger }">
      <span class="icon" :style="{ '--icon': `url(/icons/${icon ?? 'Home'}.png)` }" aria-hidden="true" />
    </div>
    <h2 class="card-title">{{ title }}</h2>
    <p v-if="hint" class="caption hint">{{ hint }}</p>
    <slot />
  </div>
</template>

<style scoped>
.empty { display: flex; flex-direction: column; align-items: center; gap: 8px; padding: 56px 22px; text-align: center; }
.badge { width: 76px; height: 76px; border-radius: 50%; display: grid; place-items: center; background: var(--accent-container); color: var(--accent-text); margin-bottom: 6px; }
.badge.danger { background: var(--danger-soft); color: var(--danger); }
.icon { width: 34px; height: 34px; background: currentColor; -webkit-mask: var(--icon) center / contain no-repeat; mask: var(--icon) center / contain no-repeat; }
.hint { margin: 0; max-width: 420px; }
</style>
