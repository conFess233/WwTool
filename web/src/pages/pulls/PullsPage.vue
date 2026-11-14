<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useRouter } from 'vue-router';
import { t } from '../../i18n';
import { gachaSource } from '../../gacha/source';
import { readPref, writePref } from '../../prefs';
import { num, iconUrl, iconFallback, poolTitleKey, mainPools } from '../../format';
import { gachaApi, HARD_PITY, type Overview, type Pool } from './gachaApi';
import { buildCards, allCards, applyFilter, isWeapon, timeRanges, fiveStarFilters, type FiveStarFilter, type FiveStarCard } from './fiveStar';
import { useFab } from '../../shell/fab';
import GachaGate from '../../components/GachaGate.vue';
import EmptyState from '../../components/EmptyState.vue';
import SegmentedBar from '../../components/SegmentedBar.vue';
import EChart from '../../components/EChart.vue';
import PillSelect from '../../components/PillSelect.vue';
import StrokeIcon from '../../components/StrokeIcon.vue';
import FiveStarRow from './FiveStarRow.vue';
import PullsTimeline from './PullsTimeline.vue';
import CharacterDetailDialog from '../../components/CharacterDetailDialog.vue';
import BindUidDialog from '../account/BindUidDialog.vue';
import type { ChartTokens } from '../../components/chart';
import type { EChartsCoreOption } from 'echarts/core';

// 抽卡统计（PullsOverviewView.axaml）：卡池页签 + 时间范围 / 过滤五星 + 视图切换（列表 / 精简 / 时间线）+ 详细图表。
// 总览为五张统计卡、各卡池最近五星、未出金进度与三张小图；单卡池为统计条 + 五星记录。
type View = 'list' | 'compact' | 'timeline';
const OTHER = -1;
const router = useRouter();
const tab = ref<number>(Number(readPref('pulls.tab') ?? 0)); // 0 总览，-1 其他卡池
const otherPool = ref<number>(Number(readPref('pulls.other') ?? 0));
const savedView = readPref('pulls.view');
const view = ref<View>(savedView === 'compact' || savedView === 'timeline' ? savedView : 'list');
const months = ref(0);
const filter = ref<FiveStarFilter>('all');
const timelineWeapons = ref(false);
const data = ref<Overview>();
const state = ref<'loading' | 'ready' | 'failed'>('loading');
const dialog = ref(false);
const detail = ref<FiveStarCard | null>(null);
let abort: AbortController | undefined;

async function load() {
  const src = gachaSource.value;
  if (!src) return;
  abort?.abort();
  abort = new AbortController();
  if (data.value?.uid !== src.uid) state.value = 'loading';
  try {
    data.value = await gachaApi.overview(src, abort.signal);
    state.value = 'ready';
  } catch (e) {
    if ((e as Error).name !== 'AbortError') state.value = 'failed';
  }
}
watch(() => `${gachaSource.value?.kind}|${gachaSource.value?.uid}|${gachaSource.value?.updatedAt}`, load, { immediate: true });
watch(tab, v => writePref('pulls.tab', String(v)));
watch(otherPool, v => writePref('pulls.other', String(v)));
watch(view, v => writePref('pulls.view', v));

const pool = (n: number) => data.value?.pools.find(p => p.pool === n);
const otherPools = computed(() => (data.value?.pools ?? []).filter(p => !mainPools.includes(p.pool) && p.recordCount > 0));
watch(otherPools, list => {
  if (list.length && !list.some(p => p.pool === otherPool.value)) otherPool.value = list[0].pool;
  if (!list.length && tab.value === OTHER) tab.value = 0;
});
const currentPoolNo = computed(() => (tab.value === OTHER ? otherPool.value : tab.value));
const current = computed<Pool | undefined>(() => (currentPoolNo.value > 0 ? pool(currentPoolNo.value) : undefined));
const isOverview = computed(() => tab.value === 0);
const isEmpty = computed(() => (data.value?.recordCount ?? 0) === 0);
// 精简视图只用于单卡池
const effectiveView = computed<View>(() => (isOverview.value && view.value === 'compact' ? 'list' : view.value));

