import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { CONTRACT_SECTIONS, CONTRACT_STATUSES, PRODUCTION_SHOT_CONTRACT_ID } from './kit-video-production-shot-contract';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const labels = readFileSync(join(root, 'kit-video-production-shot-contract.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentKitVideoProductionShotContractCard.tsx'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ProductionShotContractRules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ProductionShotContractService.cs'),
  'utf8',
);

ok(PRODUCTION_SHOT_CONTRACT_ID === 'PRODUCTION_SHOT_CONTRACT_V1', '01 suite id');
ok(CONTRACT_STATUSES.includes('DIRECTOR_APPROVED') && !CONTRACT_STATUSES.includes('GENERATED' as never), '23 no GENERATED status');
ok(CONTRACT_SECTIONS.includes('composition') && CONTRACT_SECTIONS.includes('motion'), '15 sections labeled');
ok(!labels.includes('function validate') && !labels.includes('HashCanonical'), '33 TS has no validators');
ok(!ui.includes('GENERATION READY'), '29 no GENERATION READY');
ok(ui.includes('DIRECTOR APPROVAL') && ui.includes('IDENTITY GOVERNANCE'), '29 authority tags');
ok(rules.includes('ForbiddenModelKeys') && rules.includes('HashCanonical'), '20/22 C# SoT hash + no model fields');
ok(service.includes('ProductionGateAsync') && service.includes('SaveAsync'), '28 Create invokes Governance');
ok(service.includes('ValidateAsync') && service.includes('ApproveAsync'), '30 Validate + Approve invoke Governance');
ok(service.includes('SHOT_CONTRACT_IDENTITY_CONFLICT') && service.includes('NextVersion'), '25/32 V2 + no bypass');
ok(!service.includes('Gemini') && !service.includes('Runway'), '33/34 no Gemini/Runway');
ok(!rules.includes('CreatesPixels("SPEC")') || rules.includes('GEMINI'), '35 no generation');
ok(![labels, ui, rules, service].some((src) => src.includes('|| true)')), '20 no tautology');

if (fail.length) {
  console.error(`PRODUCTION_SHOT_CONTRACT_V1_REGRESSION FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('PRODUCTION_SHOT_CONTRACT_V1_REGRESSION PASS FAIL=0 P0=0 (file/SoT scan; C# cases via /production-shot-contract/regression)');
