<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue';
import { api, type NoticeFeed, type Notice } from '../../api';
import { t, language } from '../../i18n';
import { readPref, writePref } from '../../prefs';
import SegmentedBar from '../../components/SegmentedBar.vue';
import TimelineView from './TimelineView.vue';
import ChallengesView from './ChallengesView.vue';
import NoticeContent from '../../components/NoticeContent.vue';
import StrokeIcon from '../../components/StrokeIcon.vue';
import { range, stamp, duration, statusText, dateText, kindText, isActive, isEnded, groupKeys } from './eventsText';

// 活动页（src/UI/Pages/Events/EventsView.axaml）：版本卡、视图胶囊、主体（版本日历 / 全部公告）与右侧 440 宽详情。
// 「深塔 · 海墟 · 矩阵」占满主体整行，不显示公告详情。
type View = 'timeline' | 'calendar' | 'notices' | 'challenges';
const views: View[] = ['timeline', 'calendar', 'notices', 'challenges'];
type Region = 'global' | 'china';

const region = ref<Region>(readPref('events.region') === 'china' ? 'china' : 'global');
const savedView = readPref('events.view') as View;
const view = ref<View>(views.includes(savedView) ? savedView : 'timeline');
const feed = ref<NoticeFeed>();
const state = ref<'loading' | 'ready' | 'failed'>('loading');
const selectedId = ref<string>();
const now = ref(Date.now());
let clock: number | undefined;
let abort: AbortController | undefined;

async function load() {
  abort?.abort();
  abort = new AbortController();
  if (!feed.value || feed.value.region !== region.value) state.value = 'loading';
  try {
    const result = await api.notices(region.value, language.value, abort.signal);
    feed.value = result;
    state.value = 'ready';
    if (!result.notices.some(n => n.id === selectedId.value)) selectedId.value = defaultSelection(result.notices);
  } catch (e) {
    if ((e as Error).name === 'AbortError') return;
    if (!feed.value || feed.value.region !== region.value) state.value = 'failed';
  }
}

function defaultSelection(list: Notice[]) {
  const n = now.value;
  return (list.find(x => x.kind === 'Gacha' && isActive(x, n)) ?? list.find(x => isActive(x, n)) ?? list[0])?.id;
}

onMounted(() => {
  load();
  clock = window.setInterval(() => (now.value = Date.now()), 30_000);
});
onBeforeUnmount(() => {
  clearInterval(clock);
  abort?.abort();
});
watch([region, language], load);
watch(region, v => writePref('events.region', v));
watch(view, v => writePref('events.view', v));

const versionTitle = computed(() => feed.value?.versionName ?? t('Nav_Events'));
const versionLabel = computed(() => (feed.value?.versionNumber ? t('Events_VersionLabel', feed.value.versionNumber) : ''));
const hasRange = computed(() => !!feed.value?.versionStart && !!feed.value?.versionEnd);
const versionRange = computed(() => (hasRange.value ? range(feed.value!.versionStart!, feed.value!.versionEnd) : ''));
const versionProgress = computed(() => {
  if (!hasRange.value) return 0;
  const vs = Date.parse(feed.value!.versionStart!);
  const ve = Date.parse(feed.value!.versionEnd!);
  return ve > vs ? Math.min(1, Math.max(0, (now.value - vs) / (ve - vs))) : 0;
});
const versionRemaining = computed(() => {
  if (!hasRange.value) return '';
  const vs = Date.parse(feed.value!.versionStart!);
  const ve = Date.parse(feed.value!.versionEnd!);
  if (now.value >= ve) return t('Events_VersionEnded');
  if (now.value < vs) return t('Events_StartsIn', duration(vs - now.value));
  return t('Events_VersionRemaining', duration(ve - now.value));
});
const updatedText = computed(() => (feed.value ? t('Events_Updated', stamp(feed.value.fetchedAt)) : ''));

const groups = computed(() => {
  const list = feed.value?.notices ?? [];
  const n = now.value;
  return groupKeys
    .map(([kind, key]) => ({
      kind,
      title: t(key),
      items: list
        .filter(x => x.kind === kind)
        .sort((a, b) => Number(isEnded(a, n)) - Number(isEnded(b, n)) || Date.parse(b.start) - Date.parse(a.start)),
    }))
    .filter(g => g.items.length > 0);
});