const pct = (v?: number) => (v === undefined || v === null ? '--' : `${num(v, 0)}%`);

// 单卡池：五星记录（新→旧，按筛选）
const poolCards = computed(() => (current.value ? applyFilter(buildCards(current.value), months.value, filter.value) : []));
const pending = computed(() => {
  const p = current.value;
  if (!p || p.progress.totalPulls === 0) return null;
  const next = p.progress.nextIsGuaranteed === true ? t('Pulls_NextGuaranteed') : p.progress.nextIsGuaranteed === false ? t('Pulls_NextSoftPity') : '';
  return { pity: p.progress.currentPity, next, toHard: p.progress.pullsToHardPity, last: p.progress.lastPullAt?.replace('T', ' ').slice(0, 16) ?? '' };
});

// 时间线：单卡池为该卡池（从旧到新），总览为全部卡池并可切角色 / 武器
const overviewAll = computed(() => applyFilter(allCards(data.value?.pools ?? []), months.value, filter.value));
const roleCount = computed(() => overviewAll.value.filter(c => !isWeapon(c.resourceId)).length);
const weaponCount = computed(() => overviewAll.value.length - roleCount.value);
const timelineCards = computed(() =>
  isOverview.value ? overviewAll.value.filter(c => isWeapon(c.resourceId) === timelineWeapons.value) : [...poolCards.value].reverse());

// 总览：各卡池最近五星、未出金进度
const latestRows = computed(() => mainPools.map(p => {
  const d = pool(p);
  const card = d ? buildCards(d)[0] : undefined;
  return { pool: p, card };
}));
const pityRows = computed(() => mainPools.map(p => {
  const d = pool(p);
  const pity = d?.progress.currentPity ?? 0;
  const g = d?.progress.nextIsGuaranteed;
  return {
    pool: p, pity, near: pity >= 70,
    next: g === true ? t('Pulls_NextGuaranteed') : g === false ? t('Pulls_NextSoftPity') : '',
    toHard: t('Pulls_ToHardPityShort', Math.max(0, HARD_PITY - pity)),
  };
}));

// 下方三张小图：近 30 天每日抽数、各卡池五星分布、不歪概率与四星比例
const DAILY_DAYS = 30;
const dailyOption = ref<(tok: ChartTokens) => EChartsCoreOption>(() => ({}));
const shareBars = computed(() => {
  const totals = mainPools.map(p => ({ pool: p, count: pool(p)?.summary.fiveStars ?? 0 }));
  const all = totals.reduce((s, p) => s + p.count, 0);
  return totals.map(p => ({ ...p, ratio: all ? p.count / all : 0 }));
});
const winValues = computed(() => ({ wins: pool(1)?.summary.wins ?? 0, misses: pool(1)?.summary.misses ?? 0 }));
const fourStar = computed(() => ({ character: data.value?.fourStarCharacters ?? 0, weapon: data.value?.fourStarWeapons ?? 0 }));

async function loadCharts() {
  const src = gachaSource.value;
  if (!src || isEmpty.value) return;
  const pad = (n: number) => String(n).padStart(2, '0');
  const key = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  const days = Array.from({ length: DAILY_DAYS }, (_, i) => { const d = new Date(); d.setDate(d.getDate() - DAILY_DAYS + 1 + i); return d; });
  const c = await gachaApi.charts(src, null, key(days[0])).catch(() => null);
  if (!c) return;
  const byDay = new Map(c.daily.map(d => [d[0].slice(0, 10), d[1]]));
  const values = days.map(d => byDay.get(key(d)) ?? 0);
  const labels = days.map(d => `${pad(d.getMonth() + 1)}/${pad(d.getDate())}`);
  const peak = Math.max(0, ...values);
  dailyOption.value = (tok: ChartTokens) => ({
    grid: { left: 0, right: 0, top: 6, bottom: 20 },
    xAxis: { type: 'category', data: labels, axisLine: { show: false }, axisTick: { show: false },
      axisLabel: { color: tok.muted, fontSize: 11, interval: (i: number) => [0, 9, 19, DAILY_DAYS - 1].includes(i) } },
    yAxis: { type: 'value', show: false },
    tooltip: { trigger: 'axis', formatter: (p: { name: string; value: number }[]) => `${p[0].name} · ${p[0].value} ${t('Pulls_PullUnit')}` },
    series: [{ type: 'bar', barMaxWidth: 8, itemStyle: { borderRadius: [3, 3, 0, 0] },
      data: values.map(v => ({ value: v, itemStyle: { color: v === peak && peak > 0 ? tok.danger : tok.accent } })) }],
  });
}
watch(data, loadCharts);

