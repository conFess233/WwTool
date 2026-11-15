<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { t, language } from '../i18n';
import { readPref, writePref } from '../prefs';
import { currentUid } from '../account/session';
import { loadRoles, syncRoles, hasGuide, type RolesData, type Role } from '../game/roles';
import { iconUrl, iconFallback, dateTime } from '../format';
import { toast, toastError } from '../components/toast';
import AccountGate from '../components/AccountGate.vue';
import EmptyState from '../components/EmptyState.vue';
import CharacterDetailDialog from '../components/CharacterDetailDialog.vue';
import GameLoginDialog from './account/GameLoginDialog.vue';
import { useRouter } from 'vue-router';
import { useFab } from '../shell/fab';

// 角色数据（RolesView.axaml）：已有角色 / 已装备武器两个页签，按星级分组的网格；点角色看「我的养成」。
// 数据来自官方攻略站，经本站后端转发同步（见 game/roles.ts）。
type Tab = 'roles' | 'weapons';
type Sort = 'star' | 'name' | 'time';
const tab = ref<Tab>(readPref('roles.tab') === 'weapons' ? 'weapons' : 'roles');
const sort = ref<Sort>((['star', 'name', 'time'] as Sort[]).find(s => s === readPref('roles.sort')) ?? 'star');
watch(tab, v => writePref('roles.tab', v));
watch(sort, v => writePref('roles.sort', v));

const data = ref<RolesData>();
const state = ref<'loading' | 'ready' | 'failed'>('loading');
const syncing = ref(false);
const canSync = ref(false);
const gameLogin = ref(false);
const selected = ref<Role | null>(null);
let abort: AbortController | undefined;

async function load() {
  const uid = currentUid.value?.uid;
  if (!uid) return;
  abort?.abort();
  abort = new AbortController();
  if (!data.value) state.value = 'loading';
  try {
    data.value = await loadRoles(uid, abort.signal);
    state.value = 'ready';
  } catch (e) {
    if ((e as Error).name !== 'AbortError') state.value = 'failed';
  }
  canSync.value = await hasGuide(uid);
}
watch([() => currentUid.value?.uid, language], load, { immediate: true });

async function sync() {
  const uid = currentUid.value?.uid;
  if (!uid) return;
  if (!canSync.value) {
    gameLogin.value = true;
    return;
  }
  syncing.value = true;
  try {
    await syncRoles(uid);
    toast(t('Guide_SyncSuccess'), 'success');
    await load();
  } catch (e) {
    toastError(e);
    canSync.value = await hasGuide(uid);
  } finally {
    syncing.value = false;
  }
}

interface Tile { key: string; id: string; name: string; star: number; limited: boolean; order: number; acquiredAt?: string; sub?: string; sequence?: number; role?: Role }
const tiles = computed<Tile[]>(() => {
  const d = data.value;
  if (!d) return [];
  return tab.value === 'roles'
    ? d.roles.map((r, i) => ({ key: r.id, id: r.id, name: r.name, star: r.star, limited: r.limited, order: i, acquiredAt: r.acquiredAt, sequence: r.sequence, role: r }))
    : d.weapons.map((w, i) => ({ key: `${w.id}-${w.ownerId}`, id: w.id, name: w.name, star: w.star, limited: w.limited, order: i, acquiredAt: w.acquiredAt, sub: t('Guide_Owner') + w.ownerName }));
});

// 与 Core 的 GuideCardSorter 相同：限定五星、常驻五星、四星、其余；组内按所选方式排序
const rank = (x: Tile) => (x.star === 5 && x.limited ? 0 : x.star === 5 ? 1 : x.star === 4 ? 2 : 3);
const collator = computed(() => new Intl.Collator(language.value));
const sorted = computed(() => [...tiles.value].sort((a, b) => {
  const r = rank(a) - rank(b);
  if (r) return r;
  if (sort.value === 'name') return collator.value.compare(a.name, b.name) || a.order - b.order;
  if (sort.value === 'time') {
    const ta = a.limited && a.acquiredAt ? Date.parse(a.acquiredAt) : null;
    const tb = b.limited && b.acquiredAt ? Date.parse(b.acquiredAt) : null;
    if ((ta === null) !== (tb === null)) return ta === null ? 1 : -1;
    if (ta !== null && tb !== null && ta !== tb) return tb - ta;
  }
  return a.order - b.order;
}));
const starKey: Record<number, string> = { 5: 'Roles_FiveStar', 4: 'Roles_FourStar', 3: 'Roles_ThreeStar', 2: 'Roles_TwoStar', 1: 'Roles_OneStar' };
const groups = computed(() => {
  const map = new Map<number, Tile[]>();
  for (const x of sorted.value) {
    const s = Math.min(5, Math.max(1, x.star));
    map.set(s, [...(map.get(s) ?? []), x]);
  }
  return [...map.entries()].sort((a, b) => b[0] - a[0]).map(([star, items]) => ({
    star, title: t(starKey[star]), count: t(tab.value === 'roles' ? 'Roles_RoleCountUnit' : 'Roles_WeaponCountUnit', items.length), items,
  }));
});

