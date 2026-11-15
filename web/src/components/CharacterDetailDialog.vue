<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount, nextTick } from 'vue';
import { t } from '../i18n';
import { iconUrl, iconFallback, dateTime, poolTitleKey } from '../format';
import { loadCharacter, rich, elementColor, statAt, type EncoreCharacter, type EncoreSkill } from '../game/encore';
import { gachaSource } from '../gacha/source';
import { gachaApi } from '../pages/pulls/gachaApi';
import { allCards, ratingOf, ratingColor, kindKey, type FiveStarCard } from '../pages/pulls/fiveStar';
import type { Role } from '../game/roles';
import StrokeIcon from './StrokeIcon.vue';

// 角色详情弹窗（CharacterDetailView.axaml）：左侧立绘与名片，右侧页签——概览、技能、共鸣链、我的养成、故事与语音。
// 资料来自 encore.moe（后端缓存转发），「我的抽取」来自当前抽卡记录，「我的养成」来自攻略站快照。抽卡时间线与角色页共用。
type Tab = 'overview' | 'skills' | 'chain' | 'build' | 'lore';
const props = defineProps<{
  roleId: number | string;
  name: string;
  star: number;
  sequence?: number;
  sequenceText?: string;
  build?: Role;
  syncedAt?: string;
  tab?: Tab;
}>();
const emit = defineEmits<{ close: [] }>();

const data = ref<EncoreCharacter | null>(null);
const pageUrl = ref('');
const state = ref<'loading' | 'ready' | 'failed'>('loading');
const tab = ref<Tab>(props.tab === 'build' && !props.build ? 'overview' : props.tab ?? 'overview');
let abort: AbortController | undefined;

async function load() {
  abort?.abort();
  abort = new AbortController();
  state.value = 'loading';
  try {
    const r = await loadCharacter(props.roleId, abort.signal);
    data.value = r.character;
    pageUrl.value = r.pageUrl;
    skin.value = 0;
    state.value = 'ready';
  } catch (e) {
    if ((e as Error).name !== 'AbortError') state.value = 'failed';
  }
}

// 我的抽取：当前抽卡记录里该角色的五星（从早到晚）
const pulls = ref<FiveStarCard[]>([]);
async function loadPulls() {
  const src = gachaSource.value;
  if (!src) return;
  try {
    const o = await gachaApi.overview(src);
    pulls.value = allCards(o.pools).filter(c => String(c.resourceId) === String(props.roleId));
  } catch {
    pulls.value = [];
  }
}
const pullsSummary = computed(() => pulls.value.length
  ? t('CharDetail_PullsSummary', pulls.value.length, (Math.round((pulls.value.reduce((s, p) => s + p.pity, 0) / pulls.value.length) * 10) / 10).toString())
  : t('CharDetail_NoPulls'));

const tabs = computed(() => [
  { key: 'overview' as Tab, title: t('CharDetail_TabOverview') },
  { key: 'skills' as Tab, title: t('CharDetail_TabSkills') },
  { key: 'chain' as Tab, title: t('CharDetail_TabChain') },
  ...(props.build ? [{ key: 'build' as Tab, title: t('CharDetail_TabBuild') }] : []),
  { key: 'lore' as Tab, title: t('CharDetail_TabLore') },
]);

// 头部：皮肤立绘、元素与武器类型、定位标签
const skin = ref(0);
const skinOpen = ref(false);
const portrait = computed(() => data.value?.skins[skin.value]?.portraitUrl ?? data.value?.portraitUrl);
const elementName = computed(() => {
  const id = data.value?.elementId ?? -1;
  return id >= 0 && id <= 6 ? t(`Challenge_Element_${id}`) : data.value?.elementName;
});
const stars = computed(() => '★'.repeat(Math.min(5, Math.max(1, props.star))));

// 概览：基础属性（按等级）与档案
const statLevels = [1, 20, 40, 50, 60, 70, 80, 90];
const statLevel = ref(90);
const stats = computed(() => (data.value?.stats ?? []).map(s => ({ name: s.name, value: statAt(s, statLevel.value) })).filter(s => s.value));
const profile = computed(() => (data.value?.profile ?? []).map(f => ({ label: t(`CharDetail_Profile_${f.key}`), value: f.value })));

