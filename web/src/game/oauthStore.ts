// 在本设备上加密保存 oauthCode 与攻略站令牌（Guide Token）：
// AES-GCM 256、不可导出的密钥与密文都存在 IndexedDB；每次加密使用随机 IV；按「站点账号 + UID」分开存放。
// 加密只防磁盘 / 备份层面的直接拷贝。oauthCode 从不发给本站后端；Guide Token 只在同步角色时随请求交给后端转发，后端不保存。

const DB = 'wwtool-game';
const STORE = 'kv';

function open(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB, 1);
    req.onupgradeneeded = () => req.result.createObjectStore(STORE);
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

async function tx<T>(mode: IDBTransactionMode, fn: (s: IDBObjectStore) => IDBRequest<T> | void): Promise<T | undefined> {
  const db = await open();
  return new Promise((resolve, reject) => {
    const t = db.transaction(STORE, mode);
    const req = fn(t.objectStore(STORE));
    t.oncomplete = () => { db.close(); resolve(req ? req.result : undefined); };
    t.onerror = () => { db.close(); reject(t.error); };
  });
}

const keyName = (userId: string) => `key:${userId}`;
const entryName = (userId: string, uid: string) => `oauth:${userId}:${uid}`;
const guideName = (userId: string, uid: string) => `guide:${userId}:${uid}`;

interface Entry { iv: Uint8Array; cipher: ArrayBuffer; savedAt: string; region: string }

async function getKey(userId: string): Promise<CryptoKey> {
  const existing = await tx<CryptoKey>('readonly', s => s.get(keyName(userId)));
  if (existing) return existing;
  const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']);
  await tx('readwrite', s => s.put(key, keyName(userId)));
  return key;
}

export async function saveOauth(userId: string, uid: string, region: string, oauthCode: string) {
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const cipher = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, await getKey(userId), new TextEncoder().encode(oauthCode));
  const entry: Entry = { iv, cipher, savedAt: new Date().toISOString(), region };
  await tx('readwrite', s => s.put(entry, entryName(userId, uid)));
}

export async function loadOauth(userId: string, uid: string): Promise<{ oauthCode: string; region: string; savedAt: string } | null> {
  try {
    const entry = await tx<Entry>('readonly', s => s.get(entryName(userId, uid)));
    if (!entry) return null;
    const plain = await crypto.subtle.decrypt({ name: 'AES-GCM', iv: entry.iv as BufferSource }, await getKey(userId), entry.cipher);
    return { oauthCode: new TextDecoder().decode(plain), region: entry.region, savedAt: entry.savedAt };
  } catch {
    return null;
  }
}

export async function hasOauth(userId: string, uid: string) {
  try {
    return !!(await tx<Entry>('readonly', s => s.get(entryName(userId, uid))));
  } catch {
    return false;
  }
}

export async function forgetOauth(userId: string, uid: string) {
  await tx('readwrite', s => s.delete(entryName(userId, uid)));
}

/** 攻略站令牌：region 字段存放库洛账号 ID（cUid，不是凭据），同步时一并交给后端 */
export async function saveGuide(userId: string, uid: string, cUid: string, guideToken: string) {
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const cipher = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, await getKey(userId), new TextEncoder().encode(guideToken));
  const entry: Entry = { iv, cipher, savedAt: new Date().toISOString(), region: cUid };
  await tx('readwrite', s => s.put(entry, guideName(userId, uid)));
}

export async function loadGuide(userId: string, uid: string): Promise<{ token: string; cUid: string } | null> {
  try {
    const entry = await tx<Entry>('readonly', s => s.get(guideName(userId, uid)));
    if (!entry) return null;
    const plain = await crypto.subtle.decrypt({ name: 'AES-GCM', iv: entry.iv as BufferSource }, await getKey(userId), entry.cipher);
    return { token: new TextDecoder().decode(plain), cUid: entry.region };
  } catch {
    return null;
  }
}

export async function forgetGuide(userId: string, uid: string) {
  await tx('readwrite', s => s.delete(guideName(userId, uid)));
}

/** 删除一个 UID 在本设备上的全部授权（oauthCode 与攻略站令牌） */
export async function forgetUid(userId: string, uid: string) {
  await forgetOauth(userId, uid);
  await forgetGuide(userId, uid);
}

/** 退出或删除站点账号时：删除该账号在本设备上的全部授权与密钥。 */
export async function forgetAll(userId: string) {
  await tx('readwrite', s => s.delete(IDBKeyRange.bound(`oauth:${userId}:`, `oauth:${userId}:￿`)));
  await tx('readwrite', s => s.delete(IDBKeyRange.bound(`guide:${userId}:`, `guide:${userId}:￿`)));
  await tx('readwrite', s => s.delete(keyName(userId)));
}
