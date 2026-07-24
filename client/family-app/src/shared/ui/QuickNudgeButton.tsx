import { useState } from 'react';
import type { DayFlowCommitment } from '@/shared/api/family-os.api';
import { incrementFamilyNudge } from '@/shared/api/family-os.api';
import { getNudgeCount, setNudgeCountLocal } from '@/shared/nudge/nudge-stats';

type Props = {
  items: DayFlowCommitment | DayFlowCommitment[];
  familyId: string;
  flowDate: string;
  label?: string;
  className?: string;
  /** Called after nudge is recorded (for nudge KPI). */
  onNudged?: (count: number) => void;
};

export function QuickNudgeButton({
  items,
  familyId,
  flowDate,
  label = 'Nhắc nhanh',
  className,
  onNudged,
}: Props) {
  const list = Array.isArray(items) ? items : [items];
  const [status, setStatus] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (list.length === 0) return null;

  const onClick = async () => {
    const count = list.length;
    setBusy(true);
    try {
      const localNext = getNudgeCount(familyId, flowDate) + Math.max(1, count);
      setNudgeCountLocal(familyId, flowDate, localNext);
      await incrementFamilyNudge(familyId, flowDate, Math.max(1, count));
      onNudged?.(count);
      const detail =
        count === 1
          ? `«${list[0].title}»`
          : `${count} việc`;
      setStatus(`Đã ghi nhận nhắc ${detail}`);
    } catch {
      onNudged?.(count);
      setStatus('Đã ghi nhận — sẽ đồng bộ khi có mạng');
    } finally {
      setBusy(false);
      window.setTimeout(() => setStatus(null), 2200);
    }
  };

  return (
    <div className="nudge-wrap">
      <button
        type="button"
        className={className ?? 'pill nudge-btn'}
        disabled={busy}
        onClick={() => void onClick()}
      >
        {busy ? 'Đang…' : label}
      </button>
      {status ? <span className="nudge-toast">{status}</span> : null}
    </div>
  );
}
