import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { IMAGE_GENERATION_CONTRACT_ID, IMAGE_GENERATION_CONTRACT_STATUSES } from './kit-video-image-generation-contract';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const labels = readFileSync(join(root, 'kit-video-image-generation-contract.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentKitVideoImageGenerationContractCard.tsx'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ImageGenerationContractRules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ImageGenerationContractService.cs'),
  'utf8',
);

ok(IMAGE_GENERATION_CONTRACT_ID === 'PRODUCTION_IMAGE_GENERATION_CONTRACT_V1', '01 suite id');
ok(IMAGE_GENERATION_CONTRACT_STATUSES.includes('DIRECTOR_APPROVED') && !IMAGE_GENERATION_CONTRACT_STATUSES.includes('GENERATED' as never), '06 no GENERATED');
ok(!labels.includes('function evaluate') && !labels.includes('HashCanonical'), '33 TS has no validators');
ok(!ui.includes('GENERATE IMAGE') && !ui.includes('RUN GEMINI') && !ui.includes('RUNWAY'), '22 no generate buttons');
ok(ui.includes('CREATE CONTRACT') && ui.includes('DIRECTOR APPROVE'), '21 create + approve');
ok(rules.includes('AutoFix() => false') && rules.includes('HashCanonical'), '15 no auto-fix + hash');
ok(service.includes('ProductionGateAsync') && service.includes('ICharacterIdentityGovernanceService'), '19 Governance SoT');
ok(!service.includes('IGemini') && !service.includes('IRunway') && !service.includes('Gemini') && !service.includes('Runway'), '07 no provider');
ok(![labels, ui, rules, service].some((src) => src.includes('|| true)')), '20 no tautology');

if (fail.length) {
  console.error(`PRODUCTION_IMAGE_GENERATION_CONTRACT_V1_REGRESSION FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('PRODUCTION_IMAGE_GENERATION_CONTRACT_V1_REGRESSION PASS FAIL=0 P0=0 (file/SoT scan; C# via /image-generation-contract/regression)');
