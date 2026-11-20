/** AI-MODEL-03: Model AI Configure → register, Edit → rotate by provider/model. Run: npx tsx <this file> */

import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import ts from 'typescript';
import type * as CatalogApi from '../../shared/api/production-ai-catalog.api';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

type Call = { method: 'get' | 'post'; url: string; body?: unknown };

const root = dirname(fileURLToPath(import.meta.url));
const pageSource = readFileSync(join(root, 'ContentAiSettingsPage.tsx'), 'utf8');
const apiSource = readFileSync(join(root, '../../shared/api/production-ai-catalog.api.ts'), 'utf8');
const httpImport = "import { http } from '@/shared/api/http';";

async function loadApi(): Promise<typeof CatalogApi> {
  const stubbed = apiSource.replace(
    httpImport,
    'const http = (globalThis as unknown as { __modelAiHttp: unknown }).__modelAiHttp as any;',
  );
  const js = ts.transpileModule(stubbed, {
    compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 },
  }).outputText;
  const file = join(mkdtempSync(join(tmpdir(), 'model-ai-')), 'production-ai-catalog.api.mjs');
  writeFileSync(file, js);
  return (await import(pathToFileURL(file).href)) as typeof CatalogApi;
}

function fakeStorage(writes: string[]) {
  return {
    getItem: () => null,
    setItem: (key: string, value: string) => writes.push(`${key}=${value}`),
    removeItem: () => undefined,
    clear: () => undefined,
    key: () => null,
    length: 0,
  };
}

const model = (credentialStatus: CatalogApi.ProductionAiCatalogModel['credentialStatus']) => ({
  providerId: 'fal',
  modelId: 'sync-lipsync-v3',
  displayName: 'Sync Lipsync V3',
  credentialStatus,
});

async function main() {
  ok(apiSource.includes(httpImport), 'api imports shared http client');
  const calls: Call[] = [];
  const g = globalThis as unknown as Record<string, unknown>;
  g.__modelAiHttp = {
    get: async (url: string) => {
      calls.push({ method: 'get', url });
      return { data: { video: [], voice: [], lipSync: [] } };
    },
    post: async (url: string, body: unknown) => {
      calls.push({ method: 'post', url, body });
      return {
        data: {
          providerId: 'fal',
          modelId: 'sync-lipsync-v3',
          status: 'ACTIVE',
          version: 2,
          credentialId: 'server-id-must-not-pass',
          credentialVersionId: 'server-version-must-not-pass',
        },
      };
    },
  };
  const storageWrites: string[] = [];
  g.localStorage = fakeStorage(storageWrites);
  g.sessionStorage = fakeStorage(storageWrites);
  const secret = 'model-ai-smoke-secret';
  const api = await loadApi();

  // T2 T4 T5: real rotate client posts provider/model/secret only to /credentials/rotate.
  const rotated = await api.rotateProductionCredential({ providerId: 'fal', modelId: 'sync-lipsync-v3', secret });
  ok(calls.length === 1 && calls[0].method === 'post', 'T2 rotate issues one POST');
  ok(calls[0]?.url === '/content/production/credentials/rotate', 'T2 rotate URL');
  ok(
    JSON.stringify(Object.keys(calls[0]?.body as object).sort()) === JSON.stringify(['modelId', 'providerId', 'secret']),
    'T4 rotate body has providerId, modelId, secret only',
  );
  const body = JSON.stringify(calls[0]?.body);
  ok(!/credentialId|credentialVersionId|tenantId/i.test(body), 'T5 rotate body has no identifiers');
  ok(
    JSON.stringify(Object.keys(rotated).sort()) === JSON.stringify(['modelId', 'providerId', 'status', 'version']),
    'rotate result drops server ids',
  );
  ok(!JSON.stringify(rotated).includes(secret), 'rotate result never echoes secret');

  // T1: NOT_CONFIGURED → register, never rotate.
  const registerCalls: unknown[] = [];
  const rotateCalls: unknown[] = [];
  let reloads = 0;
  const client: CatalogApi.ModelAiCredentialClient = {
    register: async (b) => {
      registerCalls.push(b);
      return {};
    },
    rotate: async (b) => {
      rotateCalls.push(b);
      return { providerId: b.providerId, modelId: b.modelId, status: 'ACTIVE', version: 2 };
    },
    reload: async () => {
      reloads += 1;
    },
  };
  const configured = await api.submitModelAiCredential(model('NOT_CONFIGURED'), { secret, displayName: '' }, client);
  ok(configured.kind === 'configured', 'T1 outcome configured');
  ok(registerCalls.length === 1 && rotateCalls.length === 0, 'T1 NOT_CONFIGURED calls register only');
  ok(
    JSON.stringify(registerCalls[0]) ===
      JSON.stringify({ providerId: 'fal', modelId: 'sync-lipsync-v3', secret, displayName: 'Sync Lipsync V3' }),
    'T1 register body',
  );
  ok(reloads === 1, 'T8 register reloads catalog');

  // T2 T3 T8: ACTIVE → rotate with the card provider/model, no register, then reload.
  const edited = await api.submitModelAiCredential(model('ACTIVE'), { secret, displayName: 'ignored' }, client);
  ok(rotateCalls.length === 1, 'T2 ACTIVE calls rotate');
  ok(registerCalls.length === 1, 'T3 ACTIVE does not call register');
  ok(
    JSON.stringify(rotateCalls[0]) === JSON.stringify({ providerId: 'fal', modelId: 'sync-lipsync-v3', secret }),
    'T4 ACTIVE passes card provider/model + secret only',
  );
  ok(edited.kind === 'rotated' && edited.version === 2 && edited.status === 'ACTIVE', 'rotated outcome carries version');
  ok(!JSON.stringify(edited).includes(secret), 'T9 outcome never echoes secret');
  ok(reloads === 2, 'T8 rotate reloads catalog');

  // T6: AMBIGUOUS never writes.
  let ambiguousRejected = false;
  try {
    await api.submitModelAiCredential(model('AMBIGUOUS'), { secret }, client);
  } catch {
    ambiguousRejected = true;
  }
  ok(ambiguousRejected, 'T6 AMBIGUOUS rejected');
  ok(rotateCalls.length === 1 && registerCalls.length === 1 && reloads === 2, 'T6 AMBIGUOUS makes no request');

  // T7: no browser storage writes.
  ok(storageWrites.length === 0, 'T7 no localStorage/sessionStorage writes');

  // Page wiring.
  const submit = pageSource.slice(pageSource.indexOf('const onConfigure'), pageSource.indexOf('const renderModelCard'));
  ok(submit.includes('submitModelAiCredential(configureModel, values'), 'page submits through helper');
  ok(submit.includes('register: registerProductionCredential'), 'page wires register');
  ok(submit.includes('rotate: rotateProductionCredential'), 'page wires rotate');
  ok(submit.includes("credentialStatus === 'AMBIGUOUS') return"), 'T6 page guards AMBIGUOUS');
  ok(submit.includes('await load()'), 'T8 page reload refreshes catalog');
  ok(submit.includes('form.resetFields()'), 'secret input reset after success');
  const ambiguousCard = pageSource.slice(
    pageSource.indexOf("model.credentialStatus === 'AMBIGUOUS' && ("),
    pageSource.indexOf('</Card>', pageSource.indexOf("model.credentialStatus === 'AMBIGUOUS' && (")),
  );
  ok(!ambiguousCard.includes('setConfigureModel') && !ambiguousCard.includes('onClick'), 'T6 AMBIGUOUS card has no write action');
  ok(!/listProductionCredentials|addProductionCredentialVersion/.test(pageSource), 'no credential list / id-based rotation');
  ok(!/credentialId|credentialVersionId|tenantId/.test(pageSource + apiSource), 'no identifiers in page or api');
  ok(!/localStorage|sessionStorage|console\./.test(pageSource + apiSource), 'T7 no storage or console in page/api');
  ok(pageSource.includes('visibilityToggle={false}'), 'secret stays masked');

  if (fail.length) {
    console.error('AI_MODEL_03 FAIL', fail);
    process.exit(1);
  }
  console.log('AI_MODEL_03 OK');
}

void main();
