<script setup lang="ts">
import { computed, ref, watch, onBeforeUnmount } from 'vue';
import { feeds, type NoticeFeed, type Notice, type Livestream } from '../../api';
import { t } from '../../i18n';
import { toast } from '../../components/toast';
import { stamp, range, duration } from './eventsText';

// 时间线（EventsViewModel.BuildTimeline）：唤取、活动与循环玩法三组；时间范围覆盖版本周期，
// 向前延伸到正在进行的循环玩法的开始（最多 4 周）；刻度为每周一，版本周期画底色。国服在上方显示前瞻兑换码。
const props = defineProps<{ feed: NoticeFeed; now: number; selectedId?: string }>();
const emit = defineEmits<{ select: [id: string] }>();

type Lane = 'character' | 'weapon' | 'reverb' | 'event' | 'recurring';
interface Bar { id: string; noticeId?: string; title: string; subtitle?: string; lane: Lane; start: number; end: number; tip: string }
type Row = { header: string; count: number } | Bar;

const DAY = 86_400_000;

const model = computed(() => {
  const feed = props.feed;
  const now = props.now;
  const notices = feed.notices.filter(n => (n.kind === 'Gacha' || n.kind === 'Event') && n.end);
  const ms = (s?: string) => (s ? Date.parse(s) : undefined);
  const vStartRaw = ms(feed.versionStart);
  const vEndRaw = ms(feed.versionEnd);
  const versionStart = vStartRaw ?? (notices.length ? Math.min(...notices.map(n => Date.parse(n.start))) : now);
  let versionEnd = vEndRaw ?? (notices.length ? Math.max(...notices.map(n => Date.parse(n.end!))) : versionStart + 42 * DAY);
  if (versionEnd <= versionStart) versionEnd = versionStart + 42 * DAY;

  const recurring = feed.recurring ?? [];
  let start = versionStart;
  for (const w of recurring) start = Math.min(start, Date.parse(w.start));
  start = Math.max(start, versionStart - 28 * DAY);
  // 左边留出 2 天，第一个刻度文字不贴边；对齐到本地 0 点
  const ls = new Date(start - 2 * DAY);
  start = new Date(ls.getFullYear(), ls.getMonth(), ls.getDate()).getTime();
  const end = versionEnd;
  const total = end - start;
  const ratio = (x: number) => Math.min(1, Math.max(0, (x - start) / total));

  const visible = notices.filter(n => Date.parse(n.end!) > start && Date.parse(n.start) < end);
  const sep = t('Events_ListSeparator');
  const lane = (n: Notice): Lane =>
    n.kind === 'Event' ? 'event' : n.pool === 'Weapon' ? 'weapon' : n.pool === 'CharacterReverb' || n.pool === 'WeaponReverb' ? 'reverb' : 'character';
  const bar = (n: Notice): Bar => {
    let subtitle: string | undefined = n.featured.length ? n.featured.join(sep) : undefined;
    // 卡池行只写卡池名，类型由横条颜色区分；武器池的 UP 就是卡池名，不重复写
    const title = n.kind === 'Gacha' && n.poolName ? n.poolName : n.title;
    if (subtitle === title) subtitle = undefined;
    return { id: n.id, noticeId: n.id, title, subtitle, lane: lane(n), start: Date.parse(n.start), end: Date.parse(n.end!), tip: `${n.title}\n${range(n.start, n.end)}` };
  };
  const poolOrder = (n: Notice) => (n.pool === 'Character' ? 0 : n.pool === 'Weapon' ? 1 : n.pool === 'CharacterReverb' ? 2 : 3);
  const gacha = visible.filter(n => n.kind === 'Gacha').sort((a, b) => poolOrder(a) - poolOrder(b) || Date.parse(a.start) - Date.parse(b.start));
  const events = visible.filter(n => n.kind === 'Event').sort((a, b) => Date.parse(a.start) - Date.parse(b.start) || Date.parse(a.end!) - Date.parse(b.end!));

  const rows: Row[] = [];
  if (gacha.length) rows.push({ header: t('Events_GroupGacha'), count: gacha.length }, ...gacha.map(bar));
  if (events.length) rows.push({ header: t('Events_GroupEvent'), count: events.length }, ...events.map(bar));
  if (recurring.length) {
    rows.push({ header: t('Events_GroupRecurring'), count: recurring.length });
    for (const w of recurring) {
      const rule = w.kind === 'Weekly' ? t('Events_RuleWeekly') : w.kind === 'Interval' ? t('Events_RuleEvery', w.everyDays) : t('Events_RuleVersion');
      const title = t(`Events_Recur_${w.key}`);
      rows.push({ id: `recur-${w.key}`, title, subtitle: rule, lane: 'recurring', start: Date.parse(w.start), end: Date.parse(w.end), tip: `${title}\n${range(w.start, w.end)}\n${t('Events_RecurHint')}` });
    }
  }

  // 刻度：时间范围内的每个周一（本地日期）
  const ticks: { left: number; text: string }[] = [];
  const day = new Date(start);
  while (day.getDay() !== 1) day.setDate(day.getDate() + 1);
  for (; day.getTime() < end; day.setDate(day.getDate() + 7)) ticks.push({ left: ratio(day.getTime()), text: `${day.getMonth() + 1}/${day.getDate()}` });

  const bars = rows.filter((r): r is Bar => 'id' in r).length;
  return {
    rows, ticks, bars, start, end, ratio,
    band: vStartRaw !== undefined && vEndRaw !== undefined ? { left: ratio(versionStart), right: ratio(versionEnd) } : undefined,
  };
});

