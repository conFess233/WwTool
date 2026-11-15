<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch } from 'vue';
import { useRouter } from 'vue-router';
import { t, language } from '../i18n';
import { currentUid, me, refreshMe } from '../account/session';
import { profile, fromTimestamp, loadProfile } from '../game/profile';
import { refreshProfile } from '../game/profileSync';
import { hasOauth } from '../game/oauthStore';
import { get } from '../http';
import { num, date, iconUrl, iconFallback } from '../format';
import { toast, toastError } from '../components/toast';
import { useFab } from '../shell/fab';
import { readPref, writePref } from '../prefs';
import ProfileGate from '../components/ProfileGate.vue';
import SegmentedBar from '../components/SegmentedBar.vue';
import StrokeIcon from '../components/StrokeIcon.vue';

// 首页（HomeView.axaml）：左栏上「账号总览」、下「便捷操作」；右栏 430 宽「游戏公告」。
// 启动游戏、打开目录等桌面专属操作换成网页里能做的跳转。
const MINUTES_PER_WAVEPLATE = 6;
const WEEKLY_MAX = 3;
const WEEKLY_COST = 60;
const router = useRouter();
const now = ref(Date.now());
let clock: number | undefined;
onMounted(() => (clock = window.setInterval(() => (now.value = Date.now()), 1000)));
onBeforeUnmount(() => clearInterval(clock));

// ---------- 账号卡 ----------
const canRefresh = ref(false);
watch(() => [me.value?.id, currentUid.value?.uid], async () => {
  canRefresh.value = !!(me.value && currentUid.value && (await hasOauth(me.value.id, currentUid.value.uid)));
}, { immediate: true });
const syncing = ref(false);
async function sync() {
  if (!currentUid.value || syncing.value) return;
  if (!canRefresh.value) return toast(t('Web_NoOauth'), 'info');
  syncing.value = true;
  try {
    await refreshProfile(currentUid.value.uid);
    await loadProfile();
    toast(t('Web_ProfileSynced'), 'success');
  } catch (e) {
    toastError(e);
  } finally {
    syncing.value = false;
  }
}

const copied = ref(false);
async function copyUid() {
  if (!currentUid.value) return;
  try {
    await navigator.clipboard.writeText(currentUid.value.uid);
    copied.value = true;
    setTimeout(() => (copied.value = false), 2000);
  } catch {
    toast(t('Web_CopyFailed'), 'error');
  }
}

const pad = (n: number) => String(n).padStart(2, '0');
function formatDay(d: Date) {
  const time = `${pad(d.getHours())}:${pad(d.getMinutes())}`;
  const today = new Date(); today.setHours(0, 0, 0, 0);
  const day = new Date(d); day.setHours(0, 0, 0, 0);
  const diff = (day.getTime() - today.getTime()) / 86_400_000;
  if (diff === 0) return t('Home_Today', time);
  if (diff === 1) return t('Home_Tomorrow', time);
  return `${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${time}`;
}

interface Card { key: string; title: string; icon: string; value: string; suffix?: string; current: number; max: number; full?: boolean; rows: [string, string][]; cells?: boolean[] }