const sequenceText = (n: number) =>
  language.value === 'en' ? `S${n}` : language.value === 'ja' ? `${n}共鳴` : n === 0 ? '零链' : `${'一二三四五六'[Math.min(6, Math.max(1, n)) - 1]}链`;
const stars = (n: number) => '★'.repeat(Math.max(0, n));
const router = useRouter();
// 悬浮按钮：刷新数据、返回（RolesView 的 FAB）
useFab(() => ({
  actions: [
    { key: 'sync', title: t('Fab_RefreshData'), icon: 'Sync', run: sync },
    { key: 'back', title: t('Fab_Back'), icon: 'Back', run: () => router.back() },
  ],
}));
const syncInfo = computed(() => (data.value?.syncedAt ? t('Roles_SyncInfo', dateTime(data.value.syncedAt)) : t('Guide_NeverSynced')));
const acquiredText = (at?: string) => (at ? t('Guide_FirstAcquired', dateTime(at)) : t('Guide_AcquiredUnknown'));
</script>

<template>
  <div class="roles">
    <AccountGate icon="Role">
      <div class="toolbar">
        <div class="chips" role="tablist">
          <button class="chip" role="tab" :aria-pressed="tab === 'roles'" @click="tab = 'roles'">
            {{ t('Guide_RolesTab') }}<span class="count">{{ data?.roles.length ?? 0 }}</span>
          </button>
          <button class="chip" role="tab" :aria-pressed="tab === 'weapons'" @click="tab = 'weapons'">
            {{ t('Guide_WeaponsTab') }}<span class="count">{{ data?.weapons.length ?? 0 }}</span>
          </button>
        </div>
        <div class="chips sorts">
          <span class="caption">{{ t('Guide_Sort') }}</span>
          <button class="chip small" :aria-pressed="sort === 'star'" @click="sort = 'star'">{{ t('Guide_SortStar') }}</button>
          <button class="chip small" :aria-pressed="sort === 'name'" @click="sort = 'name'">{{ t('Guide_SortName') }}</button>
          <button class="chip small" :aria-pressed="sort === 'time'" :disabled="!data?.hasGachaRecords" :title="data?.hasGachaRecords ? '' : t('Guide_NoLocalGacha')"
            @click="sort = 'time'">{{ t('Guide_SortTime') }}</button>
        </div>
        <span class="spacer" />
        <span class="caption sync-info" :title="t('Web_RolesRelayHint')">{{ syncing ? t('Guide_Syncing') : syncInfo }}</span>
      </div>

      <div v-if="state === 'loading'" class="card"><span class="caption">{{ t('Web_Loading') }}</span></div>
      <EmptyState v-else-if="state === 'failed'" danger :title="t('Web_LoadFailed')" icon="Role">
        <button class="btn" @click="load">{{ t('Common_Retry') }}</button>
      </EmptyState>
      <EmptyState v-else-if="!groups.length" :title="t(tab === 'roles' ? 'Guide_NoRoles' : 'Guide_NoWeapons')" :hint="canSync ? '' : t('Web_RolesNeedLogin')" icon="Role">
        <button class="btn primary" :disabled="syncing" @click="sync">{{ canSync ? t('Guide_Sync') : t('Web_GameLogin') }}</button>
      </EmptyState>

      <div v-else :key="tab" class="groups stagger">
        <section v-for="(g, i) in groups" :key="g.star" class="group stagger-item" :style="{ '--i': i }">
          <div class="group-head">
            <strong>{{ g.title }}</strong>
            <span class="label">{{ g.count }}</span>
            <span class="line" />
          </div>
          <div class="grid">
            <component :is="x.role ? 'button' : 'div'" v-for="x in g.items" :key="x.key" class="tile" :class="{ clickable: !!x.role }"
              :title="x.limited ? acquiredText(x.acquiredAt) : undefined" @click="x.role && (selected = x.role)">
              <span class="avatar"><img :src="iconUrl(x.id)" alt="" loading="lazy" @error="iconFallback" /></span>
              <span class="tile-text">
                <span class="name-row">
                  <strong class="name">{{ x.name }}</strong>
                  <span v-if="x.sequence !== undefined" class="seq" :class="{ mid: x.sequence >= 3 && x.sequence <= 5, full: x.sequence >= 6 }">{{ sequenceText(x.sequence) }}</span>
                </span>
                <span class="stars" :class="{ five: x.star === 5 }">{{ stars(x.star) }}</span>
                <span v-if="x.sub" class="caption sub">{{ x.sub }}</span>
              </span>
            </component>
          </div>
        </section>
      </div>

      <!-- 角色详情：从角色页打开时停在「我的养成」 -->
      <CharacterDetailDialog v-if="selected" :role-id="selected.id" :name="selected.name" :star="selected.star" :sequence="selected.sequence"
        :sequence-text="sequenceText(selected.sequence)" :build="selected" :synced-at="data?.syncedAt" tab="build" @close="selected = null" />
      <GameLoginDialog v-if="gameLogin" @close="gameLogin = false" @done="load" />
    </AccountGate>
  </div>
