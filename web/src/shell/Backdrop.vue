<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch } from 'vue';
import { backdropImage, backdropSlogan, backdropVideoUrl, reloadBackdrop } from './backdrop';
import { effectiveReduced } from '../theme';

// 窗口背景层（MainWindow.axaml）：主题背景色 + 底纹；有背景图时铺满（四周多留 14 供视差），
// 左侧叠一层背景色遮罩（45% 不透明渐隐到 0，止于 45% 宽处），标语图叠在遮罩之上。
const video = ref<HTMLVideoElement | null>(null);
const hasFrame = ref(false);
const imageLoaded = ref(false);
const offset = ref({ x: 0, y: 0 });

watch(backdropImage, () => (imageLoaded.value = false));
watch(backdropVideoUrl, () => (hasFrame.value = false));

// 视差：指针相对窗口中心最多移动 14
let raf = 0;
function onMove(e: PointerEvent) {
  if (effectiveReduced.value || raf) return;
  raf = requestAnimationFrame(() => {
    raf = 0;
    offset.value = { x: (0.5 - e.clientX / innerWidth) * 14, y: (0.5 - e.clientY / innerHeight) * 14 };
  });
}
watch(effectiveReduced, r => r && (offset.value = { x: 0, y: 0 }));

// 动态背景只在设置里打开时出现，与「减少动画」无关（同桌面端）；首帧出来后再淡入，并确保静音自动播放
function onFrame() {
  hasFrame.value = true;
  const v = video.value;
  if (v && !document.hidden) { v.muted = true; v.play().catch(() => undefined); }
}

// 页面不可见时暂停视频
function onVisibility() {
  const v = video.value;
  if (!v) return;
  if (document.hidden) v.pause();
  else v.play().catch(() => undefined);
}

onMounted(() => {
  reloadBackdrop();
  window.addEventListener('pointermove', onMove, { passive: true });
  document.addEventListener('visibilitychange', onVisibility);
});
onBeforeUnmount(() => {
  window.removeEventListener('pointermove', onMove);
  document.removeEventListener('visibilitychange', onVisibility);
  cancelAnimationFrame(raf);
});
</script>

<template>
  <div class="backdrop" aria-hidden="true">
    <div class="texture" :class="{ hidden: backdropImage && imageLoaded }" />
    <div v-if="backdropImage" class="art" :class="{ shown: imageLoaded }"
      :style="{ transform: `translate(${offset.x}px, ${offset.y}px)` }">
      <img class="fill" :src="backdropImage" alt="" @load="imageLoaded = true" @error="imageLoaded = false" />
      <video v-if="backdropVideoUrl" ref="video" class="fill" :class="{ shown: hasFrame }" :src="backdropVideoUrl"
        autoplay muted loop playsinline @loadeddata="onFrame" />
    </div>
    <div v-if="backdropImage && imageLoaded" class="scrim" />
    <img v-if="backdropImage && imageLoaded && backdropSlogan" class="slogan fill" :src="backdropSlogan" alt=""
      :style="{ transform: `translate(${offset.x}px, ${offset.y}px)` }" />
  </div>
</template>

<style scoped>
.backdrop { position: fixed; inset: 0; z-index: -1; overflow: hidden; pointer-events: none; background: var(--background); }
/* 默认底纹：40 网格细线 + 右上暖光、左下强调色光晕（BackdropTexture） */
.texture {
  position: absolute; inset: 0; transition: opacity var(--motion-relaxed) var(--ease-standard);
  background:
    radial-gradient(circle farthest-corner at 92% 6%, color-mix(in srgb, color-mix(in srgb, var(--glow), white 28%) calc(var(--glow-opacity) * 100%), transparent) 0, transparent 34%),
    radial-gradient(circle farthest-corner at 4% 96%, color-mix(in srgb, var(--glow) calc(var(--glow-opacity) * 60%), transparent) 0, transparent 30%),
    linear-gradient(to right, color-mix(in srgb, var(--grid-line) calc(var(--grid-line-opacity) * 100%), transparent) 1px, transparent 1px) -0.5px 0 / 40px 40px,
    linear-gradient(to bottom, color-mix(in srgb, var(--grid-line) calc(var(--grid-line-opacity) * 100%), transparent) 1px, transparent 1px) 0 -0.5px / 40px 40px;
}
.texture.hidden { opacity: 0; }
.art, .slogan { position: absolute; inset: -14px; transition: transform 600ms var(--ease-smooth); }
.art { opacity: 0; transition: opacity var(--motion-relaxed) var(--ease-standard), transform 600ms var(--ease-smooth); }
.art.shown { opacity: 1; }
.fill { width: 100%; height: 100%; object-fit: cover; }
.art .fill { position: absolute; inset: 0; }
video.fill { opacity: 0; transition: opacity var(--motion-relaxed) linear; }
video.fill.shown { opacity: 1; }
.scrim { position: absolute; inset: 0; background: linear-gradient(to right, color-mix(in srgb, var(--background) 45%, transparent) 0, transparent 45%); }
</style>
