import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { fetchFamilies, type FamilyMembership } from '@/shared/api/family-os.api';
import { useSessionStore } from '@/shared/auth/session.store';
import { applyOnboardingPlan, skipOnboarding } from '@/shared/onboarding/apply-plan';
import {
  AGE_OPTIONS,
  GOAL_OPTIONS,
  STRUGGLE_OPTIONS,
  buildStarterPlan,
  isOnboardingDone,
  type AgeBand,
  type GoalCode,
  type StruggleCode,
} from '@/shared/onboarding/onboarding';
import { syncSaveOnboarding } from '@/shared/value/value-sync';
import { avatarEmoji, inferGenderFromName } from '@/shared/ui/avatarGender';

type Step = 'welcome' | 'child' | 'age' | 'struggle' | 'goal' | 'preview';

export function OnboardingPage() {
  const navigate = useNavigate();
  const familyId = useSessionStore((s) => s.familyId);
  const familyName = useSessionStore((s) => s.familyName);
  const member = useSessionStore((s) => s.member);

  const [step, setStep] = useState<Step>('welcome');
  const [children, setChildren] = useState<FamilyMembership[]>([]);
  const [childId, setChildId] = useState('');
  const [childName, setChildName] = useState('');
  const [ageBand, setAgeBand] = useState<AgeBand>('7-9');
  const [struggles, setStruggles] = useState<StruggleCode[]>([]);
  const [goal, setGoal] = useState<GoalCode>('fewer_nudges');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!familyId) {
      navigate('/unlock', { replace: true });
      return;
    }
    if (member?.roleCode === 'child') {
      navigate('/today', { replace: true });
      return;
    }
    void fetchFamilies()
      .then((families) => {
        const family = families.find((f) => f.id === familyId) ?? families[0];
        const kids = (family?.members ?? []).filter((m) => m.roleCode === 'child');
        setChildren(kids);
        if (kids[0] && !childId) {
          setChildId(kids[0].id);
          setChildName(kids[0].displayName);
        }
      })
      .catch(() => setError('Không tải được danh sách con.'));
  }, [familyId, member, navigate, childId]);

  const answers = useMemo(
    () => ({
      childId,
      childName,
      ageBand,
      struggles: struggles.length ? struggles : (['morning_forget'] as StruggleCode[]),
      goal,
    }),
    [childId, childName, ageBand, struggles, goal],
  );

  const plan = useMemo(() => buildStarterPlan(answers), [answers]);

  const toggleStruggle = (code: StruggleCode) => {
    setStruggles((prev) => {
      if (prev.includes(code)) return prev.filter((x) => x !== code);
      if (prev.length >= 2) return [...prev.slice(1), code];
      return [...prev, code];
    });
  };

  const finish = async () => {
    if (!familyId || !childId) return;
    setBusy(true);
    setError(null);
    try {
      const result = await applyOnboardingPlan({ familyId, answers });
      navigate('/today', {
        replace: true,
        state: { onboardingAdded: result.added, onboardingChild: childName },
      });
    } catch {
      setError(
        'Chưa gắn được routine lên server. Đã lưu hồ sơ onboarding — bạn vẫn vào Home được.',
      );
      await syncSaveOnboarding(familyId, {
        ...answers,
        completedAt: new Date().toISOString(),
        missionTitles: plan.missions.map((m) => m.title),
      });
      window.setTimeout(() => navigate('/today', { replace: true }), 1200);
    } finally {
      setBusy(false);
    }
  };

  const onSkip = async () => {
    if (!familyId) return;
    await skipOnboarding(familyId, { childId, childName, ageBand, struggles, goal });
    navigate('/today', { replace: true });
  };

  if (!familyId) return null;

  return (
    <section className="ob-page">
      <header className="ob-top">
        <p className="ob-brand">Famixa</p>
        <p className="ob-family">{familyName ?? 'Gia đình mình'}</p>
      </header>

      {step === 'welcome' ? (
        <article className="ob-card">
          <div className="ob-fox" aria-hidden>
            🦊
          </div>
          <h1>Foxy sẽ setup nhà giúp bạn</h1>
          <p>
            Chỉ vài câu hỏi — Famixa sinh nhịp sống vừa sức. Không cần tự tạo checklist dài.
          </p>
          <ul className="ob-bullets">
            <li>Hiểu tuổi & khó khăn của con</li>
            <li>Sinh 4–6 việc starter</li>
            <li>Nhắm 3 kết quả sau 30 ngày: ít nhắc · tự giác · thời gian chất lượng</li>
          </ul>
          {isOnboardingDone(familyId) ? (
            <p className="ob-note">Bạn đã setup trước đó — có thể chạy lại để tinh chỉnh.</p>
          ) : null}
          <button type="button" className="btn btn-primary" onClick={() => setStep('child')}>
            Bắt đầu với Foxy
          </button>
          <button type="button" className="pill is-soft" onClick={() => void onSkip()}>
            Bỏ qua lần này
          </button>
        </article>
      ) : null}

      {step === 'child' ? (
        <article className="ob-card">
          <p className="ob-step">1 / 4</p>
          <h1>Con nào mình tập trung trước?</h1>
          <p>Onboarding gắn routine cho một con — anh/chị em setup thêm lần sau cũng được.</p>
          <div className="ob-choices">
            {children.map((c) => (
              <button
                key={c.id}
                type="button"
                className={`ob-choice${childId === c.id ? ' is-on' : ''}`}
                onClick={() => {
                  setChildId(c.id);
                  setChildName(c.displayName);
                }}
              >
                <span aria-hidden>{avatarEmoji(inferGenderFromName(c.displayName), 'child')}</span>
                {c.displayName}
              </button>
            ))}
          </div>
          {children.length === 0 ? (
            <p className="ob-note">Chưa có thành viên con — thêm trong Admin rồi quay lại.</p>
          ) : null}
          <div className="ob-nav">
            <button type="button" className="pill is-soft" onClick={() => setStep('welcome')}>
              Quay lại
            </button>
            <button
              type="button"
              className="btn btn-primary"
              disabled={!childId}
              onClick={() => setStep('age')}
            >
              Tiếp
            </button>
          </div>
        </article>
      ) : null}

      {step === 'age' ? (
        <article className="ob-card">
          <p className="ob-step">2 / 4</p>
          <h1>{childName} bao nhiêu tuổi?</h1>
          <p>Foxy chỉnh độ dài việc và giọng khen theo độ tuổi.</p>
          <div className="ob-choices is-stack">
            {AGE_OPTIONS.map((o) => (
              <button
                key={o.value}
                type="button"
                className={`ob-choice is-wide${ageBand === o.value ? ' is-on' : ''}`}
                onClick={() => setAgeBand(o.value)}
              >
                <strong>{o.label}</strong>
                <span>{o.hint}</span>
              </button>
            ))}
          </div>
          <div className="ob-nav">
            <button type="button" className="pill is-soft" onClick={() => setStep('child')}>
              Quay lại
            </button>
            <button type="button" className="btn btn-primary" onClick={() => setStep('struggle')}>
              Tiếp
            </button>
          </div>
        </article>
      ) : null}

      {step === 'struggle' ? (
        <article className="ob-card">
          <p className="ob-step">3 / 4</p>
          <h1>Khó khăn lớn nhất hiện nay?</h1>
          <p>Chọn 1–2 ý — Foxy ưu tiên đúng chỗ đau.</p>
          <div className="ob-choices">
            {STRUGGLE_OPTIONS.map((o) => (
              <button
                key={o.value}
                type="button"
                className={`ob-choice${struggles.includes(o.value) ? ' is-on' : ''}`}
                onClick={() => toggleStruggle(o.value)}
              >
                <span aria-hidden>{o.icon}</span>
                {o.label}
              </button>
            ))}
          </div>
          <div className="ob-nav">
            <button type="button" className="pill is-soft" onClick={() => setStep('age')}>
              Quay lại
            </button>
            <button
              type="button"
              className="btn btn-primary"
              disabled={struggles.length === 0}
              onClick={() => setStep('goal')}
            >
              Tiếp
            </button>
          </div>
        </article>
      ) : null}

      {step === 'goal' ? (
        <article className="ob-card">
          <p className="ob-step">4 / 4</p>
          <h1>Sau 30 ngày, muốn đổi điều gì nhất?</h1>
          <p>Đây là “lý do trả tiền” Famixa sẽ đo.</p>
          <div className="ob-choices is-stack">
            {GOAL_OPTIONS.map((o) => (
              <button
                key={o.value}
                type="button"
                className={`ob-choice is-wide${goal === o.value ? ' is-on' : ''}`}
                onClick={() => setGoal(o.value)}
              >
                <span aria-hidden>{o.icon}</span>
                <strong>{o.label}</strong>
              </button>
            ))}
          </div>
          <div className="ob-nav">
            <button type="button" className="pill is-soft" onClick={() => setStep('struggle')}>
              Quay lại
            </button>
            <button type="button" className="btn btn-primary" onClick={() => setStep('preview')}>
              Xem kế hoạch Foxy
            </button>
          </div>
        </article>
      ) : null}

      {step === 'preview' ? (
        <article className="ob-card">
          <div className="ob-fox is-small" aria-hidden>
            🦊
          </div>
          <h1>Kế hoạch starter của {childName}</h1>
          <p className="ob-pitch">{plan.coachPitch}</p>
          <p className="ob-focus">{plan.focusLine}</p>
          <p className="ob-note">
            Foxy gắn việc vào routine nhà. Nếu hôm nay đã mở Day Flow, starter đầy đủ hiện từ
            ngày mai — mục tiêu 30 ngày vẫn được lưu ngay.
          </p>
          <ul className="ob-missions">
            {plan.missions.map((m) => (
              <li key={m.title}>
                <strong>
                  {m.title}{' '}
                  <em>
                    {m.windowStart}–{m.windowEnd}
                  </em>
                </strong>
                <span>{m.why}</span>
              </li>
            ))}
          </ul>
          {error ? <div className="banner-error">{error}</div> : null}
          <button
            type="button"
            className="btn btn-primary"
            disabled={busy}
            onClick={() => void finish()}
          >
            {busy ? 'Đang gắn routine…' : 'Áp dụng & vào Home'}
          </button>
          <div className="ob-nav">
            <button
              type="button"
              className="pill is-soft"
              disabled={busy}
              onClick={() => setStep('goal')}
            >
              Quay lại
            </button>
            <button type="button" className="pill is-soft" disabled={busy} onClick={() => void onSkip()}>
              Bỏ qua
            </button>
          </div>
        </article>
      ) : null}
    </section>
  );
}
