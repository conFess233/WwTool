<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useRouter } from 'vue-router';
import { t } from '../../i18n';
import { gachaSource } from '../../gacha/source';
import { num, poolTitleKey, mainPools } from '../../format';
import { gachaApi, HARD_PITY, type Charts, type Overview, type Summary } from './gachaApi';
import { timeRanges } from './fiveStar';
import { useFab } from '../../shell/fab';
import GachaGate from '../../components/GachaGate.vue';
import EChart from '../../components/EChart.vue';
import PillSelect from '../../components/PillSelect.vue';
import SegmentedBar from '../../components/SegmentedBar.vue';
import StrokeIcon from '../../components/StrokeIcon.vue';
import BindUidDialog from '../account/BindUidDialog.vue';
import type { ChartTokens } from '../../components/chart';

// 详细图表（PullsChartsView.axaml）：上排每日活跃（近 90 天）、出金抽数分布、各卡池抽数占比；
// 下排每个五星花费、累计抽数（按月）、稀有度构成与小保底不歪率。数据来自 Core 的 PullsChartBuilder。
const DAILY_DAYS = 90;
const MAX_COST_ROWS = 8;
const router = useRouter();
const pool = ref(0); // 0 为全部卡池
const months = ref(0);
const data = ref<Charts>();
const overview = ref<Overview>();
const state = ref<'loading' | 'ready' | 'failed'>('loading');
const dialog = ref(false);

const pad = (n: number) => String(n).padStart(2, '0');
const dayKey = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

async function load() {
  const src = gachaSource.value;
  if (!src) return;
  if (!data.value) state.value = 'loading';
  let since: string | null = null;
  if (months.value) {
    const d = new Date();
    d.setMonth(d.getMonth() - months.value);
    since = dayKey(d);
  }
  try {
    [data.value, overview.value] = await Promise.all([
      gachaApi.charts(src, pool.value ? [pool.value] : null, since),
      overview.value?.uid === src.uid ? Promise.resolve(overview.value) : gachaApi.overview(src),
    ]);
    state.value = 'ready';
  } catch {
    state.value = 'failed';
  }
}
watch([() => `${gachaSource.value?.kind}|${gachaSource.value?.uid}|${gachaSource.value?.updatedAt}`, pool, months], load, { immediate: true });

const shortTitle = (p: number) => t(poolTitleKey(p)).replace(/(唤取| Convene)$/, '');
const percent = (v: number, digits = 1) => `${(v * 100).toFixed(digits)}%`;

// 每日抽卡：近 90 天，峰值用危险色
const dailyOption = computed(() => (tok: ChartTokens) => {
  const byDay = new Map((data.value?.daily ?? []).map(d => [d[0].slice(0, 10), d[1]]));
  const days = Array.from({ length: DAILY_DAYS }, (_, i) => { const d = new Date(); d.setDate(d.getDate() - DAILY_DAYS + 1 + i); return d; });
  const values = days.map(d => byDay.get(dayKey(d)) ?? 0);
  const peak = Math.max(0, ...values);
  return {
    grid: { left: 0, right: 0, top: 10, bottom: 22 },
    xAxis: { type: 'category', data: days.map(d => `${pad(d.getMonth() + 1)}/${pad(d.getDate())}`), axisLine: { show: false }, axisTick: { show: false },
      axisLabel: { color: tok.muted, fontSize: 12, interval: (i: number) => [0, 30, 60, DAILY_DAYS - 1].includes(i) } },
    yAxis: { type: 'value', show: false },
    tooltip: { trigger: 'axis', formatter: (p: { name: string; value: number }[]) => `${p[0].name} · ${p[0].value} ${t('Pulls_PullUnit')}` },
    series: [{ type: 'bar', barMaxWidth: 4, barMinHeight: 2,
      data: values.map(v => ({ value: v, itemStyle: { color: v === peak && peak > 0 ? tok.danger : v === 0 ? tok.track : tok.accent } })) }],
  };
});