// 技能：左侧列表，右侧描述与倍率
const skillIndex = ref(0);
const skillLevel = ref(10);
const skill = computed<EncoreSkill | undefined>(() => data.value?.skills[skillIndex.value]);
// 技能等级与桌面端一致取 1–10
const maxSkillLevel = 10;
const multipliers = computed(() => (skill.value?.attributes ?? [])
  .map(a => ({ name: a.name, value: a.values.length ? a.values[Math.min(skillLevel.value, a.values.length) - 1] : undefined }))
  .filter(a => a.value));

// 共鸣链
const chainSummary = computed(() => (props.sequence !== undefined ? t('CharDetail_ChainUnlocked', Math.min(6, Math.max(0, props.sequence))) : t('CharDetail_ChainUnknown')));
const unlocked = (index: number) => (props.sequence === undefined ? undefined : index <= props.sequence);

// 我的养成：技能图标优先用 encore.moe 的同名技能（白色线稿，与技能页一致）
function buildSkillIcon(name?: string, type?: string) {
  const list = data.value?.skills ?? [];
  const match = (name && list.find(s => s.name === name)) || (type && list.filter(s => s.type === type).length === 1 ? list.find(s => s.type === type) : undefined);
  return match ? match.iconUrl : undefined;
}

// 故事与语音：点击展开
const open = ref(new Set<string>());
function toggle(key: string) {
  const s = new Set(open.value);
  if (s.has(key)) s.delete(key); else s.add(key);
  open.value = s;
}

const scroller = ref<HTMLElement | null>(null);
watch(tab, () => nextTick(() => scroller.value?.scrollTo({ top: 0 })));

function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') { if (skinOpen.value) skinOpen.value = false; else emit('close'); }
}
let previous: Element | null = null;
onMounted(() => {
  previous = document.activeElement;
  document.addEventListener('keydown', onKey);
  load();
  loadPulls();
});
onBeforeUnmount(() => {
  abort?.abort();
  document.removeEventListener('keydown', onKey);
  (previous as HTMLElement | null)?.focus?.();
});
</script>

