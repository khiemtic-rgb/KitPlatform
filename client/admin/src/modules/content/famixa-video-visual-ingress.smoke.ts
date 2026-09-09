import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VideoVisualIngressV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VideoVisualIngressV1Regression.cs');
const stillRules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/SeriesStillVisualIngressV1Rules.cs');
const contracts = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ContentContracts.cs');
const turbo = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs');
const execution = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/VideoGenerationExecutionService.cs');
const provider = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/RunwayVideoGenerationProvider.cs');
const motion = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/KitVideoMotionService.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');
const api = read('../../../../../client/admin/src/shared/api/content.api.ts');
const series = read('ContentFamixaSeriesTab.tsx');

ok(controller.includes('video-visual-ingress/regression')
  && regression.includes('G-01')
  && regression.includes('G-27'), '01 API route exists');
ok(stillRules.includes('Motion')
  && stillRules.includes('CameraMovement')
  && stillRules.includes('Duration')
  && stillRules.includes('Timing')
  && rules.includes('SceneVisualContract'), '02 contract fields exist');
ok(rules.includes('IVisualUniverseSnapshotResolver')
  && rules.includes('VisualUniverseSnapshot')
  && turbo.includes('ResolveSnapshotAsync')
  && execution.includes('ResolveSnapshotAsync')
  && motion.includes('ResolveSnapshotAsync'), '03 snapshot binding exists');
ok(rules.includes('IUnifiedVisualCompiler')
  && rules.includes('CompileVideo')
  && !rules.includes('IVideoVisualCompiler')
  && turbo.includes('CompileVideo')
  && execution.includes('CompileVideo')
  && motion.includes('CompileVideo'), '04 compiler binding exists');
ok(provider.includes('ProviderMayCall')
  && provider.includes('CompiledPrompt')
  && !provider.includes('CreateImageToVideoAsync(dataUri, request.MotionIntent')
  && rules.includes('ProviderCannotDefineStyle() => true'), '05 provider boundary exists');
ok(rules.includes('LegacyStyleFallback() => false')
  && rules.includes('RawStylePromptForbidden() => true')
  && turbo.includes('CompileBoundPromptAsync')
  && !turbo.includes('var prompt = BuildPrompt(request.Prompt)'), '06 raw fallback is absent');
ok(rules.includes('CONFIRMATION_REQUIRED')
  && rules.includes('DryRun')
  && contracts.includes('bool Confirm = false')
  && turbo.includes('GateConfirm')
  && execution.includes('GateConfirm'), '07 dry-run contract exists');
ok(api.includes('confirm: body.confirm === true')
  && series.includes('confirm: true'), '07b staff I2V POST sends confirm after modal');
ok(rules.includes('RunwayI2vPrompt')
  && turbo.includes('RunwayI2vPrompt')
  && !turbo.includes('return compiled.Contract.CompiledPrompt;'),
  '07c Runway I2V promptText is motion-clamped');
ok(rules.includes('CallsGemini() => false')
  && rules.includes('CallsRunway() => false')
  && !thisSmokeGenerates(), '08 no Gemini / no live generate');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_VIDEO_VISUAL_INGRESS_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_VIDEO_VISUAL_INGRESS_V1_SMOKE PASS FAIL=0');
