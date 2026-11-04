/**
 * Menu của đúng payload đăng nhập (vai trò, quyền, vertical).
 * Chạy từ bài kiểm thử API, không dùng preview.
 */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { canApproveTenantKnowledge } from './knowledge-pharmacy-groups.ts';
import { knowledgeMenuGroups } from './knowledge-experience.ts';

interface SessionPayload {
  name: string;
  roles: string[];
  permissions: string[];
  vertical: string | null;
}

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) throw new Error(message);
}

const expected: Record<string, string[]> = {
  'pharm-admin': ['pharmacy-knowledge', 'pharmacy-owned-knowledge', 'pharmacy-local-regimen', 'pharmacy-knowledge-in-use'],
  'mkt-admin': [],
  staff: ['pharmacy-knowledge-in-use'],
  platform: ['platform-knowledge', 'platform-knowledge-governance'],
  reviewer: ['platform-knowledge', 'platform-knowledge-governance'],
  verifier: ['platform-knowledge', 'platform-knowledge-governance'],
  pinner: ['pharmacy-knowledge', 'pharmacy-knowledge-in-use'],
  delegate: ['pharmacy-knowledge-in-use'],
  'unknown-admin': [],
  'unset-admin': ['pharmacy-knowledge', 'pharmacy-owned-knowledge', 'pharmacy-local-regimen', 'pharmacy-knowledge-in-use'],
  'other-admin': ['pharmacy-knowledge', 'pharmacy-owned-knowledge', 'pharmacy-local-regimen', 'pharmacy-knowledge-in-use'],
  'switch-pharm': ['pharmacy-knowledge', 'pharmacy-owned-knowledge', 'pharmacy-local-regimen', 'pharmacy-knowledge-in-use'],
  'switch-mkt': [],
};

const raw = readFileSync(0, 'utf8');
const sessions = JSON.parse(raw) as SessionPayload[];
assert(sessions.length === Object.keys(expected).length, 'session count');

const seen = new Set<string>();
for (const session of sessions) {
  assert(!seen.has(session.name), `duplicate session ${session.name}`);
  seen.add(session.name);
  const want = expected[session.name];
  assert(want, `unexpected session ${session.name}`);
  if (session.name === 'unset-admin') {
    assert(session.vertical === 'pharmacy', 'unset vertical stays on the platform settings default');
  }
  if (session.name === 'unknown-admin') {
    assert(session.vertical === 'unknown', 'explicit unknown vertical is preserved');
  }
  const groups = knowledgeMenuGroups({
    loaded: true,
    vertical: session.vertical,
    review: session.permissions.includes('pharmacy.knowledge.review'),
    verify: session.permissions.includes('pharmacy.knowledge.verify-source'),
    publish: session.permissions.includes('platform.knowledge.publish'),
    canPin: canApproveTenantKnowledge(session.roles, session.permissions),
    isTenantAdmin: session.roles.includes('ADMIN'),
  });
  const keys = groups.flatMap((group) => group.children.map((child) => child.key));
  assert(JSON.stringify(keys) === JSON.stringify(want), `${session.name} menu ${keys.join(',')}`);
  const paths = groups.flatMap((group) => group.children.map((child) => child.path));
  assert(new Set(paths).size === paths.length, `${session.name} duplicate path`);
  const labels = groups.map((group) => `${group.label}: ${group.children.map((child) => child.label).join(', ')}`);
  console.log(`${session.name} [${session.vertical ?? ''}] ${labels.join(' | ') || '(ẩn)'}`);
}

const here = dirname(fileURLToPath(import.meta.url));
const router = readFileSync(join(here, '../../app/router.tsx'), 'utf8');
const count = (needle: string) => router.split(needle).length - 1;
assert(count('path="platform/pharmacy-knowledge"') === 1, 'catalog route once');
assert(count('path="platform/pharmacy-knowledge/xu-ly"') === 1, 'governance route once');
assert(count('path="pharmacy/knowledge"') === 1, 'pin route once');
assert(count('path="pharmacy/owned-knowledge"') === 1, 'owned route once');
assert(count('path="pharmacy/knowledge-in-use"') === 1, 'staff route once');
assert(count('path="pharmacy/local-regimens"') === 1, 'regimen route once');
const catalogAt = router.indexOf('path="platform/pharmacy-knowledge"');
const governanceAt = router.indexOf('path="platform/pharmacy-knowledge/xu-ly"');
const publishAt = router.indexOf('<KnowledgePublishPage />');
assert(catalogAt >= 0 && governanceAt > catalogAt, 'governance route follows catalog');
assert(publishAt > catalogAt && publishAt < governanceAt, 'catalog uses the publish page');
assert(router.indexOf('<KnowledgePublishPage />', publishAt + 1) > governanceAt, 'governance uses the same page');
assert(count('<KnowledgeApprovalPage />') === 1, 'pin page is not mounted twice in the app shell');
console.log('session-menu ok');