const selected = computed(() => feed.value?.notices.find(n => n.id === selectedId.value));
const hotspots = computed(() => {
  const cal = feed.value?.calendar;
  if (!cal) return [];
  const byId = new Map(feed.value!.notices.map(n => [n.id, n]));
  return cal.nodes.map(node => {
    const item = node.published ? byId.get(node.noticeId) : undefined;
    return { ...node, item, tip: item?.title ?? t('Events_NotPublished') };
  });
});

// 手机上详情以底部面板覆盖显示：只有点选公告时才打开（默认选中项不弹出）
const sheetOpen = ref(false);
function select(id?: string) {
  if (!id) return;
  selectedId.value = id;
  sheetOpen.value = true;
}
</script>

<template>
  <div class="events">
    <!-- 版本卡 -->
    <section class="card version">
      <div class="version-main">
        <div class="version-tags">
          <span class="parent-title">{{ t('Events_CurrentVersion') }}</span>
          <span v-if="versionLabel" class="ntag">{{ versionLabel }}</span>
        </div>
        <h2 class="page-title version-title">{{ versionTitle }}</h2>
        <span v-if="hasRange" class="caption">{{ versionRange }}</span>
      </div>
      <div class="version-side">
        <div class="region" role="radiogroup" :aria-label="t('Nav_Events')">
          <span class="region-thumb" :class="{ right: region === 'global' }" />
          <button class="region-opt" role="radio" :aria-checked="region === 'china'" @click="region = 'china'">{{ t('Home_RegionCN') }}</button>
          <button class="region-opt" role="radio" :aria-checked="region === 'global'" @click="region = 'global'">{{ t('Home_RegionGlobal') }}</button>
        </div>
        <div v-if="hasRange" class="remaining">
          <span class="remaining-text">{{ versionRemaining }}</span>
          <SegmentedBar :value="versionProgress" thin />
        </div>
      </div>
    </section>

    <!-- 视图胶囊与更新时间 -->
    <div class="toolbar">
      <div class="views" role="radiogroup">
        <button class="chip small" role="radio" :aria-checked="view === 'timeline'" @click="view = 'timeline'">{{ t('Events_ViewTimeline') }}</button>
        <button class="chip small" role="radio" :aria-checked="view === 'calendar'" @click="view = 'calendar'">{{ t('Events_ViewCalendar') }}</button>
        <button class="chip small" role="radio" :aria-checked="view === 'notices'" @click="view = 'notices'">{{ t('Events_ViewNotices') }}</button>
        <button class="chip small" role="radio" :aria-checked="view === 'challenges'" @click="view = 'challenges'">{{ t('Events_ViewChallenges') }}</button>
      </div>
      <span v-if="state === 'ready' && view !== 'challenges'" class="caption updated">{{ updatedText }}</span>
    </div>

    <!-- 主体 -->
    <ChallengesView v-if="view === 'challenges'" :now="now" />
    <div v-else class="body">
      <div class="main">
        <section v-if="state === 'loading'" class="card"><span class="caption">{{ t('Events_Loading') }}</span></section>
        <section v-else-if="state === 'failed'" class="card failed">
          <h3 class="card-title">{{ t('Events_FailedTitle') }}</h3>
          <p class="caption">{{ t('Events_FailedHint') }}</p>
          <button class="btn" @click="load">{{ t('Events_Retry') }}</button>
        </section>
        <section v-else-if="!feed?.notices.length" class="card"><span class="caption">{{ t('Events_Empty') }}</span></section>

        <TimelineView v-else-if="view === 'timeline'" :feed="feed" :now="now" :selected-id="selectedId" @select="select" />

        <template v-else-if="view === 'calendar'">
          <section v-if="feed.calendar?.backgroundUrl" class="card calendar">
            <div class="calendar-box">
              <img :src="feed.calendar.backgroundUrl" alt="" referrerpolicy="no-referrer" />
              <button
                v-for="h in hotspots" :key="h.noticeId" class="hotspot" :class="{ pending: !h.item }" :title="h.tip" :aria-label="h.tip"
                :style="{ left: `${h.x1}%`, top: `${h.y1}%`, width: `${h.x2 - h.x1}%`, height: `${h.y2 - h.y1}%` }"
                @click="select(h.item?.id)"
              />
            </div>
          </section>
          <section v-else class="card"><span class="caption">{{ t('Events_CalendarMissing') }}</span></section>
        </template>

        <div v-else class="groups stagger">
          <section v-for="(g, i) in groups" :key="g.kind" class="card group stagger-item" :style="{ '--i': i }">
            <h3 class="card-title group-title">{{ g.title }}</h3>
            <button
              v-for="n in g.items" :key="n.id" class="notice-row" :class="{ selected: n.id === selectedId }" @click="select(n.id)"
            >
              <span class="thumb"><img v-if="n.banner" :src="n.banner" alt="" loading="lazy" referrerpolicy="no-referrer" /></span>
              <span class="row-text">
                <span class="aux row-title">{{ n.title }}</span>
                <span class="caption">{{ dateText(n) }}</span>
              </span>
              <span class="status" :class="isActive(n, now) ? 'status-active' : 'muted'">{{ statusText(n, now) }}</span>
            </button>
          </section>
        </div>
      </div>

      <!-- 详情 -->
      <div v-if="sheetOpen" class="sheet-scrim" aria-hidden="true" @click="sheetOpen = false" />
      <aside class="card detail" :class="{ 'sheet-open': sheetOpen }">
        <button class="sheet-close" :aria-label="t('Action_Close')" @click="sheetOpen = false"><StrokeIcon name="Close" :size="18" /></button>
        <span v-if="!selected" class="caption select-hint">{{ t('Events_SelectHint') }}</span>
        <div v-else :key="selected.id" class="detail-scroll">
          <img v-if="selected.banner" class="banner" :src="selected.banner" alt="" referrerpolicy="no-referrer" />
          <div class="detail-body">
            <div class="detail-tags">
              <span class="ntag" :class="{ update: selected.kind === 'Update', event: selected.kind === 'Event', 'muted-tag': selected.kind === 'Info' }">{{ kindText(selected) }}</span>
              <span class="status" :class="isActive(selected, now) ? 'status-active' : 'muted'">{{ statusText(selected, now) }}</span>
            </div>
            <h3 class="detail-title">{{ selected.title }}</h3>
            <span class="aux secondary">{{ dateText(selected) }}</span>
            <div v-if="selected.featured.length" class="mini featured">
              <span class="caption">{{ t('Events_Featured') }}</span>
              <strong>{{ selected.featured.join(t('Events_ListSeparator')) }}</strong>
            </div>
            <hr class="divider" />
            <NoticeContent :html="selected.content" />
          </div>
        </div>
      </aside>
    </div>
  </div>
