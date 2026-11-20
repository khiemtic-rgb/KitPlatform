/** AI-MODEL-06: /content/ai = Production Model AI, /content/marketing-ai = Marketing AI. Run: npx tsx <this file> */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const networkCalls: string[] = [];
(globalThis as unknown as { fetch: unknown }).fetch = async (url: unknown) => {
  networkCalls.push(String(url));
  throw new Error('AI_MODEL_06_NO_NETWORK');
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (rel: string) => readFileSync(join(root, rel), 'utf8');

const router = read('../../app/router.tsx');
const nav = read('content-nav.tsx');
const modelAi = read('ContentAiSettingsPage.tsx');
const marketing = read('ContentMarketingAiSettingsPage.tsx');
const marketingConsumers = {
  topics: read('ContentTopicsPage.tsx'),
  brands: read('ContentBrandsPage.tsx'),
  ops: read('ContentOpsPage.tsx'),
  videos: read('ContentVideosPage.tsx'),
};

function lazyTarget(name: string): string | null {
  const m = router.match(new RegExp(`const ${name} = lazy\\(\\(\\) =>\\s*import\\('([^']+)'\\)\\.then\\(\\(m\\) => \\(\\{\\s*default: m\\.(\\w+),?\\s*\\}\\)\\)`));
  return m ? `${m[1]}#${m[2]}` : null;
}

function routeElement(path: string): string | null {
  const m = router.match(new RegExp(`<Route path="${path}" element=\\{<(\\w+) />\\} />`));
  return m ? m[1] : null;
}

// T1: /content/ai → catalog-driven Model AI.
ok(routeElement('ai') === 'ContentAiSettingsPage', 'T1 ai route mounts ContentAiSettingsPage');
ok(
  lazyTarget('ContentAiSettingsPage') === '@/modules/content/ContentAiSettingsPage#ContentAiSettingsPage',
  'T1 ContentAiSettingsPage lazy import',
);
ok(modelAi.includes("from '@/shared/api/production-ai-catalog.api'"), 'T1 Model AI uses production AI catalog');
ok(modelAi.includes('submitModelAiCredential('), 'T1 Model AI uses catalog credential helper');

// T2: /content/marketing-ai → Marketing AI settings.
ok(routeElement('marketing-ai') === 'ContentMarketingAiSettingsPage', 'T2 marketing-ai route mounts Marketing page');
ok(
  lazyTarget('ContentMarketingAiSettingsPage') ===
    '@/modules/content/ContentMarketingAiSettingsPage#ContentMarketingAiSettingsPage',
  'T2 Marketing lazy import',
);
ok(marketing.includes('export function ContentMarketingAiSettingsPage()'), 'T2 Marketing page export');
ok(/key === 'facebook'/.test(marketing) && marketing.includes("key: 'video'"), 'T2 Marketing keeps facebook/video tabs');
ok(marketing.includes('testContentFacebook') && marketing.includes('FB_RETURN_KEY'), 'T2 Marketing keeps Facebook OAuth flow');
ok(marketing.includes('geminiApiKey') && marketing.includes('imageModel'), 'T2 Marketing keeps Gemini write/image');
ok(marketing.includes('creatomateApiKey'), 'T2 Marketing keeps Creatomate');
ok(!marketing.includes('production-ai-catalog'), 'T2 Marketing page does not touch Production credentials');

// T3: Model AI has no legacy key inputs.
ok(!/FAL_KEY|RUNWAY_API_KEY|ELEVENLABS_API_KEY/.test(modelAi), 'T3 no legacy env key names on Model AI');
ok(!/creatomate|Test Creatomate|Lưu video|Runway Turbo/i.test(modelAi), 'T3 no Creatomate / legacy video controls on Model AI');
ok(!/fetchContentSettings|updateContentSettings|testContentFacebook/.test(modelAi), 'T3 Model AI does not use org settings API');

// T4 / T5: Marketing links.
const all = Object.values(marketingConsumers).join('\n');
ok(!/\/content\/ai#/.test(all), 'T4/T5 no Marketing link still targets /content/ai#');
ok(marketingConsumers.topics.includes("navigate('/content/marketing-ai#facebook')"), 'T4 topics Facebook navigate');
ok(marketingConsumers.brands.includes('<Link to="/content/marketing-ai#facebook">'), 'T4 brands Facebook link');
ok(marketingConsumers.ops.includes('<Link to="/content/marketing-ai#facebook">'), 'T4 ops Facebook link');
ok(marketingConsumers.videos.includes('<Link to="/content/marketing-ai#video">'), 'T5 videos Creatomate link');
ok(/model ai\|marketing ai/.test(marketingConsumers.topics), 'T4 topics still redirects on backend "Model AI" message');

// Navigation: one Model AI item, one Marketing AI item, no other AI config item.
const navPaths = [...nav.matchAll(/path: '([^']+)'/g)].map((m) => m[1]);
ok(navPaths.filter((p) => p === '/content/ai').length === 1, 'nav has exactly one /content/ai');
ok(navPaths.filter((p) => p === '/content/marketing-ai').length === 1, 'nav has exactly one /content/marketing-ai');
const navLabels = [...nav.matchAll(/label: '([^']+)'/g)].map((m) => m[1]);
ok(navLabels.filter((l) => /Model AI/.test(l)).length === 1, 'one Model AI label');
ok(!navLabels.some((l) => /Cấu hình AI/.test(l)), 'no legacy "Cấu hình AI" nav label');
ok(!navPaths.some((p) => p.startsWith('/content/ai') && p !== '/content/ai'), 'no alias under /content/ai');

// T6 is covered by ContentAiSettingsPage.smoke.ts (unchanged).
// T7: no network.
ok(networkCalls.length === 0, 'T7 no network calls');

if (fail.length) {
  console.error('AI_MODEL_06 FAIL', fail);
  process.exit(1);
}
console.log('AI_MODEL_06 OK');
