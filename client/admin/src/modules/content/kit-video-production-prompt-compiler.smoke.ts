import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PRODUCTION_PROMPT_COMPILER_ID, PROMPT_COMPILER_STATUSES, PROMPT_SECTIONS } from './kit-video-production-prompt-compiler';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const labels = readFileSync(join(root, 'kit-video-production-prompt-compiler.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentKitVideoProductionPromptCompilerCard.tsx'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ProductionPromptCompilerRules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ProductionPromptCompilerService.cs'),
  'utf8',
);

ok(PRODUCTION_PROMPT_COMPILER_ID === 'PRODUCTION_PROMPT_COMPILER_V1', '01 suite id');
ok(PROMPT_COMPILER_STATUSES.includes('COMPILED') && !PROMPT_COMPILER_STATUSES.includes('GENERATED' as never), '23 no GENERATED');
ok(PROMPT_SECTIONS.includes('IDENTITY') && PROMPT_SECTIONS.includes('FORBIDDEN CONSTRAINTS'), '09 sections labeled');
ok(!labels.includes('function compile') && !labels.includes('HashPrompt'), '33 TS has no compiler');
ok(!ui.includes('GENERATE IMAGE') && !ui.includes('RUN GEMINI') && !ui.includes('RUNWAY'), '28 no generate buttons');
ok(ui.includes('COMPILE PROMPT') && ui.includes('IDENTITY GOVERNANCE'), '27 compile + authority tags');
ok(rules.includes('AutoFix() => false') && rules.includes('HashPrompt'), '02 no auto-fix + hash');
ok(service.includes('ProductionGateAsync') && service.includes('ICharacterIdentityGovernanceService'), '07 Governance SoT');
ok(service.includes('PROMPT_COMPILER_IDENTITY_CONFLICT') && service.includes('PROMPT_COMPILER_NOT_READY'), '07 block codes');
ok(!service.includes('IGemini') && !service.includes('IRunway') && !service.includes('Gemini') && !service.includes('Runway'), '26/27 no provider');
ok(![labels, ui, rules, service].some((src) => src.includes('|| true)')), '20 no tautology');

if (fail.length) {
  console.error(`PRODUCTION_PROMPT_COMPILER_V1_REGRESSION FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('PRODUCTION_PROMPT_COMPILER_V1_REGRESSION PASS FAIL=0 P0=0 (file/SoT scan; C# cases via /production-prompt-compiler/regression)');
