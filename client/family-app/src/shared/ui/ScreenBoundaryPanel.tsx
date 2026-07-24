import { useEffect, useState } from 'react';
import {
  SCREEN_BOUNDARY_ANDROID_URL,
  SCREEN_BOUNDARY_IOS_URL,
  SCREEN_CHECKLIST,
  loadChecklistDone,
  saveChecklistDone,
  screenBoundaryShareText,
} from '@/shared/screen/screenBoundary';
import { shareOrCopyNudge } from '@/shared/nudge/nudge';

type Props = {
  flowDate: string;
  title?: string;
  body?: string;
  labelVi?: string;
  compact?: boolean;
};

export function ScreenBoundaryPanel({
  flowDate,
  title = 'Screen Agreement · ranh giới trên máy',
  body = 'FamilyOS khóa nhẹ trong app. Để giới hạn game/YouTube trên máy, bố mẹ cấu hình Screen Time hoặc Family Link.',
  labelVi = 'thỏa thuận màn hình',
  compact = false,
}: Props) {
  const [done, setDone] = useState<Record<string, boolean>>(() => loadChecklistDone(flowDate));

  useEffect(() => {
    setDone(loadChecklistDone(flowDate));
  }, [flowDate]);

  const toggle = (id: string) => {
    setDone((prev) => {
      const next = { ...prev, [id]: !prev[id] };
      saveChecklistDone(flowDate, next);
      return next;
    });
  };

  const completed = SCREEN_CHECKLIST.filter((i) => done[i.id]).length;

  return (
    <div className={`screen-bound${compact ? ' is-compact' : ''}`}>
      <div className="screen-bound-head">
        <strong>{title}</strong>
        <span className="muted">
          {completed}/{SCREEN_CHECKLIST.length} bước
        </span>
      </div>
      <p className="muted screen-bound-body">{body}</p>
      <ul className="screen-bound-list">
        {SCREEN_CHECKLIST.map((item) => (
          <li key={item.id}>
            <label>
              <input
                type="checkbox"
                checked={Boolean(done[item.id])}
                onChange={() => toggle(item.id)}
              />
              <span>{item.label}</span>
            </label>
          </li>
        ))}
      </ul>
      <div className="screen-bound-actions">
        <a className="pill" href={SCREEN_BOUNDARY_IOS_URL} target="_blank" rel="noreferrer">
          Screen Time (iPhone)
        </a>
        <a className="pill" href={SCREEN_BOUNDARY_ANDROID_URL} target="_blank" rel="noreferrer">
          Family Link (Android)
        </a>
        <button
          type="button"
          className="pill is-soft"
          onClick={() => void shareOrCopyNudge(screenBoundaryShareText(labelVi))}
        >
          Copy hướng dẫn
        </button>
      </div>
    </div>
  );
}
