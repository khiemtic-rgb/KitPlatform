import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  MASTER_REVISION_DIRECTOR_REVIEW_SUITE,
  MASTER_REVISION_DIRECTOR_REVIEW_V1_ID,
  MASTER_REVISION_SUITE,
  MASTER_REVISION_V1_ID,
  masterRevisionMayApprove,
  masterRevisionMayLock,
  masterRevisionMayReject,
  masterRevisionMayRequestNew,
} from './kit-video-character-studio';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const ui = read('ContentFamixaCharacterStudio.tsx');
const ts = read('kit-video-character-studio.ts');
const api = read('../../shared/api/content.api.ts');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterMasterRevisionV1Rules.cs');
const review = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterMasterRevisionDirectorReviewV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterMasterRevisionV1Regression.cs');
const reviewRegression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterMasterRevisionDirectorReviewV1Regression.cs');
const orch = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterStudioOrchestrator.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(MASTER_REVISION_V1_ID === 'FAMIXA_CHARACTER_MASTER_REVISION_V1', '01 document id');
ok(MASTER_REVISION_SUITE === 'FAMIXA_CHARACTER_MASTER_REVISION_V1_REGRESSION', '02 suite id');
ok(ui.includes('Bắt đầu tạo Master Revision') && ui.includes('Master hiện tại sẽ được giữ nguyên')
  && ui.includes('requestCharacterStudioMasterRevision'),
  '03 Studio revision CTA');
ok(ts.includes('MASTER_REVISION_V1_ID') && api.includes('/master/revision'), '04 client types + API');
ok(rules.includes('UsesCharacterName() => false')
  && !rules.includes('if (characterId')
  && !rules.includes('NamMasterService')
  && !rules.includes('NamRevisionRules')
  && !rules.includes('characterName =='), '05 no name branch');
ok(rules.includes('Keep exact face from Master') === false
  || rules.includes('Do not keep exact face'), '06 AGE_MISMATCH does not lock Master pixels');
ok(rules.includes('ContinuityInstruction')
  && rules.includes('CurrentMasterIsAppearanceLock'), '07 preserve vs revise policy');
ok(regression.includes('AGE_MISMATCH') && regression.includes('no character-name')
  && regression.includes('ProtectedMinhUnchanged'), '08 Nam fixture / Minh protected');
ok(orch.includes('RequestMasterRevisionAsync')
  && orch.includes('STUDIO-MASTER-')
  && !orch.includes('characterName == "Nam"'), '09 shared orchestrator');
ok(controller.includes('character-studio/{characterId}/master/revision')
  && controller.includes('character-studio/master-revision/regression'), '10 API + regression');
ok(!thisSmokeGenerates() && rules.includes('CallsGemini() => false')
  && rules.includes('AutoApprove() => false')
  && rules.includes('CrpRegenerationRevisesMaster() => false'), '11 no Gemini / no auto / CRP ≠ Master');

ok(MASTER_REVISION_DIRECTOR_REVIEW_V1_ID === 'FAMIXA_CHARACTER_MASTER_REVISION_DIRECTOR_REVIEW_V1'
  && MASTER_REVISION_DIRECTOR_REVIEW_SUITE === 'FAMIXA_CHARACTER_MASTER_REVISION_DIRECTOR_REVIEW_V1_REGRESSION',
  '12 director review document');
ok((ui.includes('CURRENT MASTER') || ui.includes('Master hiện tại'))
  && ui.includes('CURRENT AUTHORITY')
  && ui.includes('LOCKED'), '13 Current Master shown');
ok(!masterRevisionMayApprove('') && !masterRevisionMayReject('')
  && !masterRevisionMayLock('') && !ui.includes('Duyệt Master') === false,
  '14 no candidate → review helpers off');
ok(masterRevisionMayApprove('MASTER_REVISION_PENDING_REVIEW')
  && masterRevisionMayReject('MASTER_REVISION_PENDING_REVIEW')
  && ui.includes('Duyệt Master') && ui.includes('Không đạt'),
  '15 pending → Approve + Reject visible');
ok(!masterRevisionMayLock('MASTER_REVISION_PENDING_REVIEW')
  && (ui.includes('selected.masterRevision.mayLock') || ui.includes('rev.mayLock'))
  && ui.includes('Khóa Master'),
  '16 Lock hidden while pending, shown when approved');
ok(masterRevisionMayLock('MASTER_REVISION_APPROVED'), '17 approved → Lock visible');
ok(!masterRevisionMayLock('MASTER_REVISION_REJECTED')
  && masterRevisionMayRequestNew('MASTER_REVISION_REJECTED')
  && ui.includes('Tạo Revision mới'),
  '18 rejected → Lock hidden + new revision');
ok(ui.includes('candidateMasterSha')
  && (ui.includes('MASTER REVISION CANDIDATE') || ui.includes('Master Revision candidate')),
  '19 Candidate SHA shown');
ok(!thisSmokeGenerates()
  && ui.includes('void fetchCharacterStudio(id)')
  && !ui.includes('requestCharacterStudioMasterRevision(id)')
  && review.includes('CallsGemini() => false')
  && review.includes('AutoApprove() => false')
  && review.includes('AutoLock() => false')
  && orch.includes('AdvanceRevisionAsync')
  && orch.includes('LiveAuthoritySha'),
  '20 no auto generation on open');
ok(review.includes('CallsGeminiOnApprove() => false')
  && review.includes('CallsGeminiOnReject() => false')
  && review.includes('CallsGeminiOnLock() => false')
  && reviewRegression.includes('CASE') === false
  && reviewRegression.includes('01 Pending candidate'),
  '21 no Gemini on review + regression present');
ok(ui.includes('Bạn xác nhận Master Revision này đạt yêu cầu?')
  && ui.includes('Khóa Master Revision này làm Authority mới?')
  && ui.includes('DNA / PRP / CRP hiện tại phụ thuộc Master cũ')
  && ts.includes('Vẫn quá già'),
  '22 review dialogs');
ok(controller.includes('master/revision/approve')
  && controller.includes('master/revision/reject')
  && controller.includes('master/revision/lock')
  && api.includes('rejectionReason'),
  '23 review APIs');
ok(review.includes('UsesCharacterName() => false')
  && !review.includes('if (characterId ==')
  && !review.includes('NamReview'),
  '24 review has no name branch');
ok(rules.includes('CrpStaleForCurrentMaster')
  && orch.includes('CrpStaleForCurrentMaster')
  && ui.includes('crpStale')
  && ui.includes('Tạo lại bộ 4 ảnh từ Master hiện tại')
  && ui.includes('Bộ 4 ảnh đang theo Master cũ'),
  '25 stale CRP after locked Master forces 4-view replace');
ok(rules.includes('HistoricalAuthoritySha')
  && rules.includes('LiveAuthorityRequiresCandidateBytes')
  && orch.includes('HistoricalAuthoritySha')
  && orch.includes('GateAuthorityUnreadable')
  && ui.includes('Bộ 4 ảnh vẫn theo Master cũ')
  && !orch.includes('characterName == "Nam"'),
  '26 locked revision shows historical V1 and generate binds candidate only');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_MASTER_REVISION_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_MASTER_REVISION_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