</template>

<style scoped>
.roles { padding-bottom: 70px; }
.toolbar { display: flex; align-items: center; gap: 12px; height: 44px; margin-bottom: 14px; }
.toolbar .chip:not(.small) { height: 44px; padding: 0 20px; font-size: 15px; }
.toolbar .chip.small { height: 40px; padding: 0 18px; font-size: 15px; }
.chips { display: flex; align-items: center; gap: 6px; }
.chip .count { margin-left: 8px; color: var(--text-muted); font-weight: 400; }
.sorts { margin-left: 10px; }
.spacer { flex: 1; }
.sync-info { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

.groups { display: flex; flex-direction: column; gap: 22px; }
.group-head { display: grid; grid-template-columns: auto auto 1fr; align-items: center; gap: 10px; margin-bottom: 12px; }
.group-head strong { font-size: 15px; }
.label { font-size: 13px; color: var(--label); }
.line { height: 1px; background: var(--border); }
.grid { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 12px; }

.tile {
  display: flex; align-items: center; gap: 12px; height: 72px; padding: 0 12px; border-radius: 12px; overflow: hidden;
  background: var(--surface); border: 1px solid var(--border); color: inherit; font: inherit; text-align: left;
  transition: box-shadow var(--motion-fast) var(--ease-standard), border-color var(--motion-fast) var(--ease-standard), background-color var(--motion-fast) var(--ease-standard);
}
.tile.clickable { cursor: pointer; }
.tile.clickable:hover { box-shadow: var(--shadow-hover); }
.tile.clickable:focus-visible { outline: none; border-color: var(--focus-ring); }
.avatar { width: 52px; height: 52px; border-radius: 12px; overflow: hidden; background: var(--surface-muted); flex: none; }
.avatar img { width: 100%; height: 100%; object-fit: cover; }
.tile-text { display: flex; flex-direction: column; gap: 3px; min-width: 0; flex: 1; }
.name-row { display: flex; align-items: center; gap: 6px; min-width: 0; }
.name { font-size: 14px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.sub { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.stars { font-size: 11px; letter-spacing: 2px; color: var(--four-star); }
.stars.five { color: #c98a1b; }
.seq { flex: none; padding: 3px 8px; border-radius: 999px; background: var(--surface-muted); font-size: 11px; font-weight: 600; color: var(--label); }
.seq.mid { background: var(--accent-container); color: var(--accent-text); }
.seq.full { background: var(--accent); color: var(--text-on-accent); }


/* 手机：工具栏换行，角色两列 */
@media (max-width: 768px) {
  .roles { padding-bottom: 12px; }
  .toolbar { flex-wrap: wrap; height: auto; gap: 8px 10px; }
  .toolbar .chip:not(.small) { height: 38px; padding: 0 14px; font-size: 14px; }
  .toolbar .chip.small { height: 32px; padding: 0 12px; font-size: 13px; }
  .sorts { margin-left: 0; }
  .spacer { display: none; }
  .sync-info { width: 100%; }
  .groups { gap: 16px; }
  .grid { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; }
  .tile { height: 64px; gap: 8px; padding: 0 8px; }
  .avatar { width: 44px; height: 44px; border-radius: 10px; }
  .name { font-size: 13px; }
  .seq { padding: 2px 6px; }
}
</style>
