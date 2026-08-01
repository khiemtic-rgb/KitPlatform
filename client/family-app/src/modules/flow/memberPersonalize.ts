import type {
  RelationshipTrigger,
  RelationshipTriggerState,
} from '@/shared/api/family-os.api';
import { upsertRelationshipTriggerState } from '@/shared/api/family-os.api';

export type RelTriggerUiState = 'opened' | 'dismissed' | 'sent';

type Stored = {
  state: RelTriggerUiState;
  draftBodyVi?: string;
  templateCode?: string;
  toMemberId?: string;
  titleVi?: string;
  bodyVi?: string;
  updatedAt: string;
};

function storageKey(
  familyId: string,
  viewerId: string,
  flowDate: string,
  code: string,
  toMemberId?: string,
): string {
  return `famixa.relTrigger.v1:${familyId}:${viewerId}:${flowDate}:${code}:${toMemberId ?? '-'}`;
}

function read(key: string): Stored | null {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return null;
    return JSON.parse(raw) as Stored;
  } catch {
    return null;
  }
}

function write(key: string, value: Stored): void {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    /* ignore quota */
  }
}

function asUiState(raw: string | undefined): RelTriggerUiState | null {
  if (raw === 'opened' || raw === 'dismissed' || raw === 'sent') return raw;
  return null;
}

/** Hydrate local cache from server P1.2 rows (server wins on newer updatedAt). */
export function hydrateRelTriggerStates(
  familyId: string,
  viewerId: string,
  flowDate: string,
  rows: RelationshipTriggerState[],
): void {
  for (const row of rows) {
    const state = asUiState(row.state);
    if (!state) continue;
    if (row.flowDate && row.flowDate !== flowDate) continue;
    const key = storageKey(
      familyId,
      viewerId,
      flowDate,
      row.triggerCode,
      row.toMemberId,
    );
    const prev = read(key);
    if (prev?.updatedAt && row.updatedAt) {
      const prevMs = Date.parse(prev.updatedAt);
      const nextMs = Date.parse(row.updatedAt);
      if (!Number.isNaN(prevMs) && !Number.isNaN(nextMs) && prevMs > nextMs) continue;
    }
    write(key, {
      state,
      draftBodyVi: row.draftBodyVi,
      templateCode: row.templateCode,
      toMemberId: row.toMemberId,
      titleVi: row.titleVi,
      bodyVi: row.bodyVi,
      updatedAt: row.updatedAt || new Date().toISOString(),
    });
  }
}

async function persistServer(
  familyId: string,
  viewerId: string,
  flowDate: string,
  trigger: Pick<RelationshipTrigger, 'code' | 'toMemberId'>,
  state: RelTriggerUiState,
  extra?: Partial<Stored>,
): Promise<void> {
  try {
    await upsertRelationshipTriggerState(familyId, {
      viewerMemberId: viewerId,
      triggerCode: trigger.code,
      state,
      toMemberId: trigger.toMemberId,
      flowDate,
      draftBodyVi: extra?.draftBodyVi,
      templateCode: extra?.templateCode,
      titleVi: extra?.titleVi,
      bodyVi: extra?.bodyVi,
    });
  } catch {
    /* local cache already written; retry on next hydrate */
  }
}

export function getRelTriggerState(
  familyId: string,
  viewerId: string,
  flowDate: string,
  trigger: Pick<RelationshipTrigger, 'code' | 'toMemberId'>,
): Stored | null {
  return read(
    storageKey(familyId, viewerId, flowDate, trigger.code, trigger.toMemberId),
  );
}

export function markRelTriggerOpened(
  familyId: string,
  viewerId: string,
  flowDate: string,
  trigger: RelationshipTrigger,
  draftBodyVi: string,
): void {
  const key = storageKey(
    familyId,
    viewerId,
    flowDate,
    trigger.code,
    trigger.toMemberId,
  );
  const stored: Stored = {
    state: 'opened',
    draftBodyVi,
    templateCode: trigger.templateCode,
    toMemberId: trigger.toMemberId,
    titleVi: trigger.titleVi,
    bodyVi: trigger.bodyVi,
    updatedAt: new Date().toISOString(),
  };
  write(key, stored);
  void persistServer(familyId, viewerId, flowDate, trigger, 'opened', stored);
}

