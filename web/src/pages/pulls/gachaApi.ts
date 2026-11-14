import { get, post } from '../../http';
import { loadLocal, plainPools } from '../../gacha/localGacha';
import type { GachaSource } from '../../gacha/source';
import { language } from '../../i18n';

// 后端抽卡统计接口（src/Web/Uids/GachaDtos.cs）。统计全部由后端用 Core 的同一计算链完成，前端只负责展示。
// 本地记录（未登录导入）同样交给后端的访客接口计算，后端不保存。
export interface Summary {
  totalPulls: number; fiveStars: number; limited: number; permanent: number;
  averagePerFiveStar?: number; averagePerLimited?: number;
  wins: number; misses: number; winRate?: number; astrites: number;
}
export interface FourStar { resourceId: number; name: string; count: number }
export interface FiveStar { resourceId: number; name: string; resourceType: string; time: string; pity: number; isMiss?: boolean; fourStars: FourStar[] }
export interface Progress { totalPulls: number; currentPity: number; pullsToHardPity: number; nextIsGuaranteed?: boolean; lastPullAt?: string }
export interface Pool { pool: number; recordCount: number; summary: Summary; progress: Progress; fiveStars: FiveStar[] }
export interface Overview { uid: string; recordCount: number; summary: Summary; pools: Pool[]; fourStarCharacters: number; fourStarWeapons: number }
export interface PullRecord { resourceId: number; name: string; resourceType: string; quality: number; time: string }
export interface Charts {
  daily: [string, number][]; pityBins: number[]; pityLabels: string[]; averagePity?: number;
  poolShares: [number, number][]; totalPulls: number; astrites: number;
  fiveStars: { pool: number; resourceId: number; name: string; time?: string; pity: number; isLimited?: boolean }[];
  cumulative: [string, number][];
  fiveStarCount: number; fourStarCount: number; threeStarCount: number; wins: number; misses: number; winRate?: number;
}

const lang = () => encodeURIComponent(language.value);

async function localPools(uid: string) {
  const local = await loadLocal(uid);
  if (!local) throw new Error('本地记录不存在');
  return local;
}

export const gachaApi = {
  async overview(src: GachaSource, signal?: AbortSignal) {
    if (src.kind === 'cloud') return get<Overview>(`/api/uids/${src.uid}/gacha?lang=${lang()}`, signal);
    const local = await localPools(src.uid);
    return post<Overview>(`/api/guest/gacha/overview?lang=${lang()}`, { uid: src.uid, pools: plainPools(local) }, signal);
  },
  /** 存储顺序（SourceOrder） */
  async records(src: GachaSource, pool: number, signal?: AbortSignal) {
    if (src.kind === 'cloud') return get<PullRecord[]>(`/api/uids/${src.uid}/gacha/pools/${pool}/records`, signal);
    const local = await localPools(src.uid);
    return plainPools(local)[pool] ?? [];
  },
  async charts(src: GachaSource, pools: number[] | null, since: string | null, signal?: AbortSignal) {
    if (src.kind === 'cloud') {
      const q = new URLSearchParams({ lang: language.value });
      if (pools?.length) q.set('pools', pools.join(','));
      if (since) q.set('since', since);
      return get<Charts>(`/api/uids/${src.uid}/gacha/charts?${q}`, signal);
    }
    const local = await localPools(src.uid);
    return post<Charts>(`/api/guest/gacha/charts?lang=${lang()}`, { uid: src.uid, pools: plainPools(local), filter: pools, since }, signal);
  },
};

export const HARD_PITY = 80;
