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
  /** homeB4 = mockup man-hinh-bome4 compact cards. */
  variant?: 'default' | 'homeB4';
  maxItems?: number;
  onApproveStars?: (commitmentId: string) => void | Promise<void>;
  onConsequence?: (eventId: string, status: 'applied' | 'waived') => void | Promise<void>;
  onTeamUnlock?: (unlockId: string, status: 'confirmed' | 'deferred') => void | Promise<void>;
  onRewardFulfill?: (redemptionId: string) => void | Promise<void>;
  onChanged?: () => void;
  onSeeAll?: () => void;
  /** Empty inbox → open Family Mode (≤1 phút path). */
  onOpenMode?: () => void;
};

function estimateSeconds(count: number): number {
  if (count <= 0) return 0;
  if (count === 1) return 3;
  return Math.min(60, 3 + (count - 1) * 5);
}

function headlineForCount(count: number): string {
  if (count <= 0) return 'Không việc cần duyệt — nghỉ ngơi đi.';
  const sec = estimateSeconds(count);
  if (count === 1) return `Famixa cần bạn · 1 việc · khoảng ${sec} giây`;
  return `Famixa cần bạn · ${count} việc · khoảng ${sec} giây · mục tiêu ≤1 phút`;
}

/** Soft-rewrite đề xuất cũ trong DB — không đụng SoT server. */
function friendlyDecisionCopy(text: string | null | undefined): string {
  const raw = (text ?? '').trim();
  if (!raw) return '';
  return raw
    .replace(
      /Routine\s*[「"]([^」"]+)[」"]\s*hơi dày\s*[—–-]\s*bỏ\s*(\d+)\s*việc\?/gi,
      'Lịch 「$1」 đang hơi nhiều việc — bớt $2 việc?',
    )
    .replace(/AI gợi ý tạm ẩn/gi, 'Famixa đề xuất tạm ẩn')
    .replace(/Bạn chỉ cần 👍\.?/g, 'Bạn chỉ cần bấm Áp dụng.')
    .replace(/^AI cần bạn/i, 'Famixa cần bạn');
}

/** Ai liên quan — bố mẹ phải thấy ngay đề xuất này của con nào. */
function whoLabel(item: DecisionItem): string {
  return item.memberName?.trim() || 'Cả gia đình';
}

function sortForOneMinute(items: DecisionItem[]): DecisionItem[] {
  const rank = (item: DecisionItem) => {
    if (item.recommend === 'approve') return 0;
    if (item.recommend === 'partial') return 1;
    if (!item.recommend) return 2;
    return 3;
  };
  return [...items].sort((a, b) => rank(a) - rank(b));
}

