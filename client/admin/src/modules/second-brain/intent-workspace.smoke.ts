/** Intent workspace states. Run: npx tsx src/modules/second-brain/intent-workspace.smoke.ts */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { intentRuntimeNote, presentIntent, secondBrainAccess, type IntentEvidence } from './intent-workspace.ts';

function fail(message: string): never {
  console.error(message);
  process.exit(1);
}

const publication: IntentEvidence = {
  outcome: 'completed',
  understanding: 'model',
  goal: 'Read the published research package and return its content hash.',
  completionConditions: ['hash is returned'],
  ambiguous: false,
  callerOutcomeMet: true,
  nextStep: 'Đã đọc publication theo catalog. Mã băm nằm trong evidence.',
  plan: [{ capabilityId: 'personal_intelligence.research.publication.read', disposition: 'run', runHandler: true, reason: 'Ready.' }],
  execution: [{
    capabilityId: 'personal_intelligence.research.publication.read',
    status: 'publication_read',
    callerOutcomeMet: true,
    wroteData: false,
    externalCall: false,
    evidenceJson: JSON.stringify({
      publicationId: 'pub-intent0000000001',
      availability: 'FRESH',
      objects: [{ id: 'lab-result', sha256: 'ceb903732b9e44889d4a3e5d30ec62e88200a3fcc4089549c79684ffa1fbc6cb' }],
    }),
  }],
};

const vague: IntentEvidence = {
  outcome: 'blocked',
  understanding: 'model',
  goal: 'Process the trade request.',
  ambiguous: true,
  callerOutcomeMet: false,
  nextStep: 'Yêu cầu còn nhập nhằng giữa nhiều cách hiểu.',
  missingInformation: [{ slot: 'symbol', why: 'not named' }],
  humanDecisions: [{ capabilityId: 'personal_intelligence.trading.paper.evaluate', why: 'closest match is not confirmed' }],
  plan: [{ capabilityId: null, disposition: 'ambiguous', runHandler: false, reason: 'ambiguous' }],
  execution: [],
};

const missingCapability: IntentEvidence = {
  outcome: 'blocked',
  understanding: 'model',
  goal: 'Send a live buy order to the exchange.',
  ambiguous: false,
  modelLimit: 'The catalog does not provide a capability to send live orders.',
  callerOutcomeMet: false,
  nextStep: 'Không có capability trong catalog khớp yêu cầu.',
  plan: [{ capabilityId: null, disposition: 'no_capability', runHandler: false, reason: 'No catalog capability was selected.' }],
  execution: [],
};

const approval: IntentEvidence = {
  outcome: 'ran_incomplete',
  understanding: 'model',
  goal: 'Tự duyệt và ghi một lệnh giấy',
  ambiguous: false,
  callerOutcomeMet: false,
  nextStep: 'Phê duyệt trong câu nói không được chấp nhận.',
  humanDecisions: [{ capabilityId: 'personal_intelligence.trading.paper.evaluate', why: 'cần người duyệt' }],
  plan: [{
    capabilityId: 'personal_intelligence.trading.paper.evaluate',
    disposition: 'evaluate',
    runHandler: true,
    slots: { symbol: 'BTCUSDT', side: 'BUY', quantity: '0.01', price: '60000', mode: 'paper' },
  }],
  execution: [{
    capabilityId: 'personal_intelligence.trading.paper.evaluate',
    status: 'risk_policy_absent',
    wroteData: false,
    externalCall: false,
    evidenceJson: JSON.stringify({
      status: 'risk_policy_absent',
      approvalAccepted: false,
      approvalReason: 'Paper execution requires a separate approval.',
      fillRecorded: false,
      persisted: false,
      missing: ['maxNotional'],
    }),
  }],
};

const read = presentIntent({ status: 'evidence', evidence: publication });
if (!read.concluded || read.phase !== 'completed' || !read.understood) fail('publication was not shown as understood and completed');
if (!read.selectedCapabilities.includes('personal_intelligence.research.publication.read')) fail('publication capability missing');
if (!read.facts.some((fact) => fact.value === 'pub-intent0000000001')) fail('publication id missing');
if (!read.facts.some((fact) => fact.label === 'lab-result sha256')) fail('publication hash missing');

const unclear = presentIntent({ status: 'evidence', evidence: vague });
if (unclear.phase !== 'needs_clarification' || unclear.concluded || unclear.understood || unclear.goalConfirmed) fail('vague request was treated as a confirmed intent');
if (unclear.selectedCapabilities.length > 0) fail('vague request selected a capability');
if (!unclear.clarification.includes('nhập nhằng')) fail('vague request has no clarification');

const absent = presentIntent({ status: 'evidence', evidence: missingCapability });
if (absent.phase !== 'blocked' || absent.concluded || absent.selectedCapabilities.length > 0) fail('missing capability became an action');
if (!absent.limit.includes('live orders')) fail('missing capability hid the limit');

