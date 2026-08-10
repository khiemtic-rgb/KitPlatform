import { useEffect, useRef, useState } from 'react';
import { useSessionStore } from '@/shared/auth/session.store';

type Props = {
  open: boolean;
  title?: string;
  hint?: string;
  onClose: () => void;
  onSuccess: () => void;
  verify: (pin: string) => boolean;
};

const DEMO_PIN = '1234';

export function ParentPinSheet({
  open,
  title = 'Mã bố mẹ',
  hint = 'Nhập 4 số để tiếp tục',
  onClose,
  onSuccess,
  verify,
}: Props) {
  const demoMode = useSessionStore((s) => s.demoMode);
  const [digits, setDigits] = useState('');
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!open) return;
    setDigits('');
    setError(null);
    window.setTimeout(() => inputRef.current?.focus(), 50);
  }, [open]);

  useEffect(() => {
    if (!open || digits.length !== 4) return;
    if (verify(digits)) {
      onSuccess();
      return;
    }
    setError('Sai mã — thử lại nhé');
    setDigits('');
  }, [digits, open, onSuccess, verify]);

  if (!open) return null;

  const press = (key: string) => {
    setError(null);
    if (key === 'del') {
      setDigits((d) => d.slice(0, -1));
      return;
    }
    if (digits.length >= 4) return;
    setDigits((d) => d + key);
  };

  return (
    <div className="pin-overlay" role="dialog" aria-modal="true" aria-label={title}>
      <div className="pin-sheet">
        <div className="pin-sheet-head">
          <strong>{title}</strong>
          <button type="button" className="pill is-soft" onClick={onClose}>
            Đóng
          </button>
        </div>
        <p className="muted" style={{ margin: 0 }}>
          {hint}
        </p>
        {demoMode ? (
          <p
            className="pin-demo-tip"
            style={{
              margin: '0.65rem 0 0',
              padding: '0.65rem 0.75rem',
              borderRadius: 12,
              background: 'rgba(15, 61, 46, 0.08)',
              color: '#0f3d2e',
              fontSize: '0.9rem',
              lineHeight: 1.45,
            }}
          >
            <strong>Nhà demo — thử mã {DEMO_PIN}.</strong>
            <br />
            Nhà thật: 4 số bảo mật do bố mẹ đặt khi tạo nhà / đổi trong Cài đặt, không hiện sẵn.
          </p>
        ) : (
          <p
            className="muted"
            style={{ margin: '0.5rem 0 0', fontSize: '0.82rem', lineHeight: 1.4 }}
          >
            Đây là mã bảo mật do bố mẹ đặt cho thiết bị này — không chia sẻ với con nếu không cần.
          </p>
        )}
        <div className="pin-dots" aria-hidden>
          {[0, 1, 2, 3].map((i) => (
            <span key={i} className={`pin-dot${digits.length > i ? ' is-on' : ''}`} />
          ))}
        </div>
        {error ? <div className="banner-error">{error}</div> : null}
        <input
          ref={inputRef}
          className="pin-hidden-input"
          inputMode="numeric"
          autoComplete="one-time-code"
          value={digits}
          onChange={(e) => {
            const next = e.target.value.replace(/\D/g, '').slice(0, 4);
            setError(null);
            setDigits(next);
          }}
          aria-label="Mã PIN 4 số"
        />
        <div className="pin-pad">
          {['1', '2', '3', '4', '5', '6', '7', '8', '9', '', '0', 'del'].map((key, index) =>
            key === '' ? (
              <span key={`empty-${index}`} />
            ) : (
              <button
                key={key}
                type="button"
                className="pin-key"
                onClick={() => press(key)}
              >
                {key === 'del' ? '⌫' : key}
              </button>
            ),
          )}
        </div>
      </div>
    </div>
  );
}
