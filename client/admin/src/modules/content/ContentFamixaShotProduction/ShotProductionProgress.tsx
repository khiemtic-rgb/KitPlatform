import type { ShotProductionSnapshot } from './ShotProductionState';

/** Desk checklist beside the shot. Not official Production Progress. Not an input to the series track or the production-progress card. */
export const SHOT_PROGRESS_ROWS_AUTHORITY = 'EDITORIAL' as const;

function mark(done: boolean, wait?: boolean, stale?: boolean) {
  if (stale) return '!';
  if (done) return '✓';
  if (wait) return '●';
  return '○';
}

export function shotProgressRows(snap: ShotProductionSnapshot) {
  return [
    {
      id: 'voice',
      label: 'Thoại',
      mark: snap.isSilent ? '—' : mark(snap.voiceReady, !snap.voiceReady, snap.voiceStale),
    },
    {
      id: 'picture',
      label: 'Hình ảnh',
      mark: mark(snap.keyframeApproved, snap.picturePendingApproval, snap.keyframeStale),
    },
    {
      id: 'video',
      label: 'Chuyển động',
      mark: mark(snap.motionReady, snap.motionBusy, snap.motionStale),
    },
    {
      id: 'lipsync',
      label: 'Lip-sync',
      mark: snap.isSilent ? '—' : mark(snap.lipSyncReady, snap.lipsyncBusy || !snap.qaReady, snap.lipSyncStale),
    },
    {
      id: 'final',
      label: 'Hoàn chỉnh',
      mark:
        snap.mixStale || (snap.mixReady && snap.finalArtifactReady === false)
          ? '!'
          : snap.finalArtifactReady
            ? '✓'
            : '○',
    },
  ];
}

export function ShotProductionProgress({ snap }: { snap: ShotProductionSnapshot }) {
  return (
    <ul className="fx-finish__check" data-progress-authority={SHOT_PROGRESS_ROWS_AUTHORITY}>
      {shotProgressRows(snap).map((row) => (
        <li key={row.id}>
          {row.label} {row.mark}
        </li>
      ))}
    </ul>
  );
}
