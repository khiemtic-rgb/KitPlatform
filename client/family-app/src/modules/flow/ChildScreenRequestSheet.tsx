import { useEffect, useState } from 'react';
import {
  CHILD_REQUEST_REASONS,
  createChildRequest,
  fetchScreenWallet,
  type ScreenWallet,
} from '@/shared/api/family-os.api';

type Props = {
  familyId: string;
  memberId: string;
  open: boolean;
  onClose: () => void;
  onSubmitted?: (msg: string) => void;
};

const AMOUNTS = [15, 30, 45, 60];

export function ChildScreenRequestSheet({
  familyId,
  memberId,
  open,
  onClose,
  onSubmitted,
}: Props) {
  const [minutes, setMinutes] = useState(30);
  const [reasons, setReasons] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [wallet, setWallet] = useState<ScreenWallet | null>(null);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    void fetchScreenWallet(familyId)
      .then((rows) => {
        if (cancelled) return;
        setWallet(rows.find((w) => w.memberId === memberId) ?? rows[0] ?? null);
      })
      .catch(() => {
        if (!cancelled) setWallet(null);
      });
    return () => {
      cancelled = true;
    };
  }, [open, familyId, memberId]);

  if (!open) return null;

  const toggle = (code: string) => {
    setReasons((prev) =>
      prev.includes(code) ? prev.filter((c) => c !== code) : [...prev, code],
    );
  };

  const submit = async () => {
    if (reasons.length === 0) {
      setError('Chọn ít nhất một lý do nhé.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await createChildRequest(familyId, {
        memberId,
        amountMinutes: minutes,
        reasonCodes: reasons,
      });
      onSubmitted?.('Đã gửi đề xuất cho bố mẹ — chờ duyệt vài giây.');
      setReasons([]);
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
        aria-label="Xin thêm phút màn hình"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="ph-sheet-head">
          <h2>Xin thêm phút màn hình</h2>
          <button type="button" className="ph-sheet-close" onClick={onClose} aria-label="Đóng">
            ×
          </button>
        </header>
        {wallet && wallet.status === 'active' ? (
          <p className="ph-sheet-lead">
            Ví tuần còn <strong>{wallet.remainingMinutes}</strong> phút. Đây là thỏa thuận nhà —
            Fami không khóa máy.
          </p>
        ) : (
          <p className="ph-sheet-lead">
            Xin thêm phút theo thỏa thuận nhà (không khóa máy). Nêu lý do rõ — bố mẹ quyết nhanh.
          </p>
        )}

        <div className="ph-request-amounts">
          {AMOUNTS.map((m) => (
            <button
              key={m}
              type="button"
              className={minutes === m ? 'is-on' : undefined}
              onClick={() => setMinutes(m)}
            >
              +{m}’
            </button>
          ))}
        </div>

        <p className="ph-request-label">Lý do?</p>
        <ul className="ph-request-reasons">
          {CHILD_REQUEST_REASONS.map((r) => (
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
          Gửi đề xuất
        </button>
      </div>
    </div>
  );
}
