/** Phase 05 check — no new Gemini image. Reuses existing artifacts. */
import { famixaGoldenContract } from './kit-video-pixel';

const API = process.env.KIT_API_URL || 'http://localhost:5290/api';
const PASS = process.env.KIT_MKT_PASSWORD || process.env.KIT_ADMIN_PASSWORD || 'Admin@123';

function tinyJpeg(): string {
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
  const login = (await (
    await fetch(`${API}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: 'admin', password: PASS, tenantCode: 'KIT_MKT' }),
    })
  ).json()) as { accessToken?: string };
  if (!login.accessToken) throw new Error('login failed');
  const headers = { Authorization: `Bearer ${login.accessToken}`, 'Content-Type': 'application/json' };

  const provider = await (await fetch(`${API}/content/video-engine/visual/provider`, { headers })).json();
  const productions = (await (await fetch(`${API}/content/video-engine/productions`, { headers })).json()) as {
    id: string;
    productionCode: string;
  }[];
  const production = productions.find((p) => p.productionCode === 'EP01') ?? productions[0];
  const attempts = (await (
    await fetch(`${API}/content/video-engine/visual/attempts?productionId=${production.id}&shotCode=SH01-01`, { headers })
  ).json()) as { attemptId: string; attemptNo: number; status: string; imagePath?: string }[];

  const contract = famixaGoldenContract();
  const first = attempts.find((a) => a.attemptNo === 1) ?? attempts[0];
  const img = Buffer.from(
    await (await fetch(`${API}/content/video-engine/visual/attempts/${first.attemptId}/image`, { headers })).arrayBuffer(),
  );

  const still = await (
    await fetch(`${API}/content/video-engine/visual/analyze`, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        productionId: production.id,
        shotCode: 'SH01-01',
        contract,
        imageBase64: img.toString('base64'),
      }),
    })
  ).json();

  const extra = await (
    await fetch(`${API}/content/video-engine/visual/analyze`, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        productionId: production.id,
        shotCode: 'SH01-01',
        contract: { ...contract, characters: [contract.characters[0]] },
        imageBase64: img.toString('base64'),
      }),
    })
  ).json();

  const blank = await (
    await fetch(`${API}/content/video-engine/visual/analyze`, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        productionId: production.id,
        shotCode: 'SH01-01',
        contract,
        imageBase64: tinyJpeg(),
      }),
    })
  ).json();

  const garbage = await (
    await fetch(`${API}/content/video-engine/visual/analyze`, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        productionId: production.id,
        shotCode: 'SH01-01',
        contract,
        imageBase64: Buffer.from('not-an-image').toString('base64'),
      }),
    })
  ).json();

  console.log(
    JSON.stringify(
      {
        provider: {
          image: provider.imageProvider,
          model: provider.imageModel,
          key: provider.apiKeyConfigured,
          vision: provider.visionProvider,
          runwayCalled: provider.runwayCalled,
        },
        attempts: attempts.map((a) => ({ no: a.attemptNo, status: a.status, id: a.attemptId, hasPath: Boolean(a.imagePath) })),
        stillBytes: img.length,
        stillQa: { status: still.status, qa: still.qa?.status, p0: still.qa?.p0Fail, allowI2v: still.qa?.allowI2v, runway: still.runwayCalled },
        extraQa: { status: extra.status, qa: extra.qa?.status, p0: extra.qa?.p0Fail, runway: extra.runwayCalled },
        blankQa: { status: blank.status, qa: blank.qa?.status, p0: blank.qa?.p0Fail, failure: blank.failureClass, runway: blank.runwayCalled },
        garbageQa: { status: garbage.status, failure: garbage.failureClass, runway: garbage.runwayCalled },
      },
      null,
      2,
    ),
  );
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
