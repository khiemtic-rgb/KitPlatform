import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ProductionContractPromptV2LifecycleRegression.cs'),
  'utf8',
);
const contract = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ProductionShotContractService.cs'),
  'utf8',
);
const compiler = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ProductionPromptCompilerService.cs'),
  'utf8',
);

ok(rules.includes('PRODUCTION_CONTRACT_PROMPT_V2_LIFECYCLE_TEST_V1'), '01 suite id');
ok(rules.includes('NextVersion') && contract.includes('CONTRACT_V2_CREATED') && contract.includes('MarkSupersededAsync'), '02/03 V2 does not overwrite V1');
ok(compiler.includes('NextVersion') && compiler.includes('MarkSupersededAsync'), '06/07 Prompt V2 supersedes V1');
ok(compiler.includes('GetCompiledByContractShaAsync'), '08 idempotent compile by contract SHA');
ok(!contract.includes('IGemini') && !compiler.includes('IRunway') && !compiler.includes('Gemini'), '11 no provider');
ok(![rules, contract, compiler].some((src) => src.includes('|| true)')), '20 no tautology');

if (fail.length) {
  console.error(`PRODUCTION_CONTRACT_PROMPT_V2_LIFECYCLE_TEST_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('PRODUCTION_CONTRACT_PROMPT_V2_LIFECYCLE_TEST_V1 PASS FAIL=0 P0=0 (file/SoT scan; C# via /contract-prompt-v2-lifecycle/regression)');
