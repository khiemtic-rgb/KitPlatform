import { useMemo, useState } from 'react';
import {
  sendParentVoice,
  setFamilyMemoryFavorite,
  type FamilyMemoryEntry,
} from '@/shared/api/family-os.api';
import { withEvidenceAuth } from '@/shared/upload/evidence-url';
import { SoftEvidenceImg } from '@/shared/ui/SoftEvidenceImg';
import { isKidMomentAudio } from '@/modules/flow/kidMomentAck';
import { shortMemberName } from '@/modules/flow/relationshipGraph';

type Props = {
  open: boolean;
  familyId: string;
  parentMembershipId: string;
  flowDate: string;
  memory: FamilyMemoryEntry | null;
  onClose: () => void;
  onSeen: (memoryId: string) => void;
  onUpdated: (memory: FamilyMemoryEntry) => void;
  onToast?: (msg: string) => void;
};

const WARM_LINES = [
  'Mẹ/bố đã thấy khoảnh khắc của con — ấm lắm.',
  'Cảm ơn con đã chia sẻ với nhà.',
  'Con cứ gửi những điều nhỏ nhé — nhà luôn nhìn thấy.',
] as const;

export function ParentKidMomentSheet({
  open,
  familyId,
  parentMembershipId,
  flowDate,
  memory,
  onClose,
  onSeen,
  onUpdated,
  onToast,
}: Props) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lineIdx, setLineIdx] = useState(0);

  const audio = useMemo(() => (memory ? isKidMomentAudio(memory) : false), [memory]);
  const mediaSrc = memory?.photoUrl ? withEvidenceAuth(memory.photoUrl) : undefined;
  const who = shortMemberName(memory?.memberName || 'Con');

  if (!open || !memory) return null;

  const heart = async () => {
    setBusy(true);
    setError(null);
    try {
      const next = !memory.isFavorite;
      await setFamilyMemoryFavorite(familyId, memory.id, next);
      onUpdated({ ...memory, isFavorite: next });
      onSeen(memory.id);
      onToast?.(next ? 'Đã gắn tim cho khoảnh khắc' : 'Đã bỏ tim');
    } catch {
      setError('Chưa gắn tim được. Thử lại nhé.');
    } finally {
      setBusy(false);
    }
  };

  const sendWarm = async () => {
    if (!memory.memberId) {
      setError('Chưa gắn được người nhận lời ấm.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await sendParentVoice(familyId, {
        fromMemberId: parentMembershipId,
        toMemberId: memory.memberId,
        templateCode: 'praise',
        bodyVi: WARM_LINES[lineIdx] ?? WARM_LINES[0],
        flowDate,
      });
      onSeen(memory.id);
      onToast?.('Đã gửi lời ấm cho con');
      onClose();
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        'Chưa gửi được lời ấm.';
      setError(msg);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div
      className="ph-sheet-backdrop"
      role="presentation"
      onClick={() => {
        onSeen(memory.id);
        onClose();
      }}
    >
      <div
        className="ph-sheet km-parent-sheet"
        role="dialog"
        aria-modal="true"
        aria-label={`Khoảnh khắc của ${who}`}
        onClick={(e) => e.stopPropagation()}
      >
        <header className="ph-sheet-head">
          <h2>Khoảnh khắc của {who}</h2>
          <button
            type="button"
            className="ph-sheet-close"
            aria-label="Đóng"
            onClick={() => {
              onSeen(memory.id);
              onClose();
            }}
          >
            ×
          </button>
        </header>
        <p className="ph-sheet-lead">Không phải bài kiểm tra — chỉ cần nhìn thấy và trả lời ấm.</p>

        {audio && mediaSrc ? (
          <audio controls src={mediaSrc} className="km-preview-audio" />
        ) : memory?.photoUrl || mediaSrc ? (
          <SoftEvidenceImg
            url={memory?.photoUrl ?? mediaSrc}
            fallback="📷"
            className="km-preview-img"
            auth={withEvidenceAuth}
          />
        ) : null}

        {memory.noteVi ? <p className="km-caption">“{memory.noteVi}”</p> : null}

        <div className="km-actions">
          <button type="button" className="km-secondary" disabled={busy} onClick={() => void heart()}>
            {memory.isFavorite ? 'Đã tim' : 'Tim'}
          </button>
          <button
            type="button"
            className="km-secondary"
            disabled={busy}
            onClick={() => {
              onSeen(memory.id);
              onToast?.('Đã nhìn thấy con');
              onClose();
            }}
          >
            Đã thấy
          </button>
        </div>

        <p className="ph-request-label">Gửi lời ấm nhanh</p>
        <div className="ph-request-amounts">
          {WARM_LINES.map((line, idx) => (
            <button
              key={line}
              type="button"
              className={idx === lineIdx ? 'is-on' : undefined}
              onClick={() => setLineIdx(idx)}
            >
              {line.slice(0, 28)}…
            </button>
          ))}
        </div>
        <button
          type="button"
          className="ph-request-submit"
          disabled={busy}
          onClick={() => void sendWarm()}
        >
          {busy ? 'Đang gửi…' : 'Gửi lời ấm'}
        </button>
        {error ? <p className="ph-sheet-error">{error}</p> : null}
      </div>
    </div>
  );
}