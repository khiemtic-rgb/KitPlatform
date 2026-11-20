/**
 * Tiến độ một phim luồng Novixa (ảnh từng câu + giọng + video), dùng cho thanh bước trong phim
 * và thẻ phim ở danh sách Video Studio. Chỉ đọc graph đã lưu, không gọi máy chủ.
 */
import { filmScriptHash, novixaUnits, type FilmOrchestration, type NovixaMotionRun, type NovixaStill } from './film-production-orchestrator';

export type FilmStepKey = 'script' | 'pictures' | 'video' | 'shorts' | 'publish';
export type FilmStepStatus = 'done' | 'doing' | 'todo';

export type FilmStepSummary = { key: FilmStepKey; n: number; label: string; sub: string; status: FilmStepStatus };

export type NovixaFilmSummary = {
  lines: number;
  scenes: number;
  /** Phim dài (từ 12 câu) cần cả bản ngang và bản dọc; phim ngắn chỉ cần bản dọc. */
  needsWide: boolean;
  scriptLocked: boolean;
  scriptChanged: boolean;
  wide: { stills: number; approved: number; video: boolean };
  tall: { stills: number; approved: number; video: boolean };
  shortPlans: number;
  shortsDone: number;
  exported: number;
  exportTarget: number;
  steps: FilmStepSummary[];
  next: { step: FilmStepKey; text: string };
};

type GraphLike = {
  scriptDraft?: string | null;
  packDraft?: string | null;
  filmOrchestration?: FilmOrchestration | null;
};

const SCENE = /^(?:#{1,6}\s*)?(?:\*\*)?\s*(?:cảnh|canh|sc)\s*\d+/i;

function videoReady(run?: NovixaMotionRun | null) {
  return run?.status === 'ready' && Boolean(run.videoPath);
}

function count(stills?: NovixaStill[] | null) {
  const rows = stills ?? [];
  return { stills: rows.length, approved: rows.filter((row) => row.approved).length };
}

export function novixaFilmSummary(graph: GraphLike | null | undefined): NovixaFilmSummary {
  const film = graph?.filmOrchestration ?? undefined;
  const script = film?.script || graph?.scriptDraft || graph?.packDraft || '';
  const draft = graph?.scriptDraft || script;
  const lines = novixaUnits(draft).length;
  const scenes = draft.split(/\r?\n/).filter((line) => SCENE.test(line.trim())).length;
  const needsWide = lines >= 12;
  const scriptLocked = film?.analysis === 'confirmed' && Boolean(film.script);
  const scriptChanged = scriptLocked && filmScriptHash(draft) !== (film?.scriptHash || filmScriptHash(film?.script || ''));
  const orient = film?.stillOrientation ?? ((film?.stills?.length ?? 0) ? 'tall' : needsWide ? 'wide' : 'tall');
  const current = { ...count(film?.stills), video: videoReady(film?.motion) };
  const shelf = film?.shelf;
  const other = { ...count(shelf?.stills), video: videoReady(shelf?.motion) };
  const empty = { stills: 0, approved: 0, video: false };
  const wide = orient === 'wide' ? current : shelf?.orientation === 'wide' ? other : empty;
  const tall = orient === 'tall' ? current : shelf?.orientation === 'tall' ? other : empty;
  const shortPlans = film?.shortPlans?.length ?? 0;
  const shortsDone = film?.shortsDone ?? 0;
  const exported = Object.keys(film?.exports ?? {}).length;
  const exportTarget = (needsWide ? 2 : 1) + Math.max(shortPlans, shortsDone);

  const full = (side: { stills: number; approved: number }) => lines > 0 && side.stills >= lines && side.approved >= side.stills;
  const picturesDone = full(tall) && (!needsWide || full(wide));
  const videoDone = tall.video && (!needsWide || wide.video);
  const shortsStepDone = !needsWide || (shortPlans > 0 && shortsDone >= shortPlans);
  const publishDone = exportTarget > 0 && exported >= exportTarget;

  let next: NovixaFilmSummary['next'];
  if (!lines) next = { step: 'script', text: 'Nạp hoặc dán kịch bản' };
  else if (!scriptLocked || scriptChanged) next = { step: 'script', text: scriptChanged ? 'Khóa lại kịch bản vừa sửa' : 'Khóa kịch bản' };
  else if (needsWide && !full(wide)) next = { step: 'pictures', text: wide.stills ? 'Duyệt ảnh khung ngang' : 'Tạo ảnh khung ngang' };
  else if (needsWide && !wide.video) next = { step: 'video', text: 'Dựng video ngang' };
  else if (!full(tall)) next = { step: 'pictures', text: tall.stills ? 'Duyệt ảnh khung dọc' : 'Tạo ảnh khung dọc' };
  else if (!tall.video) next = { step: 'video', text: 'Dựng video dọc' };
  else if (needsWide && !shortPlans) next = { step: 'shorts', text: 'Chọn cảnh cho video ngắn' };
  else if (needsWide && shortsDone < shortPlans) next = { step: 'shorts', text: 'Xuất video ngắn' };
  else if (!publishDone) next = { step: 'publish', text: 'Đưa thành phẩm sang Video thành phẩm' };
  else next = { step: 'publish', text: 'Xong. Duyệt và đăng ở Agent đăng bài' };

  const status = (done: boolean, key: FilmStepKey): FilmStepStatus => (done ? 'done' : next.step === key ? 'doing' : 'todo');
  const side = (label: string, s: { stills: number; approved: number }) => `${label} ${s.approved}/${lines || 0}`;
  const pictureSub = needsWide ? `${side('Ngang', wide)} · ${side('Dọc', tall)}` : side('Dọc', tall);
  const videoSub = needsWide
    ? `Ngang ${wide.video ? '✓' : 'chưa'} · Dọc ${tall.video ? '✓' : 'chưa'}`
    : `Dọc ${tall.video ? '✓' : 'chưa'}`;

  const steps: FilmStepSummary[] = [
    {
      key: 'script',
      n: 1,
      label: 'Kịch bản',
      sub: !lines ? 'Chưa có' : scriptChanged ? 'Đã sửa, cần khóa lại' : scriptLocked ? `Đã khóa · ${lines} câu` : `${lines} câu · chưa khóa`,
      status: status(scriptLocked && !scriptChanged, 'script'),
    },
    { key: 'pictures', n: 2, label: 'Hình', sub: pictureSub, status: status(picturesDone, 'pictures') },
    { key: 'video', n: 3, label: 'Giọng & video', sub: videoSub, status: status(videoDone, 'video') },
    {
      key: 'shorts',
      n: 4,
      label: 'Video ngắn',
      sub: needsWide ? `${shortsDone}/${shortPlans || '–'}` : 'Không cần (phim ngắn)',
      status: status(shortsStepDone, 'shorts'),
    },
    { key: 'publish', n: 5, label: 'Xuất & đăng', sub: `${exported}/${exportTarget}`, status: status(publishDone, 'publish') },
  ];

  return { lines, scenes, needsWide, scriptLocked, scriptChanged, wide, tall, shortPlans, shortsDone, exported, exportTarget, steps, next };
}

export type FilmScriptSource = { id: string; code: string; title: string; approved: boolean };

/** Nguồn kịch bản của phim: lưu trong phần điều phối sau khi khóa, hoặc ở graph lúc vừa tạo phim từ thư viện. */
export function filmScriptSource(graph: (GraphLike & { novixaScriptSource?: FilmScriptSource | null }) | null | undefined): FilmScriptSource | undefined {
  return graph?.filmOrchestration?.scriptSource ?? graph?.novixaScriptSource ?? undefined;
}
