/**
 * Phase 05.1 live integrity — revalidate existing artifacts only.
 * Does not call generate. Does not call Runway.
 */
import { createHash } from 'node:crypto';
import { copyFileSync, readFileSync, writeFileSync } from 'node:fs';
import { famixaGoldenContract } from './kit-video-pixel';

const API = process.env.KIT_API_URL || 'http://localhost:5290/api';
const TENANT = process.env.KIT_MKT_TENANT || 'KIT_MKT';
const USER = process.env.KIT_MKT_USER || 'admin';
const PASS = process.env.KIT_MKT_PASSWORD || process.env.KIT_ADMIN_PASSWORD || 'Admin@123';
const ATTEMPT_01 = process.env.KIT_VIDEO_ATTEMPT_01 || '6036d11b-35de-43f1-88e4-f0138365016d';
const ATTEMPT_02 = process.env.KIT_VIDEO_ATTEMPT_02 || 'ea1b91bb-a5ce-4280-a5ea-575f3fa0a7a3';
const ARTIFACT_01 =
  process.env.KIT_VIDEO_ARTIFACT_01 ||
  'E:/KitPlatform/src/KitPlatform.Api/App_Data/kit-video-kf/7ce4fe9f948d42308cfca9f5c3e89bf3/SH01-01/attempt-01.jpg';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

type Pixel = {
  attemptId: string;
  status: string;
  persistStatus?: string;
  artifactHash?: string;
  imageType?: string;
  qa?: { status: string; p0Fail?: string[]; artifactHash?: string; imageType?: string };
  i2v?: { ready: boolean; blocked: string[]; sourceArtifactHash?: string; imageType?: string };
  runwayCalled?: boolean;
};

async function json<T>(path: string, token: string, init?: RequestInit): Promise<{ status: number; body: T }> {
  const res = await fetch(`${API}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      ...(init?.headers || {}),
    },
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
  const contract = famixaGoldenContract();

  const img01 = await fetch(`${API}/content/video-engine/visual/attempts/${ATTEMPT_01}/image`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  ok(img01.ok, 'attempt-01 artifact still readable — no regenerate');

  const re01 = await json<Pixel>('/content/video-engine/visual/revalidate', token, {
    method: 'POST',
    body: JSON.stringify({ attemptId: ATTEMPT_01, contract }),
  });
  console.log('REVALIDATE 01', re01.status, re01.body.status, re01.body.imageType, re01.body.qa?.status, re01.body.qa?.p0Fail);
  ok(re01.status === 200, 'revalidate 01 http');
  ok(re01.body.runwayCalled !== true, 'revalidate 01 runway false');
  ok(re01.body.status === 'VISION_PASS' && re01.body.qa?.status === 'PASS', 'attempt-01 VISION_PASS');
  ok(re01.body.imageType === 'PRODUCTION_STILL', 'attempt-01 PRODUCTION_STILL');
  ok(re01.body.persistStatus !== 'PERSISTENCE_FAILED', 'attempt-01 persist ok after revalidate');

  let approved01 = re01.body;
  if (re01.body.status === 'VISION_PASS') {
    const dec = await json<Pixel>('/content/video-engine/visual/decide', token, {
      method: 'POST',
      body: JSON.stringify({
        productionId: '00000000-0000-0000-0000-000000000000',
        shotCode: 'SH01-01',
        attemptId: ATTEMPT_01,
        decision: 'APPROVE',
      }),
    });
    console.log('APPROVE 01', dec.status, dec.body.status, dec.body.i2v);
    ok(dec.status === 200 && dec.body.status === 'APPROVED', 'director approve attempt-01');
    approved01 = dec.body;
    ok(dec.body.i2v?.ready === true, 'attempt-01 I2V_READY');
    ok(
      Boolean(dec.body.artifactHash) &&
        dec.body.artifactHash === dec.body.qa?.artifactHash &&
        dec.body.artifactHash === dec.body.i2v?.sourceArtifactHash,
      'attempt-01 file/QA/I2V hashes match',
    );
  }

  const re02 = await json<Pixel>('/content/video-engine/visual/revalidate', token, {
    method: 'POST',
    body: JSON.stringify({ attemptId: ATTEMPT_02, contract }),
  });
  console.log('REVALIDATE 02', re02.status, re02.body.status, re02.body.imageType, re02.body.qa?.p0Fail, re02.body.i2v);
  ok(re02.status === 200, 'revalidate 02 http');
  ok(re02.body.status === 'VISION_FAIL' || (re02.body.qa?.p0Fail || []).some((x) => /IMAGE_TYPE|CHARACTER_SHEET/i.test(x)), 'attempt-02 VISION FAIL');
  ok(re02.body.imageType !== 'PRODUCTION_STILL' || (re02.body.qa?.p0Fail || []).some((x) => /IMAGE_TYPE|CHARACTER_SHEET/i.test(x)), 'attempt-02 not a still');
  ok(re02.body.i2v?.ready !== true, 'attempt-02 I2V_READY = FALSE');
  ok(re02.body.status !== 'APPROVED', 'attempt-02 APPROVED revoked');

  const ready02 = await json<{ ready: boolean; blocked: string[] }>(
    `/content/video-engine/visual/i2v-ready?attemptId=${ATTEMPT_02}`,
    token,
  );
  ok(ready02.body.ready !== true, 'i2v-ready attempt-02 false');

  const original = readFileSync(ARTIFACT_01);
  const backup = `${ARTIFACT_01}.bak-integrity`;
  copyFileSync(ARTIFACT_01, backup);
  try {
    writeFileSync(ARTIFACT_01, Buffer.concat([original, Buffer.from('TAMPER')]));
    const tamper = await json<{ ready: boolean; blocked: string[]; message?: string }>(
      `/content/video-engine/visual/i2v-ready?attemptId=${ATTEMPT_01}`,
      token,
    );
    console.log('TAMPER FILE', tamper.status, tamper.body);
    ok(tamper.body.ready !== true, 'modified artifact after Vision PASS → I2V BLOCK');
    writeFileSync(ARTIFACT_01, original);
    const restored = await json<{ ready: boolean }>(`/content/video-engine/visual/i2v-ready?attemptId=${ATTEMPT_01}`, token);
    ok(approved01.i2v?.ready === true ? restored.body.ready === true : true, 'restore artifact re-opens I2V only if still approved');
  } finally {
    writeFileSync(ARTIFACT_01, original);
  }

  if (fail.length) {
    console.error('KIT VIDEO ENGINE PHASE 05.1 LIVE FAIL');
    for (const f of fail) console.error(' -', f);
    process.exit(1);
  }
  console.log('KIT VIDEO ENGINE PHASE 05.1 LIVE PASS');
  console.log('RUNWAY CALLED = FALSE');
  console.log(JSON.stringify({
    attempt01: { id: ATTEMPT_01, hash: createHash('sha256').update(original).digest('hex') },
    attempt02: ATTEMPT_02,
  }));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
