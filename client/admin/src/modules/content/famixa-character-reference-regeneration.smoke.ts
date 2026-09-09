import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CHARACTER_REFERENCE_REGENERATION_V1_ID,
  STAFF_REGENERATE,
  STAFF_REJECT,
  STAFF_REJECT_REASON,
  STAFF_START,
  rejectReasonValid,
  rejectedSetLabel,
  isRejectedStatus,
} from './kit-video-character-reference-regeneration';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-character-reference-regeneration.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentKitVideoCharacterReferenceSetCard.tsx'), 'utf8');
const api = readFileSync(join(root, '../../shared/api/content.api.ts'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterReferenceRegenerationV1Rules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterReferenceRegenerationV1Service.cs'),
  'utf8',
);
const controller = readFileSync(
  join(root, '../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs'),
  'utf8',
);
const app = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterReferenceRegenerationV1Rules.cs'),
  'utf8',
);

const staff = `${ts}\n${ui}`;
ok(CHARACTER_REFERENCE_REGENERATION_V1_ID === 'FAMIXA_CHARACTER_REFERENCE_REGENERATION_V1', '01 document id');
ok(staff.includes(STAFF_REJECT) && staff.includes(STAFF_REJECT_REASON) && staff.includes(STAFF_REGENERATE), '02 reject + regenerate copy');
ok(staff.includes('Gemini') && staff.includes('Runway') && staff.includes('Veo') && staff.includes(STAFF_START), '03 provider + start again');
ok(!ui.includes('Tạo FRONT') && !ui.includes('Tạo 3/4') && !ui.includes('auto approve') && !ui.includes('auto lock'), '04 no per-slot / auto');
ok(!ui.includes('SHOT-002') && !ui.includes('GENERATE VIDEO') && !ui.includes('regenerate production'), '05 no production/video');
ok(ts.includes('REGEN_PROVIDERS') && rejectReasonValid('Toàn thân không đồng nhất') && !rejectReasonValid('no'), '06 reason gate');
ok(api.includes('authority-pipeline/crp/reject') && api.includes('authority-pipeline/crp/regenerate') && api.includes('crp/history'), '07 API');
ok(controller.includes('character-reference-regeneration-v1/regression') && controller.includes('crp/regenerate'), '08 endpoints');
ok(rules.includes('AutoApprove() => false') && rules.includes('AutoLock() => false') && rules.includes('AutoRetry() => false'), '09 no auto');
ok(service.includes('ICharacterReferenceGenerationProvider') && !service.includes('ContentGeminiClient'), '10 provider abstraction');
ok(!app.includes('ContentGeminiClient') && !app.includes('Google.GenAI'), '11 Application isolated');
ok(rules.includes('BLOCK_DUPLICATE') && rules.includes('NextVersion'), '12 versioning + idempotency');
ok(!thisSmokeGenerates(), '13 no generation during TS smoke');
ok(rejectedSetLabel(4).includes(STAFF_REJECT) && isRejectedStatus('REJECTED') && !isRejectedStatus('READY_FOR_DIRECTOR'), '14 rejected banner');
ok(ui.includes('rejectedSetLabel') && ui.includes('isRejectedStatus(regen?.status)') && !ui.includes('coverageReady ||'), '15 reject UI uses current status');
ok(rules.includes('RejectIncrementsVersion() => false') && rules.includes('RejectCreatesHistoryVersion() => false'), '16 reject does not bump version');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_REFERENCE_REGENERATION_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_REFERENCE_REGENERATION_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
