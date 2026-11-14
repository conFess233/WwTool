<script setup lang="ts">
import { computed, ref, watch, nextTick, onMounted } from 'vue';
import { t } from '../../i18n';
import { iconUrl, iconFallback, poolTitleKey } from '../../format';
import { kindKey, ratingOf, ratingColor, ratings, isWeapon, type FiveStarCard } from './fiveStar';

// 五星时间线（PullsTimelineView）：最新在右，节点上下交替；外圈、连线与轨道点按出金评价着色；跨月处画竖虚线与月份。
// 单卡池时末尾显示「未出金」节点。
const props = defineProps<{ cards: FiveStarCard[]; pending?: { pity: number; next: string } | null }>();
// 点击角色头像打开角色详情（武器没有详情）
const emit = defineEmits<{ open: [card: FiveStarCard] }>();

interface Node { card?: FiveStarCard; up: boolean; color: string; date: string; rating: string; month?: string; pity: number; next?: string }

const nodes = computed<Node[]>(() => {
  const out: Node[] = [];
  let last = '';
  for (const c of props.cards) {
    const month = c.time.slice(0, 7);
    const r = ratingOf(c.pity);
    out.push({ card: c, up: out.length % 2 === 0, color: ratingColor[r], date: c.time.slice(0, 10), rating: t(`Pulls_Rating_${r}`),
      month: month && month !== last ? month : undefined, pity: c.pity });
    if (month) last = month;
  }
  if (props.pending && props.pending.pity > 0)
    out.push({ up: out.length % 2 === 0, color: ratingColor[ratingOf(props.pending.pity)], date: '', rating: '', pity: props.pending.pity, next: props.pending.next });
  return out;
});

const scroller = ref<HTMLElement | null>(null);
function toEnd() {
  nextTick(() => scroller.value && (scroller.value.scrollLeft = scroller.value.scrollWidth));
}
onMounted(toEnd);
watch(nodes, toEnd);

// 滚轮横向滚动
function onWheel(e: WheelEvent) {
  const el = scroller.value;
  if (!el || Math.abs(e.deltaX) > Math.abs(e.deltaY)) return;
  if (el.scrollWidth <= el.clientWidth) return;
  e.preventDefault();
  el.scrollLeft += e.deltaY;
}
const tip = (n: Node) => n.card ? t('Pulls_TimelineTip', t(poolTitleKey(n.card.pool)), n.card.time, n.card.pity, n.rating) : '';
</script>

<template>
  <div class="timeline">
    <p v-if="!nodes.length" class="empty caption">{{ t('Pulls_TimelineEmpty') }}</p>
    <div v-else ref="scroller" class="scroller" @wheel="onWheel">
      <div class="track-row">
        <div v-for="(n, i) in nodes" :key="i" class="col" :class="{ up: n.up, down: !n.up, month: !!n.month }" :style="{ '--c': n.color }" :title="tip(n)">
          <span v-if="n.month" class="month-label caption">{{ n.month }}</span>
          <div class="node">
            <button v-if="n.card && !isWeapon(n.card.resourceId)" class="avatar clickable" :aria-label="n.card.name" @click="emit('open', n.card)">
              <img :src="iconUrl(n.card.resourceId)" alt="" loading="lazy" @error="iconFallback" />
            </button>
            <div v-else class="avatar" :class="{ pending: !n.card }">
              <img v-if="n.card" :src="iconUrl(n.card.resourceId)" alt="" loading="lazy" @error="iconFallback" />
              <span v-else>?</span>
            </div>
            <strong class="name">{{ n.card ? n.card.name : t('Pulls_NoFiveStarYet') }}</strong>
            <div class="pity-line"><b>{{ n.pity }}</b><span class="unit">{{ t('Pulls_PullUnit') }}</span><span v-if="n.rating" class="rating">{{ n.rating }}</span></div>
            <div class="tags">
              <template v-if="n.card">
                <span class="kind" :class="n.card.kind">{{ t(kindKey(n.card.kind)) }}</span>
                <span v-if="n.card.lost" class="kind lost">{{ t('Pulls_Lost') }}</span>
              </template>
              <span v-else-if="n.next" class="kind">{{ n.next }}</span>
            </div>
            <span class="stem" />
          </div>
          <span class="dot" />
          <span class="date caption">{{ n.date }}</span>
        </div>
      </div>
    </div>
    <div class="legend">
      <template v-for="(r, i) in ratings" :key="r">
        <span class="lg" :style="{ '--c': ratingColor[r] }"><i />{{ t(`Pulls_Rating_${r}`) }}</span>
        <span v-if="i < ratings.length - 1" class="caption arrow">›</span>
      </template>
      <span class="caption hint">{{ t('Web_TimelineHint') }}</span>
    </div>
  </div>
