import type { FiveStar, FourStar, Pool } from './gachaApi';

// 五星记录卡（PoolRecordsViewModel.BuildCards）：保底类型按时间顺序推算，歪了之后的下一个五星为大保底。
export type PityKind = 'soft' | 'guaranteed' | 'standard';
export interface FiveStarCard {
  pool: number;
  resourceId: number;
  name: string;
  time: string;
  pity: number;
  /** true 限定 / false 常驻 / undefined 无法判断 */
  limited?: boolean;
  kind: PityKind;
  lost: boolean;
  fourStars: FourStar[];
}

const PITY_RULE_POOLS = new Set([1, 2, 10, 11]);

/** 卡池的五星记录（新→旧）。接口返回的 fiveStars 已是新→旧。 */
export function buildCards(pool: Pool): FiveStarCard[] {
  const hasRule = PITY_RULE_POOLS.has(pool.pool);
  let guaranteed = false;
  const out: FiveStarCard[] = [];
  for (const f of [...pool.fiveStars].reverse()) {
    out.push(toCard(pool.pool, f, hasRule ? (guaranteed ? 'guaranteed' : 'soft') : 'standard', hasRule && f.isMiss === true));
    if (pool.pool === 1) guaranteed = f.isMiss === true;
  }
  return out.reverse();
}

function toCard(pool: number, f: FiveStar, kind: PityKind, lost: boolean): FiveStarCard {
  return {
    pool, resourceId: f.resourceId, name: f.name, time: f.time, pity: f.pity,
    limited: f.isMiss === undefined || f.isMiss === null ? undefined : !f.isMiss,
    kind, lost, fourStars: f.fourStars,
  };
}

/** 全部卡池的五星，按获得时间从早到晚 */
export const allCards = (pools: Pool[]) =>
  pools.flatMap(buildCards).sort((a, b) => a.time.localeCompare(b.time));

/** 武器的资源 ID 为 8 位，角色为 4 位 */
export const isWeapon = (resourceId: number) => resourceId >= 100_000;

// 出金评价：1–40 欧皇、41–60 平稳、61–70 偏非、71–80 非酋；四档明度也不同，不只靠色相区分
export type Rating = 'Lucky' | 'Steady' | 'Unlucky' | 'Worst';
export const ratings: Rating[] = ['Lucky', 'Steady', 'Unlucky', 'Worst'];
export const ratingOf = (pity: number): Rating => (pity <= 40 ? 'Lucky' : pity <= 60 ? 'Steady' : pity <= 70 ? 'Unlucky' : 'Worst');
export const ratingColor: Record<Rating, string> = { Lucky: '#3E8E5A', Steady: '#C0902E', Unlucky: '#D2702E', Worst: '#C24A36' };

// 筛选：时间范围（月）与五星类型
export type FiveStarFilter = 'all' | 'limited' | 'permanent' | 'lost';
export const timeRanges = [
  { value: 0, key: 'Stat_Filter_DateAll' },
  { value: 1, key: 'Stat_Filter_Date1Month' },
  { value: 3, key: 'Stat_Filter_Date3Months' },
  { value: 6, key: 'Pulls_Filter6Months' },
];
export const fiveStarFilters: { value: FiveStarFilter; key: string }[] = [
  { value: 'all', key: 'Pulls_FilterAll' },
  { value: 'limited', key: 'Pulls_Limited' },
  { value: 'permanent', key: 'Pulls_Permanent' },
  { value: 'lost', key: 'Pulls_Lost' },
];

export function applyFilter(cards: FiveStarCard[], months: number, filter: FiveStarFilter) {
  let since = '';
  if (months > 0) {
    const d = new Date();
    d.setMonth(d.getMonth() - months);
    const p = (n: number) => String(n).padStart(2, '0');
    since = `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
  }
  return cards.filter(c =>
    (!since || c.time >= since) &&
    (filter === 'all' || (filter === 'limited' ? c.limited === true : filter === 'permanent' ? c.limited === false : c.lost)));
}

export const kindKey = (k: PityKind) => (k === 'guaranteed' ? 'Pulls_Guaranteed' : k === 'soft' ? 'Pulls_SoftPity' : 'Pulls_StandardPool');
