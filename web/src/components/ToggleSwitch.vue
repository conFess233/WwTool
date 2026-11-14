<script setup lang="ts">
// 开关：立即生效的布尔设置；动画遵循 MotionFast，滑块带回弹，可中途反向。
const model = defineModel<boolean>({ required: true });
defineProps<{ label: string }>();
</script>

<template>
  <button class="switch" role="switch" :aria-checked="model" :aria-label="label" @click="model = !model">
    <span class="knob" />
  </button>
</template>

<style scoped>
.switch {
  position: relative; width: 44px; height: 24px; border-radius: 999px; border: 1px solid var(--track); cursor: pointer; padding: 0;
  background: var(--track); transition: background-color var(--motion-fast) var(--ease-standard), border-color var(--motion-fast) var(--ease-standard);
}
.switch[aria-checked='true'] { background: var(--accent); border-color: var(--accent); }
.knob {
  position: absolute; top: 2px; left: 2px; width: 18px; height: 18px; border-radius: 50%; background: #fff; box-shadow: var(--shadow-soft);
  transition: transform var(--motion-fast) var(--ease-bounce);
}
.switch[aria-checked='true'] .knob { transform: translateX(20px); }
</style>
