import { useCallback, useEffect, useState } from 'react';
import {
  decideAiProposal,
  decideChildRequest,
  fetchDecisionInbox,
  type DecisionInbox,
  type DecisionItem,
} from '@/shared/api/family-os.api';

type Props = {
  familyId: string;
  parentMembershipId?: string;
  /** Bump to refetch (e.g. after day flow changes). */
  refreshKey?: string | number;
  onApproveStars?: (commitmentId: string) => void | Promise<void>;
  onConsequence?: (eventId: string, status: 'applied' | 'waived') => void | Promise<void>;
  onTeamUnlock?: (unlockId: string, status: 'confirmed' | 'deferred') => void | Promise<void>;
  onRewardFulfill?: (redemptionId: string) => void | Promise<void>;
  onChanged?: () => void;
};

export function DecisionInboxPanel({
  familyId,
  parentMembershipId,
  refreshKey,
  onApproveStars,
  onConsequence,
  onTeamUnlock,
  onRewardFulfill,
  onChanged,
}: Props) {
  const [inbox, setInbox] = useState<DecisionInbox | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  const reload = useCallback(() => {
    void fetchDecisionInbox(familyId)
      .then(setInbox)
      .catch(() => setInbox({ totalCount: 0, headlineVi: 'Không tải được hộp thư.', items: [] }));
  }, [familyId]);

  useEffect(() => {
    reload();
  }, [reload, refreshKey]);

  useEffect(() => {
    if (!toast) return;
    const t = window.setTimeout(() => setToast(null), 2800);
    return () => window.clearTimeout(t);
  }, [toast]);

  const decide = async (item: DecisionItem, approve: boolean) => {
    if (!parentMembershipId) {
      setToast('Thiếu hồ sơ phụ huynh để duyệt.');
      return;
    }
    setBusyId(item.id);
    try {
      if (item.kind === 'child_request') {
        await decideChildRequest(familyId, item.id, {
          decidedByMemberId: parentMembershipId,
          decision: approve ? 'approve' : 'reject',
        });
        setToast(approve ? 'Đã đồng ý đề xuất của con' : 'Đã từ chối — nhẹ nhàng nói chuyện với con nhé');
      } else if (item.kind === 'ai_proposal') {
        await decideAiProposal(familyId, item.id, {
          decidedByMemberId: parentMembershipId,
          decision: approve ? 'approve' : 'reject',
        });
        setToast(approve ? 'Đã áp dụng đề xuất AI' : 'Đã bỏ qua đề xuất');
      } else if (item.kind === 'awaiting_stars' && approve) {
        if (!onApproveStars) throw new Error('missing_approve_stars');
        await onApproveStars(item.id);
        setToast('Đã duyệt sao');
      } else if (item.kind === 'consequence_confirm') {
        await onConsequence?.(item.id, approve ? 'applied' : 'waived');
        setToast(approve ? 'Đã áp dụng thỏa thuận' : 'Đã bỏ qua lần này');
      } else if (item.kind === 'team_unlock') {
        await onTeamUnlock?.(item.id, approve ? 'confirmed' : 'deferred');
        setToast(approve ? 'Đã mở phần thưởng đội' : 'Để sau');
      } else if (item.kind === 'reward_fulfill' && approve) {
        await onRewardFulfill?.(item.id);
        setToast('Đã xác nhận đổi quà');
      } else if (!approve) {
        setToast('Đã bỏ qua');
      }
      // Drop card immediately so parent sees progress even before refetch.
      setInbox((prev) => {
        if (!prev) return prev;
        const items = prev.items.filter((x) => !(x.kind === item.kind && x.id === item.id));
        const n = items.length;
        return {
          items,
          totalCount: n,
          headlineVi:
            n === 0
              ? 'Không việc cần duyệt — nghỉ ngơi đi.'
              : n === 1
                ? 'AI cần bạn · 1 việc · khoảng 3 giây'
                : `AI cần bạn · ${n} việc · khoảng 15 giây`,
        };
      });
      reload();
      onChanged?.();
    } catch {
      setToast('Chưa xử lý được — thử lại nhé');
    } finally {
      setBusyId(null);
    }
  };

  const count = inbox?.totalCount ?? 0;
  const items = inbox?.items ?? [];

  return (
    <section className="ph-block ph-decision-inbox">
      <header className="ph-block-head">
        <h2>
          AI CẦN BẠN
          {count > 0 ? <span className="ph-pill-count">{Math.min(count, 9)}</span> : null}
        </h2>
      </header>
      <p className="ph-digest-promise" role="status">
        {inbox?.headlineVi ?? 'Đang tải…'}
      </p>
      {items.length === 0 ? (
        <p className="ph-empty-soft">Không việc cần duyệt — nghỉ ngơi đi.</p>
      ) : (
        <ul className="ph-decision-list">
          {items.slice(0, 8).map((item) => (
            <li key={`${item.kind}-${item.id}`} className="ph-decision-card">
              <div className="ph-decision-card-body">
                <strong>{item.titleVi}</strong>
                <p>{item.bodyVi}</p>
                {item.recommend ? (
                  <span className={`ph-decision-rec rec-${item.recommend}`}>
                    AI gợi ý: {item.recommend === 'approve' ? 'Đồng ý' : item.recommend === 'reject' ? 'Từ chối' : 'Một phần'}
                  </span>
                ) : null}
              </div>
              <div className="ph-decision-actions">
                <button
                  type="button"
                  className="ph-decision-yes"
                  disabled={busyId === item.id}
                  onClick={() => void decide(item, true)}
                  aria-label="Đồng ý"
                >
                  👍
                </button>
                <button
                  type="button"
                  className="ph-decision-no"
                  disabled={busyId === item.id}
                  onClick={() => void decide(item, false)}
                  aria-label="Từ chối"
                >
                  👎
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
      {toast ? (
        <p className="ph-action-toast" role="status">
          {toast}
        </p>
      ) : null}
    </section>
  );
}
