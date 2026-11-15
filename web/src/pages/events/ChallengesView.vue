<script setup lang="ts">
import { ref, computed, watch, onBeforeUnmount } from 'vue';
import { elementColor, rich } from '../../game/encore';
import { feeds, type ChallengeKind, type Seasons, type Season } from '../../api';
import { t, language } from '../../i18n';
import { readPref, writePref } from '../../prefs';
import { stamp, duration } from './eventsText';

// 深塔 · 海墟 · 矩阵（src/UI/Pages/Events/ChallengesViewModel.cs）：数据来自 encore.moe，经后端共用缓存。
const props = defineProps<{ now: number }>();

const kinds: { key: ChallengeKind; title: string }[] = [
  { key: 'tower', title: 'Challenge_Tower' },
  { key: 'wastes', title: 'Challenge_Wastes' },
  { key: 'matrix', title: 'Challenge_Matrix' },
];
const saved = readPref('events.challenge');
const kind = ref<ChallengeKind>(saved === 'wastes' || saved === 'matrix' ? saved : 'tower');
watch(kind, v => writePref('events.challenge', v));

const seasons = ref<Seasons>();
const chosen = new Map<ChallengeKind, number>();
const seasonId = ref<number>();
const detail = ref<any>();
const fetchedAt = ref<string>();
const state = ref<'loading' | 'ready' | 'failed'>('loading');
let abort: AbortController | undefined;

async function loadSeasons() {
  abort?.abort();
  const ctl = (abort = new AbortController());
  state.value = 'loading';
  detail.value = undefined;
  try {
    const s = await feeds.seasons(kind.value, language.value, ctl.signal);
    seasons.value = s;
    const keep = chosen.get(kind.value);
    seasonId.value = s.seasons.some(x => x.id === keep) ? keep : s.currentId;
    await loadDetail(ctl);
  } catch (e) {
    if ((e as Error).name !== 'AbortError') state.value = 'failed';
  }
}

async function loadDetail(ctl = new AbortController()) {
  if (seasonId.value === undefined) {
    state.value = 'ready';
    return;
  }
  state.value = 'loading';
  const d = await feeds.detail(kind.value, seasonId.value, language.value, ctl.signal);
  if (ctl.signal.aborted) return;
  detail.value = d.detail;
  fetchedAt.value = d.fetchedAt;
  state.value = 'ready';
}

watch([kind, language], loadSeasons, { immediate: true });
onBeforeUnmount(() => abort?.abort());

const list = computed(() => seasons.value?.seasons ?? []);
const index = computed(() => list.value.findIndex(s => s.id === seasonId.value));
const season = computed<Season | undefined>(() => list.value[index.value]);

async function step(delta: number) {
  const next = list.value[index.value + delta];
  if (!next) return;
  chosen.set(kind.value, next.id);
  seasonId.value = next.id;
  abort?.abort();
  const ctl = (abort = new AbortController());
  try {
    await loadDetail(ctl);
  } catch (e) {
    if ((e as Error).name !== 'AbortError') state.value = 'failed';
  }
}

const seasonText = computed(() => {
  const s = season.value;
  if (!s) return { title: '', info: '', status: '', active: false };
  if (kind.value === 'matrix') {
    return {
      title: s.name,
      info: s.seasonName && s.endVersion ? t('Challenge_MatrixInfo', s.seasonName, s.endVersion) : s.seasonName ?? '',
      status: t('Challenge_MatrixNoDate'),
      active: false,
    };
  }
  const n = props.now;
  const start = s.start ? Date.parse(s.start) : undefined;
  const end = s.end ? Date.parse(s.end) : undefined;
  const upcoming = start !== undefined && start > n;
  const active = start !== undefined && end !== undefined && start <= n && n < end;
  return {
    title: t('Challenge_Season', s.id),
    info: s.start && s.end ? `${stamp(s.start)} – ${stamp(s.end)}` : '',
    status: active ? t('Events_Remaining', duration(end! - n)) : upcoming ? t('Challenge_Upcoming', duration(start! - n)) : t('Events_Ended'),
    active,
  };
});

// ── 内容整理（与桌面端一致） ──
const elementName = (id: number) => (id >= 0 && id <= 6 ? t(`Challenge_Element_${id}`) : '');
const num = (v: number) => v.toLocaleString();

