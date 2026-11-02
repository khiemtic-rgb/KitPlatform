/** Film-level production plan. Run: npx tsx src/modules/content/film-production-orchestrator.smoke.ts */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { emptyEpisode, ensurePilotGraph, newSceneShot, parseFamixaPack, slimPilotForServer, type SeriesPilotState } from './content-famixa-series';
import {
  analyzeFilmScript,
  applyConfirmedBreakdown,
  continueFilmProduction,
  emptyFilmState,
  failFilmTask,
  applyNovixaMotion,
  filmProductionSnapshot,
  NOVIXA_LOCAL_RENDER_DETAIL,
  novixaMotionInputs,
  novixaInputKey,
  novixaUnits,
  novixaVideoCurrent,
  novixaVideoGate,
  novixaClientOutcome,
  adoptNovixaStatus,
  mergeNovixaStills,
  novixaBrandProfile,
  novixaStillBrandStale,
  NOVIXA_VOICE,
  planFilmTasks,
  spokenLinesFromScript,
  startFilmProduction,
} from './film-production-orchestrator';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const novixa = `CẢNH 01 — Quầy thuốc
Dược sĩ đưa vỉ thuốc lên quầy.
Dược sĩ: Thuốc này uống sau ăn.
Khách: Ngày mấy lần ạ?

CẢNH 02 — Nhà
Dược sĩ: Nhớ kiểm tra hạn dùng trước khi uống.
Khách: Em ghi lại rồi.`;

const proposal = analyzeFilmScript(novixa, 'EP01');
ok(proposal.ok, '1 script analysis returns a proposal');
ok(proposal.scenes.length >= 2, '1 analysis finds more than one scene');
ok(proposal.shots.length >= 2, '1 analysis finds more than one shot');
ok(proposal.shots[0]!.order < proposal.shots.at(-1)!.order, '1 shot order follows the script');
ok(proposal.shots.some((shot) => /sau ăn/.test(shot.story)) && proposal.shots.some((shot) => /hạn dùng/.test(shot.story)), '1 dialogue stays on the matching scene');
const prose = 'Khách đứng trước quầy và nghe giải thích về cách uống thuốc trong một đoạn văn liền, không có tiêu đề cảnh và không có lời thoại tách vai. '.repeat(8);
const flat = analyzeFilmScript(prose, 'EP01');
ok(flat.shots.length === 1 && flat.scenes.length === 1, '1 a long paragraph is not split by length');

const idle = emptyFilmState();
const refused = applyConfirmedBreakdown(idle, proposal, { confirmed: false });
ok(!refused.ok && refused.reason === 'needs_confirm' && !idle.episode?.shots?.length, '2 production waits for breakdown confirmation');
const confirmed = applyConfirmedBreakdown(idle, proposal, { confirmed: true, seriesCode: 'NOVIXA' });
ok(confirmed.ok && confirmed.state.episode?.shots.length === proposal.shots.length, '2 confirmation stores the scenes and shots');
ok(confirmed.ok && confirmed.state.episode?.seriesCode === 'NOVIXA', '2 a new film keeps the series code it was opened with');
ok(confirmed.ok && confirmed.state.filmOrchestration?.analysis === 'confirmed', '2 confirmation is stored on the film');
ok(confirmed.ok && confirmed.state.filmOrchestration?.tasks.length === 0, '2 confirmation does not start production');
ok(
  confirmed.ok && ensurePilotGraph(confirmed.state).episode?.shots.length === proposal.shots.length,
  '2 opening the series graph keeps the confirmed shots',
);

const film = confirmed.ok ? confirmed.state : idle;
const steps = ['script', 'scene', 'shot', 'image', 'video', 'review', 'export', 'grade'];
const planned = planFilmTasks(film, steps);
const shotIds = new Set((film.episode?.shots ?? []).map((shot) => shot.id));
ok(planned.length > 0 && planned.every((task) => shotIds.has(task.shotId)), '3 the plan contains only this film');
const withForeign: SeriesPilotState = {
  ...film,
  runs: { ...film.runs, 'OTHER-FILM-SH01': { status: 'story_locked', previewUrl: 'https://example.invalid/other.mp4' } },
};
ok(planFilmTasks(withForeign, steps).every((task) => task.shotId !== 'OTHER-FILM-SH01'), '3 a shot from another film is not planned');
ok(!planned.some((task) => task.step === 'voice'), '3 a step that is not on this pipeline is not added');

