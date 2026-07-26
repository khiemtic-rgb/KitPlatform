import { useState } from 'react';
import { createChildRequest } from '@/shared/api/family-os.api';

type Props = {
  familyId: string;
  memberId: string;
  open: boolean;
  onClose: () => void;
  onSubmitted?: (msg: string) => void;
};

const QUICK = [
  'Học thêm Toán',
  'Đá bóng',
  'Tiếng Anh',
  'Đi sinh nhật bạn',
  'Về quê',
  'Đọc sách',
];

const REASONS = [
  { value: 'no_extra_class', label: 'Lịch hôm nay khác thường' },
  { value: 'homework_done', label: 'Đã xong việc bắt buộc' },
  { value: 'play_with_friend', label: 'Có lịch với bạn / CLB' },
  { value: 'other', label: 'Lý do khác' },
];

export function ChildMissionRequestSheet({
  familyId,
  memberId,
  open,
  onClose,
  onSubmitted,
}: Props) {
  const [title, setTitle] = useState('');
  const [windowStart, setWindowStart] = useState('16:00');
  const [windowEnd, setWindowEnd] = useState('17:00');
  const [reasons, setReasons] = useState<string[]>(['other']);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!open) return null;

  const toggle = (code: string) => {
    setReasons((prev) =>
      prev.includes(code) ? prev.filter((c) => c !== code) : [...prev, code],
    );
  };

  const submit = async () => {
    const t = title.trim();
    if (t.length < 2) {
      setError('Nhập tên việc hôm nay (vd: Học thêm Toán).');
      return;
    }
    if (reasons.length === 0) {
      setError('Chọn ít nhất một lý do.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await createChildRequest(familyId, {
        memberId,
        kind: 'day_mission',
        titleVi: t,
        windowStart: windowStart || undefined,
        windowEnd: windowEnd || undefined,
        reasonCodes: reasons,
      });
      onSubmitted?.('Đã gửi đề xuất việc — bố mẹ duyệt vài giây là vào lịch hôm nay.');
      setTitle('');
      onClose();
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        'Chưa gửi được. Thử lại nhé.';
      setError(msg);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="ph-sheet-backdrop" role="presentation" onClick={onClose}>
      <div
        className="ph-sheet ph-request-sheet"
        role="dialog"
        aria-modal="true"
        aria-label="Đề xuất việc hôm nay"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="ph-sheet-head">
          <h2>Đề xuất việc hôm nay</h2>
          <button type="button" className="ph-sheet-close" onClick={onClose} aria-label="Đóng">
            ×
          </button>
        </header>
        <p className="ph-sheet-lead">
          Lịch hôm nay khác tuần trước? Gửi đề xuất — bố mẹ chỉ cần 👍, không vào Settings.
        </p>

        <p className="ph-request-label">Gợi ý nhanh</p>
        <div className="ph-request-amounts">
          {QUICK.map((q) => (
            <button key={q} type="button" onClick={() => setTitle(q)}>
              {q}
            </button>
          ))}
        </div>

        <label className="ph-add-memory-field">
          <span>Tên việc</span>
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Vd: Học thêm Toán"
            maxLength={200}
          />
        </label>

        <div className="ph-mission-windows">
          <label className="ph-add-memory-field">
            <span>Từ</span>
            <input
              type="time"
              value={windowStart}
              onChange={(e) => setWindowStart(e.target.value)}
            />
          </label>
          <label className="ph-add-memory-field">
            <span>Đến</span>
            <input type="time" value={windowEnd} onChange={(e) => setWindowEnd(e.target.value)} />
          </label>
        </div>

        <p className="ph-request-label">Lý do?</p>
        <ul className="ph-request-reasons">
          {REASONS.map((r) => (
            <li key={r.value}>
              <label>
                <input
                  type="checkbox"
                  checked={reasons.includes(r.value)}
                  onChange={() => toggle(r.value)}
                />
                {r.label}
              </label>
            </li>
          ))}
        </ul>

        {error ? <p className="ph-sheet-error">{error}</p> : null}

        <button
          type="button"
          className="ph-request-submit"
          disabled={busy}
          onClick={() => void submit()}
        >
          Gửi đề xuất việc
        </button>
      </div>
    </div>
  );
}
