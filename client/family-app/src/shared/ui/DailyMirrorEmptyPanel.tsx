import { useCallback, useEffect, useState } from 'react';
import {
  MIRROR_AGENT_CHECKLIST,
  MIRROR_FAMILY_SAFETY_HELP_URL,
  MIRROR_FAMILY_SAFETY_URL,
  isMirrorHomeDismissed,
  loadMirrorChecklist,
  mirrorChecklistProgress,
  mirrorShareText,
  saveMirrorChecklist,
  setMirrorHomeDismissed,
} from '@/shared/mirror/dailyDigitalMirror';
import {
  fetchMirrorDay,
  postMirrorParentNote,
  type FamilyMirrorDay,
} from '@/shared/api/family-os.api';
import { shareOrCopyNudge } from '@/shared/nudge/nudge';
import { downloadMirrorAgentInstaller } from '@/shared/mirror/mirrorAgentInstaller';

type Props = {
  familyId: string;
  childShort: string;
  /** Child membership id for API (optional → server picks first child) */
  childMemberId?: string | null;
  parentMembershipId?: string | null;
  parentLabel?: string;
  /** parent: khen/nhắc + checklist · kid: chỉ gương + lời bố mẹ */
  viewer?: 'parent' | 'kid';
  compact?: boolean;
  dismissible?: boolean;
  onToast?: (msg: string) => void;
};

function formatMinutes(seconds: number): string {
  const m = Math.max(0, Math.round(seconds / 60));
  if (m < 60) return `${m} phút`;
  const h = Math.floor(m / 60);
  const rem = m % 60;
  return rem > 0 ? `${h} giờ ${rem} phút` : `${h} giờ`;
}

const NOTE_PRESETS: Array<{ tone: 'praise' | 'soft' | 'renegotiate'; label: string; body: (n: string) => string }> =
  [
    {
      tone: 'praise',
      label: 'Khen',
      body: (n) =>
        `${n} ơi, mẹ/bố thấy con đã cố gắng giữ nhịp hôm nay. Cảm ơn con đã để cả nhà cùng nhìn lại tối nay 💜`,
    },
    {
      tone: 'soft',
      label: 'Nhắc nhẹ',
      body: (n) =>
        `${n} ơi, tối nay mình cùng nhớ thỏa thuận giờ ngủ nhé — mai dậy sẽ nhẹ hơn nhiều.`,
    },
    {
      tone: 'renegotiate',
      label: 'Hẹn lại',
      body: (n) =>
        `${n} ơi, cuối mình ngồi 2 phút chỉnh lại thỏa thuận màn hình cho hợp hơn nhé.`,
    },
  ];

/**
 * M0 empty checklist + M1 live evening mirror when Agent has reported usage.
 */
