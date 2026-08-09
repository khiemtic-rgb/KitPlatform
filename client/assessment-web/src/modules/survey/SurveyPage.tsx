import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Spin, message } from 'antd';
import {
  completeSubmission,
  fetchTemplate,
  getSubmission,
  groupQuestionsByCategory,
  saveResponses,
  type AssessmentQuestion,
  type AssessmentTemplate,
} from '@/shared/api/assessment.api';
import { annotateInsightText } from '@/shared/score/score-display';
import { visibleQuestions } from '@/shared/survey/survey-logic';

const OPTION_TONES = ['rose', 'amber', 'teal', 'blue', 'violet', 'indigo'] as const;

function Icon({ d, size = 16 }: { d: string; size?: number }) {
  return (
    <svg viewBox="0 0 24 24" width={size} height={size} fill="none" aria-hidden>
      <path d={d} stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

const PATH = {
  save: 'M19 21H5a2 2 0 01-2-2V5a2 2 0 012-2h11l5 5v11a2 2 0 01-2 2zM17 21v-8H7v8M7 3v5h8',
  back: 'M19 12H5M12 19l-7-7 7-7',
  next: 'M5 12h14M12 5l7 7-7 7',
  chart: 'M4 20V10M10 20V4M16 20v-8M22 20H2',
  users: 'M12 12a4 4 0 100-8 4 4 0 000 8zM5 20c1.5-3.2 4-5 7-5s5.5 1.8 7 5',
  bulb: 'M9 18h6M10 21h4M12 3a6 6 0 00-3 11.2V16h6v-1.8A6 6 0 0012 3z',
  check: 'M5 13l4 4L19 7',
  x: 'M7 7l10 10M17 7L7 17',
  phone: 'M7 4h4l1.5 4-2 1.5a10 10 0 004.5 4.5L16.5 12l4 1.5V18a2 2 0 01-2 2A14 14 0 015 6a2 2 0 012-2z',
  db: 'M4 7c0 1.7 3.6 3 8 3s8-1.3 8-3-3.6-3-8-3-8 1.3-8 3zm0 5c0 1.7 3.6 3 8 3s8-1.3 8-3M4 17c0 1.7 3.6 3 8 3s8-1.3 8-3',
};

function splitOptionLabel(label: string): { title: string; desc?: string } {
  const annotated = annotateInsightText(label);
  const parts = annotated.split(/\n+| — | – |: /).map((p) => p.trim()).filter(Boolean);
  if (parts.length >= 2) return { title: parts[0], desc: parts.slice(1).join(' — ') };
  return { title: annotated };
}

function OptionToneIcon({ index }: { index: number }) {
  const tone = OPTION_TONES[index % OPTION_TONES.length];
  const path =
    index % 4 === 0 ? PATH.x : index % 4 === 1 ? PATH.phone : index % 4 === 2 ? PATH.users : PATH.db;
  return (
    <span className="sq-option__tone" data-tone={tone} aria-hidden>
      <Icon d={path} size={16} />
    </span>
  );
}

function QuestionArt() {
  return (
    <svg className="sq-card__art" viewBox="0 0 96 72" role="img" aria-hidden>
      <rect x="18" y="10" width="44" height="52" rx="6" fill="#ecfeff" stroke="#0f766e" strokeWidth="2" />
      <path d="M28 24h24M28 34h18M28 44h20" stroke="#0f766e" strokeWidth="2" strokeLinecap="round" />
      <rect x="52" y="28" width="28" height="34" rx="5" fill="#0f766e" opacity="0.9" />
      <path d="M60 40h12M60 48h8" stroke="#fff" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

export function SurveyPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const [template, setTemplate] = useState<AssessmentTemplate | null>(null);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [catIndex, setCatIndex] = useState(0);
  const [qIndex, setQIndex] = useState(0);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  const categories = useMemo(() => (template ? groupQuestionsByCategory(template) : []), [template]);
  const allQuestions = useMemo(() => categories.flatMap((c) => c.questions), [categories]);
  const visibleAllQuestions = useMemo(
    () => visibleQuestions(allQuestions, answers),
    [allQuestions, answers],
  );
  const visibleCategories = useMemo(
    () =>
      categories
        .map((cat) => ({
          ...cat,
          questions: visibleQuestions(cat.questions, answers),
        }))
        .filter((cat) => cat.questions.length > 0),
    [categories, answers],
  );

  const currentCat = visibleCategories[catIndex];
  const currentQuestion: AssessmentQuestion | undefined = currentCat?.questions[qIndex];
  const answeredCount = visibleAllQuestions.filter((q) => answers[q.id]).length;
  const progressPct = visibleAllQuestions.length
    ? Math.round((answeredCount / visibleAllQuestions.length) * 100)
    : 0;

  const categoryAnswered = useMemo(
    () =>
      visibleCategories.map((cat) => ({
        total: cat.questions.length,
        done: cat.questions.filter((q) => answers[q.id]).length,
      })),
    [visibleCategories, answers],
  );

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [tpl, sub] = await Promise.all([fetchTemplate(), getSubmission(id)]);
        if (cancelled) return;
        setTemplate(tpl);
        const initial: Record<string, string> = {};
        for (const [qid, resp] of Object.entries(sub.responses)) {
          if (resp.optionId) initial[qid] = resp.optionId;
          else if (resp.textValue) initial[qid] = resp.textValue;
        }
        setAnswers(initial);
        if (sub.status !== 'draft') {
          navigate(`/results/${id}`, { replace: true });
        }
      } catch {
        message.error('Không tải được khảo sát.');
        navigate('/', { replace: true });
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [id, navigate]);

  const persistAnswer = useCallback(
    async (questionId: string, patch: { optionId?: string; textValue?: string }) => {
      const stored = patch.optionId ?? patch.textValue ?? '';
      setAnswers((prev) => ({ ...prev, [questionId]: stored }));
      try {
        await saveResponses(id, [{ questionId, ...patch }]);
      } catch {
        message.warning('Lưu câu trả lời thất bại — thử lại.');
      }
    },
    [id],
  );

  const isLastQuestion =
    catIndex === visibleCategories.length - 1 &&
    qIndex === (currentCat?.questions.length ?? 1) - 1;

  async function goNext() {
    if (!currentCat || !currentQuestion) return;
    if (currentQuestion.required && !answers[currentQuestion.id]) {
      message.warning('Vui lòng chọn một đáp án.');
      return;
    }
    if (qIndex < currentCat.questions.length - 1) {
      setQIndex(qIndex + 1);
      return;
    }
    if (catIndex < visibleCategories.length - 1) {
      setCatIndex(catIndex + 1);
      setQIndex(0);
      return;
    }
    setSubmitting(true);
    try {
      const result = await completeSubmission(id);
      navigate(`/results/${id}`, { state: { result } });
    } catch (err: unknown) {
      const msg =
        err && typeof err === 'object' && 'response' in err
          ? (err as { response?: { data?: { message?: string } } }).response?.data?.message
          : undefined;
      message.error(msg ?? 'Chưa trả lời đủ câu bắt buộc.');
    } finally {
      setSubmitting(false);
    }
  }

  function goBack() {
    if (qIndex > 0) {
      setQIndex(qIndex - 1);
      return;
    }
    if (catIndex > 0) {
      setCatIndex(catIndex - 1);
      const prevCat = visibleCategories[catIndex - 1];
      setQIndex(prevCat ? prevCat.questions.length - 1 : 0);
    }
  }

  if (loading || !currentQuestion || !currentCat) {
    return (
      <div className="sq-page" style={{ textAlign: 'center', paddingTop: '4rem' }}>
        <Spin size="large" tip="Đang tải khảo sát..." />
      </div>
    );
  }

  const globalIndex =
    visibleCategories.slice(0, catIndex).reduce((n, c) => n + c.questions.length, 0) + qIndex + 1;
  const atStart = catIndex === 0 && qIndex === 0;
  const help =
    currentQuestion.helpText?.trim() ||
    'Chọn phương án mô tả đúng nhất với cách nhà thuốc đang vận hành.';

  return (
    <div className="sq-page">
      <header className="sq-topbar">
        <div className="sq-topbar__left">
          <a className="sq-brand" href="https://novixa.vn/vi/" aria-label="Novixa">
            <img src="/logo.png" alt="Novixa" width="120" height="34" />
          </a>
          <button type="button" className="sq-exit" onClick={() => navigate('/')}>
            <Icon d={PATH.save} size={14} />
            Lưu &amp; thoát
          </button>
        </div>
        <span className="sq-topbar__meta">
          Câu {globalIndex}/{visibleAllQuestions.length}
        </span>
      </header>

      <div className="sq-progress">
        <div className="sq-progress__labels">
          <span>Tiến độ hoàn thành</span>
          <strong>{progressPct}%</strong>
        </div>
        <div className="sq-progress__track">
          <span style={{ width: `${progressPct}%` }} />
        </div>
      </div>

      <nav className="sq-steps" aria-label="Nhóm câu hỏi">
        {visibleCategories.map((cat, idx) => {
          const stat = categoryAnswered[idx];
          const isActive = idx === catIndex;
          const isDone = Boolean(stat && stat.done === stat.total);
          return (
            <div
              key={cat.code}
              className={['sq-step', isActive ? 'is-active' : '', isDone ? 'is-done' : '']
                .filter(Boolean)
                .join(' ')}
            >
              <span className="sq-step__num">{isDone && !isActive ? '✓' : idx + 1}</span>
              <span className="sq-step__name">{cat.name}</span>
            </div>
          );
        })}
      </nav>

      <article className="sq-card">
        <div className="sq-card__head">
          <div className="sq-card__meta">
            <span className="sq-badge sq-badge--cat">
              <Icon d={PATH.users} size={13} />
              Nhóm: {currentCat.name}
            </span>
            <span className="sq-badge sq-badge--code">Câu {currentQuestion.code}</span>
            {!currentQuestion.scorable ? (
              <span className="sq-badge sq-badge--info">Không tính điểm · Hỗ trợ tư vấn</span>
            ) : null}
          </div>
          <QuestionArt />
        </div>

        <h1 className="sq-question">{annotateInsightText(currentQuestion.title)}</h1>
        <p className="sq-help">{annotateInsightText(help)}</p>

        <div className="sq-options" role="group" aria-label="Lựa chọn trả lời">
          {currentQuestion.questionType === 'text' || currentQuestion.questionType === 'scale' ? (
            <textarea
              className="sq-text"
              rows={3}
              value={answers[currentQuestion.id] ?? ''}
              onChange={(e) => void persistAnswer(currentQuestion.id, { textValue: e.target.value })}
              placeholder={currentQuestion.helpText ?? 'Nhập câu trả lời...'}
            />
          ) : currentQuestion.questionType === 'multi_choice' ? (
            currentQuestion.options.map((opt, idx) => {
              const selected = (answers[currentQuestion.id] ?? '')
                .split(',')
                .filter(Boolean)
                .includes(opt.id);
              const { title, desc } = splitOptionLabel(opt.label);
              return (
                <button
                  key={opt.id}
                  type="button"
                  className={['sq-option', selected ? 'is-selected' : ''].filter(Boolean).join(' ')}
                  onClick={() => {
                    const prev = (answers[currentQuestion.id] ?? '').split(',').filter(Boolean);
                    const next = selected ? prev.filter((x) => x !== opt.id) : [...prev, opt.id];
                    void persistAnswer(currentQuestion.id, { optionId: next[0] });
                    setAnswers((a) => ({ ...a, [currentQuestion.id]: next.join(',') }));
                  }}
                >
                  <OptionToneIcon index={idx} />
                  <span className={['sq-option__radio', selected ? 'is-on' : ''].filter(Boolean).join(' ')} />
                  <span className="sq-option__copy">
                    <strong>{title}</strong>
                    {desc ? <small>{desc}</small> : null}
                  </span>
                </button>
              );
            })
          ) : (
            currentQuestion.options.map((opt, idx) => {
              const selected = answers[currentQuestion.id] === opt.id;
              const { title, desc } = splitOptionLabel(opt.label);
              return (
                <button
                  key={opt.id}
                  type="button"
                  role="radio"
                  aria-checked={selected}
                  className={['sq-option', selected ? 'is-selected' : ''].filter(Boolean).join(' ')}
                  onClick={() => persistAnswer(currentQuestion.id, { optionId: opt.id })}
                >
                  <OptionToneIcon index={idx} />
                  <span className={['sq-option__radio', selected ? 'is-on' : ''].filter(Boolean).join(' ')} />
                  <span className="sq-option__copy">
                    <strong>{title}</strong>
                    {desc ? <small>{desc}</small> : null}
                  </span>
                </button>
              );
            })
          )}
        </div>

        <footer className="sq-footer">
          <button type="button" className="sq-btn sq-btn--back" disabled={atStart} onClick={goBack}>
            <Icon d={PATH.back} size={15} />
            Quay lại
          </button>
          <div className="sq-tip">
            <Icon d={PATH.bulb} size={15} />
            <span>Mẹo: Chọn phương án phản ánh đúng thực tế nhất để nhận kết quả đánh giá chính xác.</span>
          </div>
          <button
            type="button"
            className="sq-btn sq-btn--next"
            disabled={submitting}
            onClick={() => void goNext()}
          >
            {submitting ? 'Đang xử lý…' : isLastQuestion ? 'Xem kết quả' : 'Tiếp theo'}
            <Icon d={isLastQuestion ? PATH.chart : PATH.next} size={15} />
          </button>
        </footer>
      </article>
    </div>
  );
}
