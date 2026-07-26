import { useState } from 'react';
import {
  activateFamilyMode,
  FAMILY_MODE_OPTIONS,
  scanAdaptiveProposals,
  type FamilyModeResult,
} from '@/shared/api/family-os.api';

type Props = {
  familyId: string;
  parentMembershipId?: string;
  open: boolean;
  onClose: () => void;
  onActivated?: (result: FamilyModeResult) => void;
};

export function FamilyModeSheet({
  familyId,
  parentMembershipId,
  open,
  onClose,
  onActivated,
}: Props) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastResult, setLastResult] = useState<FamilyModeResult | null>(null);

  if (!open) return null;

  const activate = async (mode: string) => {
    setBusy(true);
    setError(null);
    try {
      const result = await activateFamilyMode(familyId, {
        mode,
        activatedByMemberId: parentMembershipId,
        confirmNow: true,
      });
      setLastResult(result);
      onActivated?.(result);
      void scanAdaptiveProposals(familyId).catch(() => undefined);
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        'Chưa đổi được chế độ.';
      setError(msg);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="ph-sheet-backdrop" role="presentation" onClick={onClose}>
      <div
        className="ph-sheet ph-mode-sheet"
        role="dialog"
        aria-modal="true"
        aria-label="Chế độ gia đình"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="ph-sheet-head">
          <h2>Chế độ gia đình</h2>
          <button type="button" className="ph-sheet-close" onClick={onClose} aria-label="Đóng">
            ×
          </button>
        </header>
        <p className="ph-sheet-lead">
          Chọn một chế độ — AI đổi Routine theo lịch. Không cần vào Settings.
        </p>
        <ul className="ph-mode-list">
          {FAMILY_MODE_OPTIONS.map((opt) => (
            <li key={opt.value}>
              <button
                type="button"
                className="ph-mode-option"
                disabled={busy}
                onClick={() => void activate(opt.value)}
              >
                <strong>{opt.label}</strong>
                <span>{opt.hint}</span>
              </button>
            </li>
          ))}
        </ul>
        {lastResult ? (
          <div className="fa-mode-active" role="status">
            <p>
              {lastResult.messageVi}
              {lastResult.primaryRoutineName ? (
                <>
                  <br />
                  Đang dùng: <strong>{lastResult.primaryRoutineName}</strong>
                  {lastResult.primaryTemplateCount != null
                    ? ` · ${lastResult.primaryTemplateCount} việc`
                    : null}
                </>
              ) : null}
            </p>
            <p className="fa-hint" style={{ marginBottom: 0 }}>
              Vào Quản trị gia đình → Xem việc / Chỉnh nhẹ để xem bảng hoặc sửa giờ.
            </p>
          </div>
        ) : null}
        {error ? <p className="ph-sheet-error">{error}</p> : null}
      </div>
    </div>
  );
}
