<script setup lang="ts">
import { computed } from 'vue';
import ToggleSwitch from '../components/ToggleSwitch.vue';
import { t, language, languages, setLanguage, type Language } from '../i18n';
import { preset, accent, presets, accents, presetSwatch, accentSwatch, reducedMotion, setReducedMotion, applyTheme } from '../theme';
import { backdropMode, backdropVideo, peekTransparency, customImageName, setCustomImage } from '../shell/backdrop';
import { toastError } from '../components/toast';
import { fetchCatalog } from '../format';
import { ref, onMounted } from 'vue';

// 界面设置卡（SettingsView.axaml「界面设置」）：主题色块、强调色点、显示语言、减少动画。
const languageNames: Record<Language, string> = { 'zh-Hans': '简体中文', en: 'English', ja: '日本語' };

function origin(e: MouseEvent) {
  const r = (e.currentTarget as HTMLElement).getBoundingClientRect();
  return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
}
function pickPreset(name: string, e: MouseEvent) {
  if (name !== preset.value) applyTheme(() => (preset.value = name), origin(e));
}
function pickAccent(name: string, e: MouseEvent) {
  if (name !== accent.value) applyTheme(() => (accent.value = name), origin(e));
}
const reduced = computed({ get: () => reducedMotion.value, set: v => setReducedMotion(v) });

// 窗口背景：默认（启动器背景）或自选图片；自选图片只保存在本浏览器
const fileInput = ref<HTMLInputElement | null>(null);
async function onPick(e: Event) {
  const input = e.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file) return;
  try {
    await setCustomImage(file);
  } catch (err) {
    toastError(err);
  }
}
function useCustom() {
  if (customImageName.value) backdropMode.value = 'custom';
  else fileInput.value?.click();
}

// 游戏资料：角色、武器、音乐专辑、摩托的条目数与资料版本
const catalog = ref<{ key: string; count: number }[]>([]);
const catalogVersion = ref('');
onMounted(async () => {
  const files = [['Characters', 'Catalog_Characters'], ['Weapons', 'Catalog_Weapons'], ['Albums', 'Catalog_Albums'], ['Motors', 'Catalog_Motors']];
  catalog.value = await Promise.all(files.map(async ([f, key]) => {
    try {
      const d = await fetchCatalog<{ version?: string; items?: Record<string, unknown> }>(f);
      if (!catalogVersion.value && d.version) catalogVersion.value = d.version;
      return { key, count: Object.keys(d.items ?? {}).length };
    } catch {
      return { key, count: 0 };
    }
  }));
});

const repo = 'https://github.com/conFess233/WwTool';
const version = __APP_VERSION__;
</script>