// 五张资源卡，规则同 HomeViewModel
const cards = computed<Card[]>(() => {
  const b = profile.value?.snapshot?.base;
  const bp = profile.value?.snapshot?.battlePass;
  const none = t('Common_Unavailable');
  if (!b) return [];
  // 结晶波片：按满值时间倒推当前值（HomeViewModel.UpdateEnergy）
  let energy = b.energy;
  let left: number | null = null;
  let fullAt: Date | null = null;
  if (b.energyRecoverTime > 0 && b.maxEnergy > 0 && b.energy < b.maxEnergy) {
    fullAt = fromTimestamp(b.energyRecoverTime);
    left = fullAt.getTime() - now.value;
    if (left <= 0) { energy = b.maxEnergy; left = null; }
    else energy = Math.min(b.maxEnergy, Math.max(b.energy, b.maxEnergy - Math.ceil(left / 60000 / MINUTES_PER_WAVEPLATE)));
  }
  const full = b.maxEnergy > 0 && energy >= b.maxEnergy;
  const countdown = full ? t('Home_AlreadyFull') : left !== null
    ? `${pad(Math.floor(left / 3_600_000))}:${pad(Math.floor(left / 60000) % 60)}:${pad(Math.floor(left / 1000) % 60)}` : t('Conv_PauseRecover');
  const storeGrowing = full && b.storeEnergy < b.maxStoreEnergy;

  const reset = new Date(); reset.setHours(4, 0, 0, 0);
  if (Date.now() >= reset.getTime()) reset.setDate(reset.getDate() + 1);
  const livenessLeft = Math.max(0, b.livenessMaxCount - b.liveness);
  const used = Math.min(WEEKLY_MAX, Math.max(0, b.weeklyInstCount));

  return [
    { key: 'energy', title: t('Index_CrystalSheet'), icon: 'Energy', value: `${energy}`, suffix: `/ ${b.maxEnergy}`, current: energy, max: b.maxEnergy, full,
      rows: [[t('Home_KvRecoverIn'), countdown], [t('Home_KvFullAt'), full || !fullAt ? '—' : formatDay(fullAt)], [t('Home_KvRate'), t('Home_RateValue', MINUTES_PER_WAVEPLATE)]] },
    { key: 'store', title: t('Index_CrystalElement'), icon: 'StorgeEnergy', value: `${b.storeEnergy}`, suffix: `/ ${b.maxStoreEnergy}`, current: b.storeEnergy, max: b.maxStoreEnergy,
      rows: [[t('Home_KvStatus'), t(storeGrowing ? 'Home_Recovering' : 'Conv_PauseRecover')], [t('Home_KvSource'), t('Home_SourceOverflow')],
        [t('Home_KvRefillable'), String(Math.min(b.storeEnergy, Math.max(0, b.maxEnergy - energy)))]] },
    b.livenessUnlock
      ? { key: 'liveness', title: t('Home_ResLiveness'), icon: 'Liveness', value: `${b.liveness}`, suffix: `/ ${b.livenessMaxCount}`, current: b.liveness, max: b.livenessMaxCount,
        rows: [[t('Home_KvLeftToday'), t('Home_PointsValue', livenessLeft)], [t('Home_KvStatus'), t(livenessLeft === 0 ? 'Home_Done' : 'Home_InProgress')], [t('Home_KvRefresh'), formatDay(reset)]] }
      : { key: 'liveness', title: t('Home_ResLiveness'), icon: 'Liveness', value: t('Index_Locked'), current: 0, max: 1, rows: [[t('Home_KvLeftToday'), none], [t('Home_KvStatus'), none], [t('Home_KvRefresh'), none]] },
    bp?.isUnlock
      ? { key: 'radio', title: t('Index_Radio'), icon: 'BattlePass', value: `LV.${bp.level}`, current: bp.weekExp, max: Math.max(1, bp.weekMaxExp),
        rows: [[t('Home_KvWeekExp'), `${bp.weekExp} / ${bp.weekMaxExp}`], [t('Home_KvLevelExp'), `${bp.exp} / ${bp.expLimit}`], [t('Home_KvToNext'), String(Math.max(0, bp.expLimit - bp.exp))]] }
      : { key: 'radio', title: t('Index_Radio'), icon: 'BattlePass', value: bp ? t('Index_Locked') : none, current: 0, max: 1, rows: [[t('Home_KvWeekExp'), none], [t('Home_KvLevelExp'), none], [t('Home_KvToNext'), none]] },
    { key: 'weekly', title: t('Home_ResWeekly'), icon: 'WeeklyInst', value: `${used}`, suffix: `/ ${WEEKLY_MAX}`, current: used, max: WEEKLY_MAX, cells: Array.from({ length: WEEKLY_MAX }, (_, i) => i < used),
      rows: [[t('Home_KvWeeklyLeft'), t('Home_TimesValue', WEEKLY_MAX - used)], [t('Home_KvCost'), t('Home_CostValue', WEEKLY_COST)], [t('Home_KvReset'), t('Home_ResetValue')]] },
  ];
});

// ---------- 便捷操作 ----------
interface Quick { key: string; icon: string; title: string; primary?: boolean; run: () => void }
const GUIDE_SITE = 'https://wiki.kurobbs.com/mc/home';
const quick = computed<Quick[]>(() => [
  { key: 'pulls', icon: 'Chart', title: t('Fab_GetRecords'), primary: true, run: () => router.push('/pulls') },
  { key: 'roles', icon: 'User', title: t('Nav_RoleData'), run: () => router.push('/roles') },
  { key: 'events', icon: 'Timeline', title: t('Home_AnnOpenEvents'), run: () => router.push('/events') },
  { key: 'account', icon: 'Database', title: t('Nav_AccountData'), run: () => router.push('/account') },
  { key: 'explore', icon: 'Globe', title: t('Nav_Exploration'), run: () => router.push('/explore') },
  { key: 'motor', icon: 'Play', title: t('Nav_Motor'), run: () => router.push('/motor') },
  { key: 'sync', icon: 'Sync', title: t('Fab_RefreshData'), run: sync },
  { key: 'guide', icon: 'LinkAlt', title: t('Home_GuideSite'), run: () => window.open(GUIDE_SITE, '_blank', 'noopener') },
]);

