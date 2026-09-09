import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CHARACTER_REFERENCE_COMPLETION_ID,
  STAFF_CRP_COMPLETE,
  STAFF_MISSING_FULL_BODY,
  completionCoverageLine,
} from './kit-video-character-reference-completion';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-character-reference-completion.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentKitVideoCharacterReferencePackCard.tsx'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterReferenceCompletionRules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterReferencePackService.cs'),
  'utf8',
);
const controller = readFileSync(
  join(root, '../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs'),
  'utf8',
);

ok(CHARACTER_REFERENCE_COMPLETION_ID === 'FAMIXA_CHARACTER_REFERENCE_COMPLETION_V1', '01 document id');
ok(completionCoverageLine(3, 4, ['FULL_BODY']) === 'Thiếu: Toàn thân', '02 missing full body staff');
ok(completionCoverageLine(4, 4, []) === STAFF_CRP_COMPLETE, '03 complete staff');
ok(!ts.includes('content.api') && !ts.includes('kfCount'), '04 TS map only');
ok(ui.includes('Bộ ảnh chuẩn đã hoàn tất') && ui.includes('Thiếu:'), '05 staff UI');
ok(!ui.includes('CRP_VALID') && !ui.includes('REFERENCE_PACK_READY') && !ui.includes('MASTER_SHA_MATCH'), '06 no engineering codes');
ok(rules.includes('AutoApprove() => false') && rules.includes('AutoLock() => false'), '07 no auto approve/lock');
ok(service.includes('FindExistingFullBodyAsync') && service.includes('SameAttachment'), '08 lookup + idempotent');
ok(!service.includes('GenerateAsync') && !service.includes('IRunway'), '09 no provider');
ok(controller.includes('existing-full-body') && controller.includes('character-reference-completion/regression'), '10 endpoints');
ok(STAFF_MISSING_FULL_BODY.includes('toàn thân'), '11 staff missing language');

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_REFERENCE_COMPLETION_V1_REGRESSION FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_REFERENCE_COMPLETION_V1_REGRESSION PASS FAIL=0 P0=0 (file/SoT scan; C# via /character-reference-completion/regression)');
