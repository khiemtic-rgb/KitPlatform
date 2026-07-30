import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  fetchFamilySubscription,
  type FamilySubscription,
} from '@/shared/api/family-os.api';
import { buildCheckoutPath } from '@/shared/api/payment.api';
import { useSessionStore } from '@/shared/auth/session.store';
import {
  fetchParentPushStatus,
  isParentPushSupported,
  registerParentPushSubscription,
  subscribeParentPush,
} from '@/shared/push/parentPush';
import {
  ensureNotificationPermission,
  notificationPermission,
  notificationSupport,
  shouldOfferNotificationOptIn,
} from '@/shared/reminders/localReminders';
import {
  isInAppChimeEnabled,
  playInAppDueChime,
  setInAppChimeEnabled,
} from '@/shared/reminders/inAppChime';
import { clearOnboardingProfile } from '@/shared/onboarding/onboarding';
import { ResetParentPinPanel } from '@/shared/ui/ResetParentPinPanel';
import { FamilyAdminShell } from '@/modules/admin/FamilyAdminShell';

function daysLeft(iso?: string | null, trialDays?: number | null): number | null {
  if (trialDays != null) return trialDays;
  if (!iso) return null;
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return null;
  return Math.max(0, Math.ceil((t - Date.now()) / (24 * 60 * 60 * 1000)));
}

