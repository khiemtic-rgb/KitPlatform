export const SALE_ATTEMPT_STORAGE_KEY = 'kitplatform.pos.saleAttempt.v1';

/** Axios timeout is 30s. Keep an unknown attempt through that window plus a buffer. */
export const SALE_ATTEMPT_UNKNOWN_WINDOW_MS = 45_000;

export type StoredSaleAttempt = {
  key: string;
  fingerprint: string;
  state: 'claimed' | 'unknown';
  updatedAt: number;
  sentAt: number | null;
};

export type SaleAttemptView = {
  inflight: boolean;
  stored: StoredSaleAttempt | null;
  now: number;
};

export type SaleAttemptDecision =
  | { action: 'join' }
  | { action: 'block'; reason: 'other-tab'; stored: StoredSaleAttempt }
  | { action: 'lookup'; stored: StoredSaleAttempt }
  | { action: 'send'; key: string; stored: StoredSaleAttempt };

export function salePayloadFingerprint(value: unknown): string {
  return JSON.stringify(value);
}

export function parseStoredSaleAttempt(raw: string | null): StoredSaleAttempt | null {
  if (!raw) return null;
  try {
    const value = JSON.parse(raw) as Partial<StoredSaleAttempt>;
    if (typeof value.key !== 'string' || value.key.length < 1 || value.key.length > 64) return null;
    if (typeof value.fingerprint !== 'string') return null;
    if (value.state !== 'claimed' && value.state !== 'unknown') return null;
    if (typeof value.updatedAt !== 'number') return null;
    return {
      key: value.key,
      fingerprint: value.fingerprint,
      state: value.state,
      updatedAt: value.updatedAt,
      sentAt: typeof value.sentAt === 'number' ? value.sentAt : null,
    };
  } catch {
    return null;
  }
}

export function readSaleAttempt(storage: Pick<Storage, 'getItem'> | null): StoredSaleAttempt | null {
  if (!storage) return null;
  try {
    return parseStoredSaleAttempt(storage.getItem(SALE_ATTEMPT_STORAGE_KEY));
  } catch {
    return null;
  }
}

export function writeSaleAttempt(
  storage: Pick<Storage, 'setItem' | 'removeItem'> | null,
  attempt: StoredSaleAttempt | null,
): void {
  if (!storage) return;
  try {
    if (!attempt) storage.removeItem(SALE_ATTEMPT_STORAGE_KEY);
    else storage.setItem(SALE_ATTEMPT_STORAGE_KEY, JSON.stringify(attempt));
  } catch {
    // Private mode or a full quota must not create a second sale by itself.
  }
}

export function decideSaleAttempt(
  view: SaleAttemptView,
  fingerprint: string,
  mintKey: () => string,
): SaleAttemptDecision {
  if (view.inflight) return { action: 'join' };

  const stored = view.stored;
  const staleClaim = stored
    && stored.state === 'claimed'
    && stored.fingerprint !== fingerprint
    && view.now - stored.updatedAt >= SALE_ATTEMPT_UNKNOWN_WINDOW_MS;

  if (!stored || staleClaim) {
    const key = mintKey();
    const created: StoredSaleAttempt = {
      key,
      fingerprint,
      state: 'claimed',
      updatedAt: view.now,
      sentAt: null,
    };
    return { action: 'send', key, stored: created };
  }

  if (stored.fingerprint === fingerprint) {
    return {
      action: 'send',
      key: stored.key,
      stored: { ...stored, updatedAt: view.now },
    };
  }

  if (stored.state === 'unknown') return { action: 'lookup', stored };

  return { action: 'block', reason: 'other-tab', stored };
}

export function markAttemptSent(stored: StoredSaleAttempt, now: number): StoredSaleAttempt {
  return {
    ...stored,
    state: 'unknown',
    updatedAt: now,
    sentAt: stored.sentAt ?? now,
  };
}

export type SaleLookupOutcome = 'resolved-found' | 'resolved-absent' | 'still-unknown';

export function outcomeAfterLookup(args: {
  found: boolean;
  sentAt: number | null;
  now: number;
  lookupFailed: boolean;
}): SaleLookupOutcome {
  if (args.lookupFailed) return 'still-unknown';
  if (args.found) return 'resolved-found';
  const sentAt = args.sentAt ?? args.now;
  if (args.now - sentAt >= SALE_ATTEMPT_UNKNOWN_WINDOW_MS) return 'resolved-absent';
  return 'still-unknown';
}

export function retainSaleAttemptOnCancel(current: {
  inflight: boolean;
  stored: StoredSaleAttempt | null;
} | null): boolean {
  if (!current) return false;
  return current.inflight || current.stored?.state === 'unknown';
}

/** 400/422 means this request did not commit a sale. Timeouts and 5xx stay unknown. */
export function isDefiniteSaleRejection(status: number | undefined): boolean {
  return status === 400 || status === 422;
}
