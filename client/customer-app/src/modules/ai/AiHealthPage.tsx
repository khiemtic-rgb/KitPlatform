import { useEffect, useMemo, useRef, useState } from 'react';
import { Spin, message } from 'antd';
import {
  ArrowLeftOutlined,
  ExclamationCircleFilled,
  InfoCircleFilled,
  MedicineBoxOutlined,
  MessageOutlined,
  MoreOutlined,
  PlusOutlined,
  QuestionCircleOutlined,
  RightOutlined,
  SendOutlined,
} from '@ant-design/icons';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  askAiHealth,
  fetchActiveMedications,
  getApiErrorMessage,
} from '@/shared/api/customer-app.api';
import type { ActiveMedication, AiHealthAskResponse } from '@/shared/api/customer-app.types';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import './AiHealthPage.css';

type ChatTurn = {
  id: string;
  role: 'user' | 'assistant';
  text: string;
  meta?: AiHealthAskResponse;
};

export function AiHealthPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { branding } = useCustomerBranding();
  const [searchParams, setSearchParams] = useSearchParams();
  const productIdFromUrl = searchParams.get('productId');
  const promptFromUrl = searchParams.get('q');
  const [selectedProductId, setSelectedProductId] = useState<string | undefined>(
    productIdFromUrl ?? undefined,
  );
  const [draft, setDraft] = useState(promptFromUrl ?? '');
  const [sending, setSending] = useState(false);
  const [turns, setTurns] = useState<ChatTurn[]>([]);
  const [meds, setMeds] = useState<ActiveMedication[]>([]);
  const [medsLoading, setMedsLoading] = useState(true);
  const threadRef = useRef<HTMLDivElement>(null);

  const sampleQuestions = useMemo(
    () => [t('ai.sampleQ1'), t('ai.sampleQ2'), t('ai.sampleQ3')],
    [t],
  );

  useEffect(() => {
    let cancelled = false;
    setMedsLoading(true);
    void fetchActiveMedications()
      .then((items) => {
        if (cancelled) return;
        setMeds(items);
        setSelectedProductId((prev) => {
          if (prev) return prev;
          return items.length === 1 ? items[0].productId : undefined;
        });
      })
      .catch(() => {
        if (!cancelled) setMeds([]);
      })
      .finally(() => {
        if (!cancelled) setMedsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (productIdFromUrl) {
      setSelectedProductId(productIdFromUrl);
    }
  }, [productIdFromUrl]);

  useEffect(() => {
    threadRef.current?.scrollTo({ top: threadRef.current.scrollHeight, behavior: 'smooth' });
  }, [turns, sending]);

  const focusedMed = useMemo(
    () => meds.find((m) => m.productId === selectedProductId),
    [meds, selectedProductId],
  );

  const selectProduct = (productId: string | undefined) => {
    setSelectedProductId(productId);
    if (productId) {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        next.set('productId', productId);
        return next;
      }, { replace: true });
    } else {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        next.delete('productId');
        return next;
      }, { replace: true });
    }
  };

  const send = async (question: string) => {
    const q = question.trim();
    if (!q || sending) return;
    setSending(true);
    const userTurn: ChatTurn = { id: `u-${Date.now()}`, role: 'user', text: q };
    setTurns((prev) => [...prev, userTurn]);
    setDraft('');
    try {
      const response = await askAiHealth(q, selectedProductId);
      setTurns((prev) => [
        ...prev,
        { id: `a-${Date.now()}`, role: 'assistant', text: response.answer, meta: response },
      ]);
    } catch (error) {
      message.error(getApiErrorMessage(error, t('ai.sendFailed')));
    } finally {
      setSending(false);
    }
  };

  const headerStyle = {
    background: `linear-gradient(135deg, ${branding.primaryColor}, ${branding.secondaryColor})`,
  };

  return (
    <div className="ai-hub">
      <header className="ai-hub-header" style={headerStyle}>
        <div className="ai-hub-header-inner">
          <div className="ai-hub-brand">
            <button
              type="button"
              className="ai-hub-back"
              aria-label={t('common.back')}
              onClick={() => (window.history.length > 1 ? navigate(-1) : navigate('/'))}
            >
              <ArrowLeftOutlined />
            </button>
            <BrandingLogo logoUrl={branding.logoUrl} />
            <div>
              <div className="ai-hub-brand-title">{branding.appName}</div>
              <div className="ai-hub-tagline">{branding.tagline || t('ai.hubTagline')}</div>
            </div>
          </div>
          <div className="ai-hub-header-actions">
            <div className="ai-hub-mascot" aria-hidden>
              <img src="/home/ai-robot.jpg" alt="" />
            </div>
            <button
              type="button"
              className="ai-hub-more"
              aria-label={t('ai.chatPharmacist')}
              onClick={() => navigate('/chat')}
            >
              <MoreOutlined />
            </button>
          </div>
        </div>
      </header>

      <div className="ai-hub-sheet">
        <div className="ai-hub-intro">
          <span className="ai-hub-intro-icon" aria-hidden>
            <img src="/home/ai-robot.jpg" alt="" />
          </span>
          <div>
            <h1 className="ai-hub-title">{t('ai.title')}</h1>
            <p className="ai-hub-intro-text">{t('ai.intro')}</p>
          </div>
        </div>

        <div className="ai-hub-disclaimer">
          <span className="ai-hub-disclaimer-icon">
            <ExclamationCircleFilled />
          </span>
          <div>
            <div className="ai-hub-disclaimer-title">{t('ai.disclaimerTitle')}</div>
            <div className="ai-hub-disclaimer-desc">
              {t('ai.disclaimerDesc')}{' '}
              <Link to="/chat">{t('ai.chatPharmacistShort')}</Link>
            </div>
          </div>
        </div>

        <section className="ai-hub-card">
          <div className="ai-hub-card-head">
            <div className="ai-hub-card-head-main">
              <span className="ai-hub-card-icon">
                <MedicineBoxOutlined />
              </span>
              <span className="ai-hub-card-title">{t('ai.selectMed')}</span>
            </div>
            <Link to="/medications" className="ai-hub-card-link">
              <PlusOutlined />
              {t('ai.addOtherMed')}
            </Link>
          </div>

          {medsLoading ? (
            <div className="ai-hub-card-loading">
              <Spin size="small" />
            </div>
          ) : meds.length === 0 ? (
            <div className="ai-hub-empty-meds">
              <div className="ai-hub-empty-meds-title">{t('ai.noMedsTitle')}</div>
              <div className="ai-hub-empty-meds-desc">{t('ai.noMedsDesc')}</div>
              <Link to="/orders" className="ai-hub-empty-meds-cta">
                {t('ai.orderMeds')}
              </Link>
            </div>
          ) : (
            <div className="ai-hub-meds" role="group" aria-label={t('ai.selectMed')}>
              {meds.map((item) => {
                const active = selectedProductId === item.productId;
                return (
                  <button
                    key={item.productId}
                    type="button"
                    className={`ai-hub-med-chip${active ? ' ai-hub-med-chip--active' : ''}`}
                    aria-pressed={active}
                    onClick={() => selectProduct(active ? undefined : item.productId)}
                  >
                    {item.productName}
                  </button>
                );
              })}
            </div>
          )}

          {focusedMed ? (
            <div className="ai-hub-focus">
              <strong>{t('ai.askingAbout', { name: focusedMed.productName })}</strong>
              {focusedMed.dosageNote || focusedMed.remindTime ? (
                <span>
                  {t('ai.schedule', {
                    schedule: `${focusedMed.remindTime ?? '—'}${
                      focusedMed.dosageNote ? ` · ${focusedMed.dosageNote}` : ''
                    }`,
                  })}
                </span>
              ) : null}
            </div>
          ) : null}
        </section>

        <section className="ai-hub-card">
          <div className="ai-hub-card-head">
            <div className="ai-hub-card-head-main">
              <span className="ai-hub-card-icon">
                <MessageOutlined />
              </span>
              <span className="ai-hub-card-title">{t('ai.sampleQuestions')}</span>
            </div>
          </div>
          <div className="ai-hub-suggests">
            {sampleQuestions.map((q) => (
              <button
                key={q}
                type="button"
                className="ai-hub-suggest"
                disabled={sending}
                onClick={() => void send(q)}
              >
                <QuestionCircleOutlined />
                {q}
              </button>
            ))}
          </div>
        </section>

        {turns.length > 0 || sending ? (
          <div className="ai-hub-thread" ref={threadRef}>
            {turns.map((turn) => (
              <article
                key={turn.id}
                className={`ai-hub-bubble ai-hub-bubble--${turn.role}`}
              >
                <div className="ai-hub-bubble-text">{turn.text}</div>
                {turn.meta ? (
                  <div className="ai-hub-bubble-meta">
                    <span
                      className={`ai-hub-confidence ai-hub-confidence--${turn.meta.confidence}`}
                    >
                      {turn.meta.confidence}
                    </span>
                    <p>{turn.meta.disclaimer}</p>
                    {turn.meta.suggestChat ? (
                      <Link to="/chat">{t('ai.chatPharmacist')}</Link>
                    ) : null}
                  </div>
                ) : null}
              </article>
            ))}
            {sending ? (
              <div className="ai-hub-typing">
                <Spin size="small" />
                <span>{t('ai.thinking')}</span>
              </div>
            ) : null}
          </div>
        ) : null}

        <div className="ai-hub-composer">
          <input
            className="ai-hub-input"
            value={draft}
            disabled={sending}
            placeholder={
              focusedMed
                ? t('ai.placeholderFocused', { name: focusedMed.productName })
                : t('ai.placeholderGeneral')
            }
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                void send(draft);
              }
            }}
          />
          <button
            type="button"
            className="ai-hub-send"
            disabled={sending || !draft.trim()}
            onClick={() => void send(draft)}
          >
            <SendOutlined />
            {t('ai.send')}
          </button>
        </div>

        <a className="ai-hub-emergency" href="tel:115">
          <span className="ai-hub-emergency-icon">
            <InfoCircleFilled />
          </span>
          <span className="ai-hub-emergency-text">{t('ai.emergency')}</span>
          <RightOutlined />
        </a>
      </div>
    </div>
  );
}