export function DailyMirrorEmptyPanel({
  familyId,
  childShort,
  childMemberId = null,
  parentMembershipId = null,
  parentLabel = 'bố mẹ',
  viewer = 'parent',
  compact = false,
  dismissible = false,
  onToast,
}: Props) {
  const isKid = viewer === 'kid';
  const [done, setDone] = useState<Record<string, boolean>>(() =>
    loadMirrorChecklist(familyId),
  );
  const [open, setOpen] = useState(!compact);
  const [hidden, setHidden] = useState(() =>
    dismissible ? isMirrorHomeDismissed(familyId) : false,
  );
  const [day, setDay] = useState<FamilyMirrorDay | null>(null);
  const [loading, setLoading] = useState(true);
  const [noteBusy, setNoteBusy] = useState(false);

  const toast = (msg: string) => onToast?.(msg);
  const who = childShort.trim() || 'con';

  const reload = useCallback(async () => {
    setLoading(true);
    try {
      const d = await fetchMirrorDay(familyId, {
        memberId: childMemberId || undefined,
      });
      setDay(d);
    } catch {
      setDay(null);
    } finally {
      setLoading(false);
    }
  }, [familyId, childMemberId]);

  useEffect(() => {
    setDone(loadMirrorChecklist(familyId));
    if (dismissible) setHidden(isMirrorHomeDismissed(familyId));
  }, [familyId, dismissible]);

  useEffect(() => {
    void reload();
  }, [reload]);

  if (hidden) return null;

  const { completed, total, ready } = mirrorChecklistProgress(done);
  const hasUsage = (day?.totalSeconds ?? 0) > 0 || (day?.topApps?.length ?? 0) > 0;
  const showLive = Boolean(day && (hasUsage || day.agentOnline));

  const toggle = (id: string) => {
    setDone((prev) => {
      const next = { ...prev, [id]: !prev[id] };
      saveMirrorChecklist(familyId, next);
      return next;
    });
  };

  const sendNote = async (tone: 'praise' | 'soft' | 'renegotiate', bodyVi: string) => {
    if (!parentMembershipId || !day?.memberId) {
      toast('Cần hồ sơ bố/mẹ để gửi lời tới con');
      return;
    }
    setNoteBusy(true);
    try {
      await postMirrorParentNote(familyId, {
        memberId: day.memberId,
        fromMembershipId: parentMembershipId,
        tone,
        bodyVi,
        flowDate: day.flowDate || undefined,
      });
      toast(
        tone === 'praise'
          ? 'Đã gửi lời khen — con sẽ thấy trong Mirror'
          : tone === 'soft'
            ? 'Đã gửi nhắc nhẹ'
            : 'Đã gửi lời hẹn lại thỏa thuận',
      );
      await reload();
    } catch {
      toast('Chưa gửi được — thử lại nhé');
    } finally {
      setNoteBusy(false);
    }
  };

  const downloadAgent = () => {
    const memberId = childMemberId || day?.memberId || null;
    const result = downloadMirrorAgentInstaller({
      familyId,
      childMemberId: memberId || '',
      childShort: who,
      deviceLabel: `PC học ${who}`,
    });
    if (!result.ok) {
      toast(result.reason);
      return;
    }
    toast(
      `Đã tải ${result.fileName} — mở trên máy Windows của ${who}, double-click để cài & chạy Agent`,
    );
  };

  return (
    <section
      className={`dm-mirror${compact ? ' is-compact' : ''}${ready || showLive ? ' is-ready' : ''}`}
      aria-label="Gương tối Famixa Mirror"
    >
      <header className="dm-mirror-head">
        <div className="dm-mirror-titles">
          <p className="dm-mirror-eyebrow">
            <span aria-hidden>🌙</span> {isKid ? 'Gương tối của mình' : 'Gương tối · ~22:30'}
            {!isKid && day?.agentOnline ? (
              <em className="dm-mirror-live"> · Agent online</em>
            ) : null}
            {!isKid && day && !loading && !day.agentOnline ? (
              <em className="dm-mirror-offline"> · Agent im</em>
            ) : null}
          </p>
          <h3>
            {isKid
              ? showLive
                ? day?.insightVi || 'Hôm nay mình đã sống với màn hình thế nào?'
                : 'Chưa có gương tối hôm nay'
              : showLive
                ? day?.insightVi || `Tối nay ${who} đã sống với màn hình thế nào?`
                : ready
                  ? `Nhà đã sẵn sàng — chờ Mirror cùng ${who}`
                  : `Tối nay nói gì với ${who}?`}
          </h3>
          <p className="dm-mirror-body">
            {isKid
              ? showLive
                ? `${parentLabel} cũng nhìn thấy cùng báo cáo — không khóa máy.`
                : 'Khi máy học có Famixa Agent, tối nay sẽ có tổng kết ấm ở đây.'
              : showLive
                ? `Không khóa máy. ${parentLabel} gửi một lời — ${who} cũng nhìn thấy cùng báo cáo.`
                : ready
                  ? `Chạy Famixa Agent trên máy Windows của ${who}. Mỗi tối Famixa gửi tổng kết ấm.`
                  : `Chưa có Famixa Agent trên máy học. Mirror chỉ tổng kết cuối ngày — không theo dõi live, không khóa máy.`}
          </p>
        </div>
        {dismissible ? (
          <button
            type="button"
            className="dm-mirror-dismiss"
            aria-label="Ẩn gợi ý Mirror trên trang chủ"
            onClick={() => {
              setMirrorHomeDismissed(familyId, true);
              setHidden(true);
              toast('Đã ẩn trên trang chủ — mẹ vẫn mở lại ở Nhật ký');
            }}
          >
            ✕
          </button>
        ) : null}
      </header>

      {loading ? (
        <p className="dm-mirror-hint">Đang tải gương tối…</p>
      ) : showLive && day ? (
        <>
          <ul className="dm-mirror-apps" aria-label="Top ứng dụng hôm nay">
            {day.topApps.slice(0, 5).map((a) => (
              <li key={`${a.kind}-${a.appKey}`}>
                <strong>{a.appLabel || a.appKey}</strong>
                <em>{formatMinutes(a.seconds)}</em>
              </li>
            ))}
            {day.topApps.length === 0 ? (
              <li className="is-empty">
                <strong>Agent đang online</strong>
                <em>Chưa đủ dữ liệu app</em>
              </li>
            ) : null}
          </ul>
          {day.totalSeconds > 0 ? (
            <p className="dm-mirror-total">Tổng ước lượng · {formatMinutes(day.totalSeconds)}</p>
          ) : null}

          {!isKid ? (
            <div className="dm-mirror-note-row" aria-label="Gửi lời tới con">
              {NOTE_PRESETS.map((p) => (
                <button
                  key={p.tone}
                  type="button"
                  className={`pill${p.tone === 'praise' ? '' : ' is-soft'}`}
                  disabled={noteBusy || !parentMembershipId}
                  onClick={() => void sendNote(p.tone, p.body(who))}
                >
                  {p.label}
                </button>
              ))}
            </div>
          ) : null}

          {day.parentNotes.length > 0 ? (
            <div className="dm-mirror-notes">
              {day.parentNotes.slice(0, 3).map((n) => (
                <p key={n.id}>
                  <strong>{n.fromMemberName || parentLabel}:</strong> {n.bodyVi}
                </p>
              ))}
            </div>
          ) : isKid ? (
            <p className="dm-mirror-hint">Chưa có lời từ {parentLabel} tối nay.</p>
          ) : null}

          {!isKid && day.suggestedActions.length > 0 ? (
            <p className="dm-mirror-suggest">{day.suggestedActions[0]}</p>
          ) : null}
        </>
      ) : (
        <>
          <p className="dm-mirror-hint" role="status">
            {isKid
              ? 'Hôm nay chưa có tổng kết — không sao, mai thử lại nhé.'
              : `Chưa có dữ liệu app/web — tải Agent 1 lần trên máy Windows của ${who}.`}
          </p>
          {!isKid ? (
            <div className="dm-mirror-actions dm-mirror-actions-primary">
              <button type="button" className="pill" onClick={downloadAgent}>
                Tải &amp; cài Agent Windows
              </button>
            </div>
          ) : null}
        </>
      )}

      {!isKid ? (
        <>
          {!showLive ? null : (
            <div className="dm-mirror-actions dm-mirror-actions-primary">
              <button type="button" className="pill is-soft" onClick={downloadAgent}>
                Tải lại Agent Windows
              </button>
            </div>
          )}
          <button
            type="button"
            className="dm-mirror-toggle"
            aria-expanded={open}
            onClick={() => setOpen((v) => !v)}
          >
            {open ? 'Thu gọn checklist Agent' : `Checklist Agent · ${completed}/${total}`}
            <span aria-hidden>{open ? '▴' : '▾'}</span>
          </button>

          {open ? (
            <>
              <ul className="dm-mirror-list">
                {MIRROR_AGENT_CHECKLIST.map((item) => (
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
              <div className="dm-mirror-actions">
                <button type="button" className="pill" onClick={downloadAgent}>
                  Tải &amp; cài Agent Windows
                </button>
                <a
                  className="pill is-soft"
                  href={MIRROR_FAMILY_SAFETY_URL}
                  target="_blank"
                  rel="noreferrer"
                >
                  Family Safety
                </a>
                <a
                  className="pill is-soft"
                  href={MIRROR_FAMILY_SAFETY_HELP_URL}
                  target="_blank"
                  rel="noreferrer"
                >
                  Hướng dẫn Microsoft
                </a>
                <button
                  type="button"
                  className="pill is-soft"
                  onClick={() =>
                    void shareOrCopyNudge(mirrorShareText(who))
                      .then(() => toast('Đã copy — gửi Zalo / ghi chú nhà được'))
                      .catch(() => toast('Chưa copy được — thử lại nhé'))
                  }
                >
                  Copy giải thích cho nhà
                </button>
                <button type="button" className="pill is-soft" onClick={() => void reload()}>
                  Làm mới gương
                </button>
              </div>
            </>
          ) : null}
        </>
      ) : (
        <div className="dm-mirror-actions">
          <button type="button" className="pill is-soft" onClick={() => void reload()}>
            Làm mới gương
          </button>
        </div>
      )}
    </section>
  );
}
