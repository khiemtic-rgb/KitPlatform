import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  FAMIXA_VISUAL_MODE,
  VISUAL_MODE_3D,
  VISUAL_MODE_SUITE,
  VISUAL_MODE_V1_ID,
  classifyExistingScenePipeline,
  classifyVisualReference,
  legacyPhotorealCanonClaim,
  lockedStudioClaim,
  mayChangeVisualModeDirectly,
  resolveSceneVisualReferences,
  visualGenerationPreflight,
  visualModesCompatible,
} from './kit-video-visual-mode';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ProjectVisualModeAuthorityV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ProjectVisualModeAuthorityV1Regression.cs');
const still = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesStillService.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');
const api = read('../../shared/api/content.api.ts');
const studio = read('ContentFamixaCharacterStudio.tsx');
const series = read('ContentFamixaSeriesTab.tsx');
const pvs = read('ContentFamixaProjectVisualStyleCard.tsx');
const preflight = read('kit-video-scene-image-preflight.ts');

ok(VISUAL_MODE_V1_ID === 'FAMIXA_VISUAL_MODE_AUTHORITY_V1'
  && VISUAL_MODE_SUITE === 'FAMIXA_VISUAL_MODE_AUTHORITY_V1_REGRESSION', '01 document id');
ok(rules.includes('RequiresVisualMode() => true')
  && rules.includes('SceneMayChooseMode() => false'), '02 project authority');
ok(FAMIXA_VISUAL_MODE.visualMode === VISUAL_MODE_3D
  && visualModesCompatible(VISUAL_MODE_3D, VISUAL_MODE_3D), '03 FAMIXA 3D');
ok(classifyVisualReference(legacyPhotorealCanonClaim('CHAR-001', 'Minh')).eligible === false, '04 legacy ineligible');
ok(classifyVisualReference(lockedStudioClaim('CHAR-001', 'Minh', 'ab')).eligible === true, '05 studio eligible');
ok(resolveSceneVisualReferences([legacyPhotorealCanonClaim('CHAR-001', 'Minh')]).allowed === false, '06 no fallback');
ok(classifyExistingScenePipeline([legacyPhotorealCanonClaim('CHAR-003', 'Linh')]) === 'INVALID_REFERENCE_PIPELINE', '07 scene 01 invalid');
ok(visualGenerationPreflight({
  references: [lockedStudioClaim('CHAR-001', 'Minh')],
  characterIdentityAvailable: true,
  compilerVisualMode: VISUAL_MODE_3D,
  providerPayloadVisualMode: VISUAL_MODE_3D,
}).allowed, '08 studio preflight pass');
ok(visualGenerationPreflight({
  references: [legacyPhotorealCanonClaim('CHAR-001', 'Minh')],
  characterIdentityAvailable: true,
}).allowed === false, '09 photoreal preflight fail');
ok(!mayChangeVisualModeDirectly()
  && rules.includes('MutatesAuthorities() => false')
  && rules.includes('RequiresMigration() => false'), '10 no mutate / no mig');
ok(regression.includes('VM-01') && regression.includes('VM-17')
  && controller.includes('project-visual-mode/regression'), '11 regression API');
ok(still.includes('ProjectVisualModeAuthorityV1Rules.Preflight')
  && still.includes('visualPreflight.Allowed'), '12 still service blocks before Gemini');
ok(studio.includes('ContentFamixaVisualModeBadge')
  && series.includes('ContentFamixaVisualModeBadge')
  && pvs.includes('PROJECT VISUAL STYLE'), '13 UX badge + project create');
ok(preflight.includes('visual_mode')
  && series.includes('resolveSceneVisualReferences'), '14 scene resolver + preflight');
ok(api.includes('project-visual-mode') || controller.includes('GetProjectVisualMode'), '15 GET contract');
ok(!thisSmokeGenerates() && rules.includes('CallsGemini() => false'), '16 no generation');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_VISUAL_MODE_AUTHORITY_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_VISUAL_MODE_AUTHORITY_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
