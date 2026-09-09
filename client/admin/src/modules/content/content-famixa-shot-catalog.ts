import type { FamixaSeriesShot, SeriesShotRun } from './content-famixa-series';
import { FAMIXA_VISUAL_MODE, visualModesCompatible } from './kit-video-visual-mode';

export const SHOT_PAGE_SIZES = [20, 50, 100] as const;
export const SHOT_PAGE_DEFAULT = 20;

export const CAST_NAME: Record<string, string> = {
  'CHAR-001': 'Minh',
  'CHAR-002': 'Nam',
  'CHAR-003': 'Linh',
  'CHAR-004': 'An',
};

export function storyActionsAlign(bound?: string, story?: string) {
  const a = (bound || '').replace(/\s+/g, ' ').trim().toLowerCase();
  const b = (story || '').replace(/\s+/g, ' ').trim().toLowerCase();
  if (!a || !b) return false;
  if (a === b) return true;
  const slice = a.length >= 16 ? a.slice(0, 16) : a;
  return b.includes(slice) || (b.length >= 16 && a.includes(b.slice(0, 16)));
}

/** Hide leftover / photoreal / other-story media. Approved EP takes without bind stay visible. */
export function productionMediaAllowed(run?: SeriesShotRun, story?: string) {
  if (!run) return false;
  if (run.visualMode && !visualModesCompatible(FAMIXA_VISUAL_MODE.visualMode, run.visualMode)) return false;
  if (run.visualPipelineStatus === 'INVALID_REFERENCE_PIPELINE') return false;
  const now = (story || '').replace(/\s+/g, ' ').trim();
  if (!now) return true;
  const bound = (run.kfBoundAction || '').replace(/\s+/g, ' ').trim();
  const approved = Boolean(run.kfApproved || run.status === 'approved');
  if (approved) {
    if (bound && !storyActionsAlign(bound, now)) return false;
    return true;
  }
  return Boolean(bound && storyActionsAlign(bound, now));
}

export function clipOf(run?: SeriesShotRun, story?: string) {
  const url = run?.lipsyncUrl || run?.previewUrl || run?.takeUrl || '';
  if (!url || !productionMediaAllowed(run, story)) return '';
  return url;
}

export function stillOf(run?: SeriesShotRun, story?: string) {
  const url = (run?.keyframeDataUrl || '').trim();
  if (!url || !productionMediaAllowed(run, story)) return '';
  return url;
}

export function castNames(ids?: string[]) {
  const names = (ids || []).map((id) => CAST_NAME[id] || id).filter(Boolean);
  return names.length ? names.join(', ') : '—';
}

export function shortStory(shot: FamixaSeriesShot, max = 72) {
  const text = (shot.story || shot.scene || shot.shot || '').trim() || '—';
  return text.length > max ? `${text.slice(0, max - 1)}…` : text;
}

export type ShotFilter = 'all' | 'wait' | 'work' | 'review' | 'done';

export function shotTone(run?: SeriesShotRun): ShotFilter {
  if (clipOf(run)) return 'done';
  if (stillOf(run)) return 'review';
  return 'wait';
}

export function shotStatus(run?: SeriesShotRun) {
  const tone = shotTone(run);
  if (tone === 'done') return { mark: '✓', label: 'Đã xong', tone };
  if (tone === 'review') return { mark: '●', label: 'Cần duyệt', tone };
  if (tone === 'work') return { mark: '●', label: 'Đang làm', tone };
  return { mark: '○', label: 'Chưa làm', tone };
}

export function imageStatus(run?: SeriesShotRun) {
  if (!stillOf(run)) return { mark: '○', label: 'Chưa tạo', tone: 'wait' as const };
  if (run?.kfApproved || run?.status === 'approved') {
    return { mark: '✓', label: 'Đã duyệt', tone: 'done' as const };
  }
  if (clipOf(run)) return { mark: '✓', label: 'Đã duyệt', tone: 'done' as const };
  return { mark: '●', label: 'Chờ duyệt', tone: 'review' as const };
}

export function videoStatus(run?: SeriesShotRun) {
  if (clipOf(run) && (run?.videoApproved || run?.status === 'approved')) {
    return { mark: '✓', label: 'Đã duyệt', tone: 'done' as const };
  }
  if (clipOf(run)) return { mark: '●', label: 'Chờ duyệt', tone: 'review' as const };
  if (run?.turboError) return { mark: '⚠', label: 'Lỗi tạo video', tone: 'review' as const };
  if (stillOf(run)) return { mark: '○', label: 'Chưa tạo', tone: 'wait' as const };
  return { mark: '○', label: 'Chưa đủ điều kiện', tone: 'wait' as const };
}

/** Staff copy for I2V send failures — do not tell them to fix KF unless the error is about the still. */
export function staffVideoSendError(err?: string) {
  const raw = (err || '').trim();
  if (!raw) return '';
  if (/CONFIRMATION_REQUIRED/i.test(raw)) {
    return 'Chưa gửi được vì thiếu xác nhận hệ thống. Bấm Tạo video lại — không cần sửa ảnh.';
  }
  if (/too_big|promptText|maximum.:1000|Too big/i.test(raw)) {
    return 'Prompt gửi máy tạo video dài quá 1000 ký tự. Đã rút gọn — bấm Tạo video lại, không cần sửa ảnh.';
  }
  if (/INTERNAL\.BAD_OUTPUT|RENDER_FAILURE/i.test(raw)) {
    return 'Máy tạo video lỗi render cảnh này — không phải ảnh chắc hỏng. Đừng bấm Tạo video cùng lúc. Bấm Gửi lại với camera khác (trừ credit job mới). Runway có thể hoàn credit job lỗi.';
  }
  if (/429|rate.?limit|hạn mức|quiet/i.test(raw)) {
    return 'Máy tạo video đang giới hạn tốc độ. Đợi rồi bấm Tạo video lại.';
  }
  if (/chưa có key|api key|RUNWAY_API|fal key/i.test(raw)) {
    return 'Chưa cấu hình máy tạo video. Mở Model AI → Video rồi thử lại.';
  }
  if (/Thiếu KF|Chưa có ảnh|JPEG|1280|placeholder|data-URI/i.test(raw)) {
    return raw.length > 180 ? `${raw.slice(0, 179)}…` : raw;
  }
  return raw.length > 180 ? `${raw.slice(0, 179)}…` : raw;
}

export function filterShots(
  shots: FamixaSeriesShot[],
  runOf: (id: string) => SeriesShotRun,
  filter: ShotFilter,
  query: string,
  mode: 'scenes' | 'images' | 'video',
) {
  const q = query.trim().toLowerCase();
  return shots
    .map((shot, index) => ({ shot, index, run: runOf(shot.id) }))
    .filter(({ shot, run }) => {
      const st = mode === 'images' ? imageStatus(run) : mode === 'video' ? videoStatus(run) : shotStatus(run);
      if (filter !== 'all' && st.tone !== filter) return false;
      if (!q) return true;
      return `${index + 1} ${shot.story || ''} ${shot.scene || ''} ${castNames(shot.characters)}`.toLowerCase().includes(q);
    });
}
