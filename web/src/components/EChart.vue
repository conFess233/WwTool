<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch } from 'vue';
import * as echarts from 'echarts/core';
import { BarChart, LineChart, PieChart, ScatterChart } from 'echarts/charts';
import { GridComponent, TooltipComponent, LegendComponent, DataZoomComponent } from 'echarts/components';
import { SVGRenderer } from 'echarts/renderers';
import { preset, accent, effectiveReduced } from '../theme';
import type { ChartTokens } from './chart';

echarts.use([BarChart, LineChart, PieChart, ScatterChart, GridComponent, TooltipComponent, LegendComponent, DataZoomComponent, SVGRenderer]);

// ECharts 包装：颜色、字体、动画时长与缓动全部来自 CSS 令牌，切换主题时重新生成。
// option 由调用方用 tokens() 取得的颜色构建，所以接收一个函数。
const props = defineProps<{ option: (tok: ChartTokens) => echarts.EChartsCoreOption; height?: number }>();
const el = ref<HTMLElement>();
let chart: echarts.ECharts | undefined;
let observer: ResizeObserver | undefined;



function tokens(): ChartTokens {
  const s = getComputedStyle(document.documentElement);
  const v = (name: string) => s.getPropertyValue(name).trim();
  return {
    accent: v('--chart-primary') || v('--accent'), accentContainer: v('--accent-container'), danger: v('--danger'), success: v('--success'),
    info: v('--info'), fourStar: v('--four-star'), fourStarContainer: v('--four-star-container'), track: v('--track'),
    grid: v('--chart-grid'), text: v('--text-primary'), muted: v('--text-muted'), surface: v('--surface-elevated'), border: v('--border'),
    font: v('--font-ui'), secondary: v('--chart-secondary'), tertiary: v('--chart-tertiary'),
  };
}

function render() {
  if (!el.value) return;
  chart ??= echarts.init(el.value, undefined, { renderer: 'svg' });
  const tok = tokens();
  const base: echarts.EChartsCoreOption = {
    animation: !effectiveReduced.value,
    animationDuration: 420,
    animationEasing: 'cubicOut',
    textStyle: { fontFamily: tok.font, color: tok.muted },
    tooltip: { backgroundColor: tok.surface, borderColor: tok.border, textStyle: { color: tok.text, fontFamily: tok.font, fontSize: 12 }, confine: true },
  };
  chart.setOption({ ...base, ...props.option(tok) }, true);
}

onMounted(() => {
  render();
  observer = new ResizeObserver(() => chart?.resize());
  if (el.value) observer.observe(el.value);
});
onBeforeUnmount(() => {
  observer?.disconnect();
  chart?.dispose();
});
watch(() => props.option, render);
// 主题切换后 CSS 变量已更新，下一帧重新取色
watch([preset, accent], () => requestAnimationFrame(render));
</script>

<template>
  <div ref="el" class="echart" :style="{ height: `${height ?? 200}px` }" />
</template>

<style scoped>
.echart { width: 100%; }
</style>
