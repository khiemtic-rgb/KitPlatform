import { useState } from 'react';
import type { DayFlowCommitment } from '@/shared/api/family-os.api';
import { incrementFamilyNudge } from '@/shared/api/family-os.api';
import {
  buildNudgeBatch,
  buildNudgeText,
  shareOrCopyNudge,
} from '@/shared/nudge/nudge';
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
    if (busy) return;
    setBusy(true);
    setStatus(null);

    const text =
      list.length === 1 ? buildNudgeText(list[0]) : buildNudgeBatch(list);

    let delivery: 'shared' | 'copied' | 'cancelled' | 'failed' = 'failed';
    try {
      delivery = await shareOrCopyNudge(text);
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        delivery = 'cancelled';
      } else {
        delivery = 'failed';
      }
    }

    // User dismissed the share sheet — don't count as a nudge.
    if (delivery === 'cancelled') {
      setBusy(false);
      return;
    }

    const count = Math.max(1, list.length);
    const localNext = getNudgeCount(familyId, flowDate) + count;
    setNudgeCountLocal(familyId, flowDate, localNext);
    try {
      await incrementFamilyNudge(familyId, flowDate, count);
    } catch {
      // Offline — local count already updated; sync later.
    }
    onNudged?.(count);

    const detail =
      list.length === 1 ? `«${list[0].title}»` : `${list.length} việc`;
    setStatus(
      delivery === 'copied' || delivery === 'shared'
        ? `Đã copy tin nhắc ${detail} — dán Zalo/Messenger cho con`
        : `Đã ghi nhận nhắc ${detail}`,
    );
    setBusy(false);
    window.setTimeout(() => setStatus(null), 2800);
  };

  return (
    <div className="nudge-wrap">
      <button
        type="button"
        className={className ?? 'pill nudge-btn'}
        disabled={busy}
        onClick={(e) => {
          e.stopPropagation();
          void onClick();
        }}
      >
        {busy ? 'Đang…' : label}
      </button>
      {status ? (
        <span className="nudge-toast" role="status">
          {status}
        </span>
      ) : null}
    </div>
  );
}
