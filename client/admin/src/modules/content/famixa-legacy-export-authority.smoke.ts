/** AUTHORITY-STABILIZATION-01F — legacy assemble is export-only. 0 providers. 0 FFmpeg. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
function ok(cond: boolean, name: string) {
  if (!cond) fail.push(name);
}

const here = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(here, name), 'utf8');
const src = (rel: string) => readFileSync(join(here, '../../../../../src', rel), 'utf8');

const between = (text: string, start: string, end: string) => {
  const from = text.indexOf(start);
  const to = text.indexOf(end, from + start.length);
  return from >= 0 && to > from ? text.slice(from, to) : '';
};

const assemble = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesAssembleService.cs');
const controller = src('KitPlatform.Api/Controllers/Content/ContentController.cs');
const authority = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionLegacyExportAuthorityV1.cs');
const regression = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionLegacyExportAuthorityV1Regression.cs');
const os = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs');
const series = read('ContentFamixaSeriesTab.tsx');
const prod = read('ContentFamixaProdV2.tsx');
const card = read('ContentFamixaPreviewCutCard.tsx');
const copy = read('content-famixa-assemble.ts');
const one = between(series, 'const assembleOneShot', 'const approveShotPicture');
const route = between(controller, '[HttpPost("series/assemble")]', '[HttpGet("series/veo-video")]');

const method = between(assemble, 'public async Task<(byte[] Bytes, string ContentType, string FileName)> AssembleAsync', 'private static string MixArgs');
ok(assemble.includes('EXPORT_ONLY') && method.includes('FetchAsync') && method.includes('RunFfmpeg'), 'T9 legacy export still reads a take and can render a file');
ok(!method.includes('ProductionArtifact') && !method.includes('IProductionWorkStore'), 'T1 legacy assemble does not create a ProductionArtifact');
ok(!method.includes('ProductionBinding') && !method.includes('BindNamedAsync'), 'T2 legacy assemble does not create a binding');
ok(!method.includes('FilmAssembly'), 'T3 legacy assemble does not create FilmAssembly');
ok(!method.includes('BindCurrent') && !method.includes('BindNamed') && !method.includes('AcceptedFor'), 'T4/T5/T6 legacy assemble does not mark binding roles');
ok(!method.includes('CreateEpisode'), 'T7 legacy assemble does not create an official episode');
ok(one.includes("productionOsLegacyRefuseNote('MIX_VIDEO', 'POST /content/series/assemble')") && one.indexOf('return;') < one.indexOf('assembleContentSeriesCut'), 'T8 assembleOneShot returns before the export call');
ok(!method.includes('FreezeInput') && !method.includes('ProductionTask'), 'T10 legacy export does not mutate frozen input');
ok(!method.includes('CreateRequestAsync') && !method.includes('StartFrozenAsync') && os.includes('StartFrozenAsync'), 'T11 legacy export does not replace the OS execution path');
ok(authority.includes('EXPORT_ONLY') && regression.includes('no official artifact') && !series.includes('FINAL EPISODE') && !prod.includes('FINAL EPISODE') && !card.includes('Ghép tập hoàn chỉnh') && copy.includes('không phải tập Production OS'), 'T12 export labels are compatibility, not an official episode');
ok(route.includes('return File(') && !route.includes('BindNamedAsync'), 'controller returns a file and does not bind');

if (fail.length) {
  console.error(`FAMIXA_LEGACY_EXPORT_AUTHORITY_01F FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_LEGACY_EXPORT_AUTHORITY_01F PASS FAIL=0 (no provider, no ffmpeg)');
