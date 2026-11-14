<script setup lang="ts">
import { ref, watch, onMounted, onBeforeUnmount, nextTick } from 'vue';
import { useRouter } from 'vue-router';
import type { PageDescriptor } from '../pages';
import { t, language } from '../i18n';

// 悬浮岛（FloatingNavIsland.axaml）：Ink 底胶囊，内边距 5、项间距 4；未选中项 44×44 只显示 22 图标（75% 不透明）；
// 选中项展开为「图标 + 页面名」，左右内边距 14 / 18；选中底色是独立的强调色胶囊，切换时带回弹滑到新位置（320ms）。
const props = defineProps<{ pages: PageDescriptor[]; current: string }>();
const router = useRouter();
const island = ref<HTMLElement>();
const pill = ref({ x: 0, width: 0, ready: false });

function measure() {
  const el = island.value?.querySelector<HTMLElement>(`[data-key="${props.current}"]`);
  if (!island.value) return;
  if (!el) {
    // 当前页面不在悬浮岛中（账号数据等）：胶囊收起
    pill.value = { ...pill.value, width: 0 };
    return;
  }
  const host = island.value.getBoundingClientRect();
  const r = el.getBoundingClientRect();
  pill.value = { ...pill.value, x: r.left - host.left - island.value.clientLeft, width: r.width };
}

let observer: ResizeObserver | undefined;
onMounted(() => {
  measure();
  observer = new ResizeObserver(measure);
  if (island.value) observer.observe(island.value);
  // 首次定位后再开启过渡，避免页面加载时胶囊从左侧滑入
  requestAnimationFrame(() => requestAnimationFrame(() => (pill.value.ready = true)));
});
onBeforeUnmount(() => observer?.disconnect());
watch(() => [props.current, language.value], () => nextTick(measure));

function go(page: PageDescriptor) {
  if (page.key !== props.current) router.push(page.path);
}
</script>

<template>
  <nav ref="island" class="island" :aria-label="t('Web_AppName')">
    <span class="pill" :class="{ animated: pill.ready }" :style="{ width: `${pill.width}px`, transform: `translateX(${pill.x}px)` }" aria-hidden="true" />
    <template v-for="page in pages" :key="page.key">
      <span v-if="page.startsGroup" class="separator" aria-hidden="true" />
      <button
        class="item"
        :class="{ selected: page.key === current }"
        :data-key="page.key"
        :aria-current="page.key === current ? 'page' : undefined"
        :aria-label="t(page.titleKey)"
        @click="go(page)"
      >
        <span class="icon" :style="{ '--icon': `url(/icons/${page.icon}.png)` }" aria-hidden="true" />
        <span v-if="page.key === current" class="label">{{ t(page.titleKey) }}</span>
        <span v-else class="tip" role="tooltip">{{ t(page.titleKey) }}</span>
      </button>
    </template>
  </nav>
</template>

<style scoped>
.island {
  position: relative; display: flex; align-items: center; gap: 4px; padding: 5px;
  background: var(--nav-island); border: 1px solid var(--nav-island-border); border-radius: 999px; box-shadow: var(--shadow-island);
}
.pill { position: absolute; left: 0; top: 5px; height: 44px; border-radius: 999px; background: var(--accent); pointer-events: none; }
.pill.animated { transition: transform 320ms var(--ease-bounce), width 320ms var(--ease-bounce), background-color var(--motion-standard) var(--ease-standard); }
.separator { width: 1px; height: 22px; margin: 0 6px; background: rgb(255 255 255 / 0.2); }

.item {
  position: relative; z-index: 1; display: flex; align-items: center; justify-content: center; gap: 8px;
  height: 44px; min-width: 44px; padding: 0 11px; border: 0; border-radius: 999px; cursor: pointer;
  background: transparent; color: var(--text-on-nav-island);
  transition: background-color 150ms linear;
}
.item:hover:not(.selected) { background: rgb(255 255 255 / 0.12); }
.item.selected { padding: 0 18px 0 14px; color: var(--text-on-accent); cursor: default; }
.item:focus-visible { outline: none; box-shadow: inset 0 0 0 2px var(--accent-line); }

/* 图标作为遮罩，用前景色填充：任何主题下都是单色且对比清晰 */
.icon { width: 22px; height: 22px; background: currentColor; opacity: 0.75; -webkit-mask: var(--icon) center / contain no-repeat; mask: var(--icon) center / contain no-repeat; }
.selected .icon { opacity: 1; }
.label { font-size: 14px; font-weight: 600; white-space: nowrap; }

/* 悬停提示气泡：Ink 底、圆角 8、内边距 7 / 12，顶部小三角；下移 6 淡入 180ms，延迟 300ms */
.tip {
  position: absolute; top: calc(100% + 14px); left: 50%; translate: -50% -6px; opacity: 0; pointer-events: none;
  padding: 7px 12px; border-radius: 8px; background: var(--nav-island); color: #fff; font-size: 13px; font-weight: 600; white-space: nowrap;
  box-shadow: 0 10px 20px -10px rgb(40 28 10 / 0.6);
  transition: opacity 180ms var(--ease-standard), translate 180ms var(--ease-standard);
}
.tip::before { content: ''; position: absolute; top: -6px; left: 50%; translate: -50% 0; border: 6px solid transparent; border-top: 0; border-bottom-color: var(--nav-island); }
.item:hover .tip, .item:focus-visible .tip { opacity: 1; translate: -50% 0; transition-delay: 300ms; }
</style>
