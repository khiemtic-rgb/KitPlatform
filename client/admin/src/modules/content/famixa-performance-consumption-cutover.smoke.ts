/** FAMIXA_PRODUCTION_OS_PERFORMANCE_CONSUMPTION_CUTOVER_V1 — Phase C. 0 providers. */

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
ok(contracts.includes('"PerformanceSnapshot"'), 'registry names PerformanceSnapshot');
ok(contracts.includes('CreateVideo') && contracts.includes('CreateVoice') && contracts.includes('CreateLipsync'), 'three CREATE actions present');

const consumption = cs('KitPlatform.Packs.Content.Application/Production/ProductionPerformanceConsumption.cs');
ok(consumption.includes('ActionRequiresSnapshot'), 'one required-snapshot evaluator');
ok(consumption.includes('RequireVoiceLine'), 'exact voice line match');
ok(consumption.includes('RequireSharedParentPerformance'), 'lipsync parent PerformanceHash equality');
ok(!consumption.includes('actingBeatFingerprint') && !consumption.includes('acceptedTake'), 'no legacy fallback tokens');

const gate = cs('KitPlatform.Packs.Content.Application/Production/CreateVideoActionContractGate.cs');
ok(gate.includes('CollectMissing') && gate.includes('PerformanceSnapshot'), 'GET gate collects PerformanceSnapshot');

const actions = cs('KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs');
ok(actions.includes('EvaluateCreateVoiceAsync') && actions.includes('EvaluateCreateLipsyncAsync'), 'POST voice/lipsync use same evaluator');

const engine = cs('KitPlatform.Packs.Content.Application/Production/ProductionWorkExecutionEngine.cs');
ok(engine.includes('BindRequiredPerformanceAsync'), 'freeze requires TEST snapshot');
ok(engine.includes('ApplyBound'), 'compiler consumes frozen snapshot projection');

const drift = cs('KitPlatform.Packs.Content.Application/Production/ProductionStateMachines.cs');
ok(drift.includes('PerformanceHash'), 'acting invalidation sees PerformanceHash');

const suite = cs('KitPlatform.Packs.Content.Application/Production/PerformanceConsumptionCutoverV1Regression.cs');
ok(suite.includes('FAMIXA_PRODUCTION_OS_PERFORMANCE_CONSUMPTION_CUTOVER_V1'), 'T01–T32 suite present');
ok(suite.includes('T31 GET readiness == POST execution gate'), 'GET/POST parity asserted');

const cutover = read('famixa-production-os-ui-cutover.smoke.ts');
ok(cutover.includes('FAMIXA_PRODUCTION_OS_UI_CUTOVER_V1'), 'UI cutover suite unchanged');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_PERFORMANCE_CONSUMPTION_CUTOVER_V1 FAIL=${fail.length}\n${fail.join('\n')}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_PERFORMANCE_CONSUMPTION_CUTOVER_V1 PASS FAIL=0 (no provider)');
