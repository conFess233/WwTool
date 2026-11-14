import { ref, watch } from 'vue';
import { readPref, writePref } from '../prefs';
import { get } from '../http';
import { language } from '../i18n';

// 窗口背景（与桌面端设置页「窗口背景」一致）：默认取游戏启动器背景，或使用自选图片；可选动态背景视频。
// 自选图片只存在本浏览器的 IndexedDB 中，不会上传。启动器背景、标语图与视频由浏览器直接从启动器 CDN 加载，后端只给链接。
export type BackdropMode = 'default' | 'custom';

export const backdropMode = ref<BackdropMode>(readPref('bg.mode') === 'custom' ? 'custom' : 'default');
export const backdropVideo = ref(readPref('bg.video') === '1');
/** 空白处查看背景：内容淡出到 1 - 透明度；0 表示关闭 */
export const peekTransparency = ref(clamp(Number(readPref('bg.peek') ?? 80)));

export const backdropImage = ref<string | null>(null);
export const backdropSlogan = ref<string | null>(null);
export const backdropVideoUrl = ref<string | null>(null);
export const customImageName = ref<string | null>(null);

function clamp(v: number) {
  return Number.isFinite(v) ? Math.min(100, Math.max(0, Math.round(v))) : 80;
}

watch(backdropMode, v => writePref('bg.mode', v));
watch(backdropVideo, v => writePref('bg.video', v ? '1' : '0'));
watch(peekTransparency, v => writePref('bg.peek', String(clamp(v))));

// ---------- 自选图片（IndexedDB） ----------
const DB = 'wwtool-backdrop';
const STORE = 'files';

function open(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB, 1);
    req.onupgradeneeded = () => req.result.createObjectStore(STORE);
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

async function tx<T>(mode: IDBTransactionMode, run: (s: IDBObjectStore) => IDBRequest<T>): Promise<T> {
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

let customUrl: string | null = null;

async function loadCustom(): Promise<string | null> {
  try {
    const file = await tx<File | undefined>('readonly', s => s.get('custom') as IDBRequest<File | undefined>);
    if (!file) return null;
    customImageName.value = file.name;
    if (customUrl) URL.revokeObjectURL(customUrl);
    customUrl = URL.createObjectURL(file);
    return customUrl;
  } catch {
    return null;
  }
}

/** 选择图片：存入本地并切换为自定义背景 */
export async function setCustomImage(file: File) {
  if (!file.type.startsWith('image/')) throw new Error('not-image');
  await tx('readwrite', s => s.put(file, 'custom'));
  backdropMode.value = 'custom';
  await reloadBackdrop();
}

// ---------- 启动器背景 ----------
interface BackgroundDto { image: string | null; slogan: string | null; video: string | null }
let launcher: BackgroundDto | null = null;
let launcherLang = '';

async function loadLauncher(): Promise<BackgroundDto | null> {
  if (launcher && launcherLang === language.value) return launcher;
  try {
    launcher = await get<BackgroundDto>(`/api/public/background?lang=${encodeURIComponent(language.value)}`);
    launcherLang = language.value;
  } catch {
    launcher = null;
  }
  return launcher;
}

export async function reloadBackdrop() {
  if (backdropMode.value === 'custom') {
    const url = await loadCustom();
    if (url) {
      backdropImage.value = url;
      backdropSlogan.value = null;
      backdropVideoUrl.value = null;
      return;
    }
  }
  const l = await loadLauncher();
  backdropImage.value = l?.image ?? null;
  backdropSlogan.value = l?.slogan ?? null;
  backdropVideoUrl.value = backdropVideo.value ? l?.video ?? null : null;
}

watch([backdropMode, backdropVideo, language], () => reloadBackdrop());
