import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  authorizedSseRequest,
  consumeSseBuffer,
  CUSTOMER_CHAT_EVENTS_PATH,
  CUSTOMER_DRAFT_ORDER_EVENTS_PATH,
  customerUploadRequest,
  nextSseRetryMs,
  SSE_RETRY_START_MS,
  subscribeAuthorizedSse,
} from '../src/shared/utils/bearer-transport.ts';

const token = 'jwt-customer-test';

test('customer SSE sends bearer and omits access_token', async () => {
  const calls: Array<{ url: string; init: RequestInit }> = [];
  let events = 0;
  const timers: number[] = [];
  const encoder = new TextEncoder();
  const stop = subscribeAuthorizedSse(
    CUSTOMER_CHAT_EVENTS_PATH,
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
      setTimeout: (_fn, ms) => {
        timers.push(ms);
        return timers.length;
      },
      clearTimeout: () => undefined,
    },
  );

  await new Promise((resolve) => setTimeout(resolve, 30));
  stop();

  assert.equal(events, 1);
  assert.equal(calls[0].url, CUSTOMER_CHAT_EVENTS_PATH);
  assert.equal(calls[0].url.includes('access_token'), false);
  assert.equal((calls[0].init.headers as { Authorization: string }).Authorization, `Bearer ${token}`);
  assert.deepEqual(timers, [SSE_RETRY_START_MS]);
  assert.equal(nextSseRetryMs(1000), 2000);
});

test('customer draft-order SSE URL has no access_token', () => {
  const request = authorizedSseRequest(CUSTOMER_DRAFT_ORDER_EVENTS_PATH, token);
  assert.equal(request.url.includes('access_token'), false);
  assert.equal(request.headers.Authorization, `Bearer ${token}`);
  const seen: string[] = [];
  consumeSseBuffer('data: {"draft":1}\n\n', (data) => seen.push(data));
  assert.deepEqual(seen, ['{"draft":1}']);
});

test('customer avatar and health-record fetches use bearer; branding stays public', () => {
  const avatar = customerUploadRequest('/uploads/avatars/t/a/face.jpg?access_token=secret', token);
  assert.equal(avatar.authenticated, true);
  assert.equal(avatar.headers?.Authorization, `Bearer ${token}`);
  assert.equal(avatar.url.includes('access_token'), false);

  const record = customerUploadRequest('/uploads/health-records/t/a/note.pdf', token);
  assert.equal(record.authenticated, true);
  assert.equal(record.url.includes('access_token'), false);
  assert.equal(record.headers?.Authorization, `Bearer ${token}`);

  const branding = customerUploadRequest('/uploads/branding/t/logo.png', token);
  assert.equal(branding.authenticated, false);
  assert.equal(branding.headers, undefined);
});

test('customer http client still uses the Authorization header', () => {
  const http = readFileSync(new URL('../src/shared/api/http.ts', import.meta.url), 'utf8');
  assert.match(http, /Authorization = `Bearer \$\{token\}`/);
  assert.equal(http.includes('access_token'), false);
});