function rowState(b: Bar) {
  const now = props.now;
  const upcoming = b.start > now;
  const ended = b.end <= now;
  const remaining = upcoming ? t('Events_StartsIn', duration(b.start - now)) : ended ? t('Events_Ended') : duration(b.end - now);
  const date = upcoming ? t('Events_StartsAt', stamp(new Date(b.start).toISOString())) : stamp(new Date(b.end).toISOString());
  return { upcoming, ended, remaining, date };
}

const today = computed(() => {
  const { start, end, ratio } = model.value;
  const n = props.now;
  if (n < start || n > end) return undefined;
  const d = new Date(n);
  return { left: ratio(n), text: `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}` };
});
const versionLabel = computed(() => (props.feed.versionNumber ? t('Events_VersionLabel', props.feed.versionNumber) : ''));

// ── 前瞻兑换码（只有国服社区） ──
const live = ref<Livestream>();
let abort: AbortController | undefined;
async function loadLive() {
  abort?.abort();
  live.value = undefined;
  if (props.feed.region !== 'china') return;
  abort = new AbortController();
  try {
    live.value = await feeds.livestream(abort.signal);
  } catch {
    /* 兑换码只是附加信息，失败时不显示 */
  }
}
watch(() => props.feed.region, loadLive, { immediate: true });
onBeforeUnmount(() => abort?.abort());

const codeSet = computed(() => live.value?.codes.find(c => !c.expiresAt || Date.parse(c.expiresAt) > props.now));
const codesTitle = computed(() => (codeSet.value?.version ? t('Events_CodesVersion', codeSet.value.version) : t('Events_Codes')));
const preview = computed(() => {
  const p = live.value?.preview;
  if (!p || Date.parse(p.startsAt) + 3 * 3_600_000 <= props.now) return undefined;
  return { text: t('Events_Preview', p.version ?? '', stamp(p.startsAt)).trim(), url: p.postUrl };
});
const copied = ref<string>();
async function copy(code: string) {
  try {
    await navigator.clipboard.writeText(code);
    copied.value = code;
    toast(t('Web_Copied'), 'success', 1800);
  } catch {
    toast(t('Web_CopyFailed'), 'error');
  }
}
const isBar = (r: Row): r is Bar => 'id' in r;
</script>