const donut = (a: number, b: number, colorA: (tok: ChartTokens) => string, colorB: (tok: ChartTokens) => string) => (tok: ChartTokens) => ({
  series: [{ type: 'pie', radius: ['68%', '90%'], silent: true, label: { show: false },
    data: a + b === 0 ? [{ value: 1, itemStyle: { color: tok.track } }] : [{ value: a, itemStyle: { color: colorA(tok) } }, { value: b, itemStyle: { color: colorB(tok) } }] }],
});
const winDonut = computed(() => donut(winValues.value.wins, winValues.value.misses, tok => tok.accent, tok => tok.track));
const fourDonut = computed(() => donut(fourStar.value.character, fourStar.value.weapon, tok => tok.fourStar, tok => tok.fourStarContainer));
const ratioText = (a: number, b: number) => (a + b === 0 ? '--' : `${Math.round((a * 100) / (a + b))}%`);
const shortDate = (time: string) => time.slice(0, 10);
const shortTitle = (p: number) => t(poolTitleKey(p)).replace(/(唤取| Convene)$/, '');

const timeOptions = computed(() => timeRanges.map(r => ({ value: r.value, text: t(r.key) })));
const filterOptions = computed(() => fiveStarFilters.map(r => ({ value: r.value, text: t(r.key) })));

useFab(() => (gachaSource.value ? {
  trigger: { title: t('Fab_GetRecords'), icon: 'Cloud' },
  actions: [
    { key: 'get', title: t('Fab_GetRecords'), icon: 'Cloud', run: () => (dialog.value = true) },
    { key: 'charts', title: t('Pulls_DetailedCharts'), icon: 'Chart', run: () => router.push('/pulls/charts') },
  ],
} : null));
</script>

