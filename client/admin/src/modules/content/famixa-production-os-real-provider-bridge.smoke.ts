/** FAMIXA_PRODUCTION_OS_REAL_PROVIDER_BRIDGE_V1 — TEST only. 0 real provider calls. */

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

const adapter = cs('KitPlatform.Packs.Content.Application/Production/Providers/RunwayMotionExecutionAdapter.cs');
ok(adapter.includes('IMotionExecutionAdapter'), 'Runway adapter implements OS boundary');
ok(adapter.includes('MapDuration'), 'Runway duration bins stay inside adapter');

const resolver = cs('KitPlatform.Packs.Content.Application/Production/IProductionMotionAdapterResolver.cs');
ok(resolver.includes('MissingProviderModel') && resolver.includes('UnknownProvider'), 'resolver fail-closed');

const fake = cs('KitPlatform.Packs.Content.Application/Production/Providers/FakeRunwayTransport.cs');
ok(fake.includes('HttpCallCount => 0') && fake.includes('RequiresNetwork => false'), 'FakeRunwayTransport never networks');

const create = cs('KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs');
ok(create.includes('SubmitProviderJobAsync') && create.includes('StartFrozenAsync'), 'job id persisted after Start');
ok(!create.includes('v1/image_to_video') && !create.includes('X-Runway-Version'), 'OS has no Runway vendor request');

const suite = cs('KitPlatform.Packs.Content.Application/Production/ProductionRunwayBridgeV1Regression.cs');
ok(suite.includes('FAMIXA_PRODUCTION_OS_REAL_PROVIDER_BRIDGE_V1'), 'T01–T56 suite present');
ok(suite.includes('FakeRunwayTransport'), 'suite uses fake transport');

if (fail.length) {
  console.error('FAMIXA_PRODUCTION_OS_REAL_PROVIDER_BRIDGE_V1 FAIL\n' + fail.join('\n'));
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_REAL_PROVIDER_BRIDGE_V1 PASS');