function monster(m: any) {
  const element = m.elements?.[0];
  const eid: number = element?.id ?? -1;
  const ename: string = element?.name || elementName(eid);
  const head: string[] = [];
  if (m.level != null) head.push(t('Challenge_Level', m.level));
  if (m.life != null) head.push(t('Challenge_Life', num(m.life)));
  if (ename) head.push(ename);
  const lines = [m.name, ...(head.length ? [head.join(' · ')] : []), ...(m.resistances ?? []).map((r: any) => `${r.name} ${+r.percent.toFixed(1)}%`)];
  // 某一属性抗性明显高于其余时，在头像下标出来
  let highlight: string | undefined;
  const res: any[] = m.resistances ?? [];
  if (res.length >= 3) {
    const counts = new Map<number, number>();
    for (const r of res) counts.set(r.percent, (counts.get(r.percent) ?? 0) + 1);
    const common = [...counts.entries()].sort((a, b) => b[1] - a[1])[0][0];
    const high = res.filter(r => r.percent > common);
    if (high.length > 0 && high.length <= 2) highlight = high.map(r => `${elementName(r.elementId)} ${+r.percent.toFixed(1)}%`).join(' ');
  }
  return { icon: m.icon as string | undefined, name: m.name as string, color: elementColor(eid), tip: lines.join('\n'), highlight };
}

const tower = computed(() => {
  if (kind.value !== 'tower' || !detail.value?.areas) return [];
  return (detail.value.areas as any[]).map(area => {
    const floors: any[] = area.floors ?? [];
    // 各层都有的增益只在区域上显示一次
    const shared: string[] = floors.length > 1 ? floors.slice(1).reduce((acc: string[], f: any) => acc.filter(b => f.buffs.includes(b)), [...floors[0].buffs]) : [];
    return {
      name: area.name as string,
      shared,
      floors: floors.map(f => ({
        title: t('Challenge_Floor', f.floor),
        cost: t('Challenge_Cost', f.cost),
        monsters: (f.monsters ?? []).map(monster),
        buffs: (f.buffs as string[]).filter(b => !shared.includes(b)),
        targets: (f.targets ?? []).map((x: any, i: number) => ({ stars: '★'.repeat(i + 1), text: x.text })),
      })),
    };
  });
});

const wastes = computed(() => {
  if (kind.value !== 'wastes' || !detail.value?.groups) return undefined;
  return {
    groups: (detail.value.groups as any[]).map(g => ({
      name: g.name as string,
      levels: (g.levels as any[]).map(l => ({
        title: l.title as string,
        endless: !!l.endless,
        description: l.description as string,
        grades: (l.grades as any[]).map(x => `${x.grade} ${num(x.score)}`).join(' · '),
        stages: (l.stages as any[]).map((s, i) => ({ title: t('Challenge_Stage', i + 1), description: s.description as string, monsters: (s.monsters ?? []).map(monster) })),
      })),
    })),
    tokens: (detail.value.tokens ?? []) as { name: string; icon?: string; quality: number }[],
  };
});

const matrix = computed(() => {
  if (kind.value !== 'matrix' || !detail.value?.modes) return undefined;
  const sep = t('Events_ListSeparator');
  const enemy = (e: any) => ({
    icon: e.icon as string | undefined,
    name: e.name as string,
    color: elementColor(e.elementId),
    element: elementName(e.elementId),
    tags: (e.tags ?? []) as string[],
    skills: (e.skills as any[]).map(s => (s.title ? `<b>${s.title}</b>：${s.description}` : s.description)).join('<br>'),
    kill: e.killScore > 0 ? t('Challenge_KillScore', e.killScore) : undefined,
    feature: e.features?.length ? t('Challenge_Feature', e.features.join(sep)) : undefined,
  });
  return {
    modes: (detail.value.modes as any[]).map(m => {
      const rounds: any[] = m.rounds ?? [];
      const names = (r: any) => (r.enemies as any[]).map(e => e.name).join('|');
      const same = rounds.length > 1 && rounds.every(r => names(r) === names(rounds[0]));
      // 多轮的敌人相同、只是等级不同时合成一组
      const groups = rounds.length === 1 || same
        ? rounds.length ? [{ title: t('Challenge_Levels', rounds.map(r => r.level).join(' / ')), enemies: rounds[0].enemies.map(enemy) }] : []
        : rounds.map(r => ({ title: t('Challenge_Round', r.round, r.level), enemies: r.enemies.map(enemy) }));
      return {
        name: m.name as string,
        team: m.teamLimit > 0 ? t('Challenge_TeamLimit', m.teamLimit) : t('Challenge_TeamUnlimited'),
        grades: m.thresholds?.length ? t('Challenge_Grades', (m.thresholds as number[]).map(num).join(' / ')) : '',
        buffs: (m.buffs ?? []) as { name: string; description: string; icon?: string }[],
        groups,
      };
    }),
    roles: (detail.value.roles ?? []) as { name: string; icon?: string; quality: number; description: string }[],
  };
});

