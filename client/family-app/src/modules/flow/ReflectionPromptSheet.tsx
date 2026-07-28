import { useState } from 'react';
import {
  reflectionPromptLabel,
  type ReflectionPromptCode,
} from '@/shared/api/family-os.api';

type Props = {
  title: string;
  promptCode: ReflectionPromptCode;
  busy?: boolean;
  onSubmit: (answer: string) => void | Promise<void>;
  onSkip: () => void;
};

/** Wave 1 Behavior OS — one question after completing a commitment (~15s). */
export function ReflectionPromptSheet({
  title,
  promptCode,
  busy,
  onSubmit,
  onSkip,
}: Props) {
  const [answer, setAnswer] = useState('');
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    const text = answer.trim();
    if (text.length < 1) {
      setError('Viết một chút nhé — hoặc bỏ qua.');
      return;
    }
    if (text.length > 500) {
      setError('Ngắn lại một chút (tối đa 500 ký tự).');
      return;
    }
    setError(null);
    await onSubmit(text);
  };

  return (
    <div className="fos-reflect-backdrop" role="presentation">
      <div
        className="fos-reflect-sheet"
        role="dialog"
        aria-labelledby="fos-reflect-title"
        aria-modal="true"
      >
        <p className="fos-reflect-eyebrow">Behavior OS · Một câu hỏi nhỏ</p>
        <h2 id="fos-reflect-title">{reflectionPromptLabel(promptCode)}</h2>
        <p className="fos-reflect-mission">Vừa xong: {title}</p>
        <textarea
          className="fos-reflect-input"
          rows={3}
          maxLength={500}
          placeholder="Con viết ngắn thôi cũng được…"
          value={answer}
          disabled={busy}
          onChange={(e) => setAnswer(e.target.value)}
        />
        {error ? <p className="fos-reflect-error">{error}</p> : null}
        <div className="fos-reflect-actions">
          <button type="button" className="fos-reflect-skip" disabled={busy} onClick={onSkip}>
            Bỏ qua
          </button>
          <button type="button" className="fos-reflect-go" disabled={busy} onClick={() => void submit()}>
            Gửi
          </button>
        </div>
      </div>
      <style>{`
        .fos-reflect-backdrop {
          position: fixed;
          inset: 0;
          z-index: 80;
          display: flex;
          align-items: flex-end;
          justify-content: center;
          background: rgba(20, 24, 28, 0.45);
          padding: 16px;
        }
        .fos-reflect-sheet {
          width: min(420px, 100%);
          background: #f7f4ef;
          color: #1c2420;
          border-radius: 20px 20px 16px 16px;
          padding: 20px 18px 16px;
          box-shadow: 0 -8px 32px rgba(0,0,0,0.18);
        }
        .fos-reflect-eyebrow {
          margin: 0 0 6px;
          font-size: 12px;
          letter-spacing: 0.04em;
          text-transform: uppercase;
          opacity: 0.65;
        }
        .fos-reflect-sheet h2 {
          margin: 0 0 8px;
          font-size: 1.15rem;
          line-height: 1.35;
          font-weight: 700;
        }
        .fos-reflect-mission {
          margin: 0 0 12px;
          font-size: 0.9rem;
          opacity: 0.75;
        }
        .fos-reflect-input {
          width: 100%;
          box-sizing: border-box;
          border: 1px solid #d5cfc4;
          border-radius: 12px;
          padding: 10px 12px;
          font: inherit;
          resize: vertical;
          background: #fff;
        }
        .fos-reflect-error {
          margin: 8px 0 0;
          color: #9a3412;
          font-size: 0.85rem;
        }
        .fos-reflect-actions {
          display: flex;
          gap: 10px;
          margin-top: 14px;
          justify-content: flex-end;
        }
        .fos-reflect-skip,
        .fos-reflect-go {
          border: none;
          border-radius: 999px;
          padding: 10px 16px;
          font: inherit;
          font-weight: 600;
          cursor: pointer;
        }
        .fos-reflect-skip {
          background: transparent;
          color: #4b5563;
        }
        .fos-reflect-go {
          background: #1f6f5b;
          color: #fff;
        }
        .fos-reflect-go:disabled,
        .fos-reflect-skip:disabled {
          opacity: 0.55;
          cursor: default;
        }
      `}</style>
    </div>
  );
}
