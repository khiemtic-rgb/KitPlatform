/** Backup-required notice. Run: npx tsx <this file> */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  BACKUP_BLOCKED_BODY,
  BACKUP_BLOCKED_TITLE,
  PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED,
  backupConditionLabel,
  productionCredentialErrorCode,
} from './production-credential-backup-notice';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const page = readFileSync(join(root, 'ContentAiSettingsPage.tsx'), 'utf8');
const api = readFileSync(join(root, '../../shared/api/production-ai-catalog.api.ts'), 'utf8');

ok(page.includes('BACKUP_BLOCKED_TITLE') && page.includes('BACKUP_BLOCKED_BODY'), 'page renders the blocked notice');
ok(BACKUP_BLOCKED_TITLE === 'Chưa thể lưu thông tin xác thực', 'blocked title copy');
ok(page.includes('Mở điều kiện bảo mật'), 'page opens the security flow');
ok(page.includes("navigate('/content/ai')"), 'return stays on the credential screen');
ok(page.includes('PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED'), 'page keeps the error code');
ok(!page.includes('localStorage'), 'page does not store the secret in localStorage');
ok(!page.includes('OffMachineBackupConfirmed'), 'page does not set the backup confirmation');
ok(!page.includes('Đã xác minh'), 'page has no verified label');
ok(api.includes('/content/production/credentials/backup-gate'), 'status is read from the backup gate');

ok(
  productionCredentialErrorCode({
    response: { data: { code: PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED, message: PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED } },
  }) === PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED,
  'backup code is recognized',
);
ok(
  backupConditionLabel(
    { writeGate: 'BLOCKED', restoreVerification: 'UNVERIFIED', encryptionKeyId: 'v1', blockCode: PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED },
    null,
  ) === 'Chưa đạt',
  'blocked gate is not met',
);
ok(
  backupConditionLabel(
    { writeGate: 'OPEN', restoreVerification: 'UNVERIFIED', encryptionKeyId: 'v1', blockCode: null },
    null,
  ) === 'Chưa xác minh',
  'open write gate is still unverified restore',
);
ok(
  backupConditionLabel(
    { writeGate: 'OPEN', restoreVerification: 'VERIFIED', encryptionKeyId: 'v1', blockCode: null },
    null,
  ) === 'Đạt',
  'verified restore is Đạt',
);
ok(backupConditionLabel(null, 'forbidden') === 'Không kiểm tra được', 'forbidden status is unknown');
ok(backupConditionLabel(null, null) === null, 'loading has no invented status');

ok(BACKUP_BLOCKED_BODY.includes('chưa được lưu'), 'body says the API key was not saved');
ok(!BACKUP_BLOCKED_BODY.toLowerCase().includes('sai'), 'body does not call the provider key wrong');

if (fail.length) {
  console.error(fail.join('\n'));
  process.exit(1);
}
console.log('production-credential-backup-notice smoke PASS');
