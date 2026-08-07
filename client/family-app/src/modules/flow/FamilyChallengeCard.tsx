import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  acceptFamilyChallenge,
  checkinChallengeLeg,
  fetchCurrentChallenge,
  type FamilyChallenge,
  type FamilyChallengeLeg,
} from '@/shared/api/family-os.api';

type Props = {
  familyId: string;
  /** Current viewer membership id. */
  memberId: string;
  /** When true, show accept CTA and all legs; kids only see their leg + household progress. */
  isParent: boolean;
  compact?: boolean;
};

function legLabel(leg: FamilyChallengeLeg): string {
  const emoji = leg.emoji ? `${leg.emoji} ` : '';
  return `${emoji}${leg.title}`;
}

export function FamilyChallengeCard({ familyId, memberId, isParent, compact }: Props) {
  const [challenge, setChallenge] = useState<FamilyChallenge | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setChallenge(await fetchCurrentChallenge(familyId));
    } catch (err: unknown) {
      setError(
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          'Không tải được thử thách.',
      );
    } finally {
      setLoading(false);
    }
  }, [familyId]);

  useEffect(() => {
    void load();
  }, [load]);

  const visibleLegs = useMemo(() => {
    if (!challenge) return [];
    if (isParent) return challenge.legs;
    return challenge.legs.filter(
      (l) => l.memberId === memberId || l.legKind === 'household',
    );
  }, [challenge, isParent, memberId]);

  const accept = async () => {
    setBusy(true);
    setError(null);
    try {
      setChallenge(await acceptFamilyChallenge(familyId, memberId));
    } catch (err: unknown) {
      setError(
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          'Không mở được thử thách.',
      );
    } finally {
      setBusy(false);
    }
  };

  const toggleLeg = async (leg: FamilyChallengeLeg) => {
    if (!isParent && leg.memberId && leg.memberId !== memberId) return;
    if (!isParent && leg.legKind === 'household') return;
    setBusy(true);
    try {
      const next = leg.todayDone ? 'clear' : 'done';
      setChallenge(await checkinChallengeLeg(familyId, leg.id, memberId, next));
    } catch (err: unknown) {
      setError(
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          'Không ghi nhận được.',
      );
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return (
      <section className={`fc-card${compact ? ' is-compact' : ''}`}>
        <p className="muted">Đang tải thử thách…</p>
      </section>
    );
  }

  if (!challenge) {
    if (!isParent) return null;
    return (
      <section className={`fc-card${compact ? ' is-compact' : ''}`}>
        <header className="fc-head">
          <h3>Thử thách cả nhà</h3>
          <p className="muted">Bố · Mẹ · Con · Ăn tối cùng → mở thưởng xem phim cuối tuần</p>
        </header>
        {error ? <p className="fc-error">{error}</p> : null}
        <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void accept()}>
          Bắt đầu thử thách tuần này
        </button>
      </section>
    );
  }

  const pct =
    challenge.legsTotal > 0
      ? Math.round((challenge.legsComplete / challenge.legsTotal) * 100)
      : 0;
  const done = challenge.status === 'completed';

  return (
    <section className={`fc-card${compact ? ' is-compact' : ''}${done ? ' is-done' : ''}`}>
      <header className="fc-head">
        <div>
          <h3>{challenge.title}</h3>
          <p className="muted">
            {challenge.weekStart} → {challenge.weekEnd} · {challenge.legsComplete}/
            {challenge.legsTotal} mục · thưởng {challenge.rewardLabel}
          </p>
        </div>
        <strong className="fc-pct">{pct}%</strong>
      </header>

      <div className="fc-bar" aria-hidden>
        <span style={{ width: `${pct}%` }} />
      </div>

      {error ? <p className="fc-error">{error}</p> : null}

      {done ? (
        <p className="fc-done-msg">
          Cả nhà đủ mục tuần — mở {challenge.rewardLabel} trên tab Thưởng nhé!
        </p>
      ) : null}

      <ul className="fc-legs">
        {visibleLegs.map((leg) => {
          const canToggle =
            isParent || (leg.memberId === memberId && leg.legKind !== 'household');
          return (
            <li key={leg.id} className={`fc-leg${leg.isComplete ? ' is-complete' : ''}`}>
              <button
                type="button"
                className={`fc-check${leg.todayDone ? ' is-done' : ''}`}
                disabled={busy || !canToggle}
                aria-label={leg.todayDone ? 'Bỏ đánh dấu hôm nay' : 'Đã làm hôm nay'}
                onClick={() => void toggleLeg(leg)}
              >
                {leg.todayDone ? '✓' : ''}
              </button>
              <div className="fc-leg-body">
                <span className="fc-leg-title">{legLabel(leg)}</span>
                <span className="muted">
                  {leg.doneDays}/{leg.targetDays}
                  {leg.memberName ? ` · ${leg.memberName}` : ''}
                  {leg.isComplete ? ' · đủ tuần' : ''}
                </span>
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
