import {
  SALE_ATTEMPT_UNKNOWN_WINDOW_MS,
  decideSaleAttempt,
  isDefiniteSaleRejection,
  markAttemptSent,
  outcomeAfterLookup,
  parseStoredSaleAttempt,
  readSaleAttempt,
  retainSaleAttemptOnCancel,
  salePayloadFingerprint,
  writeSaleAttempt,
  type StoredSaleAttempt,
} from './pos-sale-attempt';

function assert(cond: unknown, msg: string): asserts cond {
  if (!cond) throw new Error(msg);
}

const first = salePayloadFingerprint({ qty: 1, pay: 'cash' });
const changed = salePayloadFingerprint({ qty: 2, pay: 'cash' });
const now = 1_000_000;

const created = decideSaleAttempt({ inflight: false, stored: null, now }, first, () => 'new-key');
assert(created.action === 'send' && created.key === 'new-key', 'a new checkout mints one key');

const joined = decideSaleAttempt(
  { inflight: true, stored: created.action === 'send' ? created.stored : null, now },
  changed,
  () => 'other',
);
assert(joined.action === 'join', 'a second click joins the in-flight request');

const pending: StoredSaleAttempt = {
  key: 'k1',
  fingerprint: first,
  state: 'unknown',
  updatedAt: now,
  sentAt: now,
};
const retry = decideSaleAttempt({ inflight: false, stored: pending, now: now + 1000 }, first, () => 'other');
assert(retry.action === 'send' && retry.key === 'k1', 'an unknown result retries the same key');

const blocked = decideSaleAttempt({ inflight: false, stored: pending, now: now + 1000 }, changed, () => 'other');
assert(blocked.action === 'lookup', 'a changed cart looks up the old key before a new sale');

const otherTab: StoredSaleAttempt = {
  key: 'k2',
  fingerprint: first,
  state: 'claimed',
  updatedAt: now,
  sentAt: null,
};
const other = decideSaleAttempt({ inflight: false, stored: otherTab, now: now + 1000 }, changed, () => 'other');
assert(other.action === 'block', 'another open tab keeps the claim');

const stale = decideSaleAttempt(
  { inflight: false, stored: { ...otherTab, updatedAt: now - SALE_ATTEMPT_UNKNOWN_WINDOW_MS - 1 }, now },
  changed,
  () => 'fresh',
);
assert(stale.action === 'send' && stale.key === 'fresh', 'a claim that never left the browser can expire');

const shared = decideSaleAttempt({ inflight: false, stored: otherTab, now: now + 1000 }, first, () => 'other');
assert(shared.action === 'send' && shared.key === 'k2', 'a second tab reuses the key for the same payment');

assert(markAttemptSent(otherTab, now).state === 'unknown', 'sending marks the attempt unknown');
assert(
  outcomeAfterLookup({ found: true, sentAt: now, now: now + 1000, lookupFailed: false }) === 'resolved-found',
  'a committed sale resolves the attempt',
);
assert(
  outcomeAfterLookup({ found: false, sentAt: now, now: now + 1000, lookupFailed: false }) === 'still-unknown',
  'a recent miss can still be an in-flight commit',
);
assert(
  outcomeAfterLookup({
    found: false,
    sentAt: now,
    now: now + SALE_ATTEMPT_UNKNOWN_WINDOW_MS,
    lookupFailed: false,
  }) === 'resolved-absent',
  'an old miss means the sale was not committed',
);
assert(
  outcomeAfterLookup({ found: false, sentAt: now, now: now + 999999, lookupFailed: true }) === 'still-unknown',
  'a failed lookup does not clear the attempt',
);

assert(
  retainSaleAttemptOnCancel({ inflight: false, stored: pending }),
  'closing the modal keeps an unknown attempt',
);
assert(
  retainSaleAttemptOnCancel({ inflight: true, stored: null }),
  'closing the modal keeps an in-flight attempt',
);
assert(
  !retainSaleAttemptOnCancel({ inflight: false, stored: otherTab }),
  'closing the modal clears an attempt that never reached the server',
);

const memory = new Map<string, string>();
const storage = {
  getItem: (key: string) => memory.get(key) ?? null,
  setItem: (key: string, value: string) => { memory.set(key, value); },
  removeItem: (key: string) => { memory.delete(key); },
};
writeSaleAttempt(storage, pending);
assert(readSaleAttempt(storage)?.key === 'k1', 'the attempt survives a new page');
assert(parseStoredSaleAttempt('{') === null, 'corrupt storage is ignored');
writeSaleAttempt(storage, null);
assert(readSaleAttempt(storage) === null, 'a resolved attempt is removed');

assert(isDefiniteSaleRejection(400) && isDefiniteSaleRejection(422), 'client errors are definite');
assert(!isDefiniteSaleRejection(500) && !isDefiniteSaleRejection(undefined), 'timeout and 500 stay unknown');

console.log('pos-sale-attempt.smoke: ok');
