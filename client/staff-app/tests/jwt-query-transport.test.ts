import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  authorizedSseRequest,
  consumeSseBuffer,
  nextSseRetryMs,
  SSE_RETRY_START_MS,
  STAFF_CHAT_EVENTS_PATH,
  subscribeAuthorizedSse,
} from '../src/shared/utils/bearer-transport.ts';

const token = 'jwt-staff-test';

test('staff chat SSE sends bearer and omits access_token', async () => {
  const calls: Array<{ url: string; init: RequestInit }> = [];
  let events = 0;
  const timers: number[] = [];
  const encoder = new TextEncoder();
  const stop = subscribeAuthorizedSse(
    STAFF_CHAT_EVENTS_PATH,
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
  assert.equal(calls[0].url, STAFF_CHAT_EVENTS_PATH);
  assert.equal(calls[0].url.includes('access_token'), false);
  assert.equal((calls[0].init.headers as { Authorization: string }).Authorization, `Bearer ${token}`);
  assert.deepEqual(timers, [SSE_RETRY_START_MS]);
  assert.equal(nextSseRetryMs(SSE_RETRY_START_MS), 2000);
});

test('staff SSE parser keeps data events and ignores keepalive', () => {
  const seen: string[] = [];
  consumeSseBuffer(': keepalive\n\ndata: {"n":1}\n\n', (data) => seen.push(data));
  assert.deepEqual(seen, ['{"n":1}']);
  const request = authorizedSseRequest(`${STAFF_CHAT_EVENTS_PATH}?access_token=secret`, token);
  assert.equal(request.url, STAFF_CHAT_EVENTS_PATH);
  assert.equal(request.headers.Authorization, `Bearer ${token}`);
});

test('staff http client still uses the Authorization header', () => {
  const http = readFileSync(new URL('../src/shared/api/http.ts', import.meta.url), 'utf8');
  assert.match(http, /Authorization = `Bearer \$\{token\}`/);
  assert.equal(http.includes('access_token'), false);
});
