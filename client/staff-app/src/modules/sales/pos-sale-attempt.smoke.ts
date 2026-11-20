import {
  SALE_ATTEMPT_STORAGE_KEY,
  SALE_ATTEMPT_UNKNOWN_WINDOW_MS,
  SaleAttemptBlockedError,
  SaleAttemptPendingError,
  decideSaleAttempt,
  isDefiniteSaleRejection,
  markAttemptSent,
  outcomeAfterLookup,
  parseStoredSaleAttempt,
  readSaleAttempt,
  salePayloadFingerprint,
  settleCompletedSale,
  writeSaleAttempt,
  type StoredSaleAttempt,
} from './pos-sale-attempt.ts';

function assert(cond: unknown, msg: string): asserts cond {
  if (!cond) throw new Error(msg);
}

const adminPosAttemptKey: string = 'kitplatform.pos.saleAttempt.v1';
assert(
  SALE_ATTEMPT_STORAGE_KEY === 'kitplatform.staff.pos.saleAttempt.v1',
  'staff checkout keeps its own attempt key',
);
assert(
  SALE_ATTEMPT_STORAGE_KEY !== adminPosAttemptKey,
  'staff checkout does not share the admin POS attempt key',
);

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
assert(isDefiniteSaleRejection(400) && isDefiniteSaleRejection(422), 'client errors are definite');
assert(!isDefiniteSaleRejection(500) && !isDefiniteSaleRejection(undefined), 'timeout and 500 stay unknown');

function memoryStorage() {
  const memory = new Map<string, string>();
  return {
    getItem: (key: string) => memory.get(key) ?? null,
    setItem: (key: string, value: string) => { memory.set(key, value); },
    removeItem: (key: string) => { memory.delete(key); },
  };
}

const storage = memoryStorage();
writeSaleAttempt(storage, pending);
assert(readSaleAttempt(storage)?.key === 'k1', 'the attempt survives a new page');
assert(parseStoredSaleAttempt('{') === null, 'corrupt storage is ignored');
writeSaleAttempt(storage, null);
assert(readSaleAttempt(storage) === null, 'a resolved attempt is removed');

const clock = { value: 5_000 };
const keys: string[] = [];
const sent: string[] = [];
const firstOrder = await settleCompletedSale({
  storage,
  fingerprint: first,
  now: () => clock.value,
  mintKey: () => 'sale-1',
  lookup: async () => null,
  send: async (key) => {
    sent.push(key);
    return { id: 'order-1', key };
  },
});
assert(firstOrder.kind === 'sent' && firstOrder.order.id === 'order-1' && sent.length === 1 && sent[0] === 'sale-1', 'the first checkout sends one key');
assert(readSaleAttempt(storage) === null, 'a successful checkout clears the attempt');

clock.value += 1000;
writeSaleAttempt(storage, {
  key: 'sale-1',
  fingerprint: first,
  state: 'unknown',
  updatedAt: clock.value,
  sentAt: clock.value,
});
const recovered = await settleCompletedSale({
  storage,
  fingerprint: changed,
  now: () => clock.value + SALE_ATTEMPT_UNKNOWN_WINDOW_MS,
  mintKey: () => 'sale-2',
  lookup: async (key) => {
    keys.push(key);
    return { id: 'order-1', key };
  },
  send: async () => {
    throw new Error('must not send');
  },
});
assert(
  recovered.kind === 'already-recorded' && recovered.order.id === 'order-1' && keys[0] === 'sale-1' && sent.length === 1,
  'a found sale from another cart is not created again and is not this checkout',
);

const recent = memoryStorage();
writeSaleAttempt(recent, {
  key: 'sale-9',
  fingerprint: first,
  state: 'unknown',
  updatedAt: 10,
  sentAt: 10,
});
let pendingThrown = false;
try {
  await settleCompletedSale({
    storage: recent,
    fingerprint: changed,
    now: () => 11,
    mintKey: () => 'sale-new',
    lookup: async () => null,
    send: async () => {
      throw new Error('must not send');
    },
  });
} catch (error) {
  pendingThrown = error instanceof SaleAttemptPendingError;
}
assert(pendingThrown, 'a recent miss does not start another sale');
assert(readSaleAttempt(recent)?.key === 'sale-9', 'the unknown attempt stays until it can be checked');

const rejected = memoryStorage();
let rejectedThrown = false;
try {
  await settleCompletedSale({
    storage: rejected,
    fingerprint: first,
    now: () => 20,
    mintKey: () => 'sale-reject',
    lookup: async () => null,
    send: async () => {
      const error = new Error('bad request') as Error & { response?: { status: number } };
      error.response = { status: 400 };
      throw error;
    },
  });
} catch {
  rejectedThrown = true;
}
assert(rejectedThrown && readSaleAttempt(rejected) === null, 'a 400 clears the key so the cart can be corrected');

const held = memoryStorage();
writeSaleAttempt(held, {
  key: 'held',
  fingerprint: first,
  state: 'claimed',
  updatedAt: 30,
  sentAt: null,
});
let blockedThrown = false;
try {
  await settleCompletedSale({
    storage: held,
    fingerprint: changed,
    now: () => 31,
    mintKey: () => 'other',
    lookup: async () => null,
    send: async () => ({ id: 'nope' }),
  });
} catch (error) {
  blockedThrown = error instanceof SaleAttemptBlockedError;
}
assert(blockedThrown, 'a live claim on another cart does not create a sale');

console.log('staff pos-sale-attempt.smoke: ok');
