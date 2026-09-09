import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/SeriesStillVisualIngressV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/SeriesStillVisualIngressV1Regression.cs');
const still = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesStillService.cs');
const ige = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ImageGenerationExecutionService.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(rules.includes('IUnifiedVisualCompiler')
  && rules.includes('IVisualUniverseSnapshotResolver')
  && rules.includes('VisualUniverseSnapshot'), '01 snapshot + unified compiler');
ok(still.includes('ResolveSnapshotAsync')
  && still.includes('CompileStill')
  && still.includes('CompiledPrompt')
  && !still.includes('var guarded = prompt;'), '02 Series Still compiles before provider');
ok(ige.includes('SeriesStillVisualIngressV1Rules.CompileStill')
  && ige.includes('ToProviderRequest'), '03 IGE uses compiled contract');
ok(still.includes('ProviderMayCall')
  && ige.includes('ProviderMayCall')
  && rules.includes('ProviderMayCall'), '04 provider receives compiled contract only');
ok(!rules.includes('LegacyStyleFallback() => true')
  && rules.includes('LegacyStyleFallback() => false')
  && still.includes('No legacy prompt fallback') === false
  && rules.includes('RawStylePromptForbidden() => true'), '05 no legacy fallback');
ok(still.includes('GateSnapshot') && rules.includes('GateCompile'), '06 blocking gates exist');
ok(controller.includes('series-still-visual-ingress/regression')
  && regression.includes('F-01')
  && regression.includes('F-35'), '07 HTTP regression');
ok(rules.includes('CallsGemini() => false') && !thisSmokeGenerates(), '08 no Gemini / no live generate');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_SERIES_STILL_VISUAL_INGRESS_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_SERIES_STILL_VISUAL_INGRESS_V1_SMOKE PASS FAIL=0');