<template>
  <div class="pulls">
    <GachaGate v-slot="{ source }" icon="Statistics">
      <!-- 筛选栏 -->
      <div class="filter">
        <div class="chips" role="tablist">
          <button class="chip" role="tab" :aria-pressed="tab === 0" @click="tab = 0">{{ t('Pulls_Overview') }}<span class="count">{{ data?.recordCount ?? 0 }}</span></button>
          <button v-for="p in mainPools" :key="p" class="chip" role="tab" :aria-pressed="tab === p" @click="tab = p">
            {{ shortTitle(p) }}<span class="count">{{ pool(p)?.recordCount ?? 0 }}</span>
          </button>
          <button v-if="otherPools.length" class="chip" role="tab" :aria-pressed="tab === OTHER" @click="tab = OTHER">
            {{ t('Pulls_OtherPools') }}<span v-if="tab !== OTHER" class="count">{{ otherPools.reduce((s, p) => s + p.recordCount, 0) }}</span>
          </button>
        </div>
        <div class="tools">
          <PillSelect v-model="months" :label="t('Stat_Filter_DateRange')" :options="timeOptions" />
          <PillSelect v-model="filter" :label="t('Pulls_FilterFiveStar')" :options="filterOptions" />
          <div class="views" role="radiogroup">
            <button role="radio" :aria-checked="effectiveView === 'list'" :title="t('Pulls_ViewList')" @click="view = 'list'"><StrokeIcon name="ViewList" :size="18" /></button>
            <button v-if="!isOverview" role="radio" :aria-checked="effectiveView === 'compact'" :title="t('Pulls_ViewCompact')" @click="view = 'compact'"><StrokeIcon name="ViewGrid" :size="18" /></button>
            <button role="radio" :aria-checked="effectiveView === 'timeline'" :title="t('Pulls_ViewTimeline')" @click="view = 'timeline'"><StrokeIcon name="Timeline" :size="18" /></button>
          </div>
          <button class="icon-btn" :disabled="isEmpty" :title="t('Pulls_DetailedCharts')" :aria-label="t('Pulls_DetailedCharts')" @click="router.push('/pulls/charts')">
            <StrokeIcon name="Chart" :size="18" />
          </button>
        </div>
      </div>
      <div v-if="tab === OTHER && otherPools.length > 1" class="chips sub">
        <button v-for="p in otherPools" :key="p.pool" class="chip small" :aria-pressed="otherPool === p.pool" @click="otherPool = p.pool">
          {{ t(poolTitleKey(p.pool)) }}<span class="count">{{ p.recordCount }}</span>
        </button>
      </div>

      <div v-if="state === 'loading'" class="card"><span class="caption">{{ t('Web_Loading') }}</span></div>
      <EmptyState v-else-if="state === 'failed'" danger :title="t('Pulls_LoadFailed')" icon="Statistics">
        <button class="btn" @click="load">{{ t('Pulls_Retry') }}</button>
      </EmptyState>
      <EmptyState v-else-if="isEmpty" :title="t('Pulls_EmptyTitle')" :hint="t('Pulls_EmptyHint')" icon="Statistics">
        <button class="btn primary" @click="dialog = true">{{ t('Fab_GetRecords') }}</button>
      </EmptyState>

      <template v-else-if="data">
        <!-- 总览统计卡 / 单卡池统计条 -->
        <div v-if="isOverview && effectiveView === 'list'" :key="`m-${data.uid}`" class="metrics stagger">
          <section class="card metric"><span class="label">{{ t('Stat_TotalTides') }}</span><strong class="metric-value">{{ num(data.summary.totalPulls) }}</strong></section>
          <section class="card metric" style="--i: 1">
            <span class="label">{{ t('Pulls_FiveStars') }}</span><strong class="metric-value">{{ num(data.summary.fiveStars) }}</strong>
            <span class="caption">{{ t('Pulls_LimitedPermanent', data.summary.limited, data.summary.permanent) }}</span>
          </section>
          <section class="card metric" style="--i: 2">
            <span class="label">{{ t('Pulls_AvgPerFiveStar') }}</span><strong class="metric-value">{{ num(data.summary.averagePerFiveStar, 1) }}<small>{{ t('Pulls_PullUnit') }}</small></strong>
            <span class="caption">{{ t('Pulls_AvgPerLimited', num(data.summary.averagePerLimited, 1)) }}</span>
          </section>
          <section class="card metric" style="--i: 3">
            <span class="label">{{ t('Pulls_WinRate') }}</span><strong class="metric-value">{{ pct(data.summary.winRate) }}</strong>
            <span class="caption">{{ t('Pulls_WinMiss', data.summary.wins, data.summary.misses) }}</span>
          </section>
          <section class="card metric" style="--i: 4"><span class="label">{{ t('Pulls_Astrites') }}</span><strong class="metric-value">{{ num(data.summary.astrites) }}</strong></section>
        </div>
        <section v-else class="card strip">
          <template v-for="s in [isOverview ? data.summary : current?.summary]" :key="currentPoolNo">
            <div v-if="s" class="cell"><span class="label">{{ t('Stat_TotalTides') }}</span><strong>{{ num(s.totalPulls) }}</strong></div>
            <div v-if="s" class="cell"><span class="label">{{ t('Pulls_FiveStarShort') }}</span><strong>{{ num(s.fiveStars) }}</strong></div>
            <div v-if="s" class="cell"><span class="label">{{ t('Pulls_AvgPerFiveStar') }}</span><strong>{{ num(s.averagePerFiveStar, 1) }}<small>{{ t('Pulls_PullUnit') }}</small></strong></div>
            <div v-if="s" class="cell"><span class="label">{{ t('Pulls_AvgPerUp') }}</span><strong>{{ num(s.averagePerLimited, 1) }}<small>{{ t('Pulls_PullUnit') }}</small></strong></div>
            <div v-if="s" class="cell"><span class="label">{{ t('Pulls_WinRate') }}</span><strong>{{ pct(s.winRate) }}<small>{{ s.wins }} / {{ s.wins + s.misses }}</small></strong></div>
            <div v-if="s" class="cell"><span class="label">{{ t('Pulls_Astrites') }}</span><strong>{{ num(s.astrites) }}</strong></div>
          </template>
        </section>

        <!-- 时间线 -->
        <section v-if="effectiveView === 'timeline'" :key="`tl-${tab}-${otherPool}`" class="card timeline-card">
          <div class="tl-head">
            <h3 class="card-title">{{ t('Pulls_TimelineTitle') }}</h3>
            <template v-if="isOverview">
              <button class="chip small" :aria-pressed="!timelineWeapons" @click="timelineWeapons = false">{{ t('Pulls_TimelineRoles') }}<span class="count">{{ roleCount }}</span></button>
              <button class="chip small" :aria-pressed="timelineWeapons" @click="timelineWeapons = true">{{ t('Pulls_TimelineWeapons') }}<span class="count">{{ weaponCount }}</span></button>
            </template>
          </div>
          <PullsTimeline :cards="timelineCards" :pending="isOverview ? null : pending" @open="detail = $event" />
        </section>

        <!-- 总览 -->
        <div v-else-if="isOverview" :key="`overview-${data.uid}`" class="overview stagger">
          <div class="row-2">
            <section class="card stagger-item" style="--i: 5">
              <h3 class="card-title">{{ t('Pulls_LatestPerPool') }}</h3>
              <div class="latest">
                <template v-for="r in latestRows" :key="r.pool">
                  <FiveStarRow v-if="r.card" :card="r.card" :subtitle="t('Pulls_PoolDate', shortTitle(r.pool), shortDate(r.card.time))" />
                  <div v-else class="none-row">
                    <span class="unknown">?</span>
                    <div class="who"><strong class="none-title">{{ t('Pulls_NoFiveStarRecord') }}</strong><span class="aux muted">{{ t(poolTitleKey(r.pool)) }}</span></div>
                  </div>
                </template>
              </div>
            </section>
            <section class="card stagger-item" style="--i: 6">
              <h3 class="card-title">{{ t('Pulls_PityPerPool') }}</h3>
              <div v-for="r in pityRows" :key="r.pool" class="pity-row">
                <div class="pity-head">
                  <strong>{{ t(poolTitleKey(r.pool)) }}</strong>
                  <span v-if="r.next" class="ntag">{{ r.next }}</span>
                  <span v-if="r.near" class="ntag red near">{{ t('Pulls_NearHardPity') }}</span>
                </div>
                <div class="pity-line">
                  <span>{{ t('Pulls_HavePrefix') }}</span>
                  <strong class="pity-num" :class="{ danger: r.near }">{{ r.pity }}</strong>
                  <span>{{ t('Pulls_HaveSuffix') }}</span>
                  <span class="caption to-hard">{{ r.toHard }}</span>
                </div>
                <SegmentedBar :value="r.pity" :max="80" :danger="r.near" />
                <div class="scale caption"><span>0</span><span>20</span><span>40</span><span>60</span><span>80</span></div>
              </div>
            </section>
          </div>

          <div class="row-3">
            <section class="card stagger-item" style="--i: 7">
              <div class="daily-head"><h3 class="card-title">{{ t('Stat_DailyPullChart') }}</h3><span class="caption">{{ t('Pulls_LastDays', DAILY_DAYS) }}</span></div>
              <EChart :option="dailyOption" :height="190" />
            </section>
            <section class="card stagger-item" style="--i: 8">
              <h3 class="card-title">{{ t('Stat_GoldHistoryChart') }}</h3>
              <div class="shares">
                <div v-for="b in shareBars" :key="b.pool" class="share">
                  <div class="share-head"><span class="aux">{{ shortTitle(b.pool) }}</span><strong class="aux">{{ t('Pulls_FiveStarCount', b.count) }}</strong></div>
                  <SegmentedBar :value="b.ratio" />
                </div>
              </div>
            </section>
            <section class="card donuts stagger-item" style="--i: 9">
              <div class="donut-row">
                <div class="donut"><EChart :option="winDonut" :height="128" /><span class="donut-center">{{ ratioText(winValues.wins, winValues.misses) }}</span></div>
                <div class="legend">
                  <strong class="card-title">{{ t('Stat_MissRatePie') }}</strong>
                  <span class="caption"><i class="sw accent" />{{ t('Pulls_WinCount', winValues.wins) }}</span>
                  <span class="caption"><i class="sw track" />{{ t('Pulls_LossCount', winValues.misses) }}</span>
                </div>
              </div>
              <div class="donut-row">
                <div class="donut"><EChart :option="fourDonut" :height="128" /><span class="donut-center">{{ ratioText(fourStar.character, fourStar.weapon) }}</span></div>
                <div class="legend">
                  <strong class="card-title">{{ t('Stat_FourStarPieChart') }}</strong>
                  <span class="caption"><i class="sw four" />{{ t('Pulls_CharacterShare', ratioText(fourStar.character, fourStar.weapon)) }}</span>
                  <span class="caption"><i class="sw four-c" />{{ t('Pulls_WeaponShare', ratioText(fourStar.weapon, fourStar.character)) }}</span>
                </div>
              </div>
            </section>
          </div>
        </div>

        <!-- 单卡池：精简视图 -->
        <section v-else-if="current && effectiveView === 'compact'" :key="`c-${currentPoolNo}`" class="card">
          <h3 class="card-title">{{ t('Pulls_FiveStarRecords') }}</h3>
          <div class="tiles">
            <div v-if="pending" class="tile pending" :title="pending.next">
              <span class="tile-face q">?</span>
              <strong class="tile-pity">{{ pending.pity }}</strong>
            </div>
            <div v-for="(c, i) in poolCards" :key="i" class="tile" :class="{ near: c.pity >= 70 }"
              :title="`${c.name}\n${t('Pulls_TilePity', c.pity)}${c.lost ? t('Pulls_TileLost') : ''}${c.lost ? '\n' + t('Pulls_TileNextUp') : ''}`">
              <img class="tile-face" :src="iconUrl(c.resourceId)" alt="" loading="lazy" @error="iconFallback" />
              <strong class="tile-pity">{{ c.pity }}</strong>
              <span class="tile-bar" :style="{ width: `${Math.min(100, (c.pity / 80) * 100)}%` }" />
              <span v-if="c.lost" class="lost-badge">{{ t('Pulls_Lost') }}</span>
            </div>
          </div>
        </section>

        <!-- 单卡池：列表视图 -->
        <section v-else-if="current" :key="`l-${currentPoolNo}`" class="card">
          <div class="list-head">
            <h3 class="card-title">{{ t('Pulls_Records') }}</h3>
            <span class="caption">{{ t('Pulls_StoredCount', num(data.recordCount)) }} · {{ t('Pulls_RecordsNote') }}</span>
          </div>
          <div v-if="pending" class="pending-row">
            <span class="unknown">?</span>
            <div class="who">
              <div class="name-line"><strong>{{ t('Pulls_NoFiveStarYet') }}</strong><span v-if="pending.next" class="ntag">{{ pending.next }}</span></div>
              <span class="aux muted">{{ pending.last }}</span>
              <span class="kind-tag">{{ t('Pulls_LatestRecord') }}</span>
            </div>
            <div class="pending-mid">
              <div class="pity-line"><span>{{ t('Pulls_HavePrefix') }}</span><strong class="pity-num">{{ pending.pity }}</strong><span>{{ t('Pulls_HaveSuffix') }}</span></div>
              <SegmentedBar :value="pending.pity" :max="80" :danger="pending.pity >= 70" />
            </div>
            <span class="pending-hard">{{ t('Pulls_ToHardPityPrefix') }} <strong>{{ pending.toHard }}</strong> {{ t('Pulls_PullUnit') }}</span>
          </div>
          <div class="records">
            <FiveStarRow v-for="(c, i) in poolCards" :key="i" :card="c" :subtitle="c.time.slice(0, 16)" chips />
            <p v-if="!poolCards.length" class="caption empty-note">{{ t('Pulls_TimelineEmpty') }}</p>
          </div>
        </section>
      </template>

      <CharacterDetailDialog v-if="detail" :role-id="detail.resourceId" :name="detail.name" :star="5" @close="detail = null" />
      <BindUidDialog v-if="dialog && source.kind === 'cloud'" :uid="source.uid" @close="dialog = false" />
      <BindUidDialog v-else-if="dialog" local @close="dialog = false" />
    </GachaGate>
  </div>