// ---------- 游戏公告 ----------
type Region = 'china' | 'global';
type Category = 'Activity' | 'Notice' | 'News';
interface NewsItem { category: Category; title: string; link?: string; date?: string; dateText: string }
interface News { region: Region; items: NewsItem[]; slides: { image: string; link?: string }[] }

function defaultRegion(): Region {
  const saved = readPref('home.annRegion');
  if (saved === 'china' || saved === 'global') return saved;
  if (currentUid.value) return currentUid.value.region;
  return language.value === 'zh-Hans' ? 'china' : 'global';
}
const region = ref<Region>(defaultRegion());
const filter = ref<'all' | Category>('all');
const news = ref<News | null>(null);
const newsState = ref<'loading' | 'ready' | 'failed'>('loading');
let newsAbort: AbortController | undefined;

async function loadNews() {
  newsAbort?.abort();
  const ctrl = (newsAbort = new AbortController());
  newsState.value = 'loading';
  try {
    news.value = await get<News>(`/api/public/launcher-news?region=${region.value}&lang=${language.value}`, ctrl.signal);
    newsState.value = 'ready';
  } catch {
    if (!ctrl.signal.aborted) newsState.value = 'failed';
  }
}
function setRegion(r: Region) {
  if (region.value === r) return;
  region.value = r;
  writePref('home.annRegion', r);
}
watch([region, language], loadNews, { immediate: true });

const filters = computed(() => [
  { key: 'all' as const, title: t('Home_AnnAll') },
  { key: 'Activity' as const, title: t('Home_AnnEvent') },
  { key: 'Notice' as const, title: t('Home_AnnNotice') },
  { key: 'News' as const, title: t('Home_AnnNews') },
]);
const rows = computed(() => (news.value?.items ?? []).filter(i => filter.value === 'all' || i.category === filter.value));
const kindText = (c: Category) => t(c === 'Activity' ? 'Home_AnnEvent' : c === 'Notice' ? 'Home_AnnNotice' : 'Home_AnnNews');
const OFFICIAL = { china: 'https://mc.kurogames.com/main#news', global: 'https://wutheringwaves.kurogames.com/en/main/news/' };

// 轮播：5 秒切换，悬停暂停
const slide = ref(0);
const slides = computed(() => news.value?.slides ?? []);
watch(slides, () => (slide.value = 0));
let carousel: number | undefined;
const paused = ref(false);
onMounted(() => (carousel = window.setInterval(() => {
  if (!paused.value && !document.hidden && slides.value.length > 1) slide.value = (slide.value + 1) % slides.value.length;
}, 5000)));
onBeforeUnmount(() => clearInterval(carousel));
const open = (link?: string) => link && window.open(link, '_blank', 'noopener');

useFab(() => ({
  actions: [
    { key: 'refresh', title: t('Fab_RefreshData'), icon: 'Sync', run: sync },
    { key: 'accounts', title: t('Index_RefreshAccountList'), icon: 'User', run: () => refreshMe().then(loadProfile).catch(toastError) },
    { key: 'add', title: t('Dialog_AddAccount'), icon: 'Plus', run: () => router.push('/account') },
  ],
}));
</script>