<template>
  <div class="settings stagger">
    <section class="card set" style="--i: 0">
      <h2 class="ti">{{ t('Settings_UI') }}</h2>

      <div class="sr first">
        <div class="name">{{ t('Settings_Theme') }}</div>
        <div class="swatches" role="radiogroup" :aria-label="t('Settings_Theme')">
          <button
            v-for="p in presets" :key="p" class="swatch" role="radio" :aria-checked="p === preset"
            :aria-label="t(`Theme_${p}`)" @click="pickPreset(p, $event)"
          >
            <span class="orb">
              <span class="ring" :class="{ selected: p === preset }" />
              <span class="dot-fill" :style="{ background: presetSwatch(p).background, borderColor: presetSwatch(p).border }" />
            </span>
            <span class="swatch-name">{{ t(`Theme_${p}`) }}</span>
          </button>
        </div>
      </div>

      <div class="sr row">
        <div class="name">{{ t('Settings_AccentTheme') }}</div>
        <div class="accents" role="radiogroup" :aria-label="t('Settings_AccentTheme')">
          <button class="chip" role="radio" :aria-checked="accent === 'FollowTheme'" @click="pickAccent('FollowTheme', $event)">{{ t('Theme_Follow') }}</button>
          <button
            v-for="a in accents.slice(1)" :key="a" class="swatch accent-dot" role="radio" :aria-checked="a === accent"
            :aria-label="t(`Theme_${a}`)" :title="t(`Theme_${a}`)" @click="pickAccent(a, $event)"
          >
            <span class="orb small">
              <span class="ring dot" :class="{ selected: a === accent }" />
              <span class="dot-fill" :style="{ background: accentSwatch(a), borderColor: 'transparent' }" />
            </span>
          </button>
        </div>
      </div>

      <div class="sr row">
        <div>
          <div class="name">{{ t('Settings_WindowBackground') }}</div>
          <div class="hint">{{ t('Settings_WindowBackgroundHint') }}</div>
        </div>
        <div class="bg-opts" role="radiogroup">
          <button class="chip" role="radio" :aria-checked="backdropMode === 'default'" @click="backdropMode = 'default'">{{ t('Settings_BackgroundTexture') }}</button>
          <button class="chip" role="radio" :aria-checked="backdropMode === 'custom'" @click="useCustom">{{ t('Settings_BackgroundCustom') }}</button>
          <button class="btn" :disabled="backdropMode !== 'custom'" @click="fileInput?.click()">{{ t('Settings_PickImage') }}</button>
          <input ref="fileInput" type="file" accept="image/*" hidden @change="onPick" />
        </div>
      </div>

      <div class="sr row">
        <div>
          <div class="name">{{ t('Settings_DynamicBackground') }}</div>
          <div class="hint">{{ backdropMode === 'custom' ? t('Settings_DynamicBackgroundCustomHint') : t('Web_DynamicBackgroundHint') }}</div>
        </div>
        <ToggleSwitch v-model="backdropVideo" :label="t('Settings_DynamicBackground')" />
      </div>

      <div class="sr">
        <div class="name">{{ t('Settings_BackgroundPeek') }}</div>
        <div class="hint">{{ t('Settings_BackgroundPeekHint') }}</div>
        <div class="slider-row">
          <span class="hint">{{ t('Settings_BackgroundPeekLevel') }}</span>
          <input v-model.number="peekTransparency" class="slider" type="range" min="0" max="100" step="5" :aria-label="t('Settings_BackgroundPeekLevel')"
            :style="{ '--p': `${peekTransparency}%` }" />
          <span class="hint pct">{{ peekTransparency }}%</span>
        </div>
      </div>

      <div class="sr row">
        <div class="name">{{ t('Settings_AppLanguage') }}</div>
        <select class="select" :value="language" :aria-label="t('Settings_AppLanguage')" @change="setLanguage(($event.target as HTMLSelectElement).value as Language)">
          <option v-for="l in languages" :key="l" :value="l">{{ languageNames[l] }}</option>
        </select>
      </div>

      <div class="sr row">
        <div>
          <div class="name">{{ t('Settings_ReducedMotion') }}</div>
          <div class="hint">{{ t('Settings_ReducedMotionHint') }}</div>
        </div>
        <ToggleSwitch v-model="reduced" :label="t('Settings_ReducedMotion')" />
      </div>
    </section>

    <div class="col">
      <section class="card set" style="--i: 1">
        <h2 class="ti">{{ t('Catalog_Title') }}</h2>
        <div v-for="(c, i) in catalog" :key="c.key" class="sr row" :class="{ first: i === 0 }">
          <div class="name">{{ t(c.key) }}</div>
          <span class="hint">{{ t('Catalog_Count', c.count) }}</span>
        </div>
        <p class="hint status">{{ catalogVersion ? t('Web_CatalogVersion', catalogVersion) : t('Catalog_StatusNever') }}</p>
      </section>

      <section class="card set" style="--i: 2">
        <div class="about-head">
          <h2 class="ti">{{ t('About_Title') }}</h2>
          <span class="hint">{{ t('Settings_AboutVersion', version, '告白') }}</span>
          <a class="btn link" :href="repo" target="_blank" rel="noopener noreferrer">{{ t('About_Github') }}</a>
        </div>
        <p class="hint about-text">{{ t('About_DisclaimerText') }}</p>
      </section>
    </div>
  </div>
