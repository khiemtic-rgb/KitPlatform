import { Button } from 'antd';

export type ShotNavItem = {
  id: string;
  index: number;
  mark: '✓' | '●' | '○';
};

export function sceneKeyOf(shot: { scene?: string; sceneId?: string }) {
  return (shot.sceneId || shot.scene || '').trim();
}

export function resolveLaneAShotId<T extends { id: string; scene?: string; sceneId?: string }>(
  shots: T[],
  requested?: string,
) {
  const want = (requested || '').trim();
  if (!want) return shots[0]?.id;
  const exact = shots.find((s) => s.id === want);
  if (exact) return exact.id;
  const inScene = shots.filter((s) => {
    const key = sceneKeyOf(s);
    return key === want || key.startsWith(`${want} `) || key.startsWith(`${want}—`) || key.startsWith(`${want}–`);
  });
  return inScene[0]?.id || shots[0]?.id;
}

export function sceneShotsOf<T extends { id: string; scene?: string; sceneId?: string }>(shots: T[], currentId?: string) {
  const resolved = resolveLaneAShotId(shots, currentId);
  const current = shots.find((s) => s.id === resolved) || shots[0];
  if (!current) return [] as T[];
  const prefix = sceneKeyOf(current).split(/\s|[—–-]/)[0];
  const same = shots.filter((s) => sceneKeyOf(s).split(/\s|[—–-]/)[0] === prefix);
  return same.length ? same : shots;
}

export function ShotProductionNavigator({
  sceneLabel,
  sceneTitle,
  items,
  currentId,
  onSelect,
}: {
  sceneLabel: string;
  sceneTitle?: string;
  items: ShotNavItem[];
  currentId?: string;
  onSelect: (shotId: string) => void;
}) {
  const idx = Math.max(0, items.findIndex((it) => it.id === currentId));
  const prev = idx > 0 ? items[idx - 1] : undefined;
  const next = idx < items.length - 1 ? items[idx + 1] : undefined;
  return (
    <nav className="fx-desk" aria-label="Chuyển Shot">
      <p className="fx-prod__kicker">{sceneLabel}</p>
      {sceneTitle ? <p className="fx-desk__note">{sceneTitle}</p> : null}
      <p>
        Shot {String(idx + 1).padStart(2, '0')} / {String(items.length).padStart(2, '0')}
      </p>
      <div className="fx-desk__btns">
        <Button disabled={!prev} onClick={() => prev && onSelect(prev.id)} aria-label="Shot trước">
          ← Shot {prev ? String(prev.index + 1).padStart(2, '0') : '—'}
        </Button>
        <Button disabled={!next} onClick={() => next && onSelect(next.id)} aria-label="Shot sau">
          Shot {next ? String(next.index + 1).padStart(2, '0') : '—'} →
        </Button>
      </div>
      <div className="fx-desk__btns" role="list">
        {items.map((it) => (
          <Button
            key={it.id}
            type={it.id === currentId ? 'primary' : 'default'}
            onClick={() => onSelect(it.id)}
            aria-current={it.id === currentId ? 'true' : undefined}
          >
            {String(it.index + 1).padStart(2, '0')} {it.mark}
          </Button>
        ))}
      </div>
    </nav>
  );
}
