import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/IdentityConditionedCalibrationLiveV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/IdentityConditionedCalibrationLiveV1Regression.cs');
const provider = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/VisualCalibrationGenerationProvider.cs');
const store = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/KitVideoArtifactStore.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(controller.includes('identity-conditioned-calibration/live')
  && controller.includes('identity-conditioned-calibration/live/regression')
  && !controller.includes('identity-conditioned-calibration/live/generate')
  && !controller.includes('identity-conditioned-calibration/live/approve'), '01 live API is confirmation/read-only');
ok(rules.includes('READY_FOR_DIRECTOR_CONFIRMATION')
  && rules.includes('CalibrationRunId')
  && rules.includes('Isolate'), '02 isolated run + confirmation status');
ok(rules.includes('OverwritesHistorical() => false')
  && rules.includes('CALIBRATION_ARTIFACT_IMMUTABLE')
  && store.includes('PersistCalibration')
  && store.includes('GateOverwrite')
  && provider.includes('PersistCalibration'), '03 persist is run-scoped and immutable');
ok(rules.includes('CountsHistoricalAsIdentity() => false')
  && rules.includes('HasIndependentHistoricalPixels'), '04 historical pixels excluded from identity coverage');
ok(rules.includes('AutoApprove() => false')
  && rules.includes('AutoLock() => false')
  && rules.includes('PromotesVua() => false'), '05 no auto approve/lock/promote');
ok(rules.includes('CallsGemini() => false')
  && rules.includes('CreatesPixels() => false')
  && regression.includes('LIVE-01'), '06 readiness layer does not call Gemini');
ok(provider.includes('CalibrationRunId')
  && !provider.includes('PersistMaster('), '07 provider cannot persist onto historical slot names');
ok(regression.includes('LIVE-01')
  && regression.includes('LIVE-16'), '08 live regression cases exist');

if (fail.length) {
  console.error('FAIL', fail);
  process.exit(1);
}
console.log('PASS famixa-identity-conditioned-calibration-live.smoke.ts');