const first = film.episode?.shots[0]?.id ?? '';
const firstTasks = planned.filter((task) => task.shotId === first).map((task) => task.step);
ok(firstTasks.join(',') === 'script,scene,shot,image,video,review,export,grade', '4 steps stay in pipeline order');
ok(planned.find((task) => task.shotId === first && task.step === 'image')?.status === 'unavailable', '4 image has no film executor');
ok(planned.find((task) => task.shotId === first && task.step === 'video')?.status === 'blocked', '4 video waits for an image');
ok(planned.find((task) => task.shotId === first && task.step === 'grade')?.status === 'unavailable', '8 an unsupported step is not complete');
ok(!planned.some((task) => (task.step === 'image' || task.step === 'video' || task.step === 'grade' || task.step === 'export') && task.status === 'done'), '8 paid and unsupported steps are not marked done');

const started = startFilmProduction(film, steps);
ok(started.changed && started.state.filmOrchestration?.tasks.length === planned.length, '6 the first start records one task per step');
const again = startFilmProduction(started.state, steps);
ok(!again.changed && again.state.filmOrchestration?.tasks.length === planned.length, '6 a second start does not add tasks');
const unique = new Set(started.state.filmOrchestration?.tasks.map((task) => task.taskKey));
ok(unique.size === planned.length, '6 task keys are unique');

const imageKey = `${first}:image`;
const failed = failFilmTask(started.state, imageKey, 'provider unavailable');
const otherDone = (failed.filmOrchestration?.tasks ?? []).filter((task) => task.shotId !== first && task.step === 'script');
ok(failed.filmOrchestration?.tasks.find((task) => task.taskKey === imageKey)?.status === 'failed', '5 one shot can fail');
ok(otherDone.length > 0 && otherDone.every((task) => task.status === 'done'), '5 another shot keeps its completed step');
const resumed = continueFilmProduction(failed, steps);
ok(resumed.changed, '6 retry updates the failed task');
ok(resumed.state.filmOrchestration?.tasks.length === planned.length, '6 retry does not add a task');
ok(resumed.state.filmOrchestration?.tasks.find((task) => task.taskKey === imageKey)?.status === 'unavailable', '6 retry returns the same step');
ok(
  (resumed.state.filmOrchestration?.tasks ?? []).filter((task) => task.shotId !== first && task.step === 'script').every((task) => task.status === 'done'),
  '5 resume keeps the shot that already succeeded',
);

const saved = JSON.parse(JSON.stringify(slimPilotForServer(resumed.state))) as SeriesPilotState;
const loaded = filmProductionSnapshot(saved, steps);
ok(loaded.failed === 0 && loaded.shotCount === proposal.shots.length && saved.filmOrchestration?.tasks.length === planned.length, '7 progress survives a save and reload');
ok(saved.filmOrchestration?.script === novixa && saved.packDraft === undefined, '7 the confirmed script stays after the server projection drops pack text');
ok(loaded.imageDone === 0 && loaded.videoDone === 0, '7 reload does not invent image or video output');
ok(!loaded.reviewExportReady, '7 review and export stay closed');

const famixaScript = `VIDEO TITLE: Bữa tối
EPISODE: 01

SC01 — Bếp
Minh chạy vào bếp.
MINH
Bố ơi, con đói.

SC02 — Phòng khách
LINH
Mẹ đây.
MINH
Con cảm ơn mẹ.`;
const famixa = analyzeFilmScript(famixaScript, 'EP01');
const famixaPack = parseFamixaPack(famixaScript);
ok((famixaPack.episode?.shots.length ?? 0) > 0 && (famixaPack.scenes?.length ?? 0) >= 2, '9 the existing screenplay parser still reads a series script');
ok(famixa.parser === 'screenplay' && famixa.scenes.length >= 2 && famixa.shots.length >= 2, '9 film analysis reuses that parser');

const old = newSceneShot('SC01', 1, 'shot đã dựng');
const occupied: SeriesPilotState = {
  ...emptyFilmState(),
  episode: { ...emptyEpisode(), seriesCode: 'NOVIXA', shots: [old] },
  runs: { [old.id]: { status: 'story_locked', keyframeFileName: 'kept.png' } },
};
const blocked = applyConfirmedBreakdown(occupied, proposal, { confirmed: true, seriesCode: 'NOVIXA' });
ok(!blocked.ok && blocked.reason === 'has_production', '2 replacing produced shots requires confirmation');
const replaced = applyConfirmedBreakdown(occupied, proposal, { confirmed: true, replaceProduced: true, seriesCode: 'NOVIXA' });
ok(replaced.ok && replaced.state.runs[old.id]?.keyframeFileName === 'kept.png', '2 a replaced shot does not delete its asset');
ok(replaced.ok && replaced.state.episode?.seriesCode === 'NOVIXA', '2 an existing film keeps its series code');