<template>
  <Teleport to="body">
    <div class="scrim" @mousedown.self="emit('close')">
      <div class="dialog" role="dialog" aria-modal="true" :aria-label="data?.name ?? name">
        <!-- 左：立绘与名片 -->
        <aside class="side">
          <div class="portrait">
            <img v-if="portrait" :key="portrait" :src="portrait" alt="" referrerpolicy="no-referrer" />
            <img v-else class="fallback" :src="iconUrl(roleId)" alt="" @error="iconFallback" />
            <div v-if="(data?.skins.length ?? 0) > 1" class="skin">
              <button class="skin-btn" :aria-expanded="skinOpen" @click="skinOpen = !skinOpen">{{ data!.skins[skin]?.name }}<StrokeIcon name="ChevronDown" :size="12" /></button>
              <ul v-if="skinOpen" class="skin-menu">
                <li v-for="(s, i) in data!.skins" :key="s.id"><button :class="{ on: i === skin }" @click="skin = i; skinOpen = false">{{ s.name }}</button></li>
              </ul>
            </div>
          </div>
          <div class="card-info">
            <div class="name-row">
              <h2 class="name">{{ data?.name ?? name }}</h2>
              <span v-if="sequenceText" class="seq" :class="{ mid: (sequence ?? 0) >= 3 && (sequence ?? 0) <= 5, full: (sequence ?? 0) >= 6 }">{{ sequenceText }}</span>
            </div>
            <span class="stars" :class="{ five: star >= 5 }">{{ stars }}</span>
            <div v-if="data" class="chips">
              <span v-if="elementName" class="el" :style="{ '--el': elementColor(data.elementId) }">{{ elementName }}</span>
              <span v-if="data.weaponTypeName" class="wt">{{ data.weaponTypeName }}</span>
            </div>
            <div v-if="data?.tags.length" class="chips">
              <span v-for="tg in data.tags" :key="tg.name" class="role-tag" :style="{ '--c': `#${tg.color || '888888'}` }" :title="tg.description">{{ tg.name }}</span>
            </div>
          </div>
        </aside>

        <!-- 右：页签 -->
        <section class="main">
          <header class="head">
            <div class="tabs" role="tablist">
              <button v-for="x in tabs" :key="x.key" class="chip" role="tab" :aria-pressed="tab === x.key" @click="tab = x.key">{{ x.title }}</button>
            </div>
            <a v-if="pageUrl" class="site" :href="pageUrl" target="_blank" rel="noopener noreferrer">{{ t('Challenge_OpenSite') }}</a>
            <button class="close" :aria-label="t('CharDetail_Close')" :title="t('CharDetail_Close')" @click="emit('close')"><StrokeIcon name="Close" :size="14" /></button>
          </header>

          <div ref="scroller" class="body">
            <!-- 我的养成不依赖 encore.moe -->
            <div v-if="tab === 'build' && build" class="pane">
              <p class="caption">{{ syncedAt ? t('Roles_SyncInfo', dateTime(syncedAt)) : '' }}</p>
              <div class="two">
                <div class="box">
                  <h3 class="box-title">{{ t('CharDetail_CurrentStats') }}</h3>
                  <div v-for="a in build.attributes" :key="a.name" class="kv"><span>{{ a.name }}</span><strong>{{ a.value }}</strong></div>
                  <p v-if="!build.attributes.length" class="caption">{{ t('Guide_NoAttributeData') }}</p>
                </div>
                <div class="stack">
                  <div v-if="build.weapon" class="box weapon" :title="build.weapon.fields.map(f => `${f.name} ${f.value}`).join('\n')">
                    <img :src="iconUrl(build.weapon.id)" alt="" @error="iconFallback" />
                    <div>
                      <span class="caption">{{ t('Guide_HoverWeapon') }}</span>
                      <div class="weapon-name"><strong>{{ build.weapon.name }}</strong><span class="stars five">{{ '★'.repeat(build.weapon.star) }}</span></div>
                    </div>
                  </div>
                  <div v-if="build.build.skills.length" class="box">
                    <h3 class="box-title">{{ t('CharDetail_SkillLevels') }}</h3>
                    <div v-for="(s, i) in build.build.skills" :key="i" class="skill-level">
                      <!-- 攻略站的图标在深色底上看不见，没有 encore.moe 同名技能时换浅色底 -->
                      <span class="icon" :class="buildSkillIcon(s.name, s.type) ? 'ink' : 'light'">
                        <img v-if="buildSkillIcon(s.name, s.type) ?? s.imageUrl" :src="buildSkillIcon(s.name, s.type) ?? s.imageUrl" alt="" referrerpolicy="no-referrer" />
                      </span>
                      <div class="sl-text"><span class="caption">{{ s.type }}</span><span>{{ s.name }}</span></div>
                      <strong>Lv.{{ s.level }}</strong>
                    </div>
                  </div>
                </div>
              </div>
              <div v-if="build.build.echoes.length" class="box">
                <div class="echo-head">
                  <h3 class="box-title">{{ t('CharDetail_Echoes') }}<span v-if="build.build.mainEchoName" class="caption"> · {{ build.build.mainEchoName }}</span></h3>
                  <span v-for="(e, i) in build.build.echoSets" :key="i" class="echo-set">
                    <img v-if="e.imageUrl" :src="e.imageUrl" alt="" referrerpolicy="no-referrer" />{{ e.name }}
                  </span>
                </div>
                <div class="echoes">
                  <div v-for="(e, i) in build.build.echoes" :key="i" class="echo">
                    <div class="echo-top"><span class="caption">COST {{ e.cost }}</span><strong v-if="e.level !== undefined">+{{ e.level }}</strong></div>
                    <strong class="echo-main">{{ e.mainStat }}</strong>
                    <span class="caption">{{ e.subStat }}</span>
                  </div>
                </div>
              </div>
              <p v-if="!build.build.skills.length && !build.build.echoes.length" class="caption">{{ t('CharDetail_BuildEmpty') }}</p>
            </div>

            <div v-else-if="state === 'loading' && !data" class="center"><img src="/icons/Loading.gif" width="56" height="56" alt="" /><span class="caption">{{ t('CharDetail_Loading') }}</span></div>
            <div v-else-if="!data" class="center">
              <span class="caption">{{ t('CharDetail_LoadFailed') }}</span>
              <button class="btn" @click="load">{{ t('Common_Retry') }}</button>
            </div>

            <!-- 概览 -->
            <div v-else-if="tab === 'overview'" class="pane">
              <p v-if="data.introduction" class="intro" v-html="rich(data.introduction)" />
              <div>
                <div class="pulls-head"><h3 class="box-title">{{ t('CharDetail_MyPulls') }}</h3><span class="caption">{{ pullsSummary }}</span></div>
                <div v-for="(p, i) in pulls" :key="i" class="pull" :style="{ '--c': ratingColor[ratingOf(p.pity)] }">
                  <span class="label">{{ p.time.slice(0, 16) }}</span>
                  <span class="pull-pity"><b>{{ p.pity }}</b><span class="caption">{{ t('Pulls_PullUnit') }}</span><em>{{ t(`Pulls_Rating_${ratingOf(p.pity)}`) }}</em></span>
                  <span class="ratio"><i :style="{ width: `${Math.min(100, (p.pity / 80) * 100)}%` }" /></span>
                  <span class="pull-tags">
                    <span class="kind" :class="p.kind">{{ t(kindKey(p.kind)) }}</span>
                    <span v-if="p.lost" class="kind lost">{{ t('Pulls_Lost') }}</span>
                    <span class="kind standard">{{ t(poolTitleKey(p.pool)) }}</span>
                  </span>
                </div>
              </div>
              <div class="two">
                <div class="box">
                  <div class="box-row">
                    <h3 class="box-title">{{ t('CharDetail_BaseStats') }}</h3>
                    <label class="lv"><span class="caption">{{ t('CharDetail_Level') }}</span>
                      <select v-model.number="statLevel" class="select"><option v-for="l in statLevels" :key="l" :value="l">{{ l }}</option></select>
                    </label>
                  </div>
                  <div v-for="s in stats" :key="s.name" class="kv"><span>{{ s.name }}</span><strong>{{ s.value }}</strong></div>
                </div>
                <div v-if="profile.length" class="box">
                  <h3 class="box-title">{{ t('CharDetail_Profile') }}</h3>
                  <div class="profile">
                    <div v-for="f in profile" :key="f.label"><span class="caption">{{ f.label }}</span><span>{{ f.value }}</span></div>
                  </div>
                </div>
              </div>
            </div>

            <!-- 技能 -->
            <div v-else-if="tab === 'skills'" class="skills">
              <ul class="skill-list">
                <li v-for="(s, i) in data.skills" :key="i">
                  <button :class="{ on: i === skillIndex }" @click="skillIndex = i">
                    <span class="icon ink big"><img v-if="s.iconUrl" :src="s.iconUrl" alt="" referrerpolicy="no-referrer" /></span>
                    <span class="sl-text"><span class="caption">{{ s.type }}</span><span>{{ s.name }}</span></span>
                  </button>
                </li>
              </ul>
              <div v-if="skill" class="skill-detail">
                <div class="skill-head">
                  <h3 class="skill-name">{{ skill.name }}</h3><span class="ntag">{{ skill.type }}</span>
                  <label v-if="skill.attributes.length" class="lv"><span class="caption">{{ t('CharDetail_SkillLevel') }}</span>
                    <select v-model.number="skillLevel" class="select"><option v-for="l in maxSkillLevel" :key="l" :value="l">{{ l }}</option></select>
                  </label>
                </div>
                <div class="desc" v-html="rich(skill.description)" />
                <div v-if="multipliers.length" class="box mult">
                  <div v-for="m in multipliers" :key="m.name" class="kv"><span>{{ m.name }}</span><strong>{{ m.value }}</strong></div>
                </div>
              </div>
            </div>

            <!-- 共鸣链 -->
            <div v-else-if="tab === 'chain'" class="pane chain">
              <p class="caption">{{ chainSummary }}</p>
              <div v-for="n in data.chain" :key="n.index" class="node" :class="{ locked: unlocked(n.index) === false }">
                <span class="icon ink node-icon"><img v-if="n.iconUrl" :src="n.iconUrl" alt="" referrerpolicy="no-referrer" /></span>
                <div>
                  <div class="node-head">
                    <span class="caption">{{ n.index }}</span><strong>{{ n.name }}</strong>
                    <span v-if="unlocked(n.index) === true" class="ntag">{{ t('CharDetail_Unlocked') }}</span>
                    <span v-else-if="unlocked(n.index) === false" class="caption">{{ t('CharDetail_Locked') }}</span>
                  </div>
                  <div class="desc" v-html="rich(n.description)" />
                </div>
              </div>
            </div>

            <!-- 故事与语音 -->
            <div v-else-if="tab === 'lore'" class="two lore">
              <div v-for="group in [{ key: 's', title: t('CharDetail_Stories'), items: data.stories }, { key: 'v', title: t('CharDetail_Voices'), items: data.voices }]" :key="group.key" class="box">
                <h3 class="box-title">{{ group.title }}</h3>
                <div v-for="(x, i) in group.items" :key="i" class="lore-item">
                  <button class="lore-head" :aria-expanded="open.has(`${group.key}${i}`)" @click="toggle(`${group.key}${i}`)">
                    <span>{{ x.title }}</span><StrokeIcon name="ChevronDown" :size="12" class="chev" :class="{ open: open.has(`${group.key}${i}`) }" />
                  </button>
                  <div v-if="open.has(`${group.key}${i}`)" class="desc lore-body" v-html="rich(x.content)" />
                </div>
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
@keyframes scrim-in { from { opacity: 0; } }
@keyframes dialog-in { from { opacity: 0; transform: translateY(12px) scale(0.97); } }
.scrim { position: fixed; inset: 0; z-index: 50; display: grid; place-items: center; background: var(--scrim); animation: scrim-in var(--motion-standard) var(--ease-standard); }
.dialog {
  width: 1060px; height: 660px; max-width: calc(100vw - 32px); max-height: calc(100vh - 48px); display: grid; grid-template-columns: 344px 1fr; overflow: hidden;
  background: var(--surface-elevated); border: 1px solid var(--border); border-radius: var(--radius-dialog); box-shadow: var(--shadow-dialog);
  animation: dialog-in var(--motion-relaxed) var(--ease-standard);
}
:global(.reduced-motion) .dialog { animation: fade-in var(--motion-fast) linear; }