export function FamilySettingsPage() {
  const navigate = useNavigate();
  const familyId = useSessionStore((s) => s.familyId);
  const familyName = useSessionStore((s) => s.familyName);
  const member = useSessionStore((s) => s.member);
  const clear = useSessionStore((s) => s.clear);

  const [subscription, setSubscription] = useState<FamilySubscription | null>(null);
  const [subLoading, setSubLoading] = useState(true);
  const [pushSubscribed, setPushSubscribed] = useState(false);
  const [pushBusy, setPushBusy] = useState(false);
  const [localPerm, setLocalPerm] = useState(() => notificationPermission());
  const [offerLocal, setOfferLocal] = useState(() => shouldOfferNotificationOptIn());
  const [chimeOn, setChimeOn] = useState(() => isInAppChimeEnabled());
  const [notifyMsg, setNotifyMsg] = useState<string | null>(null);
  const [confirmLeave, setConfirmLeave] = useState(false);

  const reloadSub = useCallback(async () => {
    if (!familyId) {
      setSubscription(null);
      setSubLoading(false);
      return;
    }
    setSubLoading(true);
    try {
      setSubscription(await fetchFamilySubscription(familyId));
    } catch {
      setSubscription(null);
    } finally {
      setSubLoading(false);
    }
  }, [familyId]);

  const reloadPush = useCallback(async () => {
    if (!familyId || !member || member.roleCode === 'child') {
      setPushSubscribed(false);
      return;
    }
    try {
      const s = await fetchParentPushStatus(familyId, member.id);
      setPushSubscribed(s.subscribed);
    } catch {
      setPushSubscribed(false);
    }
  }, [familyId, member]);

  useEffect(() => {
    void reloadSub();
    void reloadPush();
  }, [reloadSub, reloadPush]);

  useEffect(() => {
    const hash = window.location.hash.replace('#', '');
    if (!hash) return;
    window.requestAnimationFrame(() => {
      document.getElementById(`fa-set-${hash}`)?.scrollIntoView({
        behavior: 'smooth',
        block: 'start',
      });
    });
  }, []);

  const peaceDays = useMemo(
    () =>
      daysLeft(
        subscription?.trialEndsAt || subscription?.currentPeriodEnd,
        subscription?.trialDaysRemaining,
      ),
    [subscription],
  );

  const goCheckout = () => {
    if (!familyId) return;
    navigate(
      buildCheckoutPath({
        productCode: 'family_os',
        subjectType: 'family',
        subjectId: familyId,
        planCode: subscription?.recommendedUpgradePlanCode || 'family_pro_month',
        returnPath: '/family-admin/settings',
      }),
    );
  };

  const enablePush = async () => {
    if (!familyId || !member) return;
    setPushBusy(true);
    setNotifyMsg(null);
    try {
      if (!isParentPushSupported()) {
        setNotifyMsg('Trình duyệt không hỗ trợ Web Push.');
        return;
      }
      const status = await fetchParentPushStatus(familyId, member.id);
      if (!status.supported || !status.publicKey) {
        setNotifyMsg('Push chưa cấu hình trên server.');
        return;
      }
      await Notification.requestPermission();
      const sub = await subscribeParentPush(status.publicKey);
      await registerParentPushSubscription(familyId, {
        membershipId: member.id,
        ...sub,
      });
      setPushSubscribed(true);
      setNotifyMsg('Đã bật nhắc push phụ huynh.');
    } catch {
      setNotifyMsg('Chưa bật được push. Thử Chrome/Edge (HTTPS hoặc localhost).');
    } finally {
      setPushBusy(false);
    }
  };

  const enableLocalReminders = async () => {
    setNotifyMsg(null);
    const permission = await ensureNotificationPermission();
    setLocalPerm(permission);
    setOfferLocal(shouldOfferNotificationOptIn());
    if (permission === 'granted') {
      setNotifyMsg('Đã cho phép nhắc trên máy (trình duyệt).');
    } else if (permission === 'denied') {
      setNotifyMsg('Trình duyệt đang chặn thông báo — mở lại trong cài đặt trình duyệt.');
    }
  };

  const toggleChime = () => {
    const next = !isInAppChimeEnabled();
    setInAppChimeEnabled(next);
    setChimeOn(next);
    if (next) void playInAppDueChime();
  };

  const leaveHouse = () => {
    clear();
    navigate('/unlock', { replace: true });
  };

  const planTitle =
    subscription?.displayNameVi ||
    subscription?.outcomeNameVi ||
    (subLoading ? 'Đang tải…' : 'Chưa có gói');

  return (
    <FamilyAdminShell
      title="Tài khoản / Cài đặt"
      subtitle="Gói · thông báo · nhà · rời nhà"
      backTo="/today"
    >
      <section className="fa-card" id="fa-set-billing">
        <h2>Gói &amp; gia hạn</h2>
        {subscription ? (
          <>
            <p className="fa-settings-value">{planTitle}</p>
            <p className="fa-hint">
              {subscription.status === 'trial'
                ? peaceDays != null
                  ? `Dùng thử · còn ${peaceDays} ngày`
                  : 'Đang dùng thử'
                : peaceDays != null
                  ? `Chu kỳ hiện tại · còn ${peaceDays} ngày`
                  : subscription.isEntitled
                    ? 'Gói đang hoạt động'
                    : 'Chưa kích hoạt gói trả phí'}
            </p>
            {subscription.upgradeHintVi ? (
              <p className="fa-hint">{subscription.upgradeHintVi}</p>
            ) : null}
          </>
        ) : (
          <p className="fa-hint">
            {subLoading ? 'Đang tải thông tin gói…' : 'Chưa tải được gói — thử gia hạn để kích hoạt.'}
          </p>
        )}
        <div className="fa-settings-actions">
          <button type="button" className="btn btn-primary" onClick={goCheckout} disabled={!familyId}>
            Gia hạn / Nâng cấp
          </button>
          <button type="button" className="pill is-soft" onClick={() => void reloadSub()}>
            Làm mới
          </button>
        </div>
      </section>

      <section className="fa-card" id="fa-set-notify">
        <h2>Thông báo</h2>
        <p className="fa-hint">
          Gom nhắc việc của bố mẹ tại một chỗ. Push dùng âm hệ thống; chuông trong app chỉ khi đang mở
          Hôm nay.
        </p>

        <div className="fa-settings-row">
          <div>
            <strong>Nhắc push phụ huynh</strong>
            <em>{pushSubscribed ? 'Đã bật trên thiết bị này' : 'Chưa bật'}</em>
          </div>
          {pushSubscribed ? (
            <span className="fa-settings-badge is-on">Bật</span>
          ) : (
            <button
              type="button"
              className="pill"
              disabled={pushBusy || !member || member.roleCode === 'child'}
              onClick={() => void enablePush()}
            >
              {pushBusy ? 'Đang bật…' : 'Bật push'}
            </button>
          )}
        </div>

        <div className="fa-settings-row">
          <div>
            <strong>Nhắc trên máy (trình duyệt)</strong>
            <em>
              {!notificationSupport()
                ? 'Không hỗ trợ'
                : localPerm === 'granted'
                  ? 'Đã cho phép'
                  : localPerm === 'denied'
                    ? 'Đang bị chặn'
                    : offerLocal
                      ? 'Chưa hỏi quyền'
                      : 'Chờ cấp quyền'}
            </em>
          </div>
          {localPerm !== 'granted' && notificationSupport() ? (
            <button type="button" className="pill is-soft" onClick={() => void enableLocalReminders()}>
              Cho phép
            </button>
          ) : localPerm === 'granted' ? (
            <span className="fa-settings-badge is-on">Bật</span>
          ) : null}
        </div>

        <div className="fa-settings-row">
          <div>
            <strong>Chuông trong app khi đến giờ</strong>
            <em>Chỉ khi đang mở Daily Flow</em>
          </div>
          <button
            type="button"
            className={`pill${chimeOn ? '' : ' is-soft'}`}
            onClick={toggleChime}
          >
            {chimeOn ? 'Bật' : 'Tắt'}
          </button>
        </div>

        {notifyMsg ? (
          <p className="fa-hint" role="status">
            {notifyMsg}
          </p>
        ) : null}
      </section>

      <section className="fa-card" id="fa-set-house">
        <h2>Nhà</h2>
        <p className="fa-settings-value">{familyName ?? 'Nhà mình'}</p>
        <p className="fa-hint">Đổi tên nhà sẽ mở trong bản cập nhật tới.</p>
        <p className="fa-settings-value" style={{ fontSize: '1rem', marginTop: 12 }}>
          Asia/Ho_Chi_Minh
        </p>
        <p className="fa-hint">Lịch ngày và nhắc việc theo giờ Việt Nam.</p>
        <button
          type="button"
          className="pill is-soft"
          onClick={() => navigate('/family-admin')}
        >
          Quản trị gia đình (thành viên · routine) →
        </button>
      </section>

      <section className="fa-card" id="fa-set-pin">
        <h2>Mã PIN bố mẹ</h2>
        <p className="fa-hint">Đổi người / mở soft-lock cần mã. Đặt lại tại đây nếu quên.</p>
        <ResetParentPinPanel />
      </section>

      <section className="fa-card" id="fa-set-more">
        <h2>Khác</h2>
        <div className="fa-settings-actions">
          <button
            type="button"
            className="pill is-soft"
            onClick={() => {
              if (familyId) clearOnboardingProfile(familyId);
              navigate('/onboarding');
            }}
          >
            Chạy lại AI Onboarding
          </button>
          <button type="button" className="pill is-soft" onClick={() => navigate('/who')}>
            Đổi người / thành viên →
          </button>
        </div>
      </section>

      <section className="fa-card" id="fa-set-leave">
        <h2>Rời nhà</h2>
        <p className="fa-hint">Đăng xuất thiết bị này. Dữ liệu nhà vẫn giữ trên máy chủ.</p>
        {!confirmLeave ? (
          <button type="button" className="pill is-soft" onClick={() => setConfirmLeave(true)}>
            Rời nhà trên máy này
          </button>
        ) : (
          <div className="fa-danger-row">
            <button type="button" className="btn btn-primary" onClick={leaveHouse}>
              Xác nhận rời
            </button>
            <button type="button" className="pill is-soft" onClick={() => setConfirmLeave(false)}>
              Huỷ
            </button>
          </div>
        )}
      </section>

      <section className="fa-card fa-card-danger">
        <h2>Xóa nhà</h2>
        <p className="fa-hint">
          Xóa vĩnh viễn toàn bộ thành viên và lịch — chưa mở trên app. Liên hệ hỗ trợ Famixa nếu cần.
        </p>
        <button type="button" className="pill is-soft" disabled>
          Xóa nhà (chưa mở)
        </button>
      </section>
    </FamilyAdminShell>
  );
}
