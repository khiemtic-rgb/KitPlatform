import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { CHARACTER_PRODUCTION_LIBRARY_ID, LIBRARY_FILTERS, pageSlice } from './kit-video-character-library';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-character-library.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentFamixaCharacterLibrary.tsx'), 'utf8');
const api = readFileSync(join(root, '../../shared/api/content.api.ts'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterProductionLibraryRules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterProductionLibraryService.cs'),
  'utf8',
);

ok(CHARACTER_PRODUCTION_LIBRARY_ID === 'CHARACTER_PRODUCTION_LIBRARY_V1', '01 document id');
ok(LIBRARY_FILTERS.map((x) => x.id).join('|') === 'all|ready|in_progress|review|problem', '02 filters');
ok(pageSlice(['a', 'b', 'c'], 0, 2).length === 2, '03 pagination helper');
ok(!ts.includes('CHAR-001') && !ts.includes('MINH') && !ts.includes('age = 11'), '04 TS no hardcoded character');
ok(!ui.includes('CHAR-001') && !ui.includes("'Minh'") && !ui.includes('"Minh"'), '05 UI no hardcoded Minh');
ok(!api.includes("character-library/{characterId} = 'CHAR-001'"), '06 API no default character');
ok(!ts.includes('evaluateIdentity') && !ts.includes('PackSha') && !ui.includes('evaluateIdentity'), '07 no frontend evaluator');
ok(!ui.includes('gemini') && !ui.includes('runway') && !ui.includes('veo') && !ui.includes('|| true'), '08 no provider / true bypass');
ok(ui.includes('Tìm nhân vật') && ui.includes('Chi tiết kỹ thuật') && ui.includes('HOÀN THIỆN BỘ ẢNH') && ui.includes('fx-clib'), '09 staff surfaces');
ok(ui.includes('Chọn nhân vật') && ui.includes('Thử lại'), '10 picker + error retry');
ok(rules.includes('EvaluateReadiness') && rules.includes('LIBRARY_INVALID') && !rules.includes('CHAR-001'), '11 C# projection');
ok(!service.includes('Gemini') && !service.includes('Runway') && !service.includes('Veo'), '12 service isolated');
ok(service.includes('ListLatestAsync') && service.includes('CountByCharacterAsync'), '13 no N+1 list');

if (fail.length) {
  console.error(`CHARACTER_PRODUCTION_LIBRARY_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('CHARACTER_PRODUCTION_LIBRARY_V1_SMOKE PASS FAIL=0 (file/SoT scan; C# via /character-library/regression)');
