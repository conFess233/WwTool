<script setup lang="ts" generic="T extends string | number">
import { ref, computed, onBeforeUnmount, watch } from 'vue';
import StrokeIcon from './StrokeIcon.vue';

// 筛选下拉（抽卡统计的「时间范围」「过滤五星」）：胶囊里是小字标签 + 粗体当前值 + 下箭头，展开为浮层菜单。
const props = defineProps<{ label: string; options: { value: T; text: string }[] }>();
const model = defineModel<T>({ required: true });
const open = ref(false);
const root = ref<HTMLElement | null>(null);
const current = computed(() => props.options.find(o => o.value === model.value)?.text ?? '');

function pick(v: T) {
  model.value = v;
  open.value = false;
}
function onDoc(e: MouseEvent) {
  if (!root.value?.contains(e.target as Node)) open.value = false;
}
function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') open.value = false;
}
watch(open, v => {
  if (v) { document.addEventListener('mousedown', onDoc); document.addEventListener('keydown', onKey); }
  else { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey); }
});
onBeforeUnmount(() => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey); });
</script>

<template>
  <div ref="root" class="pill-select">
    <button class="pill" :aria-expanded="open" aria-haspopup="listbox" @click="open = !open">
      <span class="label">{{ label }}</span>
      <strong class="value">{{ current }}</strong>
      <StrokeIcon name="ChevronDown" :size="16" class="chev" :class="{ open }" />
    </button>
    <Transition name="pop">
      <ul v-if="open" class="menu" role="listbox">
        <li v-for="o in options" :key="String(o.value)" role="option" :aria-selected="o.value === model">
          <button @click="pick(o.value)">{{ o.text }}</button>
        </li>
      </ul>
    </Transition>
  </div>
</template>

<style scoped>
.pill-select { position: relative; }
.pill {
  height: 44px; display: flex; align-items: center; gap: 8px; padding: 0 14px 0 18px; border-radius: 10px; cursor: pointer;
  background: var(--surface); border: 1px solid var(--border); color: var(--text-primary);
  transition: border-color var(--motion-fast) var(--ease-standard);
}
.pill:hover, .pill[aria-expanded='true'] { border-color: var(--accent-line); }
.label { font-size: 14px; color: var(--text-muted); }
.value { font-size: 14px; white-space: nowrap; }
.chev { margin-left: 14px; color: var(--text-secondary); transition: transform var(--motion-standard) var(--ease-standard); }
.chev.open { transform: rotate(180deg); }
.menu {
  position: absolute; right: 0; top: calc(100% + 6px); z-index: 30; min-width: 100%; margin: 0; padding: 6px; list-style: none;
  background: var(--surface); border: 1px solid var(--border); border-radius: 10px; box-shadow: var(--shadow-menu);
}
.menu button { width: 100%; text-align: left; padding: 8px 12px; border: 0; border-radius: 6px; background: transparent; cursor: pointer; white-space: nowrap; font-size: 14px; }
.menu button:hover { background: var(--surface-hover); }
.menu [aria-selected='true'] button { color: var(--accent-text); font-weight: 700; background: var(--accent-container); }
.pop-enter-active, .pop-leave-active { transition: opacity var(--motion-fast) var(--ease-standard), transform var(--motion-fast) var(--ease-standard); }
.pop-enter-from, .pop-leave-to { opacity: 0; transform: translateY(-4px); }
</style>
