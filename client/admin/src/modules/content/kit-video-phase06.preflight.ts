/** Phase 06 live preflight — 0 credit. Does not POST image_to_video unless KIT_VIDEO_RUNWAY_CONFIRM=1. */
import { famixaGoldenMotion } from './kit-video-motion';

const API = process.env.KIT_API_URL || 'http://localhost:5290/api';
const TENANT = process.env.KIT_MKT_TENANT || 'KIT_MKT';
const USER = process.env.KIT_MKT_USER || 'admin';
const PASS = process.env.KIT_MKT_PASSWORD || process.env.KIT_ADMIN_PASSWORD || 'Admin@123';
const ATTEMPT_01 = process.env.KIT_VIDEO_ATTEMPT_01 || '6036d11b-35de-43f1-88e4-f0138365016d';
const CONFIRM = process.env.KIT_VIDEO_RUNWAY_CONFIRM === '1';

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

async function main() {
  const loginRes = await fetch(`${API}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: USER, password: PASS, tenantCode: TENANT }),
  });
  const loginJson = (await loginRes.json()) as { accessToken?: string };
  if (!loginRes.ok || !loginJson.accessToken) throw new Error('login failed');
  const token = loginJson.accessToken;

  const i2v = await json<{ ready: boolean; blocked: string[]; imageType?: string; sourceArtifactHash?: string }>(
    `/content/video-engine/visual/i2v-ready?attemptId=${ATTEMPT_01}`,
    token,
  );
  ok(i2v.body.ready === true, 'attempt-01 I2V_READY');
  ok(i2v.body.imageType === 'PRODUCTION_STILL', 'attempt-01 still');

  const blocked02 = await json<{ message?: string }>('/content/video-engine/motion/preflight', token, {
    method: 'POST',
    body: JSON.stringify({
      keyframeAttemptId: ATTEMPT_01,
      motionContract: { ...famixaGoldenMotion(), shotCode: 'SH01-02' },
      confirmed: false,
      idempotencyKey: 'phase06-block-sh0102',
    }),
  });
  ok(blocked02.status === 409 || /GOLDEN_ONLY/i.test(JSON.stringify(blocked02.body)), 'SH01-02 blocked');

  const pre = await json<{
    status: string;
    runwayCalled: boolean;
    videoReady: boolean;
    preflight?: { ok: boolean; blocked: string[] };
    prompt: string;
    creditState: string;
  }>('/content/video-engine/motion/preflight', token, {
    method: 'POST',
    body: JSON.stringify({
      keyframeAttemptId: ATTEMPT_01,
      motionContract: famixaGoldenMotion(),
      confirmed: false,
      idempotencyKey: `phase06-preflight-${Date.now()}`,
    }),
  });
  console.log('PREFLIGHT', pre.status, pre.body);
  ok(pre.status === 200, 'preflight http');
  ok(pre.body.runwayCalled === false, 'preflight did not call Runway');
  ok(pre.body.creditState === 'NONE', 'credit NONE');
  ok(!/Mẹ ơi|character sheet|\{/.test(pre.body.prompt || ''), 'prompt motion only');
  if (CONFIRM) {
    console.log('KIT_VIDEO_RUNWAY_CONFIRM=1 — submit is operator-owned; this script still stops at preflight.');
  } else {
    console.log('Runway not called. Set KIT_VIDEO_RUNWAY_CONFIRM=1 in UI Confirm only.');
  }

  if (fail.length) {
    console.error('KIT VIDEO ENGINE PHASE 06 PREFLIGHT FAIL');
    for (const f of fail) console.error(' -', f);
    process.exit(1);
  }
  console.log('KIT VIDEO ENGINE PHASE 06 PREFLIGHT PASS · 0 credit');
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
