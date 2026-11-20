export const SALE_ATTEMPT_STORAGE_KEY = 'kitplatform.staff.pos.saleAttempt.v1';

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

export class SaleAttemptPendingError extends Error {
  constructor() {
    super('sale-attempt-pending');
    this.name = 'SaleAttemptPendingError';
  }
}

export class SaleAttemptBlockedError extends Error {
  constructor() {
    super('sale-attempt-blocked');
    this.name = 'SaleAttemptBlockedError';
  }
}

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

/** 400/422 means this request did not commit a sale. Timeouts and 5xx stay unknown. */
export function isDefiniteSaleRejection(status: number | undefined): boolean {
  return status === 400 || status === 422;
}

type AttemptStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'> | null;

function httpStatus(error: unknown): number | undefined {
  return (error as { response?: { status?: number } })?.response?.status;
}

export type SettledSale<T> =
  | { kind: 'sent'; order: T }
  | { kind: 'already-recorded'; order: T };

type PreparedSale<T> =
  | { kind: 'already-recorded'; order: T }
  | { kind: 'send'; key: string; sent: StoredSaleAttempt };

function runStaffSaleLock<T>(work: () => Promise<T>): Promise<T> {
  const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined;
  if (!locks) return work();
  return locks.request('kitplatform-staff-pos-sale', work);
}

async function prepareCompletedSale<T>(args: {
  storage: AttemptStorage;
  fingerprint: string;
  now: () => number;
  mintKey: () => string;
  lookup: (key: string) => Promise<T | null>;
}): Promise<PreparedSale<T>> {
  let stored = readSaleAttempt(args.storage);
  let decision = decideSaleAttempt(
    { inflight: false, stored, now: args.now() },
    args.fingerprint,
    args.mintKey,
  );

  if (decision.action === 'lookup') {
    let found: T | null = null;
    let failed = false;
    try {
      found = await args.lookup(decision.stored.key);
    } catch {
      failed = true;
    }
    const outcome = outcomeAfterLookup({
      found: found != null,
      sentAt: decision.stored.sentAt,
      now: args.now(),
      lookupFailed: failed,
    });
    if (outcome === 'still-unknown') throw new SaleAttemptPendingError();
    if (outcome === 'resolved-found' && found) {
      writeSaleAttempt(args.storage, null);
      return { kind: 'already-recorded', order: found };
    }
    writeSaleAttempt(args.storage, null);
    decision = decideSaleAttempt(
      { inflight: false, stored: null, now: args.now() },
      args.fingerprint,
      args.mintKey,
    );
  }

  if (decision.action === 'block') throw new SaleAttemptBlockedError();
  if (decision.action !== 'send') throw new SaleAttemptPendingError();

  const sent = markAttemptSent(decision.stored, args.now());
  writeSaleAttempt(args.storage, sent);
  return { kind: 'send', key: decision.key, sent };
}

/**
 * One completed staff checkout. The same cart reuses one client sale id.
 * A different cart does not reuse that sale: if the earlier sale is found, it is
 * reported separately and is not treated as this checkout.
 * The browser lock only covers choosing and storing the key. The sale request
 * itself stays outside the lock so a second tab can reuse that key.
 */
export async function settleCompletedSale<T>(args: {
  storage: AttemptStorage;
  fingerprint: string;
  now: () => number;
  mintKey: () => string;
  lookup: (key: string) => Promise<T | null>;
  send: (key: string) => Promise<T>;
}): Promise<SettledSale<T>> {
  const prepared = await runStaffSaleLock(() => prepareCompletedSale(args));
  if (prepared.kind === 'already-recorded') return prepared;

  try {
    const order = await args.send(prepared.key);
    writeSaleAttempt(args.storage, null);
    return { kind: 'sent', order };
  } catch (error) {
    if (isDefiniteSaleRejection(httpStatus(error))) writeSaleAttempt(args.storage, null);
    else writeSaleAttempt(args.storage, prepared.sent);
    throw error;
  }
}