</template>

<style scoped>
.pulls { display: flex; flex-direction: column; gap: 18px; padding-bottom: 76px; }
.filter { display: flex; justify-content: space-between; align-items: center; gap: 12px; }
.chips { display: flex; gap: 10px; flex-wrap: wrap; min-width: 0; }
.chips.sub { margin-top: -8px; gap: 6px; }
.chip { height: 44px; padding: 0 18px; font-size: 15px; }
.chip.small { height: 34px; padding: 0 14px; font-size: 13px; }
.chip .count { margin-left: 14px; color: var(--text-muted); font-weight: 400; }
.chip[aria-pressed='true'] .count { color: var(--accent-text); font-weight: 700; }
.tools { display: flex; gap: 10px; align-items: center; flex: none; }
.views { display: flex; gap: 2px; padding: 4px; border-radius: 10px; background: var(--surface-sunken); border: 1px solid var(--border); }
.views button { width: 42px; height: 34px; border: 0; border-radius: 7px; background: transparent; color: var(--text-secondary); cursor: pointer; display: grid; place-items: center; }
.views button[aria-checked='true'] { background: var(--surface); color: var(--text-primary); box-shadow: var(--shadow-pill); }
.icon-btn { width: 44px; height: 44px; border-radius: 10px; border: 1px solid var(--border); background: var(--surface); color: var(--text-secondary); cursor: pointer; display: grid; place-items: center; }
.icon-btn:hover:not(:disabled) { border-color: var(--accent-line); color: var(--accent-text); }
.icon-btn:disabled { opacity: 0.5; cursor: default; }

