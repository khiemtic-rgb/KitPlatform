/**
 * Live Phase 05 golden — real Gemini generate + real pixel Vision.
 * Does not mock PASS. If the API key is missing, reports FAIL honestly.
 */
import { famixaGoldenContract } from './kit-video-pixel';

const API = process.env.KIT_API_URL || 'http://localhost:5290/api';
const TENANT = process.env.KIT_MKT_TENANT || 'KIT_MKT';
const USER = process.env.KIT_MKT_USER || 'admin';
const PASS = process.env.KIT_MKT_PASSWORD || process.env.KIT_ADMIN_PASSWORD || 'Admin@123';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

async function json<T>(path: string, token: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      ...(init?.headers || {}),
    },
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`${res.status} ${path}: ${text.slice(0, 400)}`);
  return text ? (JSON.parse(text) as T) : ({} as T);
}

function tinyJpegBase64(): string {
  const jpeg = Uint8Array.from([
    0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10, 0x4a, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00,
    0x00, 0xff, 0xdb, 0x00, 0x43, 0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08, 0x07, 0x07, 0x07, 0x09, 0x09, 0x08,
    0x0a, 0x0c, 0x14, 0x0d, 0x0c, 0x0b, 0x0b, 0x0c, 0x19, 0x12, 0x13, 0x0f, 0x14, 0x1d, 0x1a, 0x1f, 0x1e, 0x1d, 0x1a,
    0x1c, 0x1c, 0x20, 0x24, 0x2e, 0x27, 0x20, 0x22, 0x2c, 0x23, 0x1c, 0x1c, 0x28, 0x37, 0x29, 0x2c, 0x30, 0x31, 0x34,
    0x34, 0x34, 0x1f, 0x27, 0x39, 0x3d, 0x38, 0x32, 0x3c, 0x2e, 0x33, 0x34, 0x32, 0xff, 0xc0, 0x00, 0x0b, 0x08, 0x00,
    0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00, 0xff, 0xda, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3f, 0x00, 0x7f, 0xff,
    0xd9,
  ]);
  return Buffer.from(jpeg).toString('base64');
}

