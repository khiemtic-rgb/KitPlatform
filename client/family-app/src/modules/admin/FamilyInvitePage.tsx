import { useCallback, useEffect, useState } from 'react';
import {
  createFamilyInvite,
  formatFamilyInviteShare,
  type FamilyInvite,
} from '@/shared/api/family-os.api';
import { getApiErrorMessage, isCapabilityPaywallError } from '@/shared/billing/capability-error';
import { shareOrCopyNudge } from '@/shared/nudge/nudge';
import { useSessionStore } from '@/shared/auth/session.store';
import { FamilyAdminShell } from '@/modules/admin/FamilyAdminShell';

function formatExpiry(iso?: string) {
  if (!iso) return null;
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return null;
  const d = new Date(t);
  const dd = String(d.getDate()).padStart(2, '0');
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  return `${dd}/${mm}/${d.getFullYear()}`;
}

export function FamilyInvitePage() {
  const familyId = useSessionStore((s) => s.familyId);
  const familyName = useSessionStore((s) => s.familyName);
  const [invite, setInvite] = useState<FamilyInvite | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);

  const loadInvite = useCallback(
    async (fresh = false) => {
      if (!familyId) return;
      setBusy(true);
      setError(null);
      try {
        const next = await createFamilyInvite(familyId, {
          roleCode: 'guardian',
          maxUses: 3,
          validDays: 7,
        });
        setInvite(next);
        if (fresh) setFeedback('Đã tạo mã nhà mới.');
      } catch (err: unknown) {
        setError(
          isCapabilityPaywallError(err)
            ? getApiErrorMessage(err)
            : getApiErrorMessage(err) || 'Chưa tạo được mã nhà.',
        );
      } finally {
        setBusy(false);
      }
    },
    [familyId],
  );

  useEffect(() => {
    void loadInvite(false);
  }, [loadInvite]);

  useEffect(() => {
    if (!feedback) return;
    const t = window.setTimeout(() => setFeedback(null), 2800);
    return () => window.clearTimeout(t);
  }, [feedback]);

  const shareText = invite
    ? formatFamilyInviteShare({
        code: invite.code,
        familyName,
        expiresAt: invite.expiresAt,
      })
    : '';

  const onCopy = async () => {
    if (!invite) return;
    try {
      await shareOrCopyNudge(invite.code);
      setFeedback('Đã sao chép mã nhà.');
    } catch {
      setFeedback('Chưa copy được — chọn mã và copy tay.');
    }
  };

  const onShare = async () => {
    if (!invite) return;
    try {
      const how = await shareOrCopyNudge(shareText, { preferShare: true });
      setFeedback(how === 'shared' ? 'Đã mở chia sẻ.' : 'Đã sao chép nội dung mời.');
    } catch {
      setFeedback('Chưa chia sẻ được — thử Sao chép.');
    }
  };

  const qrUrl = invite
    ? `https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(invite.code)}`
    : null;

  return (
    <FamilyAdminShell title="Mã nhà" subtitle="Một mã để cả nhà vào cùng">
      {feedback ? (
        <p className="ph-action-toast" role="status">
          {feedback}
        </p>
      ) : null}
      {error ? <p className="banner-error">{error}</p> : null}

      <section className="fa-card fa-invite-hero">
        <p className="fa-hint">Mã nhà hiện tại</p>
        <div className="fa-invite-code" aria-live="polite">
          {busy && !invite ? '…' : invite?.code ?? '—'}
        </div>
        {invite ? (
          <p className="fa-hint">
            Dùng {invite.usedCount}/{invite.maxUses}
            {formatExpiry(invite.expiresAt) ? ` · hết hạn ${formatExpiry(invite.expiresAt)}` : null}
          </p>
        ) : null}

        <div className="fa-invite-actions">
          <button type="button" className="btn btn-primary" disabled={!invite || busy} onClick={() => void onCopy()}>
            Sao chép
          </button>
          <button type="button" className="btn" disabled={!invite || busy} onClick={() => void onShare()}>
            Chia sẻ
          </button>
          <button type="button" className="pill is-soft" disabled={busy} onClick={() => void loadInvite(true)}>
            Tạo mã mới
          </button>
        </div>
      </section>

      {qrUrl ? (
        <section className="fa-card fa-invite-qr">
          <h2>QR Code</h2>
          <p className="fa-hint">Đưa màn hình này cho người thân quét / nhìn mã.</p>
          <img src={qrUrl} alt={`QR mã nhà ${invite?.code ?? ''}`} width={200} height={200} />
        </section>
      ) : null}
    </FamilyAdminShell>
  );
}
