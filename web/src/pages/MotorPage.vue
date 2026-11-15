<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { t } from '../i18n';
import { profile, itemName, useCatalog } from '../game/profile';
import { iconUrl, iconFallback } from '../format';
import ProfileGate from '../components/ProfileGate.vue';
import SegmentedBar from '../components/SegmentedBar.vue';
import { useProfileFab } from '../game/profileFab';

// 摩托数据（MotorView.axaml）：等级、当前装备、各类外观解锁与车载音乐。名称来自资料目录（catalog/Data）。
onMounted(() => useCatalog('Motors', 'Albums'));
type Tab = 'skins' | 'stickers' | 'decorations' | 'frames' | 'albums';
const tab = ref<Tab>('skins');
const motor = computed(() => profile.value?.snapshot?.motor);
const music = computed(() => profile.value?.snapshot?.music ?? []);

const tabs = computed(() => [
  { key: 'skins' as Tab, title: t('Motor_Tab_Skin'), count: motor.value?.skins?.length ?? 0 },
  { key: 'stickers' as Tab, title: t('Motor_Tab_Sticker'), count: motor.value?.stickers?.length ?? 0 },
  { key: 'decorations' as Tab, title: t('Motor_Tab_Pendant'), count: motor.value?.decorations?.length ?? 0 },
  { key: 'frames' as Tab, title: t('Motor_Tab_Frame'), count: motor.value?.frames?.length ?? 0 },
  { key: 'albums' as Tab, title: t('Motor_Tab_Album'), count: music.value.length },
]);

const items = computed(() => {
  const m = motor.value;
  switch (tab.value) {
    case 'skins': return (m?.skins ?? []).map(x => ({ id: x.id, quality: x.quality, tag: '' }));
    case 'stickers': return (m?.stickers ?? []).map(x => ({ id: x.id, quality: x.quality, tag: t('Motor_PartValue', x.partId) }));
    case 'decorations': return (m?.decorations ?? []).map(x => ({ id: x.id, quality: x.quality, tag: t('Motor_PartValue', x.partId) }));
    case 'frames': return (m?.frames ?? []).map(x => ({ id: x.id, quality: x.quality, tag: '' }));
    default: return music.value.map(x => ({ id: x.id, quality: 0, tag: t('Role_CollectionProgress', x.count, x.totalCount) }));
  }
});
const equipped = computed(() => motor.value?.equipSkin);
const stats = computed(() => [
  { key: 'skins', title: t('Motor_UnlockedSkins'), icon: 'Motor', count: motor.value?.skins?.length ?? 0 },
  { key: 'stickers', title: t('Motor_UnlockedStickers'), icon: 'Sticker', count: motor.value?.stickers?.length ?? 0 },
  { key: 'decorations', title: t('Motor_UnlockedOrnaments'), icon: 'Decorations', count: motor.value?.decorations?.length ?? 0 },
  { key: 'frames', title: t('Motor_UnlockedFrames'), icon: 'MotorFrame', count: motor.value?.frames?.length ?? 0 },
]);
useProfileFab();
</script>

<template>
  <div class="motor">
    <ProfileGate icon="Motor">
      <div v-if="motor" class="layout stagger">
        <!-- 左：摩托图、载具等级与当前装备 -->
        <section class="card side stagger-item" style="--i: 0">
          <div class="hero">
            <img v-if="equipped && equipped.id" :src="iconUrl(equipped.id)" alt="" @error="iconFallback" />
            <span v-else class="mask hero-mask" :style="{ '--icon': 'url(/icons/Motor.png)' }" aria-hidden="true" />
          </div>
          <div class="level-row">
            <span class="mask level-icon" :style="{ '--icon': 'url(/icons/MotorLevel.png)' }" aria-hidden="true" />
            <div class="level-text"><span class="caption">{{ t('Motor_Level') }}</span><strong>Lv.{{ motor.level }}</strong></div>
          </div>
          <SegmentedBar :value="motor.nextExp > 0 ? motor.exp / motor.nextExp : 1" />
          <div class="exp caption"><span>{{ t('Motor_Exp') }}</span><span>{{ motor.nextExp > 0 ? `${motor.exp} / ${motor.nextExp}` : t('Motor_MaxLevel') }}</span></div>
          <h3 class="card-title equip-title">{{ t('Motor_CurrentEquip') }}</h3>
          <div class="kv-line"><span>{{ t('Motor_Tab_Skin') }}</span><strong>{{ equipped && equipped.id ? itemName(equipped.id) : t('Motor_DefaultSkin') }}</strong></div>
          <div class="kv-line"><span>{{ t('Motor_Quality').trim().replace(/[:：]$/, '') }}</span><strong>{{ equipped?.quality ?? '--' }}</strong></div>
        </section>

        <!-- 右：解锁统计、分类与条目 -->
        <div class="main">
          <div class="stats">
            <section v-for="(x, i) in stats" :key="x.key" class="card stat stagger-item" :style="{ '--i': i + 1 }">
              <span class="mask stat-icon" :style="{ '--icon': `url(/icons/${x.icon}.png)` }" aria-hidden="true" />
              <div class="stat-text"><span class="caption">{{ x.title }}</span><strong>{{ x.count }}</strong></div>
            </section>
          </div>
          <div class="tabs" role="tablist">
            <button v-for="x in tabs" :key="x.key" class="chip" role="tab" :aria-pressed="tab === x.key" @click="tab = x.key">
              {{ x.title }}<span class="count">{{ x.count }}</span>
            </button>
          </div>
          <p v-if="!items.length" class="caption empty">{{ t('Motor_TabEmpty') }}</p>
          <div v-else class="items">
            <div v-for="it in items" :key="`${tab}-${it.id}`" class="card item" :class="{ on: tab === 'skins' && equipped?.id === it.id }">
              <img v-if="it.id" :src="iconUrl(it.id)" alt="" loading="lazy" @error="iconFallback" />
              <span v-else class="mask big" :style="{ '--icon': 'url(/icons/Motor.png)' }" aria-hidden="true" />
              <span class="item-name">{{ tab === 'skins' && it.id === 0 ? t('Motor_DefaultSkin') : itemName(it.id) }}</span>
              <span v-if="tab === 'skins' && equipped?.id === it.id" class="ntag">{{ t('Motor_Equipped') }}</span>
              <span v-else class="caption">{{ it.tag || (it.quality ? t('Motor_QualityValue', it.quality) : '') }}</span>
            </div>
          </div>
        </div>
      </div>
    </ProfileGate>
  </div>
