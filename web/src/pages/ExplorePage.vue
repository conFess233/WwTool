<script setup lang="ts">
import { computed } from 'vue';
import { t } from '../i18n';
import { profile } from '../game/profile';
import { num } from '../format';
import ProfileGate from '../components/ProfileGate.vue';
import SegmentedBar from '../components/SegmentedBar.vue';
import { useProfileFab } from '../game/profileFab';

useProfileFab();

// 探索数据（ExploreView.axaml）：声匣、奇藏箱与潮汐之遗。种类与图标同 ExploreViewModel。
const boxKinds = [
  ['1', 'Exploration_BoxPlain', 'Chest1'], ['2', 'Exploration_BoxBasic', 'Chest2'],
  ['3', 'Exploration_BoxAdvanced', 'Chest3'], ['4', 'Exploration_BoxPremium', 'Chest4'],
] as const;
const phantomKinds = [['1', 'Explore_PhantomLow', 'Phantom1'], ['2', 'Explore_PhantomMid', 'Phantom2'], ['3', 'Explore_PhantomHigh', 'Phantom3']] as const;

const sum = (v?: Record<string, number>) => Object.values(v ?? {}).reduce((s, x) => s + x, 0);
const base = computed(() => profile.value?.snapshot?.base);
const boxes = computed(() => {
  const total = sum(base.value?.boxes);
  return boxKinds.map(([k, title, icon]) => {
    const count = base.value?.boxes?.[k] ?? 0;
    const ratio = total ? count / total : 0;
    return { k, title: t(title), icon, count, ratio };
  });
});
const phantoms = computed(() => phantomKinds.map(([k, title, icon]) => ({ k, title: t(title), icon, count: base.value?.phantomBoxes?.[k] ?? 0 })));
</script>

<template>
  <div class="explore">
    <ProfileGate icon="Chest">
      <div v-if="base" class="stagger col">
        <div class="overview">
          <section class="card ov stagger-item" style="--i: 0">
            <div class="ov-head"><img src="/icons/SoundBox.png" alt="" /><span class="label">{{ t('Exploration_SonanceCasketProgress') }}</span></div>
            <div class="big"><strong>{{ num(base.soundBox) }}</strong><span class="caption">{{ t('Exploration_Collected') }}</span></div>
          </section>
          <section class="card ov stagger-item" style="--i: 1">
            <div class="ov-head"><span class="dark"><img src="/icons/Chest.png" alt="" /></span><span class="label">{{ t('Role_BoxesTotal') }}</span></div>
            <div class="big"><strong>{{ num(sum(base.boxes)) }}</strong><span class="caption">{{ t('Explore_BasicSuffix', num(sum(base.basicBoxes))) }}</span></div>
          </section>
          <section class="card ov stagger-item" style="--i: 2">
            <div class="ov-head"><img src="/icons/Phantom1.png" alt="" /><span class="label">{{ t('Role_PhantomBoxesTotal') }}</span></div>
            <div class="big"><strong>{{ num(sum(base.phantomBoxes)) }}</strong><span class="caption">{{ t('Explore_Unit') }}</span></div>
          </section>
        </div>

        <h3 class="section-title sec">{{ t('Explore_BoxesSection') }}</h3>
        <div class="kinds four">
          <section v-for="(b, i) in boxes" :key="b.k" class="card kind stagger-item" :style="{ '--i': 3 + i }">
            <div class="kind-head"><span class="dark"><img :src="`/icons/${b.icon}.png`" alt="" /></span><strong class="kind-title">{{ b.title }}</strong></div>
            <div class="big"><strong>{{ num(b.count) }}</strong><span class="caption">{{ t('Explore_Unit') }}</span></div>
            <SegmentedBar :value="b.ratio" />
          </section>
        </div>

        <h3 class="section-title sec">{{ t('Explore_PhantomSection') }}</h3>
        <div class="kinds three">
          <section v-for="(p, i) in phantoms" :key="p.k" class="card phantom stagger-item" :style="{ '--i': 7 + i }">
            <img :src="`/icons/${p.icon}.png`" alt="" class="plain" />
            <div class="phantom-text"><span class="label">{{ p.title }}</span><strong>{{ num(p.count) }}</strong></div>
          </section>
        </div>
      </div>
    </ProfileGate>
  </div>
</template>

<style scoped>
.explore { padding-bottom: 70px; }
.col { display: flex; flex-direction: column; gap: 22px; }
.overview { display: grid; grid-template-columns: 1.3fr 1fr 1fr; gap: 22px; }
.ov { display: flex; flex-direction: column; justify-content: space-between; min-height: 170px; }
.ov-head { display: flex; align-items: center; gap: 12px; }
.ov-head img { width: 56px; height: 56px; object-fit: contain; }
.label { font-size: 14px; color: var(--label); }
.dark { display: grid; place-items: center; width: 56px; height: 56px; border-radius: 12px; background: var(--nav-island); flex: none; }
.dark img { width: 44px; height: 44px; object-fit: contain; }
.big { display: flex; align-items: baseline; gap: 8px; }
.big strong { font-size: 60px; font-weight: 700; letter-spacing: -2px; line-height: 1; }
.sec { margin: 4px 0 -4px; font-size: 18px; }
.kinds { display: grid; gap: 22px; }
.kinds.four { grid-template-columns: repeat(4, 1fr); }
.kinds.three { grid-template-columns: repeat(3, 1fr); }
.kind { display: flex; flex-direction: column; gap: 16px; min-height: 260px; }
.kind-head { display: flex; align-items: center; gap: 16px; }
.kind-head .dark { width: 80px; height: 80px; border-radius: 16px; }
.kind-head .dark img { width: 52px; height: 52px; }
.kind-title { font-size: 17px; }
.kind .big { margin-top: auto; }
.phantom { display: flex; align-items: center; gap: 30px; min-height: 220px; padding-left: 36px; }
.phantom-text { display: flex; flex-direction: column; gap: 4px; }
.phantom-text strong { font-size: 48px; line-height: 1; }
.plain { width: 72px; height: 72px; object-fit: contain; }
</style>
