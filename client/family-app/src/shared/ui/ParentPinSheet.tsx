import { useEffect, useRef, useState } from 'react';

type Props = {
  open: boolean;
  title?: string;
  hint?: string;
  onClose: () => void;
  onSuccess: () => void;
  verify: (pin: string) => boolean;
};

export function ParentPinSheet({
  open,
  title = 'Mã bố mẹ',
  hint = 'Nhập 4 số để tiếp tục',
  onClose,
  onSuccess,
  verify,
}: Props) {
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
