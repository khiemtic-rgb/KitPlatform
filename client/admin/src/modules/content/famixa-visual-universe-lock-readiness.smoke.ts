import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VisualUniverseLockReadinessV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VisualUniverseLockReadinessV1Regression.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(controller.includes('visual-universe/lock-readiness')
  && !controller.includes('visual-universe/lock-readiness/approve')
  && !controller.includes('visual-universe/lock-readiness/lock'), '01 API endpoint');
ok(controller.includes('visual-universe/lock-readiness/regression')
  && regression.includes('A-01')
  && regression.includes('A-30'), '02 regression endpoint');
ok(rules.includes('CurrentAuthority')
  && rules.includes('CandidateAuthority')
  && rules.includes('IsAuthority'), '03 authority status');
ok(rules.includes('VisualUniverseSnapshot')
  && rules.includes('VisualUniverseSnapshotResolver'), '04 snapshot');
ok(rules.includes('PvsAudit')
  && rules.includes('ProtectedV1Sha')
  && rules.includes('ProjectVisualStyleV2Rules'), '05 PVS');
ok(rules.includes('CdlAudit')
  && rules.includes('CharacterDesignLanguageV2Rules'), '06 CDL');
ok(rules.includes('CompilePack')
  && rules.includes('CalibrationMatrix'), '07 calibration');
ok(rules.includes('PromptPreview')
  && rules.includes('CAL-001')
  && rules.includes('CAL-005'), '08 prompt preview');
ok(rules.includes('LockImpact')
  && rules.includes('MutationForbidden')
  && rules.includes('ArtifactStale'), '09 impact preview');
ok(rules.includes('CallsGemini() => false')
  && rules.includes('Locks() => false')
  && rules.includes('Promotes() => false')
  && !thisSmokeGenerates(), '10 no generate / no lock');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_VISUAL_UNIVERSE_LOCK_READINESS_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_VISUAL_UNIVERSE_LOCK_READINESS_V1_SMOKE PASS FAIL=0');