<template>
  <div class="home stagger">
    <div class="left">
      <!-- 账号总览 -->
      <section class="card profile stagger-item" style="--i: 0">
        <div class="dots" aria-hidden="true" />
        <img class="watermark" src="/favicon.ico" alt="" aria-hidden="true" />
        <ProfileGate icon="Home">
          <template #default="{ profile: p, snapshot: s }">
            <div class="head">
              <div class="avatar">
                <img v-if="p.account?.headPhoto" :src="iconUrl(p.account.headPhoto)" alt="" @error="iconFallback" />
              </div>
              <div class="who">
                <div class="name-row">
                  <h2 class="name">{{ s.base?.name || p.account?.name }}</h2>
                  <span class="lv">LV.{{ s.base?.level ?? p.account?.level }}</span>
                </div>
                <div class="uid-row">
                  <span class="aux muted">{{ t('Home_Uid', currentUid?.uid ?? '') }}</span>
                  <button class="copy-uid" :class="{ copied }" :title="t('Home_CopyUid')" :aria-label="t('Home_CopyUid')" @click="copyUid">
                    <StrokeIcon :name="copied ? 'Check' : 'Copy'" :size="13" :stroke="1.8" />
                  </button>
                </div>
              </div>
              <button class="refresh-data" :class="{ spinning: syncing }" :disabled="syncing" :title="canRefresh ? t('Fab_RefreshData') : t('Web_NoOauth')"
                :aria-label="t('Fab_RefreshData')" @click="sync">
                <StrokeIcon name="Sync" :size="18" :stroke="1.8" />
              </button>
            </div>

            <div class="stats">
              <div class="stat"><span class="caption">{{ t('Index_ActiveDays') }}</span><strong>{{ num(s.base?.activeDays) }}</strong></div>
              <div class="stat"><span class="caption">{{ t('Index_CreateDate') }}</span><strong>{{ s.base?.creatTime ? date(fromTimestamp(s.base.creatTime).getTime()) : '--' }}</strong></div>
              <div class="stat"><span class="caption">{{ t('Index_SolaLevel') }}</span><strong>{{ s.base?.worldLevel ?? '--' }}</strong></div>
              <div class="stat"><span class="caption">{{ t('Index_Server') }}</span><strong>{{ p.account?.region || '--' }}</strong></div>
              <div class="stat"><span class="caption">{{ t('Index_OwnedRoles') }}</span><strong>{{ s.base ? t('Home_RoleCount', s.base.roleNum) : '--' }}</strong></div>
            </div>

            <div class="resources">
              <div v-for="c in cards" :key="c.key" class="res" :class="{ full: c.full }">
                <div class="hatch" aria-hidden="true" />
                <div class="res-head">
                  <img :src="`/icons/${c.icon}.png`" alt="" class="res-icon" />
                  <span class="label">{{ c.title }}</span>
                  <span v-if="c.full" class="full-tag">{{ t('Home_Full') }}</span>
                </div>
                <div class="res-value"><strong>{{ c.value }}</strong><span v-if="c.suffix" class="unit">{{ c.suffix }}</span></div>
                <div v-if="c.cells" class="cells"><span v-for="(on, j) in c.cells" :key="j" class="cell" :class="{ on }" /></div>
                <SegmentedBar v-else :value="c.current" :max="c.max || 1" thin />
                <div class="kvs">
                  <div v-for="[k, v] in c.rows" :key="k" class="kv-row"><span class="kv-key">{{ k }}</span><span class="kv-value">{{ v }}</span></div>
                </div>
              </div>
            </div>
          </template>
        </ProfileGate>
        <span class="corner-marks" />
      </section>

      <!-- 便捷操作 -->
      <section class="card quick-card stagger-item" style="--i: 1">
        <h3 class="card-title">{{ t('Home_QuickActions') }}</h3>
        <div class="quick-grid">
          <button v-for="q in quick" :key="q.key" class="quick" :class="{ primary: q.primary, busy: q.key === 'sync' && syncing }" @click="q.run">
            <StrokeIcon class="wm" :name="q.icon" :size="76" :stroke="1.6" />
            <span class="disc"><StrokeIcon :name="q.icon" :size="20" /></span>
            <span class="q-title">{{ q.title }}</span>
          </button>
        </div>
        <span class="corner-marks" />
      </section>
    </div>

    <!-- 游戏公告 -->
    <section class="card ann stagger-item" style="--i: 2">
      <div class="ann-head">
        <h3 class="card-title">{{ t('Home_Announcements') }}</h3>
        <div class="region" role="radiogroup">
          <span class="thumb" :class="{ right: region === 'global' }" />
          <button role="radio" :aria-checked="region === 'china'" @click="setRegion('china')">{{ t('Home_RegionCN') }}</button>
          <button role="radio" :aria-checked="region === 'global'" @click="setRegion('global')">{{ t('Home_RegionGlobal') }}</button>
        </div>
      </div>
      <div class="ann-chips">
        <button v-for="f in filters" :key="f.key" class="ann-chip" :aria-pressed="filter === f.key" @click="filter = f.key">{{ f.title }}</button>
      </div>

      <template v-if="newsState === 'ready' && (slides.length || rows.length)">
        <div v-if="slides.length" class="banner" @mouseenter="paused = true" @mouseleave="paused = false">
          <TransitionGroup name="fade">
            <img v-for="(s, i) in slides" v-show="i === slide" :key="s.image" :src="s.image" alt="" @click="open(s.link)" />
          </TransitionGroup>
          <div class="dots-nav">
            <button v-for="(s, i) in slides" :key="s.image" :class="{ on: i === slide }" :aria-label="`${i + 1}`" @click="slide = i" />
          </div>
        </div>
        <div class="ann-list">
          <button v-for="r in rows" :key="r.title + r.dateText" class="ann-row" @click="open(r.link)">
            <span class="ntag" :class="{ notice: r.category === 'Notice', version: r.category === 'News' }">{{ kindText(r.category) }}</span>
            <span class="aux ann-title">{{ r.title }}</span>
            <span class="caption">{{ r.dateText }}</span>
          </button>
        </div>
      </template>
      <div v-else class="ann-state">
        <img src="/icons/Loading.gif" width="56" height="56" alt="" />
        <template v-if="newsState === 'loading'"><span class="caption">{{ t('Home_AnnLoading') }}</span></template>
        <template v-else-if="newsState === 'failed'">
          <span class="card-title">{{ t('Home_AnnFailedTitle') }}</span>
          <span class="caption">{{ t('Home_AnnFailedHint') }}</span>
          <button class="btn" @click="loadNews">{{ t('Home_AnnRetry') }}</button>
        </template>
        <template v-else>
          <span class="card-title">{{ t('Home_AnnEmptyTitle') }}</span>
          <span class="caption">{{ t('Home_AnnEmptyHint') }}</span>
        </template>
      </div>

      <div class="ann-links">
        <button class="btn link" @click="router.push('/events')">{{ t('Home_AnnOpenEvents') }}</button>
        <a class="btn link" :href="OFFICIAL[region]" target="_blank" rel="noopener noreferrer">{{ t('Home_AnnViewAll') }}</a>
      </div>
      <span class="corner-marks" />
    </section>
  </div>