</template>

<style scoped>
.settings { display: grid; grid-template-columns: 1fr 1fr; gap: var(--card-gap); align-items: start; }
.ti { font-size: 15px; font-weight: 700; margin: 0 0 12px; }
.sr { border-top: 1px solid var(--divider); padding: 7px 0; min-height: 44px; }
.sr.first { border-top: 0; }
.sr.row { display: flex; align-items: center; justify-content: space-between; gap: 16px; }
.name { font-size: 14px; color: var(--text-primary); }
.sr.first .name { margin-bottom: 12px; }
.hint { font-size: 12px; color: var(--text-muted); }

.swatches { display: flex; gap: 16px; margin: -5px 0 0 -5px; }
.swatch { display: flex; flex-direction: column; align-items: center; gap: 1px; padding: 0; border: 0; background: transparent; cursor: pointer; }
.swatch-name { font-size: 12px; color: var(--text-secondary); }
/* 主题圆形色块 38；四周留 5 放选中双环（间隙 3 + 环 2）；悬停放大 1.08 */
.orb { position: relative; width: 48px; height: 48px; transition: transform 150ms var(--ease-standard); }
.orb.small { width: 30px; height: 30px; }
.swatch:hover .orb { transform: scale(1.08); }
.ring { position: absolute; inset: 0; border-radius: 50%; border: 2px solid transparent; transition: border-color 200ms linear; }
.ring.selected { border-color: var(--accent); }
.ring.dot.selected { border-color: var(--text-primary); }
.dot-fill { position: absolute; inset: 5px; border-radius: 50%; border: 1px solid; }
.orb.small .dot-fill { inset: 4px; }
.accents { display: flex; align-items: center; gap: 2px; }
.accents .chip { margin-right: 14px; }
.col { display: flex; flex-direction: column; gap: var(--card-gap); }
.set { padding: 22px 28px; }
.ti { font-size: 18px; }
.name { font-size: 16px; font-weight: 700; }
.hint { font-size: 13px; }
.sr { padding: 12px 0; }
.bg-opts { display: flex; gap: 8px; align-items: center; flex: none; }
.slider-row { display: flex; align-items: center; gap: 14px; margin-top: 18px; }
.slider { flex: 1; appearance: none; height: 4px; border-radius: 2px; cursor: pointer;
  background: linear-gradient(to right, var(--accent) var(--p), var(--track) var(--p)); }
.slider::-webkit-slider-thumb { appearance: none; width: 22px; height: 22px; border-radius: 50%; background: var(--accent); border: 3px solid var(--surface); box-shadow: 0 0 0 1px var(--accent-line); }
.slider::-moz-range-thumb { width: 18px; height: 18px; border-radius: 50%; background: var(--accent); border: 3px solid var(--surface); }
.pct { min-width: 40px; text-align: right; }
.select { height: 42px; min-width: 186px; padding: 0 12px; border-radius: 10px; border: 1px solid var(--border); background: var(--input-background); color: var(--text-primary); font: inherit; font-size: 15px; }
.status { margin: 12px 0 0; }
.about-head { display: flex; align-items: baseline; gap: 14px; }
.about-head .ti { margin: 0; flex: 1; }
.about-text { line-height: 1.9; margin: 14px 0 0; }

/* 手机：单列；设置项的控件放不下时换到下一行 */
@media (max-width: 768px) {
  .settings { grid-template-columns: minmax(0, 1fr); }
  .set { padding: 16px; }
  .sr.row { flex-wrap: wrap; gap: 8px 12px; }
  .swatches { flex-wrap: wrap; gap: 8px 12px; }
  .accents { flex-wrap: wrap; }
  .bg-opts { flex: 1 1 100%; flex-wrap: wrap; }
  .select { min-width: 0; flex: 1; max-width: 220px; }
  .about-head { flex-wrap: wrap; gap: 6px 12px; }
}
</style>
