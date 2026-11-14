<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue';

// 刻度条（Controls/SegmentedBar.cs）：轨道与填充都是等宽直角刻度，从左端排起。默认高 10、刻宽 5、刻隙 3；细版高 8。
// 单元素绘制（CSS 重复渐变），不为每一格创建元素。数值须另以文字显示。
const props = withDefaults(defineProps<{ value: number; max?: number; thin?: boolean; danger?: boolean }>(), { max: 1 });
const el = ref<HTMLElement>();
const width = ref(0);
const TICK = 5;
const GAP = 3;
let observer: ResizeObserver | undefined;
onMounted(() => {
  observer = new ResizeObserver(([e]) => (width.value = e.contentRect.width));
  if (el.value) observer.observe(el.value);
});
onBeforeUnmount(() => observer?.disconnect());

const segments = computed(() => Math.max(1, Math.floor((width.value + GAP) / (TICK + GAP))));
const filled = computed(() => Math.round(Math.min(1, Math.max(0, props.value / (props.max || 1))) * segments.value));
const fillWidth = computed(() => (filled.value === 0 ? 0 : filled.value * (TICK + GAP) - GAP));
</script>

<template>
  <div ref="el" class="seg" :class="{ thin, danger }" role="presentation">
    <div class="fill" :style="{ width: `${fillWidth}px` }" />
  </div>
</template>

<style scoped>
.seg {
  position: relative; height: 10px;
  background: repeating-linear-gradient(90deg, var(--segment-track) 0 5px, transparent 5px 8px);
}
.seg.thin { height: 8px; }
.fill {
  position: absolute; inset: 0 auto 0 0;
  background: repeating-linear-gradient(90deg, var(--accent) 0 5px, transparent 5px 8px);
  transition: width var(--motion-relaxed) var(--ease-standard);
}
.danger .fill { background: repeating-linear-gradient(90deg, var(--danger) 0 5px, transparent 5px 8px); }
</style>
