import { useEffect, useState } from 'react';
import {
  ensureNotificationPermission,
  notificationPermission,
  notificationSupport,
} from '@/shared/reminders/localReminders';
import {
  isInAppChimeEnabled,
  playInAppDueChime,
  setInAppChimeEnabled,
} from '@/shared/reminders/inAppChime';

const DISMISS_KEY = 'famixa.kid.notify.optin.dismiss.v1';

type Props = {
  shortName: string;
};

/**
 * Kid self-serve: enable browser notification sound + in-app chime
 * without opening parent PIN settings.
 */
export function KidNotifyOptInCard({ shortName }: Props) {
  const who = shortName.trim() || 'con';
  const [perm, setPerm] = useState(() => notificationPermission());
  const [chimeOn, setChimeOn] = useState(() => isInAppChimeEnabled());
  const [dismissed, setDismissed] = useState(() => {
    try {
      return localStorage.getItem(DISMISS_KEY) === '1';
    } catch {
      return false;
    }
  });
  const [busy, setBusy] = useState(false);
  const [tip, setTip] = useState<string | null>(null);

  useEffect(() => {
    setPerm(notificationPermission());
    setChimeOn(isInAppChimeEnabled());
  }, []);

  const fullyOn = chimeOn && (perm === 'granted' || !notificationSupport());
  if (dismissed && fullyOn) return null;
  if (dismissed && perm === 'denied') return null;

  const dismiss = () => {
    try {
      localStorage.setItem(DISMISS_KEY, '1');
    } catch {
      /* ignore */
    }
    setDismissed(true);
  };

  const enable = async () => {
    setBusy(true);
    setTip(null);
    try {
      setInAppChimeEnabled(true);
      setChimeOn(true);
      if (notificationSupport() && Notification.permission !== 'granted') {
        const next = await ensureNotificationPermission();
        setPerm(next);
        if (next === 'denied') {
          setTip(
            'Máy đang chặn thông báo. Mở Cài đặt điện thoại → Thông báo → Famixa / Chrome → Cho phép.',
          );
          return;
        }
      } else {
        setPerm(notificationPermission());
      }
      await playInAppDueChime();
      setTip(`Xong! Khi đến giờ việc, ${who} sẽ nghe chuông nhắc nhé.`);
      try {
        localStorage.removeItem(DISMISS_KEY);
      } catch {
        /* ignore */
      }
      setDismissed(false);
    } finally {
      setBusy(false);
    }
  };

  const disableChime = () => {
    setInAppChimeEnabled(false);
    setChimeOn(false);
    setTip('Đã tắt chuông trong app. Bật lại bất cứ lúc nào.');
  };

  if (fullyOn && !tip) {
    return (
      <section className="kid-notify is-on" aria-label="Chuông nhắc việc">
        <div className="kid-notify-copy">
          <strong>
            <span aria-hidden>🔔</span> Chuông nhắc đã bật
          </strong>
          <p>Fami sẽ kêu khi đến giờ việc của {who}.</p>
        </div>
        <button type="button" className="pill is-soft" onClick={disableChime}>
          Tắt
        </button>
      </section>
    );
  }

  if (dismissed && !fullyOn) {
    return (
      <button type="button" className="kid-notify-mini" onClick={() => setDismissed(false)}>
        <span aria-hidden>🔔</span> Bật chuông nhắc
      </button>
    );
  }

  return (
    <section className="kid-notify" aria-label="Bật chuông nhắc việc">
      <div className="kid-notify-copy">
        <strong>
          <span aria-hidden>🔔</span> Chuông nhắc việc
        </strong>
        <p>
          {who} muốn Fami kêu leng keng khi đến giờ không? Không cần hỏi bố mẹ PIN.
        </p>
        {tip ? (
          <p className="kid-notify-tip" role="status">
            {tip}
          </p>
        ) : null}
      </div>
      <div className="kid-notify-actions">
        <button type="button" className="pill" disabled={busy} onClick={() => void enable()}>
          {busy ? 'Đang bật…' : fullyOn ? 'Thử chuông lại' : 'Bật chuông'}
        </button>
        {!fullyOn ? (
          <button type="button" className="pill is-soft" disabled={busy} onClick={dismiss}>
            Để sau
          </button>
        ) : null}
      </div>
    </section>
  );
}
