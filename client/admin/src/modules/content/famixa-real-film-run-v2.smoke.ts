/** FAMIXA_REAL_FILM_RUN_V2 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const src = (rel: string) =>
  readFileSync(join(root, '../../../../../src', rel), 'utf8');

const create = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs');
const transport = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/HttpRunwayTransport.cs');
const regression = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionEp01Sh01RealFilmRunV2Regression.cs');

ok(create.includes('ConsumeOfficialPermitBeforeStartAsync'), 'consume official permit before Start');
ok(create.includes('RequireOfficialIssuedPermitAsync'), 'ISSUED official permit required');
ok(create.includes('OfficialFilmForbidden'), 'missing permit still forbidden');
ok(transport.includes('MatchesConsumed') && transport.includes('IsOfficialFilm'), 'HTTP Start checks consumed official permit');
ok(regression.includes('exactly one Start') && regression.includes('idempotent replay'), 'one-shot + idempotent');
ok(!regression.includes('CREATE_VOICE') && !regression.includes('CREATE_LIPSYNC'), 'no Voice/LipSync in this phase');

if (fail.length) {
  console.error(`FAMIXA_REAL_FILM_RUN_V2 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_REAL_FILM_RUN_V2 ok');