/* 左栏 */
.side { display: flex; flex-direction: column; border-right: 1px solid var(--divider); background: var(--surface); min-height: 0; }
.portrait { position: relative; height: 430px; flex: none; overflow: hidden; background: var(--surface-sunken);
  -webkit-mask: linear-gradient(to bottom, #000 82%, transparent); mask: linear-gradient(to bottom, #000 82%, transparent); }
.portrait img { width: 100%; height: 100%; object-fit: cover; object-position: top; }
.portrait .fallback { object-fit: contain; width: 128px; height: 128px; margin: 150px auto; opacity: 0.5; }
.skin { position: absolute; top: 14px; right: 14px; }
.skin-btn { height: 32px; padding: 0 10px; display: inline-flex; align-items: center; gap: 8px; border-radius: 8px; border: 1px solid var(--border); background: var(--surface); cursor: pointer; font-weight: 700; font-size: 13px; }
.skin-menu { position: absolute; right: 0; top: 36px; z-index: 2; list-style: none; margin: 0; padding: 4px; min-width: 100%; border-radius: 8px; border: 1px solid var(--border); background: var(--surface); box-shadow: var(--shadow-menu); }
.skin-menu button { width: 100%; text-align: left; padding: 6px 10px; border: 0; border-radius: 6px; background: transparent; cursor: pointer; white-space: nowrap; }
.skin-menu button:hover, .skin-menu button.on { background: var(--accent-container); color: var(--accent-text); }
.card-info { display: flex; flex-direction: column; gap: 8px; padding: 6px 20px 18px; overflow: auto; }
.name-row { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
.name { margin: 0; font-size: 24px; font-weight: 700; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.seq { flex: none; height: 22px; padding: 0 9px; border-radius: 999px; display: inline-flex; align-items: center; font-size: 12px; font-weight: 700; background: var(--surface-sunken); color: var(--text-secondary); }
.seq.mid { background: var(--accent-container); color: var(--accent-text); }
.seq.full { background: var(--accent); color: var(--text-on-accent); }
.stars { color: var(--text-muted); letter-spacing: 1px; }
.stars.five { color: var(--accent); }
.chips { display: flex; flex-wrap: wrap; gap: 6px; }
.el, .wt, .role-tag { height: 24px; padding: 0 9px; border-radius: 999px; display: inline-flex; align-items: center; font-size: 13px; font-weight: 700; }
.el { border: 1.5px solid var(--el); color: var(--el); }
.wt { background: var(--surface-sunken); color: var(--text-secondary); }
.role-tag { background: color-mix(in srgb, var(--c) 16%, transparent); color: color-mix(in srgb, var(--c) 72%, #000); }

/* 右栏 */
.main { display: flex; flex-direction: column; min-width: 0; min-height: 0; }
.head { display: flex; align-items: center; gap: 12px; padding: 16px 16px 0 22px; }
.tabs { display: flex; gap: 8px; flex: 1; flex-wrap: wrap; }
.tabs .chip { height: 36px; padding: 0 16px; font-size: 14px; }
.site { font-size: 13px; font-weight: 700; color: var(--accent-text); text-decoration: none; white-space: nowrap; }
.site:hover { text-decoration: underline; }
.close { width: 36px; height: 36px; border-radius: 8px; border: 1px solid var(--border); background: var(--surface); display: grid; place-items: center; cursor: pointer; color: var(--text-secondary); flex: none; }
.close:hover { border-color: var(--accent-line); color: var(--text-primary); }
.body { flex: 1; overflow: auto; padding: 16px 22px 22px; min-height: 0; }
.center { height: 100%; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 10px; }
.pane { display: flex; flex-direction: column; gap: 16px; }
.pane > .caption { margin: 0; }
.two { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; align-items: start; }
.stack { display: flex; flex-direction: column; gap: 14px; }
.box { background: var(--surface-raised); border-radius: 10px; padding: 14px 16px; }
.box-title { margin: 0 0 8px; font-size: 15px; font-weight: 700; }
.box-row { display: flex; align-items: center; justify-content: space-between; margin-bottom: 4px; }
.box-row .box-title { margin: 0; }
.kv { display: flex; justify-content: space-between; gap: 10px; padding: 4px 0; font-size: 14px; }
.kv span { color: var(--text-secondary); }
.lv { display: inline-flex; align-items: center; gap: 8px; }
.select { height: 30px; padding: 0 8px; min-width: 70px; border-radius: 8px; border: 1px solid var(--border); background: var(--input-background); color: var(--text-primary); font: inherit; }
.intro { margin: 0; line-height: 1.8; }
.desc { line-height: 1.85; font-size: 14px; }
.desc :deep(.hl), .intro :deep(.hl) { color: var(--accent-text); }
.pulls-head { display: flex; align-items: baseline; gap: 10px; margin-bottom: 4px; }
.pulls-head .box-title { margin: 0; }
.pull { display: grid; grid-template-columns: 140px 110px 1fr auto; gap: 14px; align-items: center; padding: 9px 0; border-top: 1px solid var(--divider); }
.pull .label { font-size: 13px; color: var(--label); }
.pull-pity { display: inline-flex; align-items: baseline; gap: 4px; color: var(--c); }
.pull-pity b { font-size: 22px; }
.pull-pity em { font-style: normal; font-size: 11px; font-weight: 700; }
.ratio { height: 8px; border-radius: 4px; background: var(--track); overflow: hidden; }
.ratio i { display: block; height: 100%; border-radius: 4px; background: var(--c); }
.pull-tags { display: flex; flex-wrap: wrap; gap: 5px; }
.kind { height: 20px; padding: 0 8px; border-radius: 999px; display: inline-flex; align-items: center; font-size: 11px; font-weight: 700; background: var(--accent-container); color: var(--accent-text); }
.kind.guaranteed { background: var(--nav-island); color: var(--text-on-nav-island); }
.kind.standard { background: var(--surface-sunken); color: var(--text-secondary); }
.kind.lost { background: var(--danger-container); color: var(--danger); }
.profile { display: grid; grid-template-columns: 1fr 1fr; gap: 10px 14px; }
.profile > div { display: flex; flex-direction: column; }
.profile span:last-child { font-size: 15px; }

.icon { width: 32px; height: 32px; border-radius: 50%; display: grid; place-items: center; flex: none; overflow: hidden; }
.icon.ink { background: #2a2622; }
.icon.light { background: var(--surface-muted); }
.icon.big { width: 40px; height: 40px; }
.icon img { width: 80%; height: 80%; object-fit: contain; }
.weapon { display: flex; gap: 14px; align-items: center; }
.weapon img { width: 52px; height: 52px; border-radius: 12px; object-fit: cover; background: var(--surface-muted); }
.weapon-name { display: flex; align-items: center; gap: 8px; font-size: 16px; }
.skill-level { display: grid; grid-template-columns: 32px 1fr auto; gap: 12px; align-items: center; padding: 4px 0; }
.sl-text { display: flex; flex-direction: column; min-width: 0; line-height: 1.3; }
.echo-head { display: flex; align-items: center; gap: 12px; }
.echo-head .box-title { flex: 1; margin: 0; }
.echo-set { display: inline-flex; align-items: center; gap: 6px; font-size: 14px; }
.echo-set img { width: 22px; height: 22px; border-radius: 50%; }
.echoes { display: grid; grid-template-columns: repeat(5, 1fr); gap: 10px; margin-top: 12px; }
.echo { display: flex; flex-direction: column; gap: 2px; padding: 10px 12px; border-radius: 8px; background: var(--surface); border: 1px solid var(--divider); min-width: 0; }
.echo-top { display: flex; justify-content: space-between; }
.echo-main { color: var(--accent-text); font-size: 13px; }

.skills { display: grid; grid-template-columns: 210px 1fr; gap: 18px; min-height: 100%; }
.skill-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 4px; }
.skill-list button { width: 100%; display: flex; gap: 12px; align-items: center; padding: 6px 8px; border: 0; border-radius: 8px; background: transparent; cursor: pointer; text-align: left; }
.skill-list button:hover { background: var(--surface-hover); }
.skill-list button.on { background: var(--accent-container); }
.skill-head { display: flex; align-items: center; gap: 10px; margin-bottom: 8px; }
.skill-name { margin: 0; font-size: 20px; }
.skill-head .lv { margin-left: auto; }
.mult { margin-top: 14px; }
.mult .kv { padding: 8px 0; border-top: 1px solid var(--divider); }
.mult .kv:first-child { border-top: 0; }

.chain .node { display: grid; grid-template-columns: 40px 1fr; gap: 14px; padding: 14px 16px; border-radius: 10px; border: 1px solid var(--divider); background: var(--surface); }
.node-icon { width: 40px; height: 40px; }
.node.locked .node-icon { opacity: 0.6; }
.node-head { display: flex; align-items: baseline; gap: 8px; margin-bottom: 2px; }
.node-head strong { font-size: 15px; }
.lore-item { border-top: 1px solid var(--divider); }
.lore-head { width: 100%; display: flex; justify-content: space-between; align-items: center; padding: 10px 0; border: 0; background: transparent; cursor: pointer; text-align: left; font-size: 14px; }
.lore-head:hover { color: var(--accent-text); }
.chev { transform: rotate(-90deg); transition: transform var(--motion-standard) var(--ease-standard); }
.chev.open { transform: none; }
.lore-body { padding: 0 0 12px; color: var(--text-secondary); }

/* 手机：铺满屏幕、整体上下滚动；立绘缩短放在最上方，页签吸顶，关闭按钮固定在右上角 */
@media (max-width: 768px) {
  .dialog {
    display: block; width: 100%; height: 100dvh; max-width: none; max-height: none; overflow-y: auto;
    border: 0; border-radius: 0; padding-bottom: env(safe-area-inset-bottom);
  }
  .side { border-right: 0; border-bottom: 1px solid var(--divider); }
  .portrait { height: 300px; }
  .portrait .fallback { margin: 80px auto; }
  .skin { right: 60px; }
  .card-info { overflow: visible; padding: 4px 16px 14px; }
  .name { font-size: 22px; }
  .head { position: sticky; top: 0; z-index: 3; padding: 10px 14px; background: var(--surface-elevated); border-bottom: 1px solid var(--divider); flex-wrap: wrap; gap: 6px 10px; }
  .tabs { flex-wrap: nowrap; overflow-x: auto; scrollbar-width: none; flex: 1 1 100%; }
  .tabs .chip { flex: none; height: 34px; padding: 0 14px; }
  .close { position: fixed; top: calc(10px + env(safe-area-inset-top)); right: 10px; z-index: 5; width: 40px; height: 40px; border-radius: 50%; box-shadow: var(--shadow-pill); }
  .body { overflow: visible; padding: 12px 14px 20px; }
  .two, .profile { grid-template-columns: minmax(0, 1fr); }
  .box { padding: 12px 14px; }
  .pull { grid-template-columns: minmax(0, 1fr) auto; gap: 6px 12px; }
  .pull .ratio, .pull .pull-tags { grid-column: 1 / -1; }
  .echoes { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; }
  .echo-head { flex-wrap: wrap; }
  .skills { grid-template-columns: minmax(0, 1fr); gap: 12px; }
  .skill-list { flex-direction: row; overflow-x: auto; scrollbar-width: none; }
  .skill-list button { width: auto; flex: none; }
  .skill-head { flex-wrap: wrap; }
  .chain .node { padding: 12px; gap: 10px; }
}
</style>