</template>

<style scoped>
.motor { padding-bottom: 76px; }
.layout { display: grid; grid-template-columns: 524px 1fr; gap: 24px; align-items: start; }
.side { display: flex; flex-direction: column; gap: 10px; padding: 26px 28px 24px; }
.hero { height: 288px; border-radius: 12px; background: var(--surface-sunken); display: grid; place-items: center; margin-bottom: 20px; }
.hero img { max-width: 80%; max-height: 80%; object-fit: contain; }
.mask { background: var(--text-secondary); -webkit-mask: var(--icon) center / contain no-repeat; mask: var(--icon) center / contain no-repeat; }
.hero-mask { width: 230px; height: 140px; }
.level-row { display: flex; align-items: center; gap: 22px; padding-left: 8px; }
.level-icon { width: 50px; height: 50px; background: var(--text-primary); }
.level-text { display: flex; flex-direction: column; }
.level-text strong { font-size: 40px; line-height: 1.1; }
.exp { display: flex; justify-content: space-between; font-size: 14px; }
.equip-title { margin-top: 20px; font-size: 18px; padding-bottom: 12px; border-bottom: 1px solid var(--divider); }
.kv-line { display: flex; justify-content: space-between; padding: 12px 0; border-bottom: 1px solid var(--divider); font-size: 15px; }
.kv-line:last-child { border-bottom: 0; }
.kv-line span { color: var(--text-secondary); }
.main { display: flex; flex-direction: column; gap: 22px; min-width: 0; }
.stats { display: grid; grid-template-columns: repeat(4, 1fr); gap: 22px; }
.stat { display: flex; align-items: center; gap: 20px; padding: 22px 26px; }
.stat-icon { width: 42px; height: 42px; flex: none; background: var(--text-primary); }
.stat-text { display: flex; flex-direction: column; }
.stat-text strong { font-size: 38px; line-height: 1.1; }
.tabs { display: flex; gap: 10px; flex-wrap: wrap; }
.tabs .chip { height: 44px; padding: 0 20px; font-size: 15px; font-weight: 700; }
.chip .count { margin-left: 14px; color: var(--text-muted); font-weight: 400; }
.empty { padding: 24px 0; text-align: center; }
.items { display: grid; grid-template-columns: repeat(auto-fill, 216px); gap: 22px; }
.item { display: flex; flex-direction: column; align-items: center; gap: 8px; min-height: 232px; padding: 40px 14px 20px; text-align: center; }
.item.on { border: 2px solid var(--accent); }
.item img { width: 120px; height: 84px; object-fit: contain; }
.mask.big { width: 96px; height: 84px; }
.item-name { font-size: 17px; font-weight: 700; margin-top: 8px; }

/* 手机：单栏；统计两列，条目自适应列数 */
@media (max-width: 768px) {
  .motor { padding-bottom: 12px; }
  .layout { grid-template-columns: minmax(0, 1fr); gap: 12px; }
  .side { padding: 16px; }
  .hero { height: 180px; margin-bottom: 10px; }
  .hero-mask { width: 160px; height: 100px; }
  .level-text strong { font-size: 30px; }
  .equip-title { margin-top: 10px; }
  .main { gap: 12px; }
  .stats { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
  .stat { gap: 12px; padding: 14px; }
  .stat-icon { width: 32px; height: 32px; }
  .stat-text strong { font-size: 28px; }
  .tabs { flex-wrap: nowrap; overflow-x: auto; padding-bottom: 2px; scrollbar-width: none; }
  .tabs .chip { flex: none; height: 38px; padding: 0 14px; font-size: 14px; }
  .chip .count { margin-left: 8px; }
  .items { grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 10px; }
  .item { min-height: 0; padding: 18px 10px 14px; }
  .item img { width: 96px; height: 68px; }
  .item-name { font-size: 15px; }
}
</style>
