import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterFirstMasterVisualIngressV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterFirstMasterVisualIngressV1Regression.cs');
const revision = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterMasterRevisionV1Rules.cs');
const orch = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterStudioOrchestrator.cs');
const provider = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/GeminiCharacterAuthorityGenerationProvider.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');
const studio = read('ContentFamixaCharacterStudio.tsx');

ok(rules.includes('FAMIXA_CHARACTER_FIRST_MASTER_VISUAL_INGRESS_V1')
  && rules.includes('IUnifiedVisualCompiler')
  && rules.includes('VisualUniverseSnapshot')
  && rules.includes('LegacyStyleFallback() => false'), '01 ingress authority');
ok(orch.includes('IVisualUniverseSnapshotResolver')
  && orch.includes('IUnifiedVisualCompiler')
  && orch.includes('CompileMaster')
  && orch.includes('CompileRevision')
  && !orch.includes('ctx.IdentityBrief, "3:4"'), '02 orchestrator uses compiler');
ok(revision.includes('CompileStylePrefix')
  && revision.includes('ageBlock')
  && revision.includes('CharacterFirstMasterVisualIngressV1Rules.CompileRevision'), '03 revision shares compiler');
ok(provider.includes('RequiresBoundContract')
  && provider.includes('ProviderMayCall')
  && provider.includes('ProviderCalled'), '04 provider bound-contract gate');
ok(controller.includes('first-master-visual-ingress/regression')
  && regression.includes('D-01')
  && regression.includes('D-25'), '05 regression + HTTP');
ok(studio.includes('visualCompiler') && studio.includes('UnifiedVisualCompilerV1'), '06 diagnostic only');
ok(rules.includes('CallsGemini() => false') && !thisSmokeGenerates(), '07 no Gemini / no live generate');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_FIRST_MASTER_VISUAL_INGRESS_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_FIRST_MASTER_VISUAL_INGRESS_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
