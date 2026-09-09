import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  AUTHORITY_CHAIN,
  FAMIXA_LAYERS,
  PRODUCTION_OS_ARCHITECTURE_ID,
  PRODUCTION_OS_ID,
  PRODUCTION_OS_STAFF_STEPS,
  PRODUCTION_OS_SUITE,
  PROVIDER_LAYER,
} from './kit-video-production-os';
import { SERIES_STAFF_TABS } from './kit-video-production-ux';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const appDir = join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application');
const infraDir = join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure');
const osTs = readFileSync(join(root, 'kit-video-production-os.ts'), 'utf8');
const osRules = readFileSync(join(appDir, 'ProductionOsRules.cs'), 'utf8');
const osReg = readFileSync(join(appDir, 'ProductionOsArchitectureV1Regression.cs'), 'utf8');
const osSvc = readFileSync(join(infraDir, 'ProductionOsService.cs'), 'utf8');
const controller = readFileSync(
  join(root, '../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs'),
  'utf8',
);
const api = readFileSync(join(root, '../../shared/api/content.api.ts'), 'utf8');
const overview = readFileSync(join(root, 'ContentFamixaProductionOverview.tsx'), 'utf8');
const workspace = readFileSync(join(root, 'ContentFamixaSeriesWorkspace.tsx'), 'utf8');

ok(PRODUCTION_OS_ARCHITECTURE_ID === 'PRODUCTION_OS_ARCHITECTURE_V1', '01 architecture id');
ok(PRODUCTION_OS_ID === 'FAMIXA_VIDEO_PRODUCTION_OS_ARCHITECTURE_V1', '01b document id');
ok(PRODUCTION_OS_SUITE === 'FAMIXA_PROVIDER_AGNOSTIC_ARCHITECTURE_REGRESSION', '02 suite id');
ok(FAMIXA_LAYERS.join('|') === 'STORY|WORLD|PRODUCTION|GOVERNANCE|ORCHESTRATION', '03 Famixa layers');
ok(PROVIDER_LAYER === 'AI_PROVIDERS', '04 provider is replaceable layer');
ok(AUTHORITY_CHAIN[0] === 'MASTER' && AUTHORITY_CHAIN.at(-1) === 'ARTIFACT', '05 authority chain');
ok(
  SERIES_STAFF_TABS.map((t) => t.label).join('|') ===
    'Tổng quan|Kịch bản|Chia cảnh|Nhân vật|Thoại|Hình ảnh|Video|Hoàn thiện|Xuất bản',
  '06 staff tabs include Voice stage',
);
ok(PRODUCTION_OS_STAFF_STEPS.join('|') === 'Kịch bản|Chia cảnh|Nhân vật & bối cảnh|Tạo hình|Duyệt hình|Tạo video|Duyệt video|Hoàn tất', '06b OS staff steps are labels only');

const intentStart = osRules.indexOf('public sealed record ProductionIntent');
const intentEnd = osRules.indexOf('public sealed record CanonicalProductionDescription');
const intentSlice = osRules.slice(intentStart, intentEnd);
ok(
  intentSlice.includes('DurationSeconds') &&
    !intentSlice.includes('Provider') &&
    !intentSlice.includes('runway_prompt') &&
    !intentSlice.includes('gemini_prompt') &&
    !intentSlice.includes('gen4_turbo'),
  '07 Production Intent has no provider',
);

ok(osRules.includes('PROVIDER_CAPABILITY_UNSUPPORTED') && osRules.includes('NEEDS_PROVIDER_SELECTION'), '08 capability block codes');
ok(osRules.includes('AutoSelectProvider() => false') && osRules.includes('DecideSelection'), '09 no auto-select');
ok(osRules.includes('ExecutionFingerprint') && osRules.includes('ProductionProvenance'), '09b fingerprint + provenance');
ok(osSvc.includes('Does not call Gemini, Runway, or Veo') && !osSvc.includes('ContentGeminiClient') && !osSvc.includes('ContentRunwayClient'), '10 ProductionOsService isolated');
ok(osReg.includes('MockProductionProviderA') && osReg.includes('MockProductionProviderB'), '11 mock adapters');
ok(controller.includes('video-engine/production-os/regression') && controller.includes('veoCalled = false'), '12 regression no generate');
ok(controller.includes('video-engine/production-os/catalog') && api.includes('fetchProductionOsCatalog'), '12b catalog API');
ok(!api.includes('selectProductionProvider') && !api.includes('executeProductionOs'), '13 API has no execute/select');

const appFiles = readdirSync(appDir).filter((f) => f.endsWith('.cs') && !f.includes('Regression'));
const appLeak = appFiles.filter((f) => {
  const text = readFileSync(join(appDir, f), 'utf8');
  return text.includes('ContentGeminiClient') || text.includes('ContentRunwayClient');
});
ok(appLeak.length === 0, '14 Application has no Gemini/Runway HTTP clients');

ok(!overview.includes('Production Intent') && !overview.includes('Capability Gate') && !overview.includes('SHA256'), '15 staff overview has no OS jargon');
ok(!overview.includes('Chọn Gemini') && !workspace.includes('Provider picker'), '15b no provider picker');
ok(!osTs.includes('evaluateIdentity') && !osTs.includes('runway_prompt'), '16 TS labels only');
ok(!osSvc.includes('HttpClient') && !osSvc.includes('GenerateAsync'), '17 catalog does not generate');
ok(osRules.includes('MayOverwritePayload') && osRules.includes('BLOCK_DUPLICATE'), '18 versioning + duplicate policy');

if (fail.length) {
  console.error(`FAMIXA_PROVIDER_AGNOSTIC_ARCHITECTURE_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PROVIDER_AGNOSTIC_ARCHITECTURE_SMOKE PASS FAIL=0 (file/SoT scan; no provider live)');