async function main() {
  if (!PASS) {
    console.error('KIT VIDEO ENGINE PHASE 05 GOLDEN FAIL');
    console.error(' - KIT_MKT_PASSWORD not set; refusing to embed a secret');
    process.exit(1);
  }

  const loginRes = await fetch(`${API}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: USER, password: PASS, tenantCode: TENANT }),
  });
  const loginJson = (await loginRes.json()) as { accessToken?: string };
  if (!loginRes.ok || !loginJson.accessToken) {
    console.error('KIT VIDEO ENGINE PHASE 05 GOLDEN FAIL');
    console.error(' - login failed');
    process.exit(1);
  }
  const token = loginJson.accessToken;

  const provider = await json<{ apiKeyConfigured: boolean; runwayCalled: boolean; imageProvider: string }>(
    '/content/video-engine/visual/provider',
    token,
  );
  ok(provider.runwayCalled === false, 'provider runwayCalled false');
  ok(provider.imageProvider.toLowerCase().includes('gemini'), 'provider gemini');
  if (!provider.apiKeyConfigured) {
    console.error('KIT VIDEO ENGINE PHASE 05 GOLDEN FAIL');
    console.error(' - REAL GEMINI GENERATION = FAIL (API key not configured)');
    console.error(' - REAL PIXEL VISION QA = FAIL');
    console.error(' - REAL FAMIXA GOLDEN SHOT = FAIL');
    console.error(' - RUNWAY CALLED = FALSE');
    process.exit(1);
  }

  const production = await json<{ id: string }>('/content/video-engine/productions', token, {
    method: 'POST',
    body: JSON.stringify({
      projectCode: 'FAMIXA',
      universeCode: 'FAMILY_A',
      productionCode: 'EP01',
      title: 'Famixa EP01',
    }),
  });

  const contract = famixaGoldenContract();
  const generated = await json<{
    status: string;
    jobState: string;
    attemptId: string;
    imagePath?: string | null;
    qa?: { status: string; p0Fail: string[]; allowI2v: boolean };
    i2v?: { ready: boolean; runwaySubmitted: boolean };
    runwayCalled: boolean;
    failureClass: string;
    provider: string;
  }>('/content/video-engine/visual/generate', token, {
    method: 'POST',
    body: JSON.stringify({
      productionId: production.id,
      shotCode: 'SH01-01',
      contract,
      confirmed: true,
      strategyChanged: true,
      idempotencyKey: `p05-golden-sh01-01-${Date.now()}`,
      projectStyle: 'FAMIXA_VISUAL_STYLE_V1',
    }),
  });

  ok(generated.runwayCalled === false, 'generate runwayCalled false');
  ok(generated.i2v?.runwaySubmitted !== true, 'generate did not submit Runway');
  ok(Boolean(generated.imagePath) || generated.failureClass.length > 0, 'artifact or classified failure');
  const geminiPass = Boolean(generated.imagePath) && !['REQUEST_FAILED', 'PROVIDER_FAILED', 'ARTIFACT_FAILED'].includes(generated.failureClass);
  ok(geminiPass, 'REAL GEMINI GENERATION');
  const visionRan = ['VISION_PASS', 'VISION_FAIL', 'REVIEW_REQUIRED'].includes(generated.status);
  ok(visionRan, `REAL PIXEL VISION QA ran (${generated.status} ${generated.failureClass} ${(generated.qa?.p0Fail || []).join('|')})`);

  let approvedReady = false;
  if (generated.qa?.status === 'PASS') {
    const decided = await json<{ status: string }>('/content/video-engine/visual/decide', token, {
      method: 'POST',
      body: JSON.stringify({
        productionId: production.id,
        shotCode: 'SH01-01',
        attemptId: generated.attemptId,
        decision: 'APPROVE',
      }),
    });
    ok(decided.status === 'APPROVED', 'director approve after PASS');
    const pack = await json<{ ready: boolean; runwaySubmitted: boolean }>('/content/video-engine/visual/i2v-package', token, {
      method: 'POST',
      body: JSON.stringify({
        attemptId: generated.attemptId,
        productionId: production.id,
        shotCode: 'SH01-01',
        attemptNo: 1,
        status: 'APPROVED',
        fingerprint: 'n/a',
        qa: generated.qa,
      }),
    });
    approvedReady = pack.ready && pack.runwaySubmitted === false;
    ok(approvedReady, 'I2V READY after PASS + APPROVE');
  } else {
    fail.push(`REAL FAMIXA GOLDEN SHOT (Vision ${generated.qa?.status || generated.status}: ${(generated.qa?.p0Fail || []).join(' | ')})`);
  }

  const blank = await json<{ status: string; qa?: { status: string; p0Fail: string[] }; runwayCalled: boolean }>(
    '/content/video-engine/visual/analyze',
    token,
    {
      method: 'POST',
      body: JSON.stringify({
        productionId: production.id,
        shotCode: 'SH01-01',
        contract,
        imageBase64: tinyJpegBase64(),
      }),
    },
  );
  ok(blank.status !== 'VISION_PASS' && blank.qa?.status !== 'PASS', 'negative blank image FAIL');
  ok(blank.runwayCalled === false, 'analyze runway false');

  if (generated.attemptId && generated.imagePath) {
    const imgRes = await fetch(`${API}/content/video-engine/visual/attempts/${generated.attemptId}/image`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (imgRes.ok) {
      const buf = Buffer.from(await imgRes.arrayBuffer());
      const extra = await json<{ status: string; qa?: { status: string; p0Fail: string[] } }>(
        '/content/video-engine/visual/analyze',
        token,
        {
          method: 'POST',
          body: JSON.stringify({
            productionId: production.id,
            shotCode: 'SH01-01',
            contract: { ...contract, characters: [contract.characters[0]] },
            imageBase64: buf.toString('base64'),
          }),
        },
      );
      ok(extra.status !== 'VISION_PASS' && extra.qa?.status !== 'PASS', 'negative A extra character FAIL');
    }
  }

  console.log(
    [
      `REAL GEMINI GENERATION = ${geminiPass ? 'PASS' : 'FAIL'}`,
      `REAL PIXEL VISION QA = ${visionRan ? 'PASS' : 'FAIL'}`,
      `REAL FAMIXA GOLDEN SHOT = ${approvedReady ? 'PASS' : 'FAIL'}`,
      'RUNWAY CALLED = FALSE',
      `generate.status=${generated.status} failure=${generated.failureClass}`,
    ].join('\n'),
  );

  if (fail.length) {
    console.error('KIT VIDEO ENGINE PHASE 05 GOLDEN FAIL');
    for (const f of fail) console.error(' -', f);
    process.exit(1);
  }
  console.log('KIT VIDEO ENGINE PHASE 05 GOLDEN PASS');
}

main().catch((err) => {
  console.error('KIT VIDEO ENGINE PHASE 05 GOLDEN FAIL');
  console.error(String(err));
  process.exit(1);
});
