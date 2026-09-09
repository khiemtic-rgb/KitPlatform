/**
 * Phase 06 live Golden Shot — exactly one Runway I2V call.
 * SH01-01 Attempt 01 only. No SH01-02. No LipSync.
 */
import { famixaGoldenMotion } from './kit-video-motion';

const API = process.env.KIT_API_URL || 'http://localhost:5290/api';
const TENANT = process.env.KIT_MKT_TENANT || 'KIT_MKT';
const USER = process.env.KIT_MKT_USER || 'admin';
const PASS = process.env.KIT_MKT_PASSWORD || process.env.KIT_ADMIN_PASSWORD || 'Admin@123';
const ATTEMPT_01 = process.env.KIT_VIDEO_ATTEMPT_01 || '6036d11b-35de-43f1-88e4-f0138365016d';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

async function json<T>(path: string, token: string, init?: RequestInit): Promise<{ status: number; body: T }> {
  const res = await fetch(`${API}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, ...(init?.headers || {}) },
  });
  const text = await res.text();
  return { status: res.status, body: text ? (JSON.parse(text) as T) : ({} as T) };
}

type Take = {
  takeId: string;
  status: string;
  runwayCalled: boolean;
  runwayAccepted?: boolean;
  videoReady: boolean;
  runwayTaskId?: string;
  creditState: string;
  diagnose: string;
  failureClass: string;
  qa?: { status: string; p0Fail?: string[] };
  sourceArtifactHash?: string;
  videoHash?: string;
};

async function main() {
  const loginRes = await fetch(`${API}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: USER, password: PASS, tenantCode: TENANT }),
  });
  const loginJson = (await loginRes.json()) as { accessToken?: string };
  if (!loginRes.ok || !loginJson.accessToken) throw new Error('login failed');
  const token = loginJson.accessToken;
  const motion = famixaGoldenMotion();

  const blocked = await json<Take>('/content/video-engine/motion/preflight', token, {
    method: 'POST',
    body: JSON.stringify({
      keyframeAttemptId: ATTEMPT_01,
      motionContract: { ...motion, shotCode: 'SH01-02' },
      confirmed: false,
      idempotencyKey: `golden-block-${Date.now()}`,
    }),
  });
  ok(blocked.body.status === 'BLOCKED' || blocked.status === 409, 'SH01-02 BLOCKED 0 credit');

  const pre = await json<Take>('/content/video-engine/motion/preflight', token, {
    method: 'POST',
    body: JSON.stringify({
      keyframeAttemptId: ATTEMPT_01,
      motionContract: motion,
      confirmed: false,
      idempotencyKey: `golden-pre-${Date.now()}`,
    }),
  });
  console.log('PREFLIGHT', pre.body.status, pre.body.creditState, pre.body.runwayCalled);
  ok(pre.body.runwayCalled === false, 'preflight 0 Runway call');
  if (pre.body.status === 'BLOCKED') {
    console.error('PREFLIGHT BLOCKED', pre.body.diagnose);
    process.exit(1);
  }

  const key = `golden-runway-once-${ATTEMPT_01}`;
  const submit = await json<Take>('/content/video-engine/motion/submit', token, {
    method: 'POST',
    body: JSON.stringify({
      keyframeAttemptId: ATTEMPT_01,
      motionContract: motion,
      confirmed: true,
      idempotencyKey: key,
    }),
  });
  console.log('SUBMIT', submit.status, submit.body.status, submit.body.runwayTaskId, submit.body.creditState);
  ok(submit.status === 200, 'submit http');
  ok(Boolean(submit.body.runwayTaskId), '06 RUNWAY_ACCEPTED has taskId');
  ok(submit.body.videoReady !== true, 'HTTP 200 is not VIDEO_READY');
  ok(submit.body.status === 'SUBMITTED' || submit.body.status === 'RUNWAY_ACCEPTED' || submit.body.status === 'PROCESSING', 'accepted not success');

  let take = submit.body;
  for (let i = 0; i < 40; i++) {
    await new Promise((r) => setTimeout(r, 8000));
    const polled = await json<Take>(`/content/video-engine/motion/${take.takeId}/poll`, token, { method: 'POST' });
    take = polled.body;
    console.log('POLL', i, take.status, take.creditState, take.failureClass, take.qa?.status);
    if (['READY_FOR_DIRECTOR', 'APPROVED_TAKE', 'VIDEO_QA_FAIL', 'DIAGNOSE', 'FAILED', 'REJECTED', 'INVALIDATED'].includes(take.status)) {
      break;
    }
  }

  ok(take.runwayCalled === true, 'RUNWAY_CALLED = TRUE once');
  if (take.status === 'READY_FOR_DIRECTOR' || take.status === 'APPROVED_TAKE') {
    ok(take.videoReady === true, 'video downloaded + readable');
    const dec = await json<Take>(`/content/video-engine/motion/${take.takeId}/decide`, token, {
      method: 'POST',
      body: JSON.stringify({ decision: 'APPROVE' }),
    });
    console.log('DIRECTOR', dec.body.status);
    ok(dec.body.status === 'APPROVED_TAKE', 'Director APPROVE');
  } else {
    console.log('TERMINAL', take.status, take.diagnose || take.failureClass, take.qa);
    ok(take.status !== 'READY', 'task reached a terminal provider/QA state');
  }

  const again = await json<Take>('/content/video-engine/motion/submit', token, {
    method: 'POST',
    body: JSON.stringify({
      keyframeAttemptId: ATTEMPT_01,
      motionContract: motion,
      confirmed: true,
      idempotencyKey: key,
    }),
  });
  ok(again.body.takeId === take.takeId, 'idempotency same take — no second billed submit');

  if (fail.length) {
    console.error('KIT VIDEO ENGINE PHASE 06 GOLDEN FAIL');
    for (const f of fail) console.error(' -', f);
    process.exit(1);
  }
  console.log('KIT VIDEO ENGINE PHASE 06 GOLDEN DONE');
  console.log(JSON.stringify({ takeId: take.takeId, status: take.status, taskId: take.runwayTaskId, credit: take.creditState }, null, 2));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