// 出金抽数分布：8 档柱状，柱顶标数
const pityOption = computed(() => (tok: ChartTokens) => ({
  grid: { left: 0, right: 0, top: 20, bottom: 22 },
  tooltip: { trigger: 'axis' },
  xAxis: { type: 'category', data: data.value?.pityLabels ?? [], axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: tok.muted, fontSize: 11, interval: 0 } },
  yAxis: { type: 'value', show: false },
  series: [{ type: 'bar', barMaxWidth: 26, barMinHeight: 3, label: { show: true, position: 'top', color: tok.text, fontSize: 12 },
    data: (data.value?.pityBins ?? []).map(v => ({ value: v, itemStyle: { color: v ? tok.accent : tok.track, borderRadius: [3, 3, 0, 0] } })) }],
}));

// 各卡池抽数占比：主卡池依次着色，其他卡池合为一项
const shares = computed(() => {
  const d = data.value;
  if (!d) return [];
  const total = d.totalPulls || 1;
  const rows = mainPools.map(p => ({ title: shortTitle(p), pulls: d.poolShares.find(s => s[0] === p)?.[1] ?? 0 }))
    .filter(r => r.pulls > 0);
  const others = d.poolShares.filter(s => !mainPools.includes(s[0])).reduce((s, x) => s + x[1], 0);
  if (others > 0) rows.push({ title: t('Pulls_OtherPools'), pulls: others });
  return rows.map((r, i) => ({ ...r, index: i, text: t('Pulls_CountShare', num(r.pulls), percent(r.pulls / total)) }));
});
const shareColors = (tok: ChartTokens) => [tok.accent, tok.text, tok.secondary, tok.track, tok.track];
const shareColor = ref<string[]>([]);
const shareOption = computed(() => (tok: ChartTokens) => {
  shareColor.value = shareColors(tok);
  return {
    series: [{ type: 'pie', radius: ['76%', '100%'], silent: true, label: { show: false },
      data: shares.value.map((s, i) => ({ value: s.pulls, itemStyle: { color: shareColors(tok)[i] } })) }],
  };
});

// 每个五星花费（从近到远，最多 8 个）
const costRows = computed(() => (data.value?.fiveStars ?? []).slice(0, MAX_COST_ROWS));

// 累计抽数：按月取月末累计值，最多最近 12 个月
const cumulativeOption = computed(() => (tok: ChartTokens) => {
  const byMonth = new Map<string, number>();
  for (const [date, pulls] of data.value?.cumulative ?? []) byMonth.set(date.slice(0, 7), pulls);
  const months = [...byMonth.entries()].sort((a, b) => a[0].localeCompare(b[0])).slice(-12);
  return {
    grid: { left: 36, right: 20, top: 24, bottom: 24 },
    tooltip: { trigger: 'axis' },
    xAxis: { type: 'category', data: months.map(m => m[0].slice(5)), axisLine: { lineStyle: { color: tok.grid } }, axisTick: { show: false }, axisLabel: { color: tok.muted, fontSize: 12 } },
    yAxis: { type: 'value', axisLabel: { color: tok.muted, fontSize: 10 }, splitLine: { lineStyle: { color: tok.grid } } },
    series: [{ type: 'line', data: months.map(m => m[1]), symbol: 'circle', symbolSize: 9,
      itemStyle: { color: tok.surface, borderColor: tok.accent, borderWidth: 2 },
      label: { show: true, position: 'top', color: tok.text, fontSize: 11 },
      lineStyle: { color: tok.accent, width: 2 }, areaStyle: { color: tok.accentContainer } }],
  };
});

// 稀有度构成与小保底不歪率
const rarityRows = computed(() => {
  const d = data.value;
  if (!d) return [];
  const total = Math.max(1, d.totalPulls);
  return [
    { title: t('Pulls_FiveStar'), count: d.fiveStarCount, kind: 'gold' },
    { title: t('Pulls_FourStar'), count: d.fourStarCount, kind: 'four' },
    { title: t('Pulls_ThreeStar'), count: d.threeStarCount, kind: 'muted' },
  ].map(r => ({ ...r, ratio: r.count / total, text: t('Pulls_CountShare', num(r.count), percent(r.count / total)) }));
});
const winRow = (key: string, s?: Summary) => {
  const decided = (s?.wins ?? 0) + (s?.misses ?? 0);
  const ratio = decided ? (s!.wins / decided) : 0;
  return { title: t(key), ratio, text: decided ? t('Pulls_WinFraction', s!.wins, decided, percent(ratio, 0)) : '—' };
};
const winRows = computed(() => [
  winRow('Pulls_CharacterWinRate', overview.value?.pools.find(p => p.pool === 1)?.summary),
  winRow('Pulls_WeaponWinRate', overview.value?.pools.find(p => p.pool === 2)?.summary),
]);

