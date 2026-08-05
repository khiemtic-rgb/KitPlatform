import { useEffect, useRef, useState } from 'react';
import { useSessionStore } from '@/shared/auth/session.store';

type Step = 'idle' | 'current' | 'next' | 'confirm' | 'done';

function PinPad({
  title,
  hint,
  error,
  digits,
  onDigits,
  onClose,
}: {
  title: string;
  hint: string;
  error: string | null;
  digits: string;
  onDigits: (next: string) => void;
  onClose: () => void;
}) {
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    window.setTimeout(() => inputRef.current?.focus(), 50);
  }, [title]);

  const press = (key: string) => {
    if (key === 'del') {
      onDigits(digits.slice(0, -1));
      return;
    }
    if (digits.length >= 4) return;
    onDigits(digits + key);
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
          onChange={(e) => onDigits(e.target.value.replace(/\D/g, '').slice(0, 4))}
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

/** Đặt lại mã bố mẹ (4 số) — lưu trên máy, không gửi server. */
export function ResetParentPinPanel() {
  const verifyParentPin = useSessionStore((s) => s.verifyParentPin);
  const setParentPin = useSessionStore((s) => s.setParentPin);

  const [step, setStep] = useState<Step>('idle');
  const [digits, setDigits] = useState('');
  const [pending, setPending] = useState('');
  const [error, setError] = useState<string | null>(null);

  const resetLocal = () => {
    setDigits('');
    setPending('');
    setError(null);
  };

  const close = () => {
    setStep('idle');
    resetLocal();
  };

  const start = () => {
    resetLocal();
    setStep('current');
  };

  useEffect(() => {
    if (step === 'idle' || step === 'done' || digits.length !== 4) return;

    if (step === 'current') {
      if (!verifyParentPin(digits)) {
        setError('Sai mã hiện tại — thử lại');
        setDigits('');
        return;
      }
      setDigits('');
      setError(null);
      setStep('next');
      return;
    }

    if (step === 'next') {
      setPending(digits);
      setDigits('');
      setError(null);
      setStep('confirm');
      return;
    }

    if (step === 'confirm') {
      if (digits !== pending) {
        setError('Hai lần nhập chưa khớp — nhập lại mã mới');
        setDigits('');
        setPending('');
        setStep('next');
        return;
      }
      if (digits === '1234') {
        setError('Chọn mã khác 1234 để tránh mã mặc định.');
        setDigits('');
        setPending('');
        setStep('next');
        return;
      }
      setParentPin(digits);
      resetLocal();
      setStep('done');
    }
  }, [digits, step, pending, verifyParentPin, setParentPin]);

  return (
    <>
      <div className="screen-bound is-compact reset-pin-panel">
        <div className="screen-bound-head">
          <strong>Mã bố mẹ</strong>
          <span className="muted">4 số · trên máy này</span>
        </div>
        <p className="muted screen-bound-body">
          Dùng khi đổi người, soft-lock, hoặc đăng xuất thiết bị. Không lưu trên server.
        </p>
        {step === 'done' ? (
          <p className="muted" style={{ margin: '0 0 8px', color: 'var(--brand)' }}>
            Đã đặt mã mới trên máy này.
          </p>
        ) : null}
        <div className="screen-bound-actions">
          <button type="button" className="pill" onClick={start}>
            Đặt lại mã bố mẹ
          </button>
        </div>
      </div>

      {step === 'current' ? (
        <PinPad
          title="Mã hiện tại"
          hint="Nhập mã bố mẹ đang dùng để tiếp tục"
          error={error}
          digits={digits}
          onDigits={(next) => {
            setError(null);
            setDigits(next);
          }}
          onClose={close}
        />
      ) : null}

      {step === 'next' ? (
        <PinPad
          title="Mã mới"
          hint="Chọn 4 số mới — nhớ kỹ hoặc ghi lại"
          error={error}
          digits={digits}
          onDigits={(next) => {
            setError(null);
            setDigits(next);
          }}
          onClose={close}
        />
      ) : null}

      {step === 'confirm' ? (
        <PinPad
          title="Nhập lại mã mới"
          hint="Xác nhận lần nữa"
          error={error}
          digits={digits}
          onDigits={(next) => {
            setError(null);
            setDigits(next);
          }}
          onClose={close}
        />
      ) : null}
    </>
  );
}