</template>

<style scoped>
.events { display: grid; grid-template-rows: auto auto 1fr; gap: 14px; min-height: 640px; }

.version { display: grid; grid-template-columns: 1fr auto; gap: 28px; padding: 18px 24px; }
.version-main { display: flex; flex-direction: column; gap: 4px; justify-content: center; min-width: 0; }
.version-tags { display: flex; align-items: center; gap: 8px; }
.version-title { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.version-side { width: 300px; display: flex; flex-direction: column; gap: 10px; justify-content: center; }
.remaining { display: flex; flex-direction: column; gap: 6px; }
.remaining-text { font-size: 13px; font-weight: 600; text-align: right; }

/* 区服切换：滑块随选中项移动 */
.region { position: relative; align-self: flex-end; display: grid; grid-template-columns: 1fr 1fr; padding: 3px; border-radius: 999px; background: var(--divider); }
.region-thumb { position: absolute; top: 3px; bottom: 3px; left: 3px; width: calc(50% - 3px); border-radius: 999px; background: var(--surface); box-shadow: var(--shadow-soft); transition: transform var(--motion-standard) var(--ease-bounce); }
.region-thumb.right { transform: translateX(100%); }
.region-opt { position: relative; z-index: 1; height: 28px; padding: 0 14px; border: 0; background: transparent; border-radius: 999px; font-size: 12px; cursor: pointer; color: var(--text-muted); transition: color var(--motion-fast) linear; }
.region-opt[aria-checked='true'] { color: var(--text-primary); font-weight: 600; }

.toolbar { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.views { display: flex; gap: 6px; }
.updated { padding: 4px 12px; border-radius: 999px; background: var(--surface); }

.body { display: grid; grid-template-columns: 1fr 440px; gap: var(--card-gap); align-items: start; }
.main { min-width: 0; }
.failed { display: flex; flex-direction: column; gap: 8px; align-items: flex-start; }
.failed p { margin: 0; }

.groups { display: flex; flex-direction: column; gap: 14px; }
.group { padding: 12px; display: flex; flex-direction: column; gap: 2px; }
.group-title { margin: 2px 0 6px 10px; }
.notice-row {
  display: grid; grid-template-columns: auto 1fr auto; gap: 12px; align-items: center; width: 100%;
  padding: 9px 10px; border: 0; border-radius: 9px; background: transparent; cursor: pointer; text-align: left;
  transition: background-color 150ms linear;
}
.notice-row:hover { background: var(--surface-hover); }
.notice-row.selected { background: var(--accent-container); }
.thumb { width: 104px; height: 44px; border-radius: 7px; overflow: hidden; background: var(--surface-sunken); }
.thumb img { width: 100%; height: 100%; object-fit: cover; }
.row-text { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.row-title { font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.status { font-size: 12px; white-space: nowrap; }
.status-active { color: var(--accent-text); font-weight: 600; }

.calendar { padding: 10px; }
.calendar-box { position: relative; }
.calendar-box img { width: 100%; height: auto; border-radius: 8px; }
.hotspot { position: absolute; border: 2px solid transparent; border-radius: 6px; background: transparent; cursor: pointer; padding: 0; transition: border-color 120ms linear, background-color 120ms linear; }
.hotspot:hover { border-color: var(--accent); background: rgb(255 255 255 / 0.13); }
.hotspot.pending { cursor: default; }
.hotspot.pending:hover { border-color: var(--text-muted); background: rgb(0 0 0 / 0.2); }

.detail { position: sticky; top: 18px; padding: 0; overflow: hidden; max-height: calc(100vh - 36px); min-height: 320px; display: flex; margin-bottom: 76px; }
.select-hint { margin: auto; }
.detail-scroll { overflow-y: auto; width: 100%; animation: fade-in var(--motion-standard) var(--ease-standard); }
.banner { width: 100%; max-height: 230px; object-fit: cover; }
.detail-body { padding: 16px 22px 24px; display: flex; flex-direction: column; gap: 10px; }
.detail-tags { display: flex; align-items: center; gap: 8px; }
.detail-title { font-size: 18px; font-weight: 700; margin: 0; user-select: text; }
.featured { padding: 8px 12px; display: flex; gap: 6px; align-items: baseline; }
.featured strong { color: var(--accent-text); }
.divider { border: 0; border-top: 1px solid var(--divider); margin: 0; width: 100%; }
.sheet-close, .sheet-scrim { display: none; }

/* 手机：单栏；公告详情改为点选后从底部弹出的面板 */
@media (max-width: 768px) {
  .events { min-height: 0; gap: 12px; }
  .version { grid-template-columns: minmax(0, 1fr); gap: 12px; padding: 14px 16px; }
  .version-title { white-space: normal; }
  .version-side { width: auto; }
  .region { align-self: flex-start; }
  .toolbar { flex-wrap: wrap; }
  .views { flex-wrap: wrap; }
  .body { grid-template-columns: minmax(0, 1fr); }
  .thumb { width: 72px; height: 34px; }
  .notice-row { gap: 10px; padding: 8px 6px; }
  .notice-row .status { display: none; }
  .detail { display: none; }
  .detail.sheet-open {
    display: flex; position: fixed; z-index: 48; left: 0; right: 0; bottom: 0; top: auto; margin: 0;
    max-height: 82dvh; min-height: 0; border-radius: var(--radius-dialog) var(--radius-dialog) 0 0;
    padding-bottom: env(safe-area-inset-bottom); box-shadow: var(--shadow-dialog);
    animation: sheet-in var(--motion-relaxed) var(--ease-standard);
  }
  .sheet-scrim { display: block; position: fixed; inset: 0; z-index: 47; background: var(--scrim); animation: fade-in var(--motion-standard) var(--ease-standard); }
  .sheet-close {
    display: grid; place-items: center; position: absolute; right: 10px; top: 10px; z-index: 2; width: 34px; height: 34px; padding: 0;
    border-radius: 50%; border: 1px solid var(--border); background: var(--surface); color: var(--text-primary); cursor: pointer;
  }
  .detail-body { padding: 14px 16px 20px; }
}
@keyframes sheet-in { from { transform: translateY(40px); opacity: 0; } }
</style>
