import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  ADMIN_CHAT_EVENTS_PATH,
  ADMIN_DRAFT_ORDER_EVENTS_PATH,
  adminResourceRequest,
  authorizedSseRequest,
  consumeSseBuffer,
  nextSseRetryMs,
  SSE_RETRY_START_MS,
  subscribeAuthorizedSse,
} from '../src/shared/utils/bearer-transport.ts';

const token = 'jwt-admin-test';

test('admin SSE request sends bearer and drops access_token', async () => {
  const calls: Array<{ url: string; init: RequestInit }> = [];
  let events = 0;
  const timers: number[] = [];
  const encoder = new TextEncoder();
  const stop = subscribeAuthorizedSse(
    `${ADMIN_CHAT_EVENTS_PATH}?access_token=${token}`,
    token,
    () => {
      events += 1;
    },
    {
      fetch: async (url, init) => {
        calls.push({ url, init });
        const body = new ReadableStream({
          start(controller) {
            controller.enqueue(encoder.encode('data: {"kind":"chat"}\n\n: keepalive\n\n'));
            controller.close();
          },
        });
        return new Response(body, { status: 200 });
      },
      setTimeout: (fn, ms) => {
        timers.push(ms);
        return timers.length;
      },
      clearTimeout: () => undefined,
    },
  );

  await new Promise((resolve) => setTimeout(resolve, 30));
  stop();

  assert.equal(events, 1);
  assert.equal(calls.length, 1);
  assert.equal(calls[0].url, ADMIN_CHAT_EVENTS_PATH);
  assert.equal(calls[0].url.includes('access_token'), false);
  const headers = calls[0].init.headers as { Authorization: string };
  assert.equal(headers.Authorization, `Bearer ${token}`);
  assert.equal(calls[0].init.credentials, 'same-origin');
  assert.deepEqual(timers, [SSE_RETRY_START_MS]);
});

test('admin SSE parser ignores keepalive and named events', () => {
  const seen: string[] = [];
  const rest = consumeSseBuffer(
    'data: {"a":1}\n\n: keepalive\n\nevent: ping\ndata: skip\n\ndata: {"b":2}\n\npartial',
    (data) => seen.push(data),
  );
  assert.deepEqual(seen, ['{"a":1}', '{"b":2}']);
  assert.equal(rest, 'partial');
});

test('admin SSE reconnect delay doubles and caps at 30s', () => {
  assert.equal(nextSseRetryMs(SSE_RETRY_START_MS), 2000);
  assert.equal(nextSseRetryMs(16_000), 30_000);
  assert.equal(nextSseRetryMs(30_000), 30_000);
  assert.equal(ADMIN_DRAFT_ORDER_EVENTS_PATH.includes('access_token'), false);
  const request = authorizedSseRequest(ADMIN_DRAFT_ORDER_EVENTS_PATH, token);
  assert.equal(request.headers.Authorization, `Bearer ${token}`);
  assert.equal(request.url.includes('access_token'), false);
});

test('admin product and health-record fetches use bearer; branding stays public', () => {
  const product = adminResourceRequest(
    'https://api.novixa.vn/uploads/products/abc/photo.jpg?access_token=secret',
    token,
  );
  assert.equal(product.authenticated, true);
  assert.equal(product.headers?.Authorization, `Bearer ${token}`);
  assert.equal(product.url.includes('access_token'), false);

  const record = adminResourceRequest('/uploads/health-records/t/a/file.pdf', token);
  assert.equal(record.authenticated, true);
  assert.equal(record.headers?.Authorization, `Bearer ${token}`);

  const branding = adminResourceRequest('/uploads/branding/tenant/logo.png', token);
  assert.equal(branding.authenticated, false);
  assert.equal(branding.headers, undefined);
  assert.equal(branding.url.includes('access_token'), false);
});

test('admin http client still uses the Authorization header', () => {
  const http = readFileSync(new URL('../src/shared/api/http.ts', import.meta.url), 'utf8');
  assert.match(http, /Authorization = `Bearer \$\{token\}`/);
  assert.equal(http.includes('access_token'), false);
});
