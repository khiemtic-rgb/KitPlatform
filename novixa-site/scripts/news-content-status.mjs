import {
  EDITORIAL_HUB,
  EDITORIAL_PLAN,
  getUpcomingPlan,
  getPlanStats,
} from './lib/news-content-plan.mjs';

const upcoming = getUpcomingPlan();
const stats = getPlanStats();

console.log(`=== ${EDITORIAL_HUB.name} ===`);
console.log(`Lịch: ${stats.startDate} → ${stats.endDate} (${stats.total} bài, 1 bài/ngày)`);
console.log('Theo nhóm:');
for (const [key, row] of Object.entries(stats.byCategory)) {
  console.log(`  ${key}. ${row.label}: ${row.count}`);
}
console.log(`\nSắp tới (chưa đăng): ${upcoming.length} bài\n`);

for (const item of upcoming.slice(0, 15)) {
  console.log(`${item.publishDate}  [${item.id}]  (${item.category})  ${item.title}`);
}

if (upcoming.length > 15) {
  console.log(`… và ${upcoming.length - 15} bài nữa`);
}