export function markRelTriggerDismissed(
  familyId: string,
  viewerId: string,
  flowDate: string,
  trigger: Pick<RelationshipTrigger, 'code' | 'toMemberId'>,
): void {
  const key = storageKey(
    familyId,
    viewerId,
    flowDate,
    trigger.code,
    trigger.toMemberId,
  );
  const prev = read(key);
  const stored: Stored = {
    state: 'dismissed',
    draftBodyVi: prev?.draftBodyVi,
    templateCode: prev?.templateCode,
    toMemberId: trigger.toMemberId,
    titleVi: prev?.titleVi,
    bodyVi: prev?.bodyVi,
    updatedAt: new Date().toISOString(),
  };
  write(key, stored);
  void persistServer(familyId, viewerId, flowDate, trigger, 'dismissed', stored);
}

export function markRelTriggerSent(
  familyId: string,
  viewerId: string,
  flowDate: string,
  trigger: Pick<RelationshipTrigger, 'code' | 'toMemberId'>,
): void {
  const key = storageKey(
    familyId,
    viewerId,
    flowDate,
    trigger.code,
    trigger.toMemberId,
  );
  const stored: Stored = {
    state: 'sent',
    toMemberId: trigger.toMemberId,
    updatedAt: new Date().toISOString(),
  };
  write(key, stored);
  void persistServer(familyId, viewerId, flowDate, trigger, 'sent', stored);
}

/** Drop dismissed/sent; prefer opened (unsent) then golden server order. */
export function filterVisibleParentTriggers(
  familyId: string,
  viewerId: string,
  flowDate: string,
  triggers: RelationshipTrigger[],
): RelationshipTrigger[] {
  const visible: RelationshipTrigger[] = [];
  for (const t of triggers) {
    if (!isParentVoiceTrigger(t.code)) continue;
    const st = getRelTriggerState(familyId, viewerId, flowDate, t);
    if (st?.state === 'dismissed' || st?.state === 'sent') continue;
    if (st?.state === 'opened') {
      visible.push({
        ...t,
        draftBodyVi: st.draftBodyVi || t.draftBodyVi,
        titleVi: st.titleVi || t.titleVi,
        bodyVi: 'Còn 1 chạm — con chưa nhận lời của bạn.',
        ctaLabelVi: 'Gửi ngay',
      });
      continue;
    }
    visible.push(t);
  }
  return visible.sort((a, b) => Number(b.isGolden) - Number(a.isGolden));
}

export function primaryRelationshipTrigger(
  triggers: RelationshipTrigger[],
): RelationshipTrigger | null {
  return triggers[0] ?? null;
}

export function isParentVoiceTrigger(code: string): boolean {
  return (
    code === 'praise_streak' ||
    code === 'encourage_dip' ||
    code === 'team_early_finish' ||
    code === 'first_day_complete' ||
    code === 'warm_checkin' ||
    code === 'thank_partner' ||
    code === 'birthday_wish'
  );
}

export function isAdultVoiceTrigger(code: string): boolean {
  return code === 'thank_partner' || code === 'partner_voice_inbox';
}

export function isBirthdayWishTrigger(code: string): boolean {
  return code === 'birthday_wish';
}

export function isCheerSiblingTrigger(code: string): boolean {
  return code === 'cheer_sibling';
}

export function isThankParentTrigger(code: string): boolean {
  return code === 'thank_parent';
}

export function parentVoiceIcon(templateCode?: string): string {
  switch (templateCode) {
    case 'encourage':
      return '🌿';
    case 'thanks_partner':
      return '🤝';
    case 'help_offer':
      return '🤲';
    case 'birthday':
      return '🎂';
    default:
      return '❤️';
  }
}

export function isUnsentOpenedTrigger(
  familyId: string,
  viewerId: string,
  flowDate: string,
  trigger: RelationshipTrigger | null,
): boolean {
  if (!trigger) return false;
  const st = getRelTriggerState(familyId, viewerId, flowDate, trigger);
  return st?.state === 'opened';
}
