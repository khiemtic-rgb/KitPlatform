import { useState } from 'react';
import type {
  RetrievalMethodAnswer,
  RetrievalRecallAnswer,
} from '@/shared/api/family-os.api';

type Props = {
  title: string;
  busy?: boolean;
  onSubmit: (
    method: RetrievalMethodAnswer,
    recall: RetrievalRecallAnswer,
  ) => void | Promise<void>;
  onSkip: () => void;
};

const METHOD_OPTIONS: { value: RetrievalMethodAnswer; label: string }[] = [
  { value: 'skim', label: 'Đọc / lướt nhanh' },
  { value: 'practice', label: 'Làm bài / thực hành' },
  { value: 'retrieve', label: 'Nhớ lại / tự kiểm tra' },
];

const RECALL_OPTIONS: { value: RetrievalRecallAnswer; label: string }[] = [
  { value: 'can_explain', label: 'Giải thích được' },
  { value: 'vaguely', label: 'Nhớ đại khái' },
  { value: 'need_review', label: 'Cần xem lại' },
];

/** Wave 2 Behavior OS — 2 meta questions after learning missions (~20s). */
export function RetrievalCheckSheet({ title, busy, onSubmit, onSkip }: Props) {
  const [method, setMethod] = useState<RetrievalMethodAnswer | null>(null);
  const [recall, setRecall] = useState<RetrievalRecallAnswer | null>(null);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (!method || !recall) {
      setError('Chọn cả hai câu trả lời nhé — hoặc bỏ qua.');
      return;
    }
    setError(null);
    await onSubmit(method, recall);
  };

  return (
    <div className="fos-quiz-backdrop" role="presentation">
      <div
        className="fos-quiz-sheet"
        role="dialog"
        aria-labelledby="fos-quiz-title"
        aria-modal="true"
      >
        <p className="fos-quiz-eyebrow">Behavior OS · Kiểm tra nhớ nhẹ</p>
        <h2 id="fos-quiz-title">Con còn nhớ được không?</h2>
        <p className="fos-quiz-mission">Vừa xong: {title}</p>

        <p className="fos-quiz-q">1. Thời gian vừa rồi con chủ yếu làm gì?</p>
        <div className="fos-quiz-opts" role="group" aria-label="Cách làm">
          {METHOD_OPTIONS.map((o) => (
            <button
              key={o.value}
              type="button"
              className={`fos-quiz-opt${method === o.value ? ' is-on' : ''}`}
              disabled={busy}
              onClick={() => setMethod(o.value)}
            >
              {o.label}
            </button>
          ))}
        </div>

        <p className="fos-quiz-q">2. Nếu hỏi lại ngay, con thế nào?</p>
        <div className="fos-quiz-opts" role="group" aria-label="Mức nhớ">
          {RECALL_OPTIONS.map((o) => (
            <button
              key={o.value}
              type="button"
              className={`fos-quiz-opt${recall === o.value ? ' is-on' : ''}`}
              disabled={busy}
              onClick={() => setRecall(o.value)}
            >
              {o.label}
            </button>
          ))}
        </div>

        {error ? <p className="fos-quiz-error">{error}</p> : null}
        <div className="fos-quiz-actions">
          <button type="button" className="fos-quiz-skip" disabled={busy} onClick={onSkip}>
            Bỏ qua
          </button>
          <button type="button" className="fos-quiz-go" disabled={busy} onClick={() => void submit()}>
            Gửi
          </button>
        </div>
      </div>
      <style>{`
        .fos-quiz-backdrop {
          position: fixed;
          inset: 0;
          z-index: 81;
          display: flex;
          align-items: flex-end;
          justify-content: center;
          background: rgba(20, 24, 28, 0.45);
          padding: 16px;
        }
        .fos-quiz-sheet {
          width: min(420px, 100%);
          background: #f7f4ef;
          color: #1c2420;
          border-radius: 20px 20px 16px 16px;
          padding: 20px 18px 16px;
          box-shadow: 0 -8px 32px rgba(0,0,0,0.18);
          max-height: min(90vh, 640px);
          overflow-y: auto;
        }
        .fos-quiz-eyebrow {
          margin: 0 0 6px;
          font-size: 12px;
          letter-spacing: 0.04em;
          text-transform: uppercase;
          opacity: 0.65;
        }
        .fos-quiz-sheet h2 {
          margin: 0 0 8px;
          font-size: 1.15rem;
          line-height: 1.35;
          font-weight: 700;
        }
        .fos-quiz-mission {
          margin: 0 0 14px;
          font-size: 0.9rem;
          opacity: 0.75;
        }
        .fos-quiz-q {
          margin: 0 0 8px;
          font-size: 0.95rem;
          font-weight: 600;
        }
        .fos-quiz-opts {
          display: flex;
          flex-direction: column;
          gap: 8px;
          margin: 0 0 14px;
        }
        .fos-quiz-opt {
          text-align: left;
          border: 1px solid #d5cfc4;
          border-radius: 12px;
          padding: 10px 12px;
          font: inherit;
          background: #fff;
          cursor: pointer;
        }
        .fos-quiz-opt.is-on {
          border-color: #1f6f5b;
          background: #e8f5f1;
          font-weight: 600;
        }
        .fos-quiz-error {
          margin: 0 0 8px;
          color: #9a3412;
          font-size: 0.85rem;
        }
        .fos-quiz-actions {
          display: flex;
          gap: 10px;
          margin-top: 6px;
          justify-content: flex-end;
        }
        .fos-quiz-skip,
        .fos-quiz-go {
          border: none;
          border-radius: 999px;
          padding: 10px 16px;
          font: inherit;
          font-weight: 600;
          cursor: pointer;
        }
        .fos-quiz-skip {
          background: transparent;
          color: #4b5563;
        }
        .fos-quiz-go {
          background: #1f6f5b;
          color: #fff;
        }
        .fos-quiz-go:disabled,
        .fos-quiz-skip:disabled,
        .fos-quiz-opt:disabled {
          opacity: 0.55;
          cursor: default;
        }
      `}</style>
    </div>
  );
}
