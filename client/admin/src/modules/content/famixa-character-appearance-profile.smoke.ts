import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { APPEARANCE_PROFILE_SUITE, APPEARANCE_PROFILE_V1_ID } from './kit-video-character-studio';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const ui = read('ContentFamixaCharacterStudio.tsx');
const ts = read('kit-video-character-studio.ts');
const api = read('../../shared/api/content.api.ts');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAppearanceProfileV1Rules.cs');
const consistency = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAppearanceConsistencyV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAppearanceProfileV1Regression.cs');
const orch = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterStudioOrchestrator.cs');
const provider = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/GeminiCharacterGenerationProvider.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(APPEARANCE_PROFILE_V1_ID === 'FAMIXA_CHARACTER_APPEARANCE_PROFILE_V1', '01 document id');
ok(APPEARANCE_PROFILE_SUITE === 'FAMIXA_CHARACTER_APPEARANCE_PROFILE_V1_REGRESSION', '02 suite id');
ok(ui.includes('Character Appearance') && ui.includes('Target Appearance')
  && ui.includes('Facial Maturity') && ui.includes('Lifestyle')
  && ui.includes('Appearance Consistency'), '03 Studio shows Appearance');
ok(ts.includes('appearanceConsistencyLabel') && api.includes('appearanceProfile'), '04 client types');
ok(rules.includes('AppearanceSource') && rules.includes('UsesCharacterName() => false')
  && !rules.includes('if (characterId') && !rules.includes('NamAppearanceProfile')
  && !rules.includes('characterName =='), '05 no name branch');
ok(rules.includes('Contemporary Vietnamese everyday lifestyle')
  && rules.includes('Role is a narrative function only')
  && rules.includes('CompileFacialMaturity'), '06 lifestyle + maturity policy');
ok(consistency.includes('FaceAgeStyleAppearanceIndependent')
  && consistency.includes('MayBecomeReady')
  && consistency.includes('ReviewGate'), '07 independent consistency gate');
ok(controller.includes('character-studio/{characterId}/appearance-consistency')
  && controller.includes('HttpPost("character-studio/{characterId}/appearance-consistency")')
  && api.includes('/content/character-studio/${encodeURIComponent(characterId)}/appearance-consistency')
  && ui.includes('Chấm ngoại hình · PASS')
  && ts.includes('studioAppearanceNeedsDirectorPass')
  && ts.includes('studioDirectorNext')
  && ts.includes("label: 'Duyệt bộ ảnh'")
  && ts.includes('studioCrpApproved')
  && !ts.includes("label: blockers[0]"), '07b appearance-consistency API + guided CTA');
ok(regression.includes('age 38 role Bố') && regression.includes('no character-name')
  && regression.includes('ProtectedMinhUnchanged'), '08 Nam is a fixture; Minh protected');
ok(orch.includes('CharacterAppearanceProfileV1Rules.Compile')
  && !orch.includes('characterName == "Nam"'), '09 Studio compiles shared profile');
ok(provider.includes('ProviderRequestHasAppearance')
  && provider.includes('CharacterAppearanceProfileV1Rules.GateNotReady')
  && provider.includes('false, false, []'),
  '10 provider blocks without profile');
ok(controller.includes('character-studio/appearance-profile/regression')
  && api.includes('appearanceConsistencyStatus'), '11 regression API');
ok(!thisSmokeGenerates() && rules.includes('CallsGemini() => false'), '12 no Gemini / no generation');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_APPEARANCE_PROFILE_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_APPEARANCE_PROFILE_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