const novixaPlan = planFilmTasks(film, steps, 'novixa_local');
const novixaVideo = novixaPlan.find((task) => task.shotId === first && task.step === 'video');
ok(novixaVideo?.status === 'unavailable' && novixaVideo.detail === NOVIXA_LOCAL_RENDER_DETAIL, 'Novixa video stays unrun and is not marked done');
ok(novixaPlan.find((task) => task.shotId === first && task.step === 'image')?.status === 'unavailable', 'Novixa image is not a Famixa still');
ok(novixaPlan.find((task) => task.shotId === first && task.step === 'script')?.status === 'done', 'Novixa script confirmation stays local');
ok(!novixaPlan.some((task) => task.status === 'done' && (task.step === 'image' || task.step === 'video')), 'Novixa paid steps are not completed by the planner');
const spoken = spokenLinesFromScript(film.filmOrchestration?.script || '');
ok(spoken.includes('Thuốc này uống sau ăn.') && spoken.every((line) => !line.startsWith('CẢNH')), 'Novixa speech comes from the film script');
const kept = slimPilotForServer({ ...emptyFilmState(), scriptDraft: 'CẢNH 01\nXin chào.' });
ok(kept.scriptDraft === 'CẢNH 01\nXin chào.' && kept.packDraft === undefined, 'the typed script stays when the server copy drops pack text');
const fromStory = slimPilotForServer({ ...emptyFilmState(), packDraft: 'Nội dung tập.' });
ok(fromStory.scriptDraft === 'Nội dung tập.' && fromStory.packDraft === undefined, 'story text stays when the server copy drops pack text');
const passed = startFilmProduction(film, steps, 'novixa_local');
ok(passed.state.filmOrchestration?.tasks.find((task) => task.shotId === first && task.step === 'script')?.status === 'done', 'confirming the script passes that step');
ok(passed.state.filmOrchestration?.tasks.find((task) => task.shotId === first && task.step === 'scene')?.status === 'done', 'the scene step passes with the confirmed script');
const inputs = novixaMotionInputs(film);
ok(!inputs.block && inputs.lines.length > 0 && inputs.screens.length === 0, 'a film script renders without image files');
ok(!spokenLinesFromScript('[HƯỚNG HIỆU]\nĐó là tiền của bạn.').some((line) => line.startsWith('[')), 'direction marks are not spoken');
const ready = applyNovixaMotion(film, steps, { status: 'ready', message: 'Đã xuất video mới.', folder: 'E:/novixa-studio/film/run', videoPath: 'E:/novixa-studio/film/run/video.mp4' });
ok(ready.filmOrchestration?.motion?.videoPath?.endsWith('video.mp4'), 'a new render path is stored on the film');
ok(ready.filmOrchestration?.tasks.find((task) => task.shotId === first && task.step === 'video')?.status !== 'done', 'an older video does not count as the current pictures and voice');
ok(!String(ready.filmOrchestration?.motion?.videoPath).includes('stock.mp4'), 'the stored path is not the sample stock file');
const currentTake = applyNovixaMotion({
  ...film,
  filmOrchestration: film.filmOrchestration ? {
    ...film.filmOrchestration,
    stills: [{ index: 0, id: 'U01', text: 'Xin chào.', path: 'a.png', approved: true }],
    voiceChoice: { model: NOVIXA_VOICE.model, voiceId: NOVIXA_VOICE.voiceId, approved: true },
  } : film.filmOrchestration,
}, steps, {
  status: 'ready',
  message: 'Đã xuất video mới.',
  folder: 'E:/novixa-studio/film/run',
  videoPath: 'E:/novixa-studio/film/run/video.mp4',
  inputKey: novixaInputKey(['Xin chào.'], NOVIXA_VOICE.voiceId, ['a.png']),
});
ok(novixaVideoCurrent(currentTake), 'the new take matches the approved pictures and voice');
ok(currentTake.filmOrchestration?.tasks.find((task) => task.shotId === first && task.step === 'video')?.status === 'done', 'a matching render is stored on the film');
const storedDone: SeriesPilotState = {
  ...ready,
  filmOrchestration: ready.filmOrchestration ? {
    ...ready.filmOrchestration,
    tasks: ready.filmOrchestration.tasks.map((task) => task.step === 'video' ? { ...task, status: 'done', detail: 'old' } : task),
  } : ready.filmOrchestration,
};
const stale = continueFilmProduction(storedDone, steps, 'novixa_local');
ok(stale.changed && stale.state.filmOrchestration?.tasks.find((task) => task.shotId === first && task.step === 'video')?.status !== 'done', 'continue drops a video that does not use the current pictures and voice');