export function DecisionInboxPanel({
  familyId,
  parentMembershipId,
  refreshKey,
  variant = 'default',
  maxItems,
  onApproveStars,
  onConsequence,
  onTeamUnlock,
  onRewardFulfill,
  onChanged,
  onSeeAll,
  onOpenMode,
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
        setToast(approve ? 'Đã áp dụng đề xuất Famixa' : 'Đã bỏ qua đề xuất');
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
      setInbox((prev) => {
        if (!prev) return prev;
        const nextItems = prev.items.filter((x) => !(x.kind === item.kind && x.id === item.id));
        const n = nextItems.length;
        return {
          items: nextItems,
          totalCount: n,
          headlineVi: headlineForCount(n),
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
  const limit = maxItems ?? (variant === 'homeB4' ? 2 : 8);
  const rawItems = sortForOneMinute(inbox?.items ?? []);
  const items = (() => {
    const seenId = new Set<string>();
    const seenSoft = new Set<string>();
    const out: DecisionItem[] = [];
    for (const item of rawItems) {
      const idKey = `${item.kind}:${item.id}`;
      if (seenId.has(idKey)) continue;
      // Trùng title cùng loại (thường là AI proposal seed lặp) — chỉ hiện 1.
      const softKey = `${item.kind}:${item.titleVi.trim().toLowerCase()}`;
      if (seenSoft.has(softKey)) continue;
      seenId.add(idKey);
      seenSoft.add(softKey);
      out.push(item);
      if (out.length >= limit) break;
    }
    return out;
  })();
  const etaSec = estimateSeconds(count);
  const approveLabel = (item: DecisionItem) =>
    item.kind === 'ai_proposal' ? 'Áp dụng' : 'Đồng ý';
  const preferApprove = (item: DecisionItem) => item.recommend !== 'reject';

  if (variant === 'homeB4') {
    if (count === 0 && items.length === 0) {
      if (!onOpenMode) return null;
      return (
        <section className="ph-b4-inbox is-empty-afe" aria-label="Decision Inbox">
          <header className="ph-b4-col-head">
            <h3>
              <span aria-hidden>🤖</span> Cần bạn duyệt
            </h3>
          </header>
          <p className="ph-b4-empty">Không có đề xuất nào đang chờ bạn.</p>
          <button type="button" className="ph-b4-see-all" onClick={onOpenMode}>
            Đổi chế độ nhà (1 chạm) ›
          </button>
        </section>
      );
    }
    return (
      <section className="ph-b4-inbox" aria-label="Decision Inbox">
        <header className="ph-b4-col-head">
          <h3>
            <span aria-hidden>🤖</span> Cần bạn duyệt
            {count > 0 ? <i>{Math.min(count, 9)}</i> : null}
          </h3>
          {count > 0 ? (
            <em className="ph-b4-inbox-eta">khoảng {etaSec} giây</em>
          ) : null}
        </header>
        {items.length === 0 ? (
          <p className="ph-b4-empty">Không đề xuất cần duyệt.</p>
        ) : (
          <ul className="ph-b4-inbox-list">
            {items.map((item) => {
              const yesFirst = preferApprove(item);
              return (
                <li key={`${item.kind}-${item.id}`}>
                  <span className="ph-inbox-who">{whoLabel(item)}</span>
                  <p>{friendlyDecisionCopy(item.titleVi)}</p>
                  <div className="ph-b4-inbox-btns">
                    {yesFirst ? (
                      <>
                        <button
                          type="button"
                          className="is-yes"
                          disabled={busyId === item.id}
                          onClick={() => void decide(item, true)}
                        >
                          {approveLabel(item)}
                        </button>
                        <button
                          type="button"
                          className="is-no"
                          disabled={busyId === item.id}
                          onClick={() => void decide(item, false)}
                        >
                          Bỏ qua
                        </button>
                      </>
                    ) : (
                      <>
                        <button
                          type="button"
                          className="is-no"
                          disabled={busyId === item.id}
                          onClick={() => void decide(item, false)}
                        >
                          Bỏ qua
                        </button>
                        <button
                          type="button"
                          className="is-yes"
                          disabled={busyId === item.id}
                          onClick={() => void decide(item, true)}
                        >
                          {approveLabel(item)}
                        </button>
                      </>
                    )}
                  </div>
                </li>
              );
            })}
          </ul>
        )}
        {onSeeAll ? (
          <button type="button" className="ph-b4-see-all" onClick={onSeeAll}>
            Xem tất cả đề xuất ›
          </button>
        ) : null}
        {toast ? (
          <p className="ph-b4-inbox-toast" role="status">
            {toast}
          </p>
        ) : null}
      </section>
    );
  }

  return (
    <section className="ph-block ph-decision-inbox">
      <header className="ph-block-head">
        <h2>
          Cần bạn duyệt
          {count > 0 ? <span className="ph-pill-count">{Math.min(count, 9)}</span> : null}
        </h2>
      </header>
      <p className="ph-digest-promise" role="status">
        {friendlyDecisionCopy(inbox?.headlineVi) || headlineForCount(count) || 'Đang tải…'}
      </p>
      {count > 0 ? (
        <p className="ph-afe-eta" role="status">
          Ước tính ~{etaSec} giây · mục tiêu Famixa ≤1 phút/ngày
        </p>
      ) : null}
      {items.length === 0 ? (
        <div className="ph-empty-afe">
          <p className="ph-empty-soft">Không việc cần duyệt — nghỉ ngơi đi.</p>
          {onOpenMode ? (
            <button type="button" className="pill is-soft" onClick={onOpenMode}>
              Đổi chế độ nhà (1 chạm)
            </button>
          ) : null}
        </div>
      ) : (
        <ul className="ph-decision-list">
          {items.map((item) => {
            const yesFirst = preferApprove(item);
            return (
              <li key={`${item.kind}-${item.id}`} className="ph-decision-card">
                <div className="ph-decision-card-body">
                  <span className="ph-inbox-who">{whoLabel(item)}</span>
                  <strong>{friendlyDecisionCopy(item.titleVi)}</strong>
                  <p>{friendlyDecisionCopy(item.bodyVi)}</p>
                  {item.recommend ? (
                    <span className={`ph-decision-rec rec-${item.recommend}`}>
                      Famixa gợi ý:{' '}
                      {item.recommend === 'approve'
                        ? 'Đồng ý'
                        : item.recommend === 'reject'
                          ? 'Từ chối'
                          : 'Một phần'}
                    </span>
                  ) : null}
                </div>
                <div className="ph-decision-actions">
                  {yesFirst ? (
                    <>
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
                    </>
                  ) : (
                    <>
                      <button
                        type="button"
                        className="ph-decision-no"
                        disabled={busyId === item.id}
                        onClick={() => void decide(item, false)}
                        aria-label="Từ chối"
                      >
                        👎
                      </button>
                      <button
                        type="button"
                        className="ph-decision-yes"
                        disabled={busyId === item.id}
                        onClick={() => void decide(item, true)}
                        aria-label="Đồng ý"
                      >
                        👍
                      </button>
                    </>
                  )}
                </div>
              </li>
            );
          })}
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
