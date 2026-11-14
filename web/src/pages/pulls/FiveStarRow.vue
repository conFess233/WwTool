<script setup lang="ts">
import { ref } from 'vue';
import { t } from '../../i18n';
import { iconUrl, iconFallback } from '../../format';
import { kindKey, type FiveStarCard } from './fiveStar';
import SegmentedBar from '../../components/SegmentedBar.vue';
import StrokeIcon from '../../components/StrokeIcon.vue';

// 一次五星记录（PoolRecordsView）：头像、名称与限定标签、时间、保底类型；抽数与刻度条；期间获得的四星；点击展开四星明细。
const props = defineProps<{ card: FiveStarCard; subtitle: string; chips?: boolean }>();
const open = ref(false);
const near = () => props.card.pity >= 70;
</script>

<template>
  <div class="row" :class="{ open }">
    <button class="row-main" :class="{ 'no-chips': !chips }" :aria-expanded="open" @click="open = !open">
      <img class="face" :src="iconUrl(card.resourceId)" alt="" loading="lazy" @error="iconFallback" />
      <div class="who">
        <div class="name-line">
          <strong class="name">{{ card.name }}</strong>
          <span v-if="card.limited === true" class="ntag">{{ t('Pulls_Limited') }}</span>
          <span v-else-if="card.limited === false" class="ntag plain">{{ t('Pulls_Permanent') }}</span>
        </div>
        <span class="aux muted">{{ subtitle }}</span>
        <div class="tags">
          <span class="kind" :class="card.kind">{{ t(kindKey(card.kind)) }}</span>
          <span v-if="card.lost" class="kind lost">{{ t('Pulls_Lost') }}</span>
        </div>
      </div>
      <div class="pity-box">
        <div class="pity-line"><strong class="pity" :class="{ danger: near() }">{{ card.pity }}</strong><span class="unit">{{ t('Pulls_PullUnit') }}</span></div>
        <SegmentedBar :value="Math.min(card.pity, 80)" :max="80" :danger="near()" />
      </div>
      <div v-if="chips" class="chips">
        <span v-for="f in card.fourStars" :key="f.resourceId" class="chip4" :title="f.name">
          <img :src="iconUrl(f.resourceId)" alt="" loading="lazy" @error="iconFallback" />x{{ f.count }}
        </span>
      </div>
      <StrokeIcon class="chevron" name="ChevronDown" :size="16" />
    </button>
    <div v-if="open" class="detail">
      <span v-if="!card.fourStars.length" class="caption">{{ t('Pulls_NoFourStars') }}</span>
      <span v-for="f in card.fourStars" :key="f.resourceId" class="chip4 named">
        <img :src="iconUrl(f.resourceId)" alt="" loading="lazy" @error="iconFallback" />{{ f.name }} x{{ f.count }}
      </span>
    </div>
  </div>
</template>

<style scoped>
.row { border-top: 1px solid var(--divider); }
.row:first-child { border-top: 0; }
.row-main {
  width: 100%; display: grid; grid-template-columns: 70px minmax(150px, 220px) minmax(200px, 1fr) minmax(0, 1fr) 20px; gap: 20px; align-items: center;
  padding: 14px 6px; border: 0; background: transparent; cursor: pointer; text-align: left; color: inherit; border-radius: 10px;
  transition: background-color var(--motion-fast) linear;
}
.row-main.no-chips { grid-template-columns: 70px minmax(150px, 280px) minmax(200px, 1fr) 20px; }
.row-main:hover { background: var(--surface-hover); }
.face { width: 70px; height: 70px; border-radius: 14px; object-fit: cover; background: var(--surface-muted); }
.who { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
.name-line { display: flex; align-items: center; gap: 8px; }
.name { font-size: 17px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.ntag.plain { background: var(--surface-sunken); color: var(--text-secondary); }
.tags { display: flex; gap: 6px; margin-top: 4px; }
.kind { height: 22px; padding: 0 9px; border-radius: 999px; display: inline-flex; align-items: center; font-size: 12px; font-weight: 700; background: var(--accent-container); color: var(--accent-text); }
.kind.guaranteed { background: var(--nav-island); color: var(--text-on-nav-island); }
.kind.standard { background: var(--surface-sunken); color: var(--text-secondary); }
.kind.lost { background: var(--danger-container); color: var(--danger); }
.pity-box { display: flex; flex-direction: column; gap: 8px; min-width: 0; }
.pity-line { display: flex; align-items: baseline; gap: 6px; }
.pity { font-size: 34px; line-height: 1; letter-spacing: -0.5px; }
.pity.danger { color: var(--danger); }
.unit { font-size: 12px; color: var(--text-muted); }
.chips { display: flex; flex-wrap: wrap; gap: 8px; min-width: 0; }
.chip4 { display: inline-flex; align-items: center; gap: 6px; height: 34px; padding: 0 12px 0 3px; border-radius: 999px; background: var(--four-star-container); color: var(--four-star); font-size: 13px; font-weight: 700; }
.chip4 img { width: 28px; height: 28px; border-radius: 50%; object-fit: cover; background: var(--surface); }
.chevron { color: var(--text-muted); transition: transform var(--motion-standard) var(--ease-standard); }
.open .chevron { transform: rotate(180deg); }
.detail { display: flex; flex-wrap: wrap; gap: 8px; padding: 0 6px 16px 96px; }
.chip4.named { font-weight: 600; }
</style>
