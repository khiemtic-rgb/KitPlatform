/** FAMIXA_PRODUCTION_OS_PERFORMANCE_SNAPSHOT_BINDING_V1 — Phase B. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const cs = (...p: string[]) => read(join('../../../../../src/Packs/Content', ...p));

const contracts = cs('KitPlatform.Packs.Content.Application/Production/ProductionActionContracts.cs');
ok(contracts.includes('["ApprovedKeyframe", "Story", "ActingBeat", "VisualContract", "Timing", "FrozenProviderModel", "PerformanceSnapshot"]'), 'CREATE_VIDEO required inputs include PerformanceSnapshot');
ok(contracts.includes('["LineText", "VoiceId", "LineId", "CharacterId", "PerformanceSnapshot"]'), 'CREATE_VOICE required inputs include PerformanceSnapshot');
ok(contracts.includes('["MotionArtifactId", "VoiceArtifactId", "FrozenProviderModel", "PerformanceSnapshot"]'), 'CREATE_LIPSYNC required inputs include PerformanceSnapshot');
ok(contracts.includes('PerformanceSnapshot'), 'registry requires PerformanceSnapshot identity');

const engine = cs('KitPlatform.Packs.Content.Application/Production/ProductionWorkExecutionEngine.cs');
ok(engine.includes('BindRequiredPerformanceAsync') || engine.includes('BindPerformanceOrNullAsync'), 'freeze binds PerformanceSnapshot');
ok(engine.includes('bound?.PerformanceSnapshotId'), 'input snapshot copies snapshot id');
ok(!engine.includes('acceptedTake.url') && !engine.includes('previewUrl'), 'no URL authority');

const binding = cs('KitPlatform.Packs.Content.Application/Production/PerformanceSnapshotBinding.cs');
ok(binding.includes('FailedIdentity') && binding.includes('InvalidPerformanceHash'), 'fail closed on mismatch');
ok(!binding.includes('ActingHash') || binding.includes('IsValidPerformanceHash'), 'no ActingHash fallback');

const cutover = read('famixa-production-os-ui-cutover.smoke.ts');
ok(cutover.includes('FAMIXA_PRODUCTION_OS_UI_CUTOVER_V1'), 'T22 UI cutover suite present');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_PERFORMANCE_SNAPSHOT_BINDING_V1 FAIL=${fail.length}\n${fail.join('\n')}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_PERFORMANCE_SNAPSHOT_BINDING_V1 PASS FAIL=0 (no provider)');