</template>

<style scoped>
/* 左栏自适应，右栏 430；底部留 76 给悬浮按钮 */
.home { display: grid; grid-template-columns: minmax(0, 1fr) 430px; gap: 22px; align-items: stretch; padding-bottom: 76px; }
.left { display: flex; flex-direction: column; gap: 22px; min-width: 0; }
.card { position: relative; overflow: hidden; }
.profile { padding: 20px 22px 22px; }

/* 右上点阵与 Logo 水印 */
.dots {
  position: absolute; right: -52px; top: -60px; width: 420px; height: 200px; opacity: 0.28; pointer-events: none;
  background: radial-gradient(circle, var(--accent) 1.3px, transparent 1.6px) 0 0 / 12px 12px;
  -webkit-mask: radial-gradient(ellipse 70% 70% at 80% 20%, #000 20%, transparent 70%);
  mask: radial-gradient(ellipse 70% 70% at 80% 20%, #000 20%, transparent 70%);
}
.watermark { position: absolute; right: 4px; top: 2px; width: 120px; height: 120px; opacity: 0.08; pointer-events: none; }

.head { position: relative; display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: 18px; padding-bottom: 20px; border-bottom: 1px solid var(--divider); }
.avatar { width: 64px; height: 64px; border-radius: 50%; overflow: hidden; background: var(--accent-container); }
.avatar img { width: 100%; height: 100%; object-fit: cover; }
.name-row { display: flex; align-items: baseline; gap: 8px; }
.name { font-size: 24px; font-weight: 700; margin: 0; }
.lv { font-size: 14px; font-weight: 700; color: var(--accent-text); }
.uid-row { display: flex; align-items: center; gap: 6px; margin-top: 4px; }
.copy-uid { width: 22px; height: 22px; border-radius: 5px; border: 0; padding: 0; display: grid; place-items: center; background: transparent; color: var(--text-muted); cursor: pointer; }
.copy-uid:hover { background: var(--surface-raised); color: var(--text-primary); }
.copy-uid.copied { color: var(--success); }
.refresh-data {
  width: 40px; height: 40px; border-radius: 20px; padding: 0; display: grid; place-items: center; cursor: pointer;
  background: var(--surface); border: 1px solid var(--border-strong); color: var(--text-secondary);
  transition: transform var(--motion-fast) var(--ease-standard), border-color var(--motion-fast) var(--ease-standard);
}
.refresh-data:hover { background: var(--surface-raised); border-color: var(--accent-line); color: var(--accent-text); }
.refresh-data:active { transform: scale(0.94); }
.refresh-data.spinning svg { animation: spin 0.9s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

/* 统计行：五列等宽，列间竖线 */
.stats { position: relative; display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); padding: 18px 0; border-bottom: 1px solid var(--divider); margin-bottom: 22px; }
.stat { display: flex; flex-direction: column; gap: 4px; padding: 0 4px 0 18px; border-left: 1px solid var(--border); min-width: 0; }
.stat:first-child { border-left: 0; padding-left: 0; }
.stat strong { font-size: 22px; font-weight: 700; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

/* 资源小卡 */
.resources { position: relative; display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 12px; }
.res {
  position: relative; overflow: hidden; display: flex; flex-direction: column; padding: 14px 14px 12px; border-radius: 10px;
  background: var(--surface-raised); border: 1px solid var(--divider);
}
.res.full { border: 2px solid var(--accent); padding: 13px 13px 11px; box-shadow: 0 0 0 3px var(--accent-container); }
.hatch {
  position: absolute; left: 0; right: 0; top: 0; height: 46px; opacity: 0.06; pointer-events: none;
  background: repeating-linear-gradient(135deg, var(--accent) 0 1px, transparent 1px 7px);
}
.res-head { position: relative; display: flex; align-items: center; gap: 8px; min-width: 0; }
.res-icon { width: 30px; height: 30px; object-fit: contain; flex: none; }
.label { font-size: 13px; color: var(--label); flex: 1; min-width: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.full-tag { flex: none; padding: 1px 7px; border-radius: 999px; background: var(--accent-container); color: var(--accent-text); font-size: 11px; font-weight: 700; animation: blink 1.2s ease-in-out 3; }
@keyframes blink { 50% { opacity: 0.35; } }
.res-value { display: flex; align-items: baseline; gap: 4px; margin-top: 16px; }
.res-value strong { font-size: 34px; font-weight: 700; letter-spacing: -0.5px; line-height: 1.2; white-space: nowrap; }
.unit { font-size: 13px; color: var(--text-muted); }
.res :deep(.seg), .cells { margin-top: 10px; }
.cells { display: grid; grid-template-columns: repeat(3, 1fr); gap: 6px; }
.cell { height: 8px; border-radius: 2px; background: var(--segment-track); transition: background-color 420ms var(--ease-standard); }
.cell.on { background: var(--accent); }
.kvs { display: flex; flex-direction: column; gap: 7px; margin-top: 18px; }
.kv-row { display: flex; justify-content: space-between; gap: 6px; font-size: 12px; }
.kv-key { color: var(--text-muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.kv-value { color: var(--text-secondary); font-weight: 600; white-space: nowrap; }

/* 便捷操作 */
.quick-card { padding: 18px 22px 20px; }
.quick-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 16px; margin-top: 16px; }
.quick {
  position: relative; overflow: hidden; height: 118px; padding: 14px; border-radius: 10px; cursor: pointer; text-align: left;
  display: flex; flex-direction: column; justify-content: space-between; align-items: flex-start;
  background: var(--surface-raised); border: 1px solid var(--divider); color: var(--text-primary);
  transition: transform 150ms var(--ease-standard), box-shadow 150ms var(--ease-standard), border-color 150ms var(--ease-standard), background-color 240ms var(--ease-standard);
}
.quick:hover { transform: translateY(-4px); box-shadow: var(--shadow-hover); border-color: var(--accent-line); }
.quick:active { transform: translateY(-2px) scale(0.96); }
:global(.reduced-motion) .quick:hover { transform: none; }
.disc { width: 38px; height: 38px; border-radius: 10px; display: grid; place-items: center; background: var(--accent-container); color: var(--accent-text); }
.q-title { font-size: 14px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 100%; }
.wm { position: absolute; right: -22px; bottom: -24px; color: var(--accent); opacity: 0.09; pointer-events: none; }
.quick.primary { background: var(--accent); border-color: var(--accent-pressed); color: var(--text-on-accent); }
.quick.primary:hover { border-color: var(--accent); }
.quick.primary .disc { background: rgb(255 255 255 / 0.2); color: var(--text-on-accent); }
.quick.primary .wm { color: #fff; opacity: 0.16; }
.quick.busy .disc svg { animation: spin 0.9s linear infinite; }

/* 游戏公告 */
.ann { display: flex; flex-direction: column; gap: 12px; padding: 18px 20px 16px; min-height: 640px; align-self: stretch; }
.ann-head { display: flex; align-items: center; justify-content: space-between; }
.region { position: relative; display: grid; grid-template-columns: 1fr 1fr; padding: 3px; border-radius: 999px; background: var(--surface-sunken); }
.region button { position: relative; z-index: 1; height: 30px; padding: 0 20px; border: 0; background: transparent; cursor: pointer; font-size: 13px; color: var(--text-secondary); }
.region button[aria-checked='true'] { color: var(--text-primary); font-weight: 700; }
.thumb { position: absolute; top: 3px; bottom: 3px; left: 3px; width: calc(50% - 3px); border-radius: 999px; background: var(--surface); box-shadow: var(--shadow-pill); transition: transform var(--motion-standard) var(--ease-standard); }
.thumb.right { transform: translateX(100%); }
.ann-chips { display: flex; gap: 6px; }
.ann-chip { height: 32px; padding: 0 14px; border-radius: 999px; border: 0; cursor: pointer; font-size: 13px; background: var(--accent-container); color: var(--accent-text); }
.ann-chip[aria-pressed='true'] { background: var(--nav-island); color: var(--text-on-nav-island); font-weight: 700; }
.banner { position: relative; aspect-ratio: 486 / 273; border-radius: 10px; overflow: hidden; background: var(--surface-sunken); }
.banner img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; cursor: pointer; }
.dots-nav { position: absolute; left: 22px; bottom: 16px; display: flex; gap: 6px; }
.dots-nav button { width: 8px; height: 6px; border-radius: 3px; border: 0; padding: 0; cursor: pointer; background: rgb(255 255 255 / 0.5); transition: width var(--motion-standard) var(--ease-standard); }
.dots-nav button.on { width: 32px; background: var(--gold-light); }
.fade-enter-active, .fade-leave-active { transition: opacity 600ms var(--ease-standard); }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.ann-list { flex: 1; display: flex; flex-direction: column; min-height: 0; }
.ann-row { display: grid; grid-template-columns: auto 1fr auto; gap: 10px; align-items: center; padding: 11px 0; border: 0; border-top: 1px solid var(--divider); background: transparent; cursor: pointer; text-align: left; }
.ann-row:first-child { border-top: 0; }
.ann-row:hover .ann-title { color: var(--accent-text); }
.ann-title { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; min-width: 0; }
.ntag.notice { background: var(--surface-sunken); color: var(--text-secondary); }
.ntag.version { background: var(--info-container); color: var(--info); }
.ann-state { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 8px; text-align: center; }
.ann-links { display: flex; justify-content: center; gap: 20px; }
.ann-links .btn { font-size: 15px; font-weight: 600; text-decoration: none; }

/* 手机：单栏，公告排在账号卡片之后；统计与资源改为两列 */
@media (max-width: 768px) {
  .home { grid-template-columns: minmax(0, 1fr); gap: 12px; padding-bottom: 12px; }
  .left { gap: 12px; }
  .profile, .quick-card { padding: 14px 16px 16px; }
  .head { gap: 12px; padding-bottom: 14px; }
  .avatar { width: 52px; height: 52px; }
  .name { font-size: 20px; }
  .stats { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px 0; padding: 14px 0; margin-bottom: 14px; }
  .stat, .stat:first-child { padding: 0 4px 0 12px; border-left: 0; }
  .stat:nth-child(odd) { padding-left: 0; }
  .stat:nth-child(even) { border-left: 1px solid var(--border); }
  .stat strong { font-size: 18px; }
  .resources { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
  .res { padding: 12px 12px 10px; }
  .res.full { padding: 11px 11px 9px; }
  .res-value { margin-top: 10px; }
  .res-value strong { font-size: 26px; }
  .kvs { margin-top: 12px; }
  .quick-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; margin-top: 12px; }
  .quick { height: 96px; }
  .ann { min-height: 0; padding: 14px 16px; }
  .ann-head { flex-wrap: wrap; gap: 8px; }
  .ann-chips { flex-wrap: wrap; }
  .region button { padding: 0 14px; }
  .dots-nav { left: 14px; bottom: 10px; }
}
</style>
