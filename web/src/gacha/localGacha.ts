import { ref } from 'vue';
import { post } from '../http';

// 不登录时导入的抽卡记录：只保存在当前浏览器的 IndexedDB，后端不落库。
// 合并规则与 Core 的 GachaRepository.SyncGachaDataAsync 相同：每次拉取的结果按接口顺序（从新到旧）编号，
// 同一内容键（UID | 卡池 | 时间 | 物品 | 类型 | 星级）在这一批里第几次出现作为指纹的一部分；
// 指纹已存在的跳过，新的追加到末尾。存储顺序就是后端的 SourceOrder，统计时按原样交给后端计算。

export interface LocalRecord { resourceId: number; name: string; resourceType: string; quality: number; time: string; fp: string }
export interface LocalUid { uid: string; region: 'global' | 'china'; updatedAt: string; pools: Record<number, LocalRecord[]> }
export interface LocalSummary { uid: string; region: 'global' | 'china'; updatedAt: string; count: number }

type FetchedRecord = Omit<LocalRecord, 'fp'>;

const DB = 'wwtool-local-gacha';
const STORE = 'uids';

function open(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB, 1);
    req.onupgradeneeded = () => req.result.createObjectStore(STORE, { keyPath: 'uid' });
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

async function tx<T>(mode: IDBTransactionMode, run: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  const db = await open();
  try {
    return await new Promise<T>((resolve, reject) => {
      const req = run(db.transaction(STORE, mode).objectStore(STORE));
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
  } finally {
    db.close();
  }
}

/** 本地已有的 UID 概况，导入、删除后自动刷新 */
export const localUids = ref<LocalSummary[]>([]);

export async function refreshLocal() {
  try {
    const all = await tx<LocalUid[]>('readonly', s => s.getAll());
    localUids.value = all
      .map(x => ({ uid: x.uid, region: x.region, updatedAt: x.updatedAt, count: Object.values(x.pools).reduce((n, p) => n + p.length, 0) }))
      .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
  } catch {
    // 隐私模式等无法使用 IndexedDB 时视为没有本地数据
    localUids.value = [];
  }
}

export const loadLocal = (uid: string) => tx<LocalUid | undefined>('readonly', s => s.get(uid));

export async function deleteLocal(uid: string) {
  await tx('readwrite', s => s.delete(uid));
  await refreshLocal();
}

const contentKey = (uid: string, pool: number, r: FetchedRecord) =>
  `${uid}|${pool}|${r.time.trim()}|${r.resourceId}|${r.resourceType.trim()}|${r.quality}`;

/** 用抽卡链接拉取一次（后端不保存），合并进本地。返回 UID 与新增条数。 */
export async function importWithLink(url: string, signal?: AbortSignal) {
  const fetched = await post<{ uid: string; region: 'global' | 'china'; pools: Record<string, FetchedRecord[]> }>(
    '/api/guest/gacha/fetch', { url }, signal);
  const existing = (await loadLocal(fetched.uid)) ?? { uid: fetched.uid, region: fetched.region, updatedAt: '', pools: {} };
  let added = 0;
  for (const [key, records] of Object.entries(fetched.pools)) {
    const pool = Number(key);
    const list = existing.pools[pool] ?? [];
    const known = new Set(list.map(r => r.fp));
    const occurrences = new Map<string, number>();
    for (const r of records) {
      const k = contentKey(fetched.uid, pool, r);
      const n = occurrences.get(k) ?? 0;
      occurrences.set(k, n + 1);
      const fp = `${k}|${n}`;
      if (known.has(fp)) continue;
      known.add(fp);
      list.push({ resourceId: r.resourceId, name: r.name, resourceType: r.resourceType, quality: r.quality, time: r.time, fp });
      added++;
    }
    existing.pools[pool] = list;
  }
  existing.region = fetched.region;
  existing.updatedAt = new Date().toISOString();
  await tx('readwrite', s => s.put(existing));
  await refreshLocal();
  return { uid: fetched.uid, added };
}

/** 发给后端统计或上传时去掉本地指纹，只保留记录字段 */
export function plainPools(local: LocalUid) {
  const pools: Record<number, FetchedRecord[]> = {};
  for (const [key, list] of Object.entries(local.pools)) {
    pools[Number(key)] = list.map(({ fp: _fp, ...r }) => r);
  }
  return pools;
}