<template>
  <div class="timeline-view stagger">
    <section v-if="codeSet || preview" class="card live stagger-item" style="--i: 0">
      <div v-if="preview" class="live-row">
        <strong>{{ preview.text }}</strong>
        <a class="btn" :href="preview.url" target="_blank" rel="noopener noreferrer">{{ t('Events_OpenPost') }}</a>
      </div>
      <template v-if="codeSet">
        <div class="live-row">
          <div class="live-head">
            <h3 class="card-title">{{ codesTitle }}</h3>
            <span v-if="codeSet.expiresAt" class="caption">{{ t('Events_CodesExpires', stamp(codeSet.expiresAt)) }}</span>
          </div>
          <a class="btn" :href="codeSet.postUrl" target="_blank" rel="noopener noreferrer">{{ t('Events_OpenPost') }}</a>
        </div>
        <div class="codes">
          <button v-for="c in codeSet.codes" :key="c" class="code" :class="{ copied: copied === c }" :title="t('Events_Copy')" @click="copy(c)">{{ c }}</button>
        </div>
        <span class="caption">{{ t('Events_CodesSource') }}</span>
      </template>
    </section>

    <section v-if="model.bars" class="card tl stagger-item" style="--i: 1">
      <div class="tl-head">
        <h3 class="card-title">{{ t('Events_ViewTimeline') }}</h3>
        <span class="caption">{{ t('Events_Count', model.bars) }}</span>
      </div>
      <div class="tl-grid">
        <!-- 背景层：版本周期底色、每周一竖线、当前时刻 -->
        <div class="lane-area overlay" aria-hidden="true">
          <div v-if="model.band" class="band" :style="{ left: `${model.band.left * 100}%`, right: `${(1 - model.band.right) * 100}%` }" />
          <div v-for="k in model.ticks" :key="k.text" class="vline" :style="{ left: `${k.left * 100}%` }" />
        </div>

        <div class="tl-row header">
          <span class="caption name">{{ t('Events_ColName') }}</span>
          <div class="lane ticks">
            <span v-if="model.band && versionLabel" class="band-label" :style="{ left: `${model.band.left * 100}%` }">{{ versionLabel }}</span>
            <span v-for="k in model.ticks" :key="k.text" class="tick mono" :style="{ left: `${k.left * 100}%` }">{{ k.text }}</span>
          </div>
          <span class="caption right">{{ t('Events_ColRemaining') }}</span>
        </div>

        <template v-for="r in model.rows" :key="isBar(r) ? r.id : `h-${r.header}`">
          <div v-if="!isBar(r)" class="group-head">
            <strong>{{ r.header }}</strong><span class="count-badge">{{ r.count }}</span>
          </div>
          <button
            v-else class="tl-row bar-row" :class="{ static: !r.noticeId, selected: r.noticeId && r.noticeId === selectedId }"
            :title="r.tip" @click="r.noticeId && emit('select', r.noticeId)"
          >
            <span class="name">
              <span class="tl-title" :class="{ muted: rowState(r).ended }">{{ r.title }}</span>
              <span v-if="r.subtitle" class="caption ellipsis">{{ r.subtitle }}</span>
            </span>
            <span class="lane">
              <span
                class="tl-bar" :class="[r.lane, { upcoming: rowState(r).upcoming, ended: rowState(r).ended }]"
                :style="{ left: `${model.ratio(r.start) * 100}%`, width: `max(10px, ${(model.ratio(r.end) - model.ratio(r.start)) * 100}%)` }"
              />
            </span>
            <span class="right">
              <strong :class="{ muted: rowState(r).ended }">{{ rowState(r).remaining }}</strong>
              <span class="caption mono">{{ rowState(r).date }}</span>
            </span>
          </button>
        </template>

        <div v-if="today" class="lane-area overlay top" aria-hidden="true">
          <div class="now-line" :style="{ left: `${today.left * 100}%` }" />
          <span class="now-pill mono" :style="{ left: `${today.left * 100}%` }">{{ today.text }}</span>
        </div>
      </div>
    </section>
    <section v-else class="card"><span class="caption">{{ t('Events_Empty') }}</span></section>
  </div>
</template>

