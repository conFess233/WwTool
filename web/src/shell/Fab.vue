<script setup lang="ts">
import { ref, computed, watch, onBeforeUnmount } from 'vue';
import { fab, type FabAction } from './fab';
import { t } from '../i18n';
import StrokeIcon from '../components/StrokeIcon.vue';

// 悬浮按钮（src/UI/Shell/FabMenu.axaml）：右 40、下 26；56 圆形强调色；菜单项为白色胶囊（文字 + 圆形图标），自下而上展开。
const open = ref(false);
const state = computed(() => fab.value);
const extended = computed(() => !!state.value?.trigger);
const menuItems = computed(() => (state.value ? (extended.value ? state.value.actions.slice(1) : state.value.actions) : []));
const isImage = (icon: string) => icon.endsWith('.png');

watch(fab, () => (open.value = false));

function onTrigger() {
  const s = state.value;
  if (!s) return;
  if (s.trigger) {
    if (!s.busy) s.actions[0]?.run();
    return;
  }
  open.value = !open.value;
}

function run(a: FabAction) {
  open.value = false;
  a.run();
}

function onDocClick(e: MouseEvent) {
  if (!(e.target as HTMLElement).closest('.fab')) open.value = false;
}
watch(open, v => (v ? document.addEventListener('click', onDocClick) : document.removeEventListener('click', onDocClick)));
onBeforeUnmount(() => document.removeEventListener('click', onDocClick));
</script>

<template>
  <div v-if="state && state.actions.length" class="fab" :class="{ open }" @mouseleave="open = false">
    <!-- 有主操作时，悬停展开其余操作 -->
    <TransitionGroup tag="div" name="fab-item" class="menu" :class="{ hover: extended }">
      <button v-for="(a, i) in menuItems" v-show="open || extended" :key="a.key" class="item" :class="{ danger: a.danger }"
        :style="{ '--i': menuItems.length - i }" @click="run(a)">
        <span class="item-title">{{ a.title }}</span>
        <span class="item-icon">
          <span v-if="isImage(a.icon)" class="mask" :style="{ '--icon': `url(/icons/${a.icon})` }" />
          <StrokeIcon v-else :name="a.icon" :size="18" />
        </span>
      </button>
    </TransitionGroup>
    <button class="trigger" :class="{ extended }" :aria-expanded="open" :aria-label="state.trigger?.title ?? t('Fab_Open')"
      @click.stop="onTrigger" @mouseenter="extended && menuItems.length && (open = true)">
      <template v-if="state.trigger">
        <span v-if="state.busy" class="spinner" aria-hidden="true" />
        <StrokeIcon v-else :name="state.trigger.icon" :size="20" />
        <span class="trigger-title">{{ state.trigger.title }}</span>
      </template>
      <StrokeIcon v-else name="Plus" :size="22" :stroke="2.6" class="plus" />
    </button>
  </div>
</template>

<style scoped>
.fab { position: fixed; right: 40px; bottom: 26px; z-index: 40; display: flex; flex-direction: column; align-items: flex-end; gap: 10px; }
.menu { display: flex; flex-direction: column; align-items: flex-end; gap: 10px; }
.menu.hover .item { display: none; }
.fab.open .menu.hover .item { display: flex; }
.item {
  display: flex; align-items: center; gap: 12px; padding: 5px 5px 5px 18px; border-radius: 999px; border: 1px solid var(--border);
  background: var(--surface); color: var(--text-primary); font: inherit; font-size: 14px; font-weight: 600; cursor: pointer;
  box-shadow: var(--shadow-menu);
  transition: border-color var(--motion-fast) var(--ease-standard), transform var(--motion-fast) var(--ease-standard);
}
.item:hover { border-color: var(--accent-line); }
.item.danger { color: var(--danger); }
.item-icon { width: 36px; height: 36px; border-radius: 50%; display: grid; place-items: center; background: var(--accent-container); color: var(--accent-text); }
.mask { width: 18px; height: 18px; background: currentColor; -webkit-mask: var(--icon) center / contain no-repeat; mask: var(--icon) center / contain no-repeat; }
.trigger {
  height: 56px; min-width: 56px; border-radius: 28px; border: 0; padding: 0; display: grid; place-items: center;
  background: var(--accent); color: var(--text-on-accent); cursor: pointer; box-shadow: var(--shadow-fab);
  transition: background-color var(--motion-fast) var(--ease-standard), transform var(--motion-fast) var(--ease-bounce);
}
.trigger:hover { background: var(--accent-hover); }
.trigger:active { transform: scale(0.96); }
.trigger.extended { display: flex; gap: 10px; padding: 0 26px 0 22px; font: inherit; font-size: 16px; font-weight: 700; }
.plus { transition: transform var(--motion-standard) var(--ease-bounce); }
.fab.open .plus { transform: rotate(45deg); }
.spinner { width: 20px; height: 20px; border-radius: 50%; border: 2.5px solid currentColor; border-right-color: transparent; animation: spin 0.8s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
.fab-item-enter-active { transition: opacity var(--motion-fast) var(--ease-standard), transform var(--motion-standard) var(--ease-bounce); transition-delay: calc(var(--i) * 30ms); }
.fab-item-leave-active { transition: opacity 120ms linear; }
.fab-item-enter-from { opacity: 0; transform: translateY(10px) scale(0.96); }
.fab-item-leave-to { opacity: 0; }
</style>