</template>

<style scoped>
.timeline { display: flex; flex-direction: column; min-height: 0; }
.empty { padding: 60px 0; text-align: center; }
.scroller { overflow-x: auto; overflow-y: hidden; padding-bottom: 6px; }
.track-row { display: flex; height: 470px; padding: 0 20px; position: relative; width: max-content; min-width: 100%; }
/* 中线轨道 */
.track-row::before { content: ''; position: absolute; left: 0; right: 0; top: 50%; height: 2px; margin-top: -1px; background: var(--divider); }
.col { position: relative; width: 140px; flex: none; }
.col.month::before { content: ''; position: absolute; left: 0; top: 0; bottom: 0; border-left: 1px dashed var(--accent-line); }
.month-label { position: absolute; left: 6px; top: 0; }
.dot { position: absolute; left: 50%; top: 50%; width: 10px; height: 10px; margin: -5px 0 0 -5px; border-radius: 50%; background: var(--c); }
.date { position: absolute; left: 0; right: 0; text-align: center; white-space: nowrap; }
.up .date { top: calc(50% + 10px); }
.down .date { bottom: calc(50% + 10px); }
.node { position: absolute; left: 0; right: 0; display: flex; flex-direction: column; align-items: center; gap: 2px; }
.up .node { bottom: calc(50% + 6px); }
.down .node { top: calc(50% + 6px); }
/* 下方节点：连线在上，内容自上而下 */
.down .stem { order: -1; }
.stem { width: 2px; height: 22px; background: var(--c); opacity: 0.7; }
.avatar { width: 76px; height: 76px; border-radius: 16px; border: 3px solid var(--c); overflow: hidden; background: var(--surface-muted); display: grid; place-items: center; }
.avatar img { width: 100%; height: 100%; object-fit: cover; }
.avatar.clickable { padding: 0; cursor: pointer; transition: transform var(--motion-fast) var(--ease-standard), box-shadow var(--motion-fast) var(--ease-standard); }
.avatar.clickable:hover { transform: translateY(-3px); box-shadow: var(--shadow-hover); }
:global(.reduced-motion) .avatar.clickable:hover { transform: none; }
.avatar.pending { border-style: dashed; font-size: 30px; font-weight: 700; color: var(--accent-text); background: var(--accent-container); }
.name { font-size: 14px; margin-top: 4px; max-width: 132px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.pity-line { display: flex; align-items: baseline; gap: 4px; color: var(--c); }
.pity-line b { font-size: 22px; line-height: 1.1; }
.unit { font-size: 11px; color: var(--text-muted); }
.rating { font-size: 12px; font-weight: 700; }
.tags { display: flex; gap: 6px; min-height: 24px; margin-top: 2px; }
.kind { height: 22px; padding: 0 9px; border-radius: 999px; display: inline-flex; align-items: center; font-size: 12px; font-weight: 700; background: var(--accent-container); color: var(--accent-text); }
.kind.guaranteed { background: var(--nav-island); color: var(--text-on-nav-island); }
.kind.standard { background: var(--surface-sunken); color: var(--text-secondary); }
.kind.lost { background: var(--danger-container); color: var(--danger); }
.legend { display: flex; align-items: center; gap: 10px; padding-top: 12px; border-top: 1px solid var(--divider); }
.lg { display: inline-flex; align-items: center; gap: 6px; font-size: 13px; font-weight: 700; color: var(--c); }
.lg i { width: 10px; height: 10px; border-radius: 2px; background: var(--c); }
.hint { margin-left: 20px; }
</style>
