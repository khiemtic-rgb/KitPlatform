import { useMemo, useState } from 'react';
import { Button, Input, Modal } from 'antd';
import type { ContentSeriesBuildSummary } from '@/shared/api/content.api';
import type { ProductionProgressView } from './kit-video-production-progress';
import {
  STUDIO_PAGE_SIZE,
  filterStudioBuilds,
  studioBuildCounts,
  studioBuildLabel,
  studioNextAction,
  suggestNextEpisodeCode,
  videoPrimaryAction,
  type StudioFilter,
} from './content-famixa-series-desk';
import { studioCardFromProgress } from './kit-video-production-progress';

const FILTERS: { id: StudioFilter; label: string }[] = [
  { id: 'all', label: 'Tất cả' },
  { id: 'work', label: 'Đang làm' },
  { id: 'done', label: 'Hoàn thành' },
  { id: 'paused', label: 'Tạm dừng' },
];

export function ContentFamixaSeriesDesk({
  seriesTitle,
  builds,
  progressById,
  busy,
  onOpen,
  onCreate,
  onDelete,
}: {
  seriesTitle: string;
  builds: ContentSeriesBuildSummary[];
  progressById?: Record<string, ProductionProgressView>;
  busy?: boolean;
  onOpen: (id: string) => void;
  onCreate: (input: { title: string; premise: string; note: string; episode: string; script?: string }) => void;
  onDelete?: (id: string, title: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const [step, setStep] = useState(1);
  const [title, setTitle] = useState('');
  const [note, setNote] = useState('');
  const [code, setCode] = useState('');
  const [script, setScript] = useState('');
  const [filter, setFilter] = useState<StudioFilter>('all');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(0);

  const filtered = useMemo(
    () => filterStudioBuilds(builds, filter, query, progressById),
    [builds, filter, query, progressById],
  );
  const pages = Math.max(1, Math.ceil(filtered.length / STUDIO_PAGE_SIZE));
  const visible = filtered.slice(page * STUDIO_PAGE_SIZE, page * STUDIO_PAGE_SIZE + STUDIO_PAGE_SIZE);

  return (
    <section className="fx-desk fx-series-desk">
      <header className="fx-series-desk__head">
        <div>
          <p className="fx-sw__brand">VIDEO STUDIO</p>
          <h1 className="fx-sw__title">{seriesTitle}</h1>
          <p className="fx-desk__lead">Quản lý và sản xuất các video giáo dục của Famixa.</p>
        </div>
        <Button type="primary" size="large" onClick={() => { setStep(1); setOpen(true); }}>
          + Tạo video mới
        </Button>
      </header>

      <div className="fx-series-desk__tools">
        <div className="fx-series-desk__filters">
          {FILTERS.map((item) => (
            <button
              key={item.id}
              type="button"
              className={filter === item.id ? 'is-on' : ''}
              onClick={() => {
                setFilter(item.id);
                setPage(0);
              }}
            >
              {item.label}
            </button>
          ))}
        </div>
        <Input
          allowClear
          placeholder="Tìm video..."
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setPage(0);
          }}
        />
        <p className="fx-desk__note">Mới cập nhật</p>
      </div>

      <p className="fx-prod__kicker">DANH SÁCH VIDEO · {filtered.length}</p>
      {!builds.length ? (
        <article className="fx-media-card">
          <h3>Chưa có video</h3>
          <p>Tạo tập mới để bắt đầu từ kịch bản. Máy không tự viết hay tạo hình.</p>
          <Button type="primary" onClick={() => setOpen(true)}>
            Tạo video mới
          </Button>
        </article>
      ) : null}
      {builds.length && !filtered.length ? (
        <p className="fx-desk__note">{filter === 'paused' ? 'Chưa có video tạm dừng.' : 'Không tìm thấy video.'}</p>
      ) : null}
      {visible.map((row) => {
        const progress = progressById?.[row.id];
        const card = studioCardFromProgress(progress);
        const next = studioNextAction(row, progress);
        return (
          <article key={row.id} className={`fx-series-card fx-series-card--${card.tone}`}>
            <div>
              <p className="fx-prod__code">{row.episodeCode || 'Tập mới'}</p>
              <h3>{studioBuildLabel(row, builds)}</h3>
              <p className="fx-series-card__facts">{studioBuildCounts(row)}</p>
              <p className="fx-prod__st">
                {card.tone === 'done' ? '✓' : card.tone === 'work' ? '●' : '○'} {card.label}
              </p>
              {card.storyLine ? <p>{card.storyLine}</p> : null}
              <p>{card.stageLine || 'Đang tải tiến độ'}</p>
              <ul className="fx-series-card__checks">
                {card.checks.map((c) => (
                  <li key={c.id}>
                    {c.done ? '✓' : '○'} {c.label}
                  </li>
                ))}
              </ul>
              <p className="fx-series-card__need">Cần làm tiếp: {next.need}</p>
              <p className="fx-desk__note">
                Cập nhật: {row.updatedAt ? new Date(row.updatedAt).toLocaleString('vi-VN') : '—'}
              </p>
            </div>
            <div className="fx-series-card__actions">
              <Button type="primary" disabled={busy} onClick={() => onOpen(row.id)}>
                {videoPrimaryAction(row, progress)}
              </Button>
              {onDelete ? (
                <Button danger disabled={busy} onClick={() => onDelete(row.id, row.title || row.episodeCode || '')}>
                  Xóa
                </Button>
              ) : null}
            </div>
          </article>
        );
      })}
      {pages > 1 ? (
        <div className="fx-series-desk__page">
          <Button disabled={page <= 0} onClick={() => setPage((p) => p - 1)}>
            Trước
          </Button>
          <span>
            {page + 1}/{pages}
          </span>
          <Button disabled={page + 1 >= pages} onClick={() => setPage((p) => p + 1)}>
            Sau
          </Button>
        </div>
      ) : null}

      <Modal
        title={step === 1 ? 'Bước 1 · Thông tin video' : step === 2 ? 'Bước 2 · Kịch bản' : 'Bước 3 · Xác nhận'}
        open={open}
        footer={null}
        onCancel={() => {
          setOpen(false);
          setStep(1);
        }}
      >
        {step === 1 ? (
          <>
            <label>
              Series
              <Input value={seriesTitle} disabled />
            </label>
            <label>
              Tên video
              <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Ví dụ: Một ngày bình yên" />
            </label>
            <label>
              Mã video
              <Input value={code} onChange={(e) => setCode(e.target.value)} placeholder={suggestNextEpisodeCode(builds)} />
            </label>
            <label>
              Mô tả
              <Input.TextArea rows={3} value={note} onChange={(e) => setNote(e.target.value)} />
            </label>
          </>
        ) : null}
        {step === 2 ? (
          <label>
            Kịch bản
            <Input.TextArea
              rows={8}
              value={script}
              onChange={(e) => setScript(e.target.value)}
              placeholder="Dán hoặc viết kịch bản. Có thể để trống và viết sau."
            />
          </label>
        ) : null}
        {step === 3 ? (
          <ul className="fx-wizard__sum">
            <li>Tên video: {title.trim() || '—'}</li>
            <li>Mã video: {(code.trim() || suggestNextEpisodeCode(builds)).toUpperCase()}</li>
            <li>Mô tả: {note.trim() || '—'}</li>
            <li>Kịch bản: {script.trim() ? 'Đã nhập' : 'Sẽ viết sau'}</li>
          </ul>
        ) : null}
        <div className="fx-desk__btns">
          <Button
            onClick={() => {
              if (step > 1) setStep((s) => s - 1);
              else {
                setOpen(false);
                setStep(1);
              }
            }}
          >
            {step > 1 ? 'Quay lại' : 'Hủy'}
          </Button>
          <Button
            type="primary"
            disabled={(step === 1 && !title.trim()) || busy}
            onClick={() => {
              if (step === 1) {
                if (!title.trim()) return;
                setStep(2);
                return;
              }
              if (step === 2) {
                setStep(3);
                return;
              }
              onCreate({
                title: title.trim(),
                premise: note.trim(),
                note: note.trim(),
                episode: (code.trim() || suggestNextEpisodeCode(builds)).toUpperCase(),
                script: script.trim(),
              });
              setOpen(false);
              setStep(1);
              setTitle('');
              setNote('');
              setCode('');
              setScript('');
            }}
          >
            {step < 3 ? 'Tiếp tục' : 'Bắt đầu sản xuất'}
          </Button>
        </div>
      </Modal>
    </section>
  );
}
