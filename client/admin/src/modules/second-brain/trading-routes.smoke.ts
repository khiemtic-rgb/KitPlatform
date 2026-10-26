/** Confirms the Second Brain trading routes are registered. Run: npx tsx src/modules/second-brain/trading-routes.smoke.ts */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { operatorNotices } from './operator-notices.ts';

const root = dirname(fileURLToPath(import.meta.url));
const router = readFileSync(join(root, '../../app/router.tsx'), 'utf8');
const registry = readFileSync(join(root, '../registry.tsx'), 'utf8');
const gates = readFileSync(join(root, '../../shared/platform/platform-feature-map.ts'), 'utf8');
const required = ['session', 'overview', 'ledger', 'spot', 'futures', 'import', 'journal', 'agent', 'risk', 'exchange', 'account'];
const missing = required.filter((path) => !router.includes(`path="${path}"`) || !router.includes('second-brain'));
if (!registry.includes("key: 'secondBrain'") || !gates.includes("secondBrain: 'personal_intelligence'")) missing.push('registry');
if (missing.length > 0) {
  console.error(missing.join(','));
  process.exit(1);
}
const empty = operatorNotices({
  tradeCount: 0, fixtureTradeCount: 0, otherSourceTradeCount: 0, decisionCount: 0,
  paperExecutionCount: 0, riskVetoCount: 0, latestSource: null, latestRecordedAt: null,
  onlyFixtureData: false, missingData: [], importTradeCount: 0, exchangeTradeCount: 0,
});
if (!empty.some((item) => item.id === 'no-trades')) {
  console.error('operator notices missing');
  process.exit(1);
}
const layout = readFileSync(join(root, 'TradingLayout.tsx'), 'utf8');
const desk = readFileSync(join(root, 'TradingResearchDeskPage.tsx'), 'utf8');
if (!layout.includes("path: '/second-brain/agent'") || !layout.includes('Trading Agent') || !desk.includes('tradingResearchApi') || !desk.includes('Chưa xác định') || !desk.includes('Trade Ledger') || !desk.includes('Chưa có giao dịch đã ghép.') || !desk.includes('Đang tải nhiệm vụ research.') || !desk.includes('Không kết nối được Trading Research desk')) {
  console.error('trading agent desk missing');
  process.exit(1);
}
const pages = readFileSync(join(root, 'TradingPages.tsx'), 'utf8');
const panel = readFileSync(join(root, 'ResearchPanel.tsx'), 'utf8');
const researchRoute = readFileSync(join(root, 'ResearchReadPage.tsx'), 'utf8');
if (pages.includes('<ResearchPanel />') || !pages.includes('Sổ giao dịch Personal Intelligence')) {
  console.error('trading overview still renders research');
  process.exit(1);
}
if (!router.includes('path="second-brain/research"') || !registry.includes("path: '/second-brain/agent'")) {
  console.error('second brain entry missing');
  process.exit(1);
}
if (!researchRoute.includes('<ResearchPanel />') || !researchRoute.includes('personal_intelligence.research.read') || researchRoute.includes('tradingApi')) {
  console.error('research route is not read-only');
  process.exit(1);
}
if (desk.includes("from './trading.api'") || desk.includes('pack_personal_intelligence')) {
  console.error('trading agent ledger mixes tenant trades');
  process.exit(1);
}
if (panel.includes('type="success"') || panel.includes('color="green"') || panel.includes('color="success"') || panel.includes('tradingApi')) {
  console.error('research panel mixes trading data or success styling');
  process.exit(1);
}
const fixture = operatorNotices({
  tradeCount: 2, fixtureTradeCount: 2, otherSourceTradeCount: 0, decisionCount: 0,
  paperExecutionCount: 1, riskVetoCount: 0, latestSource: 'FIXTURE', latestRecordedAt: null,
  onlyFixtureData: true, missingData: [], importTradeCount: 0, exchangeTradeCount: 0,
});
if (empty.some((item) => item.id === 'research-not-connected') || fixture.some((item) => item.id === 'research-not-connected')) {
  console.error('research status must come from the publication API');
  process.exit(1);
}
if (!fixture.some((item) => item.blocker?.includes('Không dùng các số này'))) {
  console.error('fixture notice missing');
  process.exit(1);
}
console.log('trading routes ok');
