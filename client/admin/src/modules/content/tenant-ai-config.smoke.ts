/** TENANT-AI-CONFIG-V1: tenant AI configuration card + API. Run: npx tsx <this file> */

import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import ts from 'typescript';
import type * as TenantApi from '../../shared/api/tenant-ai-config.api';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const card = readFileSync(join(root, 'TenantAiConfigurationCard.tsx'), 'utf8');
const page = readFileSync(join(root, 'ContentAiSettingsPage.tsx'), 'utf8');
const apiSource = readFileSync(join(root, '../../shared/api/tenant-ai-config.api.ts'), 'utf8');
const httpImport = "import { http } from '@/shared/api/http';";

async function loadApi(): Promise<typeof TenantApi> {
  const stubbed = apiSource.replace(
    httpImport,
    'const http = (globalThis as unknown as { __tenantAiHttp: unknown }).__tenantAiHttp as any;',
  );
  const js = ts.transpileModule(stubbed, {
    compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 },
  }).outputText;
  const file = join(mkdtempSync(join(tmpdir(), 'tenant-ai-')), 'tenant-ai-config.api.mjs');
  writeFileSync(file, js);
  return (await import(pathToFileURL(file).href)) as typeof TenantApi;
}

async function main() {
  ok(apiSource.includes(httpImport), 'api imports shared http client');
  const calls: { method: string; url: string; body?: unknown }[] = [];
  (globalThis as unknown as Record<string, unknown>).__tenantAiHttp = {
    get: async (url: string) => {
      calls.push({ method: 'get', url });
      return { data: { capabilities: [] } };
    },
    put: async (url: string, body: unknown) => {
      calls.push({ method: 'put', url, body });
      return { data: body };
    },
    delete: async (url: string) => {
      calls.push({ method: 'delete', url });
      return { data: null };
    },
  };
  const api = await loadApi();

  await api.fetchTenantAiConfig();
  await api.saveTenantAiConfig('VIDEO', { providerId: 'runway', modelId: 'gen4_turbo', credentialId: 'c-1', enabled: true });
  await api.clearTenantAiConfig('VIDEO');
  ok(calls[0]?.url === '/content/production/tenant-ai-config', 'GET tenant-ai-config');
  ok(calls[1]?.method === 'put' && calls[1]?.url === '/content/production/tenant-ai-config/VIDEO', 'PUT per capability');
  ok(JSON.stringify(calls[1]?.body) === JSON.stringify({ providerId: 'runway', modelId: 'gen4_turbo', credentialId: 'c-1', enabled: true }),
    'PUT body has no secret and no tenantId');
  ok(calls[2]?.method === 'delete', 'DELETE per capability');

  const settings = {
    credentials: [
      { credentialId: 'a', displayName: 'Runway A', providerId: 'runway', modelId: 'gen4_turbo', version: 2 },
      { credentialId: 'b', displayName: 'Fal A', providerId: 'fal', modelId: 'sync-lipsync-v3', version: 1 },
    ],
  };
  ok(api.credentialsForModel(settings, { providerId: 'Runway', modelId: 'gen4_turbo' }).map((c) => c.credentialId).join() === 'a',
    'credential options filtered to the selected model');
  ok(api.credentialsForModel(settings, null).length === 0, 'no model → no credential options');

  ok(card.includes('data-tenant-ai-configuration'), 'card marker');
  ok(card.includes('Tenant hiện tại'), 'card names the current tenant scope');
  ok(card.includes('row.catalog.map('), 'model options come from API catalog only');
  ok(!card.includes('Input.Password') && !/secret/i.test(card.replace(/credential/gi, '')), 'card has no secret field');
  ok(!card.includes('wan-2.1') && !card.includes('veo-3.1'), 'card hardcodes no provider/model');
  ok(page.includes('<TenantAiConfigurationCard'), 'Model AI page mounts tenant card');
  ok(!page.includes('org_settings') && !card.includes('/content/settings'), 'no org_settings read');
  ok(page.includes("key: 'image'") && page.includes('catalog.image.map('), 'Model AI lists image models from API catalog');
  ok(!/gemini/i.test(page) && !/gemini/i.test(card), 'no hardcoded Gemini model in UI');

  if (fail.length) {
    console.error('TENANT_AI_CONFIG_V1 FAIL', fail);
    process.exit(1);
  }
  console.log('TENANT_AI_CONFIG_V1 OK');
}

void main();