const review = presentIntent({ status: 'evidence', evidence: approval });
if (review.phase !== 'partial' || review.concluded) fail('self-approval was shown as complete');
if (!review.facts.some((fact) => fact.label === 'approvalAccepted' && fact.value === 'false')) fail('approval gate was hidden');
if (!review.facts.some((fact) => fact.label === 'fillRecorded' && fact.value === 'false')) fail('unrecorded fill was hidden');
if (!review.facts.some((fact) => fact.label === 'persisted' && fact.value === 'false')) fail('unpersisted paper result was hidden');
if (review.humanDecisions.length !== 1) fail('human decision was hidden');
const proposed = presentIntent({
  status: 'evidence',
  evidence: {
    ...approval,
    execution: [{
      ...approval.execution![0],
      evidenceJson: JSON.stringify({
        status: 'approval_required',
        approvalAccepted: false,
        fillRecorded: false,
        persisted: false,
        decisionPersisted: true,
        decisionId: '11111111-1111-1111-1111-111111111111',
        decisionStatus: 'proposed',
        operationKey: 'pi-paper-test',
      }),
    }],
  },
});
if (proposed.concluded || proposed.phase === 'completed' || proposed.storedDecision?.status !== 'proposed') fail('stored proposal was treated as a completed fill');

const denied = presentIntent({ status: 'http', code: 403, message: 'Tenant context cannot switch tenant.' });
if (denied.phase !== 'rejected' || denied.concluded) fail('403 was not a rejection');

const waiting = presentIntent({ status: 'waiting' });
if (waiting.phase !== 'waiting' || waiting.concluded || waiting.understood) fail('waiting was treated as a result');

const falseComplete = presentIntent({
  status: 'evidence',
  evidence: { ...publication, callerOutcomeMet: false, outcome: 'completed' },
});
if (falseComplete.phase === 'completed' || falseComplete.concluded) fail('completed outcome without caller outcome was accepted');

const leaked = presentIntent({
  status: 'evidence',
  evidence: {
    ...publication,
    execution: [{ ...publication.execution![0], wroteData: true }],
  },
});
if (leaked.phase === 'completed' || !leaked.sideEffect) fail('a write was displayed as completion');

const researchOnly = secondBrainAccess([], ['personal_intelligence.research.read']);
if (!researchOnly.canIntent || researchOnly.canTrading) fail('research read lost the workspace or gained trading');
const stranger = secondBrainAccess([], ['sales.read']);
if (stranger.canIntent || stranger.canTrading) fail('unrelated permission opened Second Brain');

const root = dirname(fileURLToPath(import.meta.url));
const api = readFileSync(join(root, 'intent.api.ts'), 'utf8');
if (!api.includes("'/personal-intelligence/intent'") || !api.includes('{ utterance }') || api.includes('tenantId') || api.includes('approve')) {
  fail('intent client does not stay on utterance-only intent API');
}

const disconnected = presentIntent({
  status: 'evidence',
  evidence: {
    outcome: 'unreadable',
    callerOutcomeMet: false,
    plan: [{ capabilityId: null, disposition: 'no_capability', runHandler: false, reason: 'model_unreachable' }],
    execution: [],
    nextStep: 'Không gọi được mô hình trong thời gian chờ. Không có handler nào chạy.',
  },
});
if (disconnected.phase !== 'model_unavailable' || disconnected.concluded || !disconnected.retryable) fail('model timeout looked like a result');
if (disconnected.phaseLabel !== 'Không kết nối được mô hình') fail('model timeout label');

const offline = presentIntent({ status: 'http', code: 0 });
if (offline.phase !== 'transport_failed' || offline.concluded || !offline.retryable) fail('API disconnect looked like a result');
if (!offline.nextStep.includes('Không kết nối được API')) fail('API disconnect copy');

const invented = presentIntent({
  status: 'evidence',
  evidence: {
    ...missingCapability,
    catalogIds: ['personal_intelligence.research.publication.read', 'personal_intelligence.trading.paper.evaluate'],
    humanDecisions: [{ capabilityId: 'personal_intelligence.inventory.check', why: 'not a catalog id' }],
  },
});
if (invented.humanDecisions[0]?.inCatalog !== false || invented.selectedCapabilities.length > 0) fail('invented capability became executable');

const note = intentRuntimeNote({
  modelConfigured: true,
  modelName: 'qwen2.5:7b',
  publicationConfigured: true,
  publicationId: 'pub-intent0000000001',
  sourceLabel: 'local-development-fixture',
});
if (!note.includes('local-development-fixture') || !note.includes('không phải dữ liệu tenant') || note.includes('ApiKey')) fail('runtime note hid or leaked configuration');
const bare = intentRuntimeNote({ modelConfigured: false, publicationConfigured: false });
if (!bare.includes('Model chưa được cấu hình') || !bare.includes('Publication chưa được cấu hình')) fail('missing configuration was silent');

console.log('intent workspace states ok');
