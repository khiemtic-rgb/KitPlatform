/** Re-run real pixel Vision on an existing Gemini artifact. Does not generate a new image. */
import { famixaGoldenContract } from './kit-video-pixel';

const API = process.env.KIT_API_URL || 'http://localhost:5290/api';
const TENANT = process.env.KIT_MKT_TENANT || 'KIT_MKT';
const USER = process.env.KIT_MKT_USER || 'admin';
const PASS = process.env.KIT_MKT_PASSWORD || process.env.KIT_ADMIN_PASSWORD || 'Admin@123';
const ATTEMPT = process.env.KIT_VIDEO_ATTEMPT_ID || '6036d11b-35de-43f1-88e4-f0138365016d';

async function main() {
  const loginRes = await fetch(`${API}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: USER, password: PASS, tenantCode: TENANT }),
  });
  const loginJson = (await loginRes.json()) as { accessToken?: string };
  if (!loginRes.ok || !loginJson.accessToken) throw new Error('login failed');
  const token = loginJson.accessToken;
  const headers = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };

  const imgRes = await fetch(`${API}/content/video-engine/visual/attempts/${ATTEMPT}/image`, { headers });
  if (!imgRes.ok) throw new Error(`image ${imgRes.status}`);
  const buf = Buffer.from(await imgRes.arrayBuffer());
  const productions = (await (
    await fetch(`${API}/content/video-engine/productions`, { headers })
  ).json()) as { id: string; productionCode: string }[];
  const production = productions.find((p) => p.productionCode === 'EP01') ?? productions[0];
  if (!production) throw new Error('no production');

  const contract = famixaGoldenContract();
  const analyzed = await (
    await fetch(`${API}/content/video-engine/visual/analyze`, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        productionId: production.id,
        shotCode: 'SH01-01',
        contract,
        imageBase64: buf.toString('base64'),
      }),
    })
  ).json();

  console.log(JSON.stringify({
    status: analyzed.status,
    qa: analyzed.qa,
    repair: analyzed.repair,
    failureClass: analyzed.failureClass,
    runwayCalled: analyzed.runwayCalled,
    visionSnippet: String(analyzed.visionJson || '').slice(0, 800),
  }, null, 2));

  const blank = await (
    await fetch(`${API}/content/video-engine/visual/analyze`, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        productionId: production.id,
        shotCode: 'SH01-01',
        contract,
        imageBase64: Buffer.from([0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10, 0x4a, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xff, 0xd9]).toString('base64'),
      }),
    })
  ).json();
  console.log('BLANK', blank.status, blank.qa?.status, blank.qa?.p0Fail);

  if (analyzed.runwayCalled === true) {
    console.error('RUNWAY CALLED = TRUE — illegal');
    process.exit(1);
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