const timeOptions = computed(() => timeRanges.map(r => ({ value: r.value, text: t(r.key) })));

useFab(() => (gachaSource.value ? {
  trigger: { title: t('Fab_GetRecords'), icon: 'Cloud' },
  actions: [
    { key: 'get', title: t('Fab_GetRecords'), icon: 'Cloud', run: () => (dialog.value = true) },
    { key: 'back', title: t('Pulls_BackToStatistics'), icon: 'Back', run: () => router.push('/pulls') },
  ],
} : null));
</script>

<template>
  <div class="charts">
    <GachaGate v-slot="{ source }" icon="Graph">
      <div class="filter">
        <div class="chips">
          <button class="chip" :aria-pressed="pool === 0" @click="pool = 0">{{ t('Pulls_AllPools') }}</button>
          <button v-for="p in mainPools" :key="p" class="chip" :aria-pressed="pool === p" @click="pool = p">{{ shortTitle(p) }}</button>
        </div>
        <div class="tools">
          <span class="caption basis">{{ t('Pulls_ChartsBasis', num(overview?.recordCount ?? 0)) }}</span>
          <PillSelect v-model="months" :label="t('Stat_Filter_DateRange')" :options="timeOptions" />
          <button class="back" @click="router.push('/pulls')"><StrokeIcon name="ChevronLeft" :size="16" />{{ t('Pulls_BackToStatistics') }}</button>
        </div>
      </div>

      <div v-if="state === 'loading' && !data" class="card"><span class="caption">{{ t('Web_Loading') }}</span></div>
      <div v-else-if="state === 'failed'" class="card"><span class="caption">{{ t('Web_LoadFailed') }}</span></div>
      <div v-else-if="data && data.totalPulls === 0" class="card"><span class="caption">{{ t('Stat_EmptyChart') }}</span></div>
      <div v-else-if="data" class="grid stagger">
        <div class="row top">
          <section class="card stagger-item" style="--i: 0">
            <div class="head"><h3 class="card-title">{{ t('Stat_DailyPullChart') }}</h3><span class="caption">{{ t('Pulls_LastDays', DAILY_DAYS) }}</span></div>
            <EChart :option="dailyOption" :height="330" />
          </section>
          <section class="card stagger-item" style="--i: 1">
            <div class="head"><h3 class="card-title">{{ t('Pulls_PityChart') }}</h3><span class="caption">{{ data.averagePity != null ? t('Pulls_AveragePityCaption', num(data.averagePity, 1)) : '' }}</span></div>
            <EChart :option="pityOption" :height="330" />
          </section>
          <section class="card stagger-item" style="--i: 2">
            <div class="head"><h3 class="card-title">{{ t('Pulls_PoolShareChart') }}</h3><span class="caption">{{ t('Pulls_TotalPullsCaption', num(data.totalPulls)) }}</span></div>
            <div class="share-body">
              <div class="share-donut"><EChart :option="shareOption" :height="136" /><strong class="donut-center">{{ num(data.totalPulls) }}</strong></div>
              <ul class="share-legend">
                <li v-for="s in shares" :key="s.title"><i :style="{ background: shareColor[s.index] }" /><span>{{ s.title }}</span><strong>{{ s.text }}</strong></li>
              </ul>
            </div>
          </section>
        </div>
        <div class="row bottom">
          <section class="card stagger-item" style="--i: 3">
            <div class="head"><h3 class="card-title">{{ t('Pulls_FiveStarCostChart') }}</h3><span class="caption">{{ t('Pulls_FiveStarCostHint') }}</span></div>
            <div class="cost-list">
              <div v-for="(f, i) in costRows" :key="i" class="cost-row">
                <span class="cost-name">{{ f.name }}</span>
                <SegmentedBar :value="Math.min(f.pity, HARD_PITY)" :max="HARD_PITY" :danger="f.pity >= 70" />
                <strong>{{ f.pity }}</strong>
              </div>
            </div>
          </section>
          <section class="card stagger-item" style="--i: 4">
            <div class="head"><h3 class="card-title">{{ t('Stat_CumulativeTrend') }}</h3><span class="caption">{{ t('Pulls_CumulativeCaption', num(data.astrites)) }}</span></div>
            <EChart :option="cumulativeOption" :height="330" />
          </section>
          <section class="card stagger-item" style="--i: 5">
            <h3 class="card-title head">{{ t('Pulls_RarityChart') }}</h3>
            <div class="rarity">
              <div v-for="r in rarityRows" :key="r.title" class="rarity-row" :class="r.kind">
                <div class="rarity-head"><span>{{ r.title }}</span><strong>{{ r.text }}</strong></div>
                <SegmentedBar :value="r.ratio" />
              </div>
              <hr />
              <div v-for="r in winRows" :key="r.title" class="rarity-row">
                <div class="rarity-head"><span>{{ r.title }}</span><strong>{{ r.text }}</strong></div>
                <SegmentedBar :value="r.ratio" />
              </div>
            </div>
          </section>
        </div>
      </div>

      <BindUidDialog v-if="dialog && source.kind === 'cloud'" :uid="source.uid" @close="dialog = false" />
      <BindUidDialog v-else-if="dialog" local @close="dialog = false" />
    </GachaGate>
  </div>
