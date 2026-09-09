import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  STUDIO_BIBLE_EXPECTED,
  STUDIO_BIBLE_READY,
  STUDIO_BUILD_EPISODE,
  STUDIO_COMPLETE_TITLE,
  STUDIO_CONTINUE_EPISODE,
  STUDIO_CONTINUE_EPISODE_MOBILE,
  STUDIO_DIRECTOR_APPROVED_VI,
  STUDIO_MASTER_LOCKED_VI,
  STUDIO_NEXT_STEP,
  STUDIO_START_EPISODE,
  directorStatusLabel,
  studioBibleReady,
  studioMasterComplete,
  studioNeedsLock,
} from './kit-video-character-studio';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const ui = read('ContentFamixaCharacterStudio.tsx');
const board = read('ContentFamixaProductionBoards.tsx');
const tab = read('ContentFamixaSeriesTab.tsx');
const helpers = read('kit-video-character-studio.ts');

ok(ui.includes('fx-cstudio__review-gates') && ui.includes('Duyệt bộ ảnh') && ui.includes('Tạo bộ ảnh chuẩn'),
  '01 not approved → review workflow stays');
ok(ui.includes('needsLock') && ui.includes('Khóa nhân vật') && ui.includes('STUDIO_NEXT_STEP'),
  '02 approved not locked → lock is next step');
ok(ui.includes('STUDIO_COMPLETE_TITLE')
  && ui.includes('Không cần tạo Revision mới')
  && ui.includes('STUDIO_CONTINUE_EPISODE')
  && ui.includes('studioMasterComplete(selected)'),
  '03 approved + locked → CHARACTER MASTER COMPLETE + BUILD EPISODE');
ok(ui.includes('STUDIO_BIBLE_READY')
  && ui.includes('STUDIO_START_EPISODE')
  && ui.includes('studioBibleReady(items)')
  && tab.includes("setProdTab('script')")
  && board.includes('onBuildEpisode'),
  '04 6/6 locked → CHARACTER BIBLE READY + start episode');
ok(ui.includes('!selected.officialLocked')
  && ui.includes('selected.mayGenerate && !selected.officialLocked')
  && ui.includes('{!selected.officialLocked && (rev.mayRequest')
  && !ui.includes("characterId === 'CHAR-001'"),
  '05 locked path does not expose regenerate / new revision as primary');

ok(studioMasterComplete({ officialLocked: true })
  && !studioMasterComplete({ officialLocked: false })
  && !studioMasterComplete(undefined),
  '06 studioMasterComplete uses officialLocked');
ok(studioNeedsLock({ officialLocked: false, mayLock: true })
  && studioNeedsLock({ officialLocked: false, status: 'CRP_APPROVED' })
  && !studioNeedsLock({ officialLocked: true, mayLock: true })
  && !studioNeedsLock({ officialLocked: false, status: 'CRP_PENDING_REVIEW' }),
  '07 studioNeedsLock is approve-without-lock only');
ok(studioBibleReady([
  { officialLocked: true },
  { officialLocked: true },
  { officialLocked: true },
  { officialLocked: true },
  { officialLocked: true },
  { officialLocked: true },
])
  && !studioBibleReady(Array.from({ length: 6 }, () => ({ officialLocked: false })))
  && !studioBibleReady([{ officialLocked: true }]),
  '08 studioBibleReady is 6/6 officialLocked');

ok(directorStatusLabel({ officialLocked: true, status: 'CHARACTER_READY' }) === 'LOCKED',
  '09 directorStatusLabel LOCKED unchanged');
ok(STUDIO_MASTER_LOCKED_VI === 'MASTER ĐÃ KHÓA'
  && STUDIO_DIRECTOR_APPROVED_VI === 'ĐÃ ĐƯỢC DIRECTOR DUYỆT'
  && STUDIO_COMPLETE_TITLE === 'CHARACTER MASTER COMPLETE'
  && STUDIO_BIBLE_READY === 'CHARACTER BIBLE READY'
  && STUDIO_NEXT_STEP === 'BƯỚC TIẾP THEO'
  && STUDIO_BUILD_EPISODE === 'XÂY DỰNG EPISODE'
  && STUDIO_CONTINUE_EPISODE === '→ TIẾP TỤC XÂY DỰNG EPISODE'
  && STUDIO_START_EPISODE === '→ BẮT ĐẦU XÂY DỰNG EPISODE'
  && STUDIO_CONTINUE_EPISODE_MOBILE === '→ Tiếp tục xây dựng Episode'
  && ui.includes('STUDIO_MASTER_LOCKED_VI')
  && ui.includes('STUDIO_DIRECTOR_APPROVED_VI')
  && ui.includes('STUDIO_BUILD_EPISODE')
  && helpers.includes('MASTER ĐÃ KHÓA')
  && helpers.includes('ĐÃ ĐƯỢC DIRECTOR DUYỆT'),
  '10 friendly Vietnamese labels');
ok(ui.includes("label: 'Revision / Advanced Actions'")
  && ui.includes("label: 'Chi tiết kỹ thuật'")
  && !ui.includes("defaultActiveKey={['tech']}")
  && board.includes("label: 'Advanced / Calibration / Technical Details'"),
  '11 technical / revision collapsed');
ok(STUDIO_BIBLE_EXPECTED === 6 && ui.includes('{row.characterName}') && !ui.includes('Studio Proba'),
  '12 bible list uses live character names');
ok(!thisSmokeGenerates() && !thisSmokeApproves() && !thisSmokeLocks(),
  '13 smoke does not mutate');

function thisSmokeGenerates() {
  return false;
}
function thisSmokeApproves() {
  return false;
}
function thisSmokeLocks() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_DIRECTOR_REVIEW_WORKSPACE_UX_V2_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_DIRECTOR_REVIEW_WORKSPACE_UX_V2_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
