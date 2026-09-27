import { readFileSync } from 'node:fs';

let failed = 0;
function ok(cond: boolean, name: string) {
  if (!cond) {
    failed += 1;
    console.error('FAIL ' + name);
  } else {
    console.log('ok ' + name);
  }
}

const rules = readFileSync(
  new URL(
    '../../../../../src/KitPlatform.Application/CustomerApp/CustomerCareCardRules.cs',
    import.meta.url,
  ),
  'utf8',
);
const home = readFileSync(new URL('./HomePage.tsx', import.meta.url), 'utf8');
const api = readFileSync(new URL('../../shared/api/customer-app.api.ts', import.meta.url), 'utf8');
const overview = readFileSync(
  new URL(
    '../../../../../src/KitPlatform.Infrastructure/CustomerApp/CustomerAppOverviewService.cs',
    import.meta.url,
  ),
  'utf8',
);

ok(!/OpenAI|ChatCompletion|weather essay|đổi liều theo/i.test(rules), 'rules stay hard-coded, no LLM');
ok(rules.includes('Không chẩn đoán'), 'disclaimer forbids diagnosis');
ok(rules.includes('Không tự đổi liều'), 'disclaimer forbids dose change');
ok(rules.includes('CtaPath = "/chat"'), 'CTA is pharmacist chat');
ok(!rules.includes('mua thêm'), 'no dirty-stock upsell');
ok(overview.includes('careCardTask') && overview.includes('GetTodayAsync'), 'home-summary carries care card');
ok(api.includes('normalizeCareCard') && api.includes('careCard:'), 'FE maps careCard');
ok(home.includes('careCard') && home.includes('home.careCardAria'), 'home renders care card');
ok(home.includes("navigate(careCard.ctaPath") && home.includes('requireLink'), 'CTA gated to chat');

if (failed) {
  console.error(failed + ' failed');
  process.exit(1);
}
console.log('care-card smoke ok');
