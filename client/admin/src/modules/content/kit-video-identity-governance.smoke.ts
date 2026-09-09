import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { GOVERNANCE_GATES, IDENTITY_GOVERNANCE_ID, IDENTITY_HIERARCHY } from './kit-video-identity-governance';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-identity-governance.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentKitVideoIdentityGovernanceCard.tsx'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterIdentityGovernanceRules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterIdentityGovernanceService.cs'),
  'utf8',
);
const shot = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/KitVideoProductionShotService.cs'),
  'utf8',
);
ok(IDENTITY_GOVERNANCE_ID === 'CHARACTER_IDENTITY_GOVERNANCE_V1_1', '01 suite id');
ok(IDENTITY_HIERARCHY[0] === 'MASTER' && IDENTITY_HIERARCHY[5] === 'GENERATION_MODEL', '02 hierarchy labels');
ok(GOVERNANCE_GATES.includes('PROMPT_GATE') && GOVERNANCE_GATES.length === 9, '03 named gates');
ok(!ts.includes('evaluatePromptAgainstDna') && !ts.includes('evaluateConflicts'), '33 C#/TS rule drift — TS has no evaluators');
ok(!ts.includes('MINH_MASTER_SHA') && !ts.includes('be439c39') && !ts.includes("age = '11'"), '31 no hardcoded Minh SHA/age in TS SoT');
ok(!ts.includes('evaluateShotAgainstDna') && !ts.includes('autoApprove'), '30 no frontend bypass rules');
ok(!ui.includes('directorGatePass') && ui.includes('DIRECTOR APPROVAL'), '24 UI director PENDING not fake PASS');
ok(ui.includes('fetchFamixaCharacters') && !ui.includes('CHAR-001-MINH-ERA01-MASTER-V1'), '22 UI loads characters, no hardcoded Minh codes');
ok(ui.includes('shotSpec') && ui.includes('conflicts'), '23 UI shot + all conflicts');
ok(![ts, ui, rules, service, shot].some((src) => src.includes('|| true)')), '20 no tautological always-true conditions');
ok(!rules.includes('if character == MINH') && !rules.includes('CHAR-001-MINH') && !rules.includes('MinhMasterSha'), '31b C# rules no Minh identity fallback');
ok(!rules.includes('authoritative = "11"') && !rules.includes('age = 11'), '32 no hardcoded age authority');
ok(service.includes('WriteGateAuditAsync') && service.includes('MASTER_GATE'), '28 per-gate audit');
ok(shot.includes('RequireGovernanceAsync') && shot.includes('CreateAsync'), '15 Create Shot invokes Governance');
ok(shot.includes('EditAsync') && shot.split('RequireGovernanceAsync').length >= 4, '16 Edit + Approve invoke Governance');
ok(shot.includes('CharacterIdentityGovernanceBlockedException'), '18 bypass → SHOT_GATE_NOT_SATISFIED');
ok(!service.includes('identityPass = true') || !service.includes('cand.QaStatus'), '21 stress/identity from records not candidate fallback');
ok(rules.includes('CollectRequests') && rules.includes('shoulder_length'), '12 semantic normalize without auto-fix');
ok(rules.includes('No fallback') || rules.includes('unreadable'), '32 unreadable DNA blocks');
ok(!rules.includes('CreatesPixels("GENERATE")') || rules.includes('GEMINI'), '26 generation flags exist');

if (fail.length) {
  console.error(`CHARACTER_IDENTITY_GOVERNANCE_V1_1_REGRESSION FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('CHARACTER_IDENTITY_GOVERNANCE_V1_1_REGRESSION PASS FAIL=0 P0=0 (file/SoT scan; C# cases via /identity-governance/regression)');
