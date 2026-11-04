/** Actionable error registry. Run: npx tsx <this file> */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  readActionableRecovery,
  recoveryForCode,
  registeredRecoveryRoute,
} from './actionable-error';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const page = readFileSync(join(root, '../../modules/content/ContentAiSettingsPage.tsx'), 'utf8');
const router = readFileSync(join(root, '../../app/router.tsx'), 'utf8');
const panel = readFileSync(join(root, '../../modules/content/ProductionCredentialSecurityPanel.tsx'), 'utf8');

const backup = recoveryForCode('PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED');
ok(backup?.route === '/content/ai/credential-security', 'backup code maps to the security route');
ok(backup?.category === 'security_gate', 'backup code is a security gate');
ok(backup?.retryable === true, 'explicit retry stays available');
ok(recoveryForCode('PERMISSION_DENIED') === null, 'unverified codes get no action');
ok(registeredRecoveryRoute('https://evil.example/phish') === null, 'external targets are rejected');
ok(registeredRecoveryRoute('/content/settings') === null, 'raw paths from an error are rejected');
ok(registeredRecoveryRoute('content.productionCredentialSecurity') === backup?.route, 'registered id resolves');
ok(
  readActionableRecovery({
    response: { data: { code: 'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED', actionTarget: 'https://evil.example' } },
  })?.route === '/content/ai/credential-security',
  'known code ignores a hostile target',
);
ok(readActionableRecovery({ response: { data: { code: 'VALIDATION_FAILED' } } }) === null, 'unknown payload has no button');

ok(router.includes('path="credential-security"'), 'security route is registered');
ok(page.includes('backupRecovery.route') || page.includes('navigate(backupRecovery.route)'), 'page opens the registered route');
ok(page.includes('Mã lỗi'), 'page still shows the support code');
ok(!page.includes('backupGuideSteps'), 'page no longer renders the long checklist');
ok(!page.includes('OffMachineBackupConfirmed'), 'page does not set the confirmation');
ok(!page.includes('localStorage'), 'page does not store the secret');
ok(panel.includes('Kiểm tra lại'), 'flow can recheck the authoritative status');
ok(panel.includes('Tiếp tục lưu thông tin xác thực'), 'flow returns to the credential save');
ok(panel.includes('không có nút xác nhận'), 'flow does not offer a bypass');
ok(!panel.includes('onConfigure'), 'returning does not submit the secret');

if (fail.length) {
  console.error(fail.join('\n'));
  process.exit(1);
}
console.log('actionable-error smoke PASS');
