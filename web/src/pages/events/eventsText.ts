import { t } from '../../i18n';
import type { Notice } from '../../api';

// 与 src/UI/Pages/Events/EventsViewModel.cs、ChallengesViewModel.cs 的 EventsText 文案规则一致。

const pad = (n: number) => String(n).padStart(2, '0');

/** MM/dd HH:mm（本地时间） */
export function stamp(iso: string) {
  const d = new Date(iso);
  return `${pad(d.getMonth() + 1)}/${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function range(start: string, end?: string) {
  return end ? `${stamp(start)} – ${stamp(end)}` : stamp(start);
}

/** 「3 天 5 小时」「5 小时」「12 分钟」 */
export function duration(ms: number) {
  const span = Math.max(0, ms);
  const days = Math.floor(span / 86_400_000);
  const hours = Math.floor((span % 86_400_000) / 3_600_000);
  if (days >= 1) return hours > 0 ? t('Events_DaysHours', days, hours) : t('Events_Days', days);
  const totalHours = Math.floor(span / 3_600_000);
  if (totalHours >= 1) return t('Events_Hours', totalHours);
  return t('Events_Minutes', Math.max(1, Math.ceil(span / 60_000)));
}

export const isUpcoming = (n: Notice, now: number) => Date.parse(n.start) > now;
export const isEnded = (n: Notice, now: number) => !!n.end && Date.parse(n.end) <= now;
export const isActive = (n: Notice, now: number) => !isUpcoming(n, now) && !isEnded(n, now);

export function statusText(n: Notice, now: number) {
  if (isUpcoming(n, now)) return t('Events_StartsIn', duration(Date.parse(n.start) - now));
  if (!n.end) return n.endText ? t('Events_Ongoing') : t('Events_Permanent');
  const end = Date.parse(n.end);
  return end <= now ? t('Events_Ended') : t('Events_Remaining', duration(end - now));
}

export function dateText(n: Notice) {
  if (!n.end && n.endText) return `${range(n.start)} – ${t('Events_UntilVersion', n.endText.split('~').pop()!.trim())}`;
  return range(n.start, n.end);
}

export function kindText(n: Notice) {
  switch (n.kind) {
    case 'Update': return t('Events_KindUpdate');
    case 'Gacha':
      return t(n.pool === 'Weapon' ? 'Events_PoolWeapon' : n.pool === 'CharacterReverb' || n.pool === 'WeaponReverb' ? 'Events_PoolReverb' : 'Events_PoolCharacter');
    case 'Event': return t('Events_KindEvent');
    case 'Shop': return t('Events_KindShop');
    default: return t('Events_KindInfo');
  }
}

export const groupKeys: [Notice['kind'], string][] = [
  ['Update', 'Events_GroupUpdate'],
  ['Gacha', 'Events_GroupGacha'],
  ['Event', 'Events_GroupEvent'],
  ['Shop', 'Events_GroupShop'],
  ['Info', 'Events_GroupInfo'],
];
