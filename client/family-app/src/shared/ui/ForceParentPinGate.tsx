import { useEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { useSessionStore } from '@/shared/auth/session.store';

const DEFAULT_PIN = '1234';

/**
 * First-use: nếu PIN trên máy vẫn là mặc định 1234 thì bắt đặt mã mới (≠ 1234).
 * Không chặn màn unlock / child.
 */
export function ForceParentPinGate() {
  const location = useLocation();
  const accessToken = useSessionStore((s) => s.accessToken);
  const member = useSessionStore((s) => s.member);
  const parentPin = useSessionStore((s) => s.parentPin);
  const setParentPin = useSessionStore((s) => s.setParentPin);
  const demoMode = useSessionStore((s) => s.demoMode);

  const [digits, setDigits] = useState('');
  const [pending, setPending] = useState('');
  const [step, setStep] = useState<'next' | 'confirm'>('next');
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const blocked =
    Boolean(accessToken) &&
    !demoMode &&
    member?.roleCode !== 'child' &&
    member?.roleCode !== 'viewer' &&
    location.pathname !== '/unlock' &&
    location.pathname !== '/demo' &&
    (parentPin || DEFAULT_PIN) === DEFAULT_PIN;

  useEffect(() => {
    if (!blocked) return;
    window.setTimeout(() => inputRef.current?.focus(), 60);
  }, [blocked, step]);

  useEffect(() => {
    if (!blocked || digits.length !== 4) return;

    if (step === 'next') {
      if (digits === DEFAULT_PIN) {
        setError('Chọn mã khác 1234 để bảo vệ thiết bị dùng chung.');
        setDigits('');
        return;
      }
      setPending(digits);
      setDigits('');
      setError(null);
      setStep('confirm');
      return;
    }

    if (digits !== pending) {
      setError('Hai lần nhập chưa khớp — nhập lại mã mới.');
      setDigits('');
      setPending('');
      setStep('next');
      return;
    }
    setParentPin(digits);
    setDigits('');
    setPending('');
    setError(null);
    setStep('next');
  }, [digits, step, pending, blocked, setParentPin]);

  if (!blocked) return null;

  const press = (key: string) => {
    if (key === 'del') {
      setDigits((d) => d.slice(0, -1));
      return;
    }
    if (digits.length >= 4) return;
    setError(null);
    setDigits((d) => d + key);
  };

  return (
    <div className="pin-overlay" role="dialog" aria-modal="true" aria-label="Đặt mã bố mẹ">
      <div className="pin-sheet">
        <div className="pin-sheet-head">
          <strong>{step === 'next' ? 'Đặt mã bố mẹ' : 'Nhập lại mã mới'}</strong>
        </div>
        <p className="muted" style={{ margin: 0 }}>
          {step === 'next'
            ? 'Thiết bị đang dùng mã mặc định 1234. Đặt 4 số mới trước khi dùng tiếp (không gửi lên server).'
            : 'Xác nhận lần nữa — nhớ kỹ hoặc ghi lại.'}
        </p>
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
            setError(null);
            setDigits(e.target.value.replace(/\D/g, '').slice(0, 4));
          }}
          aria-label="Mã PIN 4 số"
        />
        <div className="pin-pad">
          {['1', '2', '3', '4', '5', '6', '7', '8', '9', '', '0', 'del'].map((key, index) =>
            key === '' ? (
              <span key={`empty-${index}`} />
            ) : (
              <button key={key} type="button" className="pin-key" onClick={() => press(key)}>
                {key === 'del' ? '⌫' : key}
              </button>
            ),
          )}
        </div>
      </div>
    </div>
  );
}
