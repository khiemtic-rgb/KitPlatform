import type { TFunction } from 'i18next';

const RX_STATUS_COLOR: Record<string, string> = {
  draft: 'default',
  signed: 'green',
  partially_dispensed: 'orange',
  dispensed: 'blue',
  cancelled: 'red',
  expired: 'default',
};

const LINK_STATUS_COLOR: Record<string, string> = {
  active: 'green',
  pending_nt_invite: 'gold',
  pending_nt_approval: 'processing',
  rejected: 'red',
  revoked: 'default',
};

export function rxStatusLabel(t: TFunction, status: string): string {
  const key = `rxStatus.${status}`;
  const translated = t(key);
  return translated === key ? status : translated;
}

export function rxStatusColor(status: string): string {
  return RX_STATUS_COLOR[status] ?? 'default';
}

export function linkStatusLabel(t: TFunction, status: string): string {
  const key = `linkStatus.${status}`;
  const translated = t(key);
  return translated === key ? status : translated;
}

export function linkStatusColor(status: string): string {
  return LINK_STATUS_COLOR[status] ?? 'default';
}
