<script setup lang="ts">
import icons from '../generated/icons.json';

// 线条图标（StrokeIcon.cs）：路径来自 src/UI/Theming/Icons.axaml，24×24 视窗，描边跟随文字颜色。
withDefaults(defineProps<{ name: string; size?: number; stroke?: number }>(), { size: 18, stroke: 2 });
const paths = icons as Record<string, string>;
</script>

<template>
  <svg :width="size" :height="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" :stroke-width="stroke"
    stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
    <path :d="paths[name]" />
  </svg>
</template>
