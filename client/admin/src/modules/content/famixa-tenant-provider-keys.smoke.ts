/** Tenant production credentials count as configured. Run: npx tsx <this file> */

import { mergeProviderKeyFlags, tenantAiKeyFlags } from './famixa-tenant-provider-keys';
import type { TenantAiCapabilitySettings } from '@/shared/api/tenant-ai-config.api';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const row = (
  capability: TenantAiCapabilitySettings['capability'],
  providerId: string,
  modelId: string,
  enabled = true,
  credentialId: string | null = 'cred',
): TenantAiCapabilitySettings => ({
  capability,
  config: { providerId, modelId, credentialId, enabled, updatedBy: 'admin', updatedAt: '2026-10-19T00:00:00Z' },
  catalog: [],
  credentials: [],
});

const flags = tenantAiKeyFlags([
  row('IMAGE', 'GEMINI', 'gemini-3.1-flash-image'),
  row('VIDEO', 'runway', 'gen4_turbo'),
  row('VOICE', 'elevenlabs', 'eleven_v3'),
  row('LIPSYNC', 'fal', 'sync-lipsync-v3'),
]);

ok(flags.gemini && flags.runway && flags.elevenLabs && flags.fal, 'saved tenant providers are configured');
ok(!flags.veo, 'unconfigured veo stays closed');
ok(!JSON.stringify(flags).includes('cred'), 'flags contain no credential id');

const disabled = tenantAiKeyFlags([row('IMAGE', 'GEMINI', 'gemini-3.1-flash-image', false)]);
ok(!disabled.gemini, 'disabled image config is not configured');

const unbound = tenantAiKeyFlags([row('IMAGE', 'GEMINI', 'gemini-3.1-flash-image', true, null)]);
ok(!unbound.gemini, 'image without a credential is not configured');

const merged = mergeProviderKeyFlags(
  { runway: false, fal: false, elevenLabs: false, gemini: false, veo: true },
  flags,
);
ok(merged.gemini && merged.veo, 'content settings and tenant credentials combine');

if (fail.length) {
  console.error(fail.join('\n'));
  process.exit(1);
}
console.log('famixa-tenant-provider-keys smoke PASS');