.metrics { display: grid; grid-template-columns: repeat(5, 1fr); gap: 22px; }
.metric { display: flex; flex-direction: column; gap: 4px; min-height: 140px; }
.label { font-size: 14px; color: var(--label); }
.metric-value { font-size: 44px; font-weight: 700; line-height: 1.1; margin-top: auto; letter-spacing: -1px; }
.metric-value small, .strip small { font-size: 14px; font-weight: 400; margin-left: 6px; color: var(--text-muted); letter-spacing: 0; }

.strip { display: grid; grid-template-columns: repeat(6, 1fr); padding: 16px 8px; }
.cell { display: flex; flex-direction: column; gap: 2px; padding: 0 24px; border-left: 1px solid var(--divider); }
.cell:first-child { border-left: 0; }
.cell strong { font-size: 32px; letter-spacing: -0.5px; }

.overview { display: flex; flex-direction: column; gap: 22px; }
.row-2 { display: grid; grid-template-columns: 1fr 554px; gap: 22px; }
.row-3 { display: grid; grid-template-columns: 1.4fr 1fr 1fr; gap: 22px; }
.latest { display: flex; flex-direction: column; margin-top: 16px; }
.latest > :deep(.row), .latest > .none-row { padding: 14px 0; border-top: 1px solid var(--divider); }
.latest > :first-child { border-top: 0; }
.none-row { display: flex; align-items: center; gap: 20px; padding-left: 6px !important; }
.unknown { width: 70px; height: 70px; flex: none; border-radius: 14px; border: 2px dashed var(--accent-line); background: var(--accent-container); color: var(--accent-text); display: grid; place-items: center; font-size: 34px; font-weight: 700; }
.who { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
.none-title { font-size: 19px; color: var(--text-secondary); }

.pity-row { padding: 18px 0 6px; }
.pity-head { display: flex; align-items: center; gap: 10px; height: 26px; font-size: 16px; }
.ntag.red { background: var(--danger-container); color: var(--danger); }
.near { animation: near-pulse 1.6s var(--ease-smooth) 3; }
@keyframes near-pulse { 50% { box-shadow: 0 0 0 4px var(--danger-soft); } }
.pity-line { display: flex; align-items: baseline; gap: 10px; margin: 8px 0 10px; font-size: 17px; }
.pity-num { font-size: 34px; letter-spacing: -1px; line-height: 1; }
.pity-num.danger { color: var(--danger); }
.to-hard { margin-left: auto; font-size: 13px; }
.scale { display: flex; justify-content: space-between; font-size: 13px; margin-top: 8px; }

.daily-head { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 10px; }
.shares { display: flex; flex-direction: column; gap: 14px; margin-top: 16px; }
.share { display: flex; flex-direction: column; gap: 6px; }
.share-head { display: flex; justify-content: space-between; }
.donuts { display: flex; flex-direction: column; justify-content: space-between; gap: 12px; }
.donut-row { display: flex; align-items: center; gap: 24px; }
.donut { position: relative; width: 128px; flex: none; }
.donut-center { position: absolute; inset: 0; display: grid; place-items: center; font-weight: 700; font-size: 20px; pointer-events: none; }
.legend { display: flex; flex-direction: column; gap: 6px; }
.sw { display: inline-block; width: 10px; height: 10px; border-radius: 2px; margin-right: 6px; vertical-align: -1px; }
.sw.accent { background: var(--accent); } .sw.track { background: var(--track); }
.sw.four { background: var(--four-star); } .sw.four-c { background: var(--four-star-container); }

.timeline-card { display: flex; flex-direction: column; gap: 10px; }
.tl-head { display: flex; align-items: center; gap: 10px; }
.tl-head .card-title { margin-right: 6px; }

.list-head { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 12px; }
.pending-row {
  display: grid; grid-template-columns: 70px minmax(150px, 220px) minmax(200px, 1fr) auto; gap: 20px; align-items: center;
  padding: 12px 14px; margin: 0 -8px 6px; border-radius: 10px; border: 1px solid var(--accent-line); background: var(--accent-container);
}
.pending-row .unknown { background: var(--surface); }
.name-line { display: flex; align-items: center; gap: 8px; font-size: 17px; }
.kind-tag { align-self: flex-start; margin-top: 4px; height: 22px; padding: 0 9px; border-radius: 999px; display: inline-flex; align-items: center; font-size: 12px; background: var(--surface-sunken); color: var(--text-secondary); }
.pending-mid .pity-line { margin: 0 0 8px; }
.pending-mid .pity-num { color: var(--accent-text); }
.pending-hard { font-size: 13px; color: var(--text-secondary); white-space: nowrap; }
.pending-hard strong { font-size: 18px; color: var(--accent-text); }
.records { display: flex; flex-direction: column; }
.empty-note { padding: 30px 0; text-align: center; }

.tiles { display: grid; grid-template-columns: repeat(auto-fill, minmax(128px, 1fr)); gap: 16px; margin-top: 16px; }
.tile { position: relative; display: flex; flex-direction: column; align-items: center; border-radius: 10px; overflow: hidden; border: 1px solid var(--divider); background: var(--surface-raised); padding-bottom: 8px; }
.tile-face { width: 100%; aspect-ratio: 1 / 0.9; object-fit: cover; object-position: top; background: linear-gradient(var(--accent-container), var(--surface-muted)); }
.tile-face.q { display: grid; place-items: center; font-size: 40px; font-weight: 700; color: var(--accent-text); }
.tile.pending { border: 1px dashed var(--accent-line); background: repeating-linear-gradient(135deg, var(--accent-container) 0 10px, var(--surface-raised) 10px 20px); }
.tile.pending .tile-face { background: transparent; }
.tile-pity { font-size: 22px; line-height: 1.2; margin-top: 6px; color: var(--accent-text); }
.tile.near .tile-pity { color: var(--danger); }
.tile-bar { position: absolute; left: 4px; bottom: 3px; height: 3px; border-radius: 2px; background: var(--accent); }
.tile.near .tile-bar { background: var(--danger); }
.lost-badge { position: absolute; top: 6px; right: 6px; width: 30px; height: 30px; border-radius: 50%; display: grid; place-items: center; font-size: 12px; font-weight: 700; background: var(--surface); color: var(--danger); border: 1.5px solid var(--danger); }
</style>