</template>

<style scoped>
.charts { display: flex; flex-direction: column; gap: 18px; padding-bottom: 76px; }
.filter { display: flex; justify-content: space-between; align-items: center; gap: 12px; }
.chips { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; }
.chip { height: 44px; padding: 0 20px; font-size: 15px; }
.tools { display: flex; gap: 12px; align-items: center; }
.basis { opacity: 0.8; }
.back { height: 44px; display: inline-flex; align-items: center; gap: 8px; padding: 0 20px 0 16px; border-radius: 10px; border: 1px solid var(--border); background: var(--surface); cursor: pointer; font-size: 15px; font-weight: 700; }
.back:hover { border-color: var(--accent-line); }
.grid { display: flex; flex-direction: column; gap: 22px; }
.row { display: grid; gap: 22px; }
.row.top { grid-template-columns: 2fr 1fr 1fr; }
.row.bottom { grid-template-columns: 1fr 1fr 1.1fr; }
.head { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 14px; }
.share-body { display: flex; align-items: center; gap: 18px; height: 330px; }
.share-donut { position: relative; width: 136px; flex: none; }
.donut-center { position: absolute; inset: 0; display: grid; place-items: center; font-size: 24px; pointer-events: none; }
.share-legend { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 12px; min-width: 0; }
.share-legend li { display: flex; align-items: center; gap: 6px; font-size: 13px; white-space: nowrap; }
.share-legend i { width: 11px; height: 11px; border-radius: 2px; flex: none; }
.share-legend span { color: var(--text-secondary); }
.share-legend strong { overflow: hidden; text-overflow: ellipsis; }
.cost-list { display: flex; flex-direction: column; justify-content: flex-end; gap: 14px; min-height: 330px; }
.cost-row { display: grid; grid-template-columns: 100px 1fr 34px; gap: 12px; align-items: center; }
.cost-name { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.cost-row strong { text-align: right; }
.rarity { display: flex; flex-direction: column; gap: 16px; }
.rarity-row { display: flex; flex-direction: column; gap: 8px; }
.rarity-head { display: flex; justify-content: space-between; }
.rarity-row.four :deep(.fill) { background: var(--four-star); }
.rarity-row.muted :deep(.fill) { background: var(--text-secondary); }
.rarity hr { border: 0; border-top: 1px solid var(--divider); margin: 4px 0; width: 100%; }
</style>
