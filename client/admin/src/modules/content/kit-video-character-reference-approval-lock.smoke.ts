import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CHARACTER_REFERENCE_APPROVAL_LOCK_ID,
  STAFF_APPROVE,
  STAFF_APPROVED,
  STAFF_AUTHORITY,
  STAFF_COMPLETE,
  STAFF_DRAFT,
  STAFF_LOCK,
  STAFF_LOCKED,
  STAFF_NOT_READY,
  STAFF_PRODUCTION_READY,
  approvalStatusLabel,
  approveConfirm,
  lockConfirm,
  mayApprove,
  mayLock,
} from './kit-video-character-reference-approval-lock';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-character-reference-approval-lock.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentKitVideoCharacterReferencePackCard.tsx'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterReferenceApprovalLockRules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterReferencePackService.cs'),
  'utf8',
);

ok(CHARACTER_REFERENCE_APPROVAL_LOCK_ID === 'FAMIXA_CHARACTER_REFERENCE_APPROVAL_LOCK_V1', '01 document id');
ok(approvalStatusLabel('DRAFT') === STAFF_DRAFT, '02 DRAFT copy');
ok(mayApprove({
  requiredReady: 4,
  requiredTotal: 4,
  coverageReady: true,
  identityPass: true,
  readyForDirector: true,
  status: 'VALIDATED',
  immutable: false,
} as never), '03 4/4 readiness may approve');
ok(!mayApprove({ requiredReady: 3, requiredTotal: 4, coverageReady: false, identityPass: true, readyForDirector: false, status: 'DRAFT', immutable: false } as never), '04 3/4 no approve CTA');
ok(STAFF_APPROVE === 'Duyệt bộ ảnh' && approveConfirm('Minh').confirm === STAFF_APPROVE, '05 Approve CTA');
ok(mayLock({ status: 'DIRECTOR_APPROVED', immutable: false } as never) && STAFF_LOCK.includes('Khóa'), '06 Lock CTA');
ok(approvalStatusLabel('LOCKED') === STAFF_LOCKED && STAFF_AUTHORITY.includes('Authority'), '07 Locked copy');
ok(!ui.toLowerCase().includes('first real') && !ts.includes('executeCharacterReferenceGeneration'), '08 no generation CTA');
ok(!ts.toLowerCase().includes('gemini') && !ts.toLowerCase().includes('runway') && !ts.toLowerCase().includes('veo'), '09 no provider CTA');
ok(rules.includes('AutoApprove() => false') && !service.includes('AutoApprove() => true'), '10 no auto approve');
ok(rules.includes('AutoLock() => false') && service.includes('ShouldWriteLock'), '11 no auto lock');
ok(ui.includes('STAFF_APPROVE') && ui.includes('lockOpen') && ui.includes('STAFF_NOT_READY'), '12 confirm + not-ready copy');
ok(ui.includes('STAFF_AUTHORITY') && ui.includes('STAFF_COMPLETE') && ui.includes('STAFF_PRODUCTION_READY'), '13 locked staff copy');
ok(!ts.includes('sha256') && ui.includes('Xem thông tin hệ thống') && !ui.includes('Intent SHA'), '14 no SHA in primary staff UI');
ok(STAFF_APPROVED.includes('chờ khóa') && lockConfirm().confirm === 'Khóa bộ ảnh', '15 approved/lock copy');

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_REFERENCE_APPROVAL_LOCK_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_REFERENCE_APPROVAL_LOCK_V1_SMOKE PASS FAIL=0 (file/SoT scan; C# via /character-reference-approval-lock/regression)');
