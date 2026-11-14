// 后端公开接口（src/Web/Public/NoticeDtos.cs 的对应类型）
export interface NoticeFeed {
  region: 'global' | 'china';
  language: string;
  versionName?: string;
  versionNumber?: string;
  versionStart?: string;
  versionEnd?: string;
  fetchedAt: string;
  calendar?: NoticeCalendar;
  notices: Notice[];
  recurring?: Recurring[];
}

export type NoticeKind = 'Update' | 'Gacha' | 'Event' | 'Shop' | 'Info';

export interface Notice {
  id: string;
  title: string;
  kind: NoticeKind;
  pool: 'None' | 'Character' | 'Weapon' | 'CharacterReverb' | 'WeaponReverb';
  start: string;
  end?: string;
  timeText?: string;
  endText?: string;
  featured: string[];
  poolName?: string;
  banner?: string;
  content: string;
  updated: string;
}

export interface NoticeCalendar {
  backgroundUrl?: string;
  versionStart?: string;
  versionEnd?: string;
  nodes: { noticeId: string; published: boolean; x1: number; y1: number; x2: number; y2: number }[];
}

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  const res = await fetch(path, { signal, headers: { Accept: 'application/json' } });
  if (!res.ok) throw new ApiError(res.status, `${path} → HTTP ${res.status}`);
  return (await res.json()) as T;
}

export const api = {
  notices: (region: 'global' | 'china', lang: string, signal?: AbortSignal) =>
    get<{ feed: NoticeFeed; recurring: Recurring[] }>(`/api/public/notices?region=${region}&lang=${encodeURIComponent(lang)}`, signal)
      .then(r => ({ ...r.feed, recurring: r.recurring })),
};

/** 循环玩法当前一轮（后端按 RecurringActivities 规则推算） */
export interface Recurring { key: string; kind: 'Weekly' | 'Interval' | 'Version'; everyDays: number; start: string; end: string }

export interface RedeemCodes { version?: string; codes: string[]; expiresAt?: string; postedAt: string; postUrl: string }
export interface Livestream { codes: RedeemCodes[]; preview?: { version?: string; startsAt: string; title: string; postUrl: string }; fetchedAt: string }

export type ChallengeKind = 'tower' | 'wastes' | 'matrix';
export interface Season { kind: string; id: number; name: string; start?: string; end?: string; seasonName?: string; endVersion?: string }
export interface Seasons { seasons: Season[]; currentId?: number; pageUrl: string; fetchedAt: string }
export interface ChallengeDetail { kind: string; id: number; detail: any; fetchedAt: string }

export const feeds = {
  livestream: (signal?: AbortSignal) => get<Livestream>('/api/public/livestream', signal),
  seasons: (kind: ChallengeKind, lang: string, signal?: AbortSignal) =>
    get<Seasons>(`/api/public/challenges/${kind}?lang=${encodeURIComponent(lang)}`, signal),
  detail: (kind: ChallengeKind, id: number, lang: string, signal?: AbortSignal) =>
    get<ChallengeDetail>(`/api/public/challenges/${kind}/${id}?lang=${encodeURIComponent(lang)}`, signal),
};