const hasContent = computed(() => tower.value.length > 0 || !!wastes.value?.groups.length || !!matrix.value?.modes.length);
const hideImage = (e: Event) => ((e.target as HTMLImageElement).style.visibility = 'hidden');
</script>

<template>
  <div class="challenges">
    <section class="card head">
      <div class="kinds" role="radiogroup">
        <button v-for="k in kinds" :key="k.key" class="chip small" role="radio" :aria-checked="kind === k.key" @click="kind = k.key">{{ t(k.title) }}</button>
      </div>
      <div v-if="season" class="season">
        <button class="btn small" :disabled="index <= 0" :aria-label="t('Challenge_Prev')" @click="step(-1)">‹</button>
        <div class="season-text">
          <strong>{{ seasonText.title }}</strong>
          <span class="caption">{{ seasonText.info }}</span>
        </div>
        <button class="btn small" :disabled="index < 0 || index >= list.length - 1" :aria-label="t('Challenge_Next')" @click="step(1)">›</button>
      </div>
      <span v-if="season" class="status" :class="{ active: seasonText.active }">{{ seasonText.status }}</span>
      <a v-if="seasons" class="btn small" :href="seasons.pageUrl" target="_blank" rel="noopener noreferrer">{{ t('Challenge_OpenSite') }}</a>
    </section>
    <p class="caption source">{{ t('Challenge_Source') }}<template v-if="fetchedAt && state === 'ready'"> · {{ t('Events_Updated', stamp(fetchedAt)) }}</template></p>

    <section v-if="state === 'loading' && !hasContent" class="card"><span class="caption">{{ t('Challenge_Loading') }}</span></section>
    <section v-else-if="state === 'failed'" class="card failed">
      <h3 class="card-title">{{ t('Challenge_Failed') }}</h3>
      <button class="btn" @click="loadSeasons">{{ t('Events_Retry') }}</button>
    </section>
    <section v-else-if="!hasContent" class="card"><span class="caption">{{ t('Challenge_Empty') }}</span></section>

    <!-- 逆境深塔 -->
    <div v-else-if="kind === 'tower'" :key="`t-${seasonId}`" class="stack stagger">
      <section v-for="(a, i) in tower" :key="a.name" class="card stagger-item" :style="{ '--i': i }">
        <h3 class="card-title">{{ a.name }}</h3>
        <ul v-if="a.shared.length" class="buffs shared"><li v-for="b in a.shared" :key="b" v-html="rich(b)" /></ul>
        <div class="floors">
          <div v-for="f in a.floors" :key="f.title" class="floor">
            <div class="floor-head"><strong>{{ f.title }}</strong><span class="ntag muted-tag">{{ f.cost }}</span></div>
            <div class="monsters">
              <div v-for="(m, j) in f.monsters" :key="j" class="monster" :title="m.tip">
                <span class="avatar" :style="{ '--el': m.color }"><img v-if="m.icon" :src="m.icon" alt="" loading="lazy" referrerpolicy="no-referrer" @error="hideImage" /></span>
                <span class="m-name">{{ m.name }}</span>
                <span v-if="m.highlight" class="m-high">{{ m.highlight }}</span>
              </div>
            </div>
            <ul v-if="f.buffs.length" class="buffs"><li v-for="b in f.buffs" :key="b" v-html="rich(b)" /></ul>
            <div v-if="f.targets.length" class="targets">
              <div v-for="x in f.targets" :key="x.stars"><span class="stars">{{ x.stars }}</span><span v-html="rich(x.text)" /></div>
            </div>
          </div>
        </div>
      </section>
    </div>

    <!-- 冥歌海墟 -->
    <div v-else-if="kind === 'wastes' && wastes" :key="`w-${seasonId}`" class="stack stagger">
      <section v-if="wastes.tokens.length" class="card stagger-item" style="--i: 0">
        <h3 class="card-title">{{ t('Challenge_Tokens') }}</h3>
        <div class="tokens">
          <div v-for="tk in wastes.tokens" :key="tk.name" class="token" :class="{ gold: tk.quality >= 5 }">
            <img v-if="tk.icon" :src="tk.icon" alt="" loading="lazy" referrerpolicy="no-referrer" @error="hideImage" />
            <span>{{ tk.name }}</span>
          </div>
        </div>
      </section>
      <section v-for="(g, i) in wastes.groups" :key="g.name" class="card stagger-item" :style="{ '--i': i + 1 }">
        <h3 class="card-title">{{ g.name }}</h3>
        <div v-for="l in g.levels" :key="l.title" class="level">
          <div class="floor-head">
            <strong>{{ l.title }}</strong>
            <span v-if="l.endless" class="ntag">{{ t('Challenge_Endless') }}</span>
            <span v-if="l.grades" class="caption">{{ l.grades }}</span>
          </div>
          <p v-if="l.description" class="desc" v-html="rich(l.description)" />
          <div v-for="s in l.stages" :key="s.title" class="stage">
            <span class="caption stage-title">{{ s.title }}</span>
            <p v-if="s.description" class="desc" v-html="rich(s.description)" />
            <div class="monsters">
              <div v-for="(m, j) in s.monsters" :key="j" class="monster" :title="m.tip">
                <span class="avatar" :style="{ '--el': m.color }"><img v-if="m.icon" :src="m.icon" alt="" loading="lazy" referrerpolicy="no-referrer" @error="hideImage" /></span>
                <span class="m-name">{{ m.name }}</span>
                <span v-if="m.highlight" class="m-high">{{ m.highlight }}</span>
              </div>
            </div>
          </div>
        </div>
      </section>
    </div>

    <!-- 终焉矩阵 -->
    <div v-else-if="kind === 'matrix' && matrix" :key="`m-${seasonId}`" class="stack stagger">
      <section v-for="(m, i) in matrix.modes" :key="m.name" class="card stagger-item" :style="{ '--i': i }">
        <div class="floor-head">
          <h3 class="card-title">{{ m.name }}</h3>
          <span class="ntag muted-tag">{{ m.team }}</span>
          <span v-if="m.grades" class="caption">{{ m.grades }}</span>
        </div>
        <template v-if="m.buffs.length">
          <span class="caption sub">{{ t('Challenge_Buffs') }}</span>
          <div class="matrix-buffs">
            <div v-for="b in m.buffs" :key="b.name" class="mbuff">
              <img v-if="b.icon" :src="b.icon" alt="" loading="lazy" referrerpolicy="no-referrer" @error="hideImage" />
              <div><strong>{{ b.name }}</strong><p class="desc" v-html="rich(b.description)" /></div>
            </div>
          </div>
        </template>
        <div v-for="g in m.groups" :key="g.title" class="round">
          <span class="caption sub">{{ g.title }}</span>
          <div class="enemies">
            <div v-for="(e, j) in g.enemies" :key="j" class="enemy">
              <span class="avatar" :style="{ '--el': e.color }"><img v-if="e.icon" :src="e.icon" alt="" loading="lazy" referrerpolicy="no-referrer" @error="hideImage" /></span>
              <div class="enemy-body">
                <div class="enemy-head">
                  <strong>{{ e.name }}</strong>
                  <span v-if="e.element" class="caption">{{ e.element }}</span>
                  <span v-for="tg in e.tags" :key="tg" class="ntag muted-tag">{{ tg }}</span>
                  <span v-if="e.kill" class="ntag">{{ e.kill }}</span>
                </div>
                <p v-if="e.skills" class="desc" v-html="rich(e.skills)" />
                <span v-if="e.feature" class="caption">{{ e.feature }}</span>
              </div>
            </div>
          </div>
        </div>
      </section>
      <section v-if="matrix.roles.length" class="card stagger-item" :style="{ '--i': matrix.modes.length }">
        <h3 class="card-title">{{ t('Challenge_Roles') }}</h3>
        <div class="matrix-buffs">
          <div v-for="r in matrix.roles" :key="r.name" class="mbuff" :class="{ gold: r.quality >= 5 }">
            <img v-if="r.icon" class="round-img" :src="r.icon" alt="" loading="lazy" referrerpolicy="no-referrer" @error="hideImage" />
            <div><strong>{{ r.name }}</strong><p class="desc" v-html="rich(r.description)" /></div>
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.challenges { display: flex; flex-direction: column; gap: 14px; padding-bottom: 70px; }
.head { display: flex; align-items: center; gap: 16px; flex-wrap: wrap; padding: 14px 20px; }
.kinds { display: flex; gap: 6px; }
.season { display: flex; align-items: center; gap: 10px; margin-left: auto; }
.season-text { display: flex; flex-direction: column; align-items: center; min-width: 150px; }
.status { font-size: 13px; color: var(--text-muted); }
.status.active { color: var(--accent-text); font-weight: 600; }
.source { margin: -4px 4px 0; }
.failed { display: flex; flex-direction: column; gap: 8px; align-items: flex-start; }
.stack { display: flex; flex-direction: column; gap: 14px; }
.card-title { margin: 0 0 10px; }
.floors { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 12px; }
.floor, .level { padding: 12px 14px; border-radius: var(--radius-inner); background: var(--surface-raised); display: flex; flex-direction: column; gap: 8px; }
.level + .level { margin-top: 10px; }
.floor-head { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.floor-head .card-title { margin: 0; }
.monsters { display: flex; flex-wrap: wrap; gap: 10px; }
.monster { width: 76px; display: flex; flex-direction: column; align-items: center; gap: 3px; text-align: center; cursor: default; }
.avatar { width: 56px; height: 56px; border-radius: 50%; background: var(--surface-sunken); border: 2px solid var(--el); overflow: hidden; flex: none; display: block; }
.avatar img { width: 100%; height: 100%; object-fit: cover; }
.m-name { font-size: 12px; line-height: 1.3; }
.m-high { font-size: 11px; color: var(--warning); font-weight: 600; }
.buffs { margin: 0; padding-left: 18px; display: flex; flex-direction: column; gap: 4px; font-size: 13px; color: var(--text-secondary); }
.buffs.shared { margin-bottom: 10px; }
.targets { display: flex; flex-direction: column; gap: 3px; font-size: 13px; }
.stars { color: var(--gold-light, var(--accent-text)); margin-right: 8px; letter-spacing: 1px; }
:deep(.hl) { color: var(--accent-text); font-weight: 600; }
.desc { margin: 0; font-size: 13px; color: var(--text-secondary); line-height: 1.55; }
.stage { display: flex; flex-direction: column; gap: 6px; padding-top: 6px; border-top: 1px solid var(--divider); }
.stage-title { font-weight: 600; }
.tokens { display: flex; flex-wrap: wrap; gap: 10px; }
.token { display: flex; align-items: center; gap: 8px; padding: 6px 12px 6px 6px; border-radius: 999px; background: var(--surface-raised); border: 1px solid transparent; font-size: 13px; }
.token img { width: 32px; height: 32px; object-fit: contain; }
.token.gold, .mbuff.gold { border-color: var(--accent-line); }
.sub { display: block; margin: 12px 0 8px; font-weight: 600; }
.matrix-buffs { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 10px; }
.mbuff { display: flex; gap: 10px; padding: 10px 12px; border-radius: var(--radius-inner); background: var(--surface-raised); border: 1px solid transparent; }
.mbuff img { width: 40px; height: 40px; object-fit: contain; flex: none; }
.mbuff .round-img { border-radius: 50%; object-fit: cover; }
.mbuff strong { font-size: 13px; }
.enemies { display: flex; flex-direction: column; gap: 10px; }
.enemy { display: flex; gap: 12px; padding: 10px 12px; border-radius: var(--radius-inner); background: var(--surface-raised); }
.enemy-body { display: flex; flex-direction: column; gap: 4px; min-width: 0; }
.enemy-head { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }

/* 手机：卡片单列，赛季切换单独占一行 */
@media (max-width: 768px) {
  .challenges { padding-bottom: 12px; gap: 12px; }
  .head { padding: 12px 14px; gap: 10px; }
  .kinds { flex-wrap: wrap; }
  .season { margin-left: 0; width: 100%; justify-content: space-between; }
  .season-text { min-width: 0; }
  .floors, .matrix-buffs { grid-template-columns: minmax(0, 1fr); }
  .monster { width: 64px; }
  .avatar { width: 48px; height: 48px; }
}
</style>