const root = dirname(fileURLToPath(import.meta.url));
const orchestrator = readFileSync(join(root, 'film-production-orchestrator.ts'), 'utf8');
const desk = readFileSync(join(root, 'ContentFilmProductionDesk.tsx'), 'utf8');
const board = readFileSync(join(root, 'ContentPipelineBoard.tsx'), 'utf8');
const tab = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const host = readFileSync(join(root, 'ContentVideoPipelineHost.tsx'), 'utf8');
const forbidden = /from '\.\/content-famixa-runway|from '\.\/famixa-fal|gemini|draftContentSeriesScript|axios|generateContentSeriesStill|startContentSeriesLipsync/;
ok(!forbidden.test(orchestrator) && !forbidden.test(desk), '10 the film plan does not call a paid provider');
ok(desk.includes('Phân tích kịch bản') && desk.includes('Xác nhận phân cảnh') && desk.includes('Sản xuất toàn bộ phim'), 'desk exposes the film actions');
ok(desk.includes('Chưa khả dụng'), 'desk shows an unimplemented step');
ok(tab.includes('usesFilmScriptDesk(workflowKey)') && tab.includes('ContentFamixaSeriesWorkspace'), '9 the series desk stays, and the film plan is only the short or guided script surface');
ok(!tab.includes('if (explicitBuildRef.current) return;') && tab.includes('flushServerSaveRef'), 'an open film saves its story text to the server');
ok(host.includes('pipelineSteps={row.steps}') && board.includes('steps ?? []'), '3 each film uses its stored steps');
const novixaStudio = readFileSync(join(root, 'ContentNovixaLocalStudio.tsx'), 'utf8');
ok(novixaStudio.includes('executor="novixa_local"') && novixaStudio.includes('build_stock_motion') && novixaStudio.includes('eleven_v3') && novixaStudio.includes('novixaUnits') && novixaStudio.includes('ContentNovixaStages') && !novixaStudio.includes('stock.mp4') && !novixaStudio.includes('sellers-five') && !novixaStudio.includes('ContentFamixaSeriesTab') && !novixaStudio.includes('ContentFamixaCharacterStudio') && !novixaStudio.includes('ContentVideosPage') && !novixaStudio.includes('renderNovixaGeminiStill'), 'Novixa studio runs the stock-motion job from the film');
const stages = readFileSync(join(root, 'ContentNovixaStages.tsx'), 'utf8');
ok(stages.includes('Tạo lại tất cả') && stages.includes('Tạo lại ảnh') && stages.includes('Tạo ảnh Gemini') && stages.split('renderNovixaGeminiStill').length - 1 === 2, 'each short can redraw locally or ask Gemini on purpose');
const units = novixaUnits('[HOOK]\nBạn có biết, nhà thuốc đang có tiền: hết hạn rồi.\nMINH: Xin chào.');
ok(units.map((unit) => unit.text).join('|') === 'Bạn có biết, nhà thuốc đang có tiền: hết hạn rồi.|Xin chào.', 'a sentence colon stays in the picture unit');
const notes = novixaUnits('# KỊCH BẢN\n**Phong cách:** Chân thực, gần gũi, có chiều sâu, không quảng cáo lộ liễu\n**Hình ảnh:** Kệ thuốc buổi sáng.\n**Lời dẫn:**\nMột ngày ở nhà thuốc.\n### CẢNH 01 — MỞ\n---\n**“Bán đâu có ít?”**');
ok(notes.map((unit) => unit.text).join('|') === 'Một ngày ở nhà thuốc.|“Bán đâu có ít?”', 'style notes and shot directions are not spoken');
ok(novixaClientOutcome('ECONNABORTED').status === 'running' && novixaClientOutcome('ERR_BAD_REQUEST').status === 'failed', 'a browser timeout is not a failed render');
const keptVideo = adoptNovixaStatus({ status: 'failed', message: 'timeout', videoPath: null }, { status: 'ready', message: 'Đã xuất video mới.', videoPath: 'E:/novixa-studio/film/run/video.mp4' });
ok(keptVideo.status === 'ready' && keptVideo.videoPath?.endsWith('video.mp4'), 'an existing video replaces the timeout error');
const keptReady = adoptNovixaStatus({ status: 'ready', message: 'có', videoPath: 'E:/keep/video.mp4' }, { status: 'failed', message: 'mới' });
ok(keptReady.videoPath === 'E:/keep/video.mp4', 'a successful video is not replaced by a later failure');
const merged = mergeNovixaStills(
  [{ index: 0, id: 'U01', text: 'Giữ', path: 'a.png', approved: true }, { index: 1, id: 'U02', text: 'Sửa', path: 'b.png', approved: true }],
  [{ index: 0, id: 'U01', text: 'Giữ', path: 'a2.png' }, { index: 1, id: 'U02', text: 'Đã sửa', path: 'b2.png' }],
  1,
);
ok(merged[0]?.approved === true && merged[1]?.approved === false, 'redrawing one picture keeps the approved one');
ok(novixaBrandProfile({ id: 'brand-a', name: 'Alpha', knowledge: { visualColors: '#1144aa', visualStyle: 'ấm', imageNotes: 'gỗ', products: ['kệ'], avoidTerms: ['logo'] } })?.colors === '#1144aa', 'film image colors come from the stored brand');
ok(novixaStillBrandStale({ index: 0, id: 'U01', text: 'Giữ', path: 'a.png', approved: true, brandId: 'brand-a' }, 'brand-b'), 'an approved picture from another brand is marked, not replaced');
ok(!novixaStillBrandStale({ index: 0, id: 'U01', text: 'Giữ', path: 'a.png', approved: true }, 'brand-b'), 'a picture with no stored brand is left as it is');
const redrawn = mergeNovixaStills(
  [{ index: 0, id: 'U01', text: 'Giữ', path: 'a.png', approved: true }],
  [{ index: 0, id: 'U01', text: 'Giữ', path: 'a2.png' }],
  undefined,
  true,
);
ok(redrawn[0]?.approved === false, 'redrawing the set clears approval');
ok(stages.includes('Ảnh chưa theo thương hiệu') && stages.includes('brand') && !stages.includes('useContentBrand'), 'the picture stage uses the film brand, not the nav brand');
const gated = novixaVideoGate({
  ...emptyFilmState(),
  filmOrchestration: {
    analysis: 'confirmed',
    scriptHash: 'x',
    tasks: [],
    stills: [{ index: 0, id: 'U01', text: 'Một câu', path: 'a.png', approved: true }],
    voiceChoice: { model: NOVIXA_VOICE.model, voiceId: NOVIXA_VOICE.voiceId, approved: true },
  },
});
ok(gated == null, 'approved pictures and the tested voice can make a video');
const otherVoice = novixaVideoGate({
  ...emptyFilmState(),
  filmOrchestration: {
    analysis: 'confirmed',
    scriptHash: 'x',
    tasks: [],
    stills: [{ index: 0, id: 'U01', text: 'Một câu', path: 'a.png', approved: true }],
    voiceChoice: { model: NOVIXA_VOICE.model, voiceId: 'abcdefghij123456', approved: true },
  },
});
ok(otherVoice == null, 'a confirmed voice can replace the tested default');
ok(novixaVideoGate({
  ...emptyFilmState(),
  filmOrchestration: {
    analysis: 'confirmed',
    scriptHash: 'x',
    tasks: [],
    stills: [{ index: 0, id: 'U01', text: 'Một câu', path: 'a.png', approved: true }],
    voiceChoice: { model: NOVIXA_VOICE.model, voiceId: NOVIXA_VOICE.voiceId, approved: false },
  },
}) === 'Chưa xác nhận giọng.', 'video waits until the chosen voice is confirmed');
ok(novixaVideoGate({ ...emptyFilmState(), filmOrchestration: { analysis: 'confirmed', scriptHash: 'x', tasks: [], stills: [{ index: 0, id: 'U01', text: 'Một câu', path: 'a.png', approved: false }] } }) === 'Còn ảnh chưa duyệt.', 'video waits until every picture is approved');
ok(board.includes("workspace === 'unconfigured'") && !board.includes('famixa_series'), 'a short or guided film is not sent to another pipeline');

if (fail.length) {
  console.error(fail.join('\n'));
  process.exit(1);
}
console.log('film production orchestrator ok');