<style scoped>
.timeline-view { display: flex; flex-direction: column; gap: 14px; }
.live { display: flex; flex-direction: column; gap: 10px; padding: 14px 20px; }
.live-row { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.live-head { display: flex; flex-direction: column; gap: 2px; }
.codes { display: flex; flex-wrap: wrap; gap: 8px; }
.code {
  font-family: var(--font-mono, ui-monospace, monospace); font-size: 13px; font-weight: 600; padding: 6px 12px; border-radius: var(--radius-small);
  border: 1px solid var(--border); background: var(--surface-raised); color: var(--text-primary); cursor: pointer; user-select: text;
  transition: border-color 150ms linear, background-color 150ms linear;
}
.code:hover { border-color: var(--accent-line); }
.code.copied { background: var(--accent-container); border-color: var(--accent-line); color: var(--accent-text); }

.tl { padding: 0; overflow: hidden; }
.tl-head { display: flex; align-items: center; gap: 10px; padding: 16px 22px 14px; border-bottom: 1px solid var(--divider); }
.tl-grid { position: relative; padding-bottom: 10px; --name-w: 232px; --right-w: 132px; }
.overlay { position: absolute; top: 0; bottom: 0; left: var(--name-w); right: var(--right-w); pointer-events: none; }
.overlay.top { z-index: 2; }
.band { position: absolute; top: 0; bottom: 0; background: var(--accent-container); opacity: 0.5; }
.vline { position: absolute; top: 0; bottom: 0; width: 1px; background: var(--divider); transform: translateX(-0.5px); }
.now-line { position: absolute; top: 52px; bottom: 0; width: 2px; background: var(--accent); transform: translateX(-1px); }
.now-pill { position: absolute; top: 46px; transform: translateX(-50%); padding: 1px 7px; border-radius: 999px; background: var(--accent); color: var(--text-on-accent); font-size: 11px; font-weight: 700; }

.tl-row { position: relative; z-index: 1; display: grid; grid-template-columns: var(--name-w) 1fr var(--right-w); align-items: center; width: 100%; }
.tl-row.header { height: 58px; border-bottom: 1px solid var(--divider); }
.tl-row .name { padding: 0 14px 0 22px; display: flex; flex-direction: column; gap: 1px; min-width: 0; text-align: left; }
.tl-row .right { padding-right: 22px; display: flex; flex-direction: column; gap: 1px; align-items: flex-end; text-align: right; font-size: 13px; }
.tl-row .right .caption { font-size: 11px; }
.lane { position: relative; height: 100%; display: block; }
.ticks { overflow: hidden; }
.tick { position: absolute; top: 22px; transform: translateX(-50%); font-size: 12px; font-weight: 600; color: var(--text-secondary); padding: 0 4px; }
.band-label { position: absolute; top: 5px; margin-left: 6px; font-size: 11px; font-weight: 700; color: var(--accent-text); white-space: nowrap; }

.group-head { position: relative; z-index: 1; display: flex; align-items: center; gap: 8px; margin: 14px 0 4px 22px; font-size: 13px; }
.count-badge { min-width: 20px; height: 18px; padding: 0 6px; border-radius: 999px; background: var(--divider); font-size: 11px; display: inline-flex; align-items: center; justify-content: center; }

.bar-row { height: 50px; border: 0; background: transparent; padding: 0; cursor: pointer; color: inherit; font: inherit; transition: background-color 150ms linear; }
.bar-row:hover { background: var(--surface-hover); }
.bar-row.static { cursor: default; }
.bar-row.selected { background: var(--accent-container); }
.tl-title { font-size: 13.5px; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.ellipsis { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.muted { color: var(--text-muted); }
.tl-bar { position: absolute; top: 50%; height: 10px; margin-top: -5px; border-radius: 5px; background: var(--accent); }
.tl-bar.weapon { background: var(--info); }
.tl-bar.reverb { background: var(--four-star); }
.tl-bar.event { background: var(--success); }
.tl-bar.recurring { background: var(--text-muted); }
.tl-bar.upcoming { opacity: 0.55; }
.tl-bar.ended { opacity: 0.3; }

/* 手机：名称与右侧列收窄，时间轴整体可左右滑动 */
@media (max-width: 768px) {
  .live { padding: 12px 14px; }
  .live-row { flex-wrap: wrap; }
  .tl { overflow-x: auto; }
  .tl-head { padding: 12px 14px; }
  .tl-grid { --name-w: 150px; --right-w: 96px; min-width: 620px; }
  .tl-row .name { padding: 0 10px 0 14px; }
  .tl-row .right { padding-right: 14px; }
  .group-head { margin-left: 14px; }
}
</style>
