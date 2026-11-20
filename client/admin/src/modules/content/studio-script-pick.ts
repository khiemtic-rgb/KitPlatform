/**
 * Hộp "Tạo phim từ kịch bản": kịch bản nào đã có phim thì ẩn mặc định,
 * kịch bản đã duyệt mà chưa có phim lên trước và được chọn sẵn.
 */

export type PickScript = { id: string; code: string; title: string; approved: boolean };
export type PickFilm = { id: string; title?: string | null; episodeCode?: string | null; source?: { id?: string; code?: string } | null };

export type ScriptRow<S extends PickScript, F extends PickFilm> = {
  script: S;
  /** Phim đã dựng từ kịch bản này, mới nhất trước. */
  films: F[];
};

function norm(value: string | undefined | null) {
  return (value || '').trim().toLowerCase();
}

/** Ghép phim với kịch bản: theo id nguồn, không có id thì theo mã kịch bản (01, 02…). */
export function filmsByScript<S extends PickScript, F extends PickFilm>(scripts: S[], films: F[]): ScriptRow<S, F>[] {
  return scripts.map((script) => ({
    script,
    films: films.filter((film) => {
      const src = film.source;
      if (!src) return false;
      if (src.id && script.id) return src.id === script.id;
      return !!src.code && norm(src.code) === norm(script.code);
    }),
  }));
}

/**
 * Thứ tự hiện: đã duyệt + chưa có phim → chờ duyệt + chưa có phim → (nếu bật) đã có phim.
 * Trong mỗi nhóm giữ thứ tự của thư viện (theo mã).
 */
export function orderScripts<S extends PickScript, F extends PickFilm>(rows: ScriptRow<S, F>[], showMade: boolean): ScriptRow<S, F>[] {
  const rank = (row: ScriptRow<S, F>) => (row.films.length > 0 ? 2 : row.script.approved ? 0 : 1);
  return rows
    .map((row, index) => ({ row, index }))
    .filter(({ row }) => showMade || row.films.length === 0)
    .sort((a, b) => rank(a.row) - rank(b.row) || a.index - b.index)
    .map(({ row }) => row);
}

/** Chọn sẵn: kịch bản đã duyệt đầu tiên chưa có phim; không có thì không chọn sẵn (rỗng = dán tay). */
export function defaultScriptPick<S extends PickScript, F extends PickFilm>(rows: ScriptRow<S, F>[]): string {
  return rows.find((row) => row.script.approved && row.films.length === 0)?.script.id ?? '';
}

/** Tên phim gợi ý. Kịch bản đã có phim thì thêm "(bản N)". */
export function suggestFilmTitle<S extends PickScript, F extends PickFilm>(row: ScriptRow<S, F>): string {
  const base = `${row.script.code} – ${row.script.title}`;
  return row.films.length > 0 ? `${base} (bản ${row.films.length + 1})` : base;
}

/** Nhãn ngắn cho phim đã có: mã tập hoặc tên phim. */
export function filmLabel(film: PickFilm): string {
  return (film.episodeCode || '').trim() || (film.title || '').trim() || 'phim';
}
