import { useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Button, Spin, message } from 'antd';
import {
  CheckCircleFilled,
  CheckOutlined,
  ClockCircleOutlined,
  EnvironmentOutlined,
  HeartFilled,
  HeartOutlined,
  MedicineBoxOutlined,
  PhoneOutlined,
  PlusOutlined,
  SafetyCertificateOutlined,
  SendOutlined,
  ShoppingCartOutlined,
  SmileOutlined,
} from '@ant-design/icons';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import dayjs from 'dayjs';
import { useQueryClient } from '@tanstack/react-query';
import {
  getApiErrorMessage,
  sendChatMessage,
  upsertConsents,
  type ChatOverview,
} from '@/shared/api/customer-app.api';
import type { CustomerChatMessage } from '@/shared/api/customer-app.types';
import { CUSTOMER_APP_CHAT_CONSENT } from '@/shared/api/customer-app.types';
import { overviewQueryKeys, useChatOverviewQuery } from '@/shared/api/overview-queries';
import { buildCustomerChatEventsUrl, subscribeChatSse } from '@/shared/hooks/chat-sse';
import { useVisualViewportInset } from '@/shared/hooks/useVisualViewportInset';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import './ChatPage.css';

const STAFF_SENDER = 2;
const FALLBACK_POLL_MS = 30_000;

function dateLabel(iso: string, t: (k: string) => string) {
  const d = dayjs(iso);
  if (d.isSame(dayjs(), 'day')) return t('chat.today');
  if (d.isSame(dayjs().subtract(1, 'day'), 'day')) return t('chat.yesterday');
  return d.format('DD/MM/YYYY');
}

function ChatBubble({
  item,
  isMine,
  showAvatar,
  showDate,
  showReaction,
  logoUrl,
  pharmacistName,
}: {
  item: CustomerChatMessage;
  isMine: boolean;
  showAvatar: boolean;
  showDate: boolean;
  showReaction: boolean;
  logoUrl: string | null;
  pharmacistName: string;
}) {
  const { t } = useTranslation();
  const time = dayjs(item.createdAt).format('HH:mm');
  const read = Boolean(item.readAt);

  return (
    <>
      {showDate ? (
        <div className="chat-v2-date">
          <span>{dateLabel(item.createdAt, t)}</span>
        </div>
      ) : null}
      <div className={`chat-v2-row${isMine ? ' chat-v2-row--mine' : ' chat-v2-row--staff'}`}>
        {!isMine ? (
          <div className="chat-v2-row-avatar" aria-hidden={!showAvatar}>
            {showAvatar ? <BrandingLogo logoUrl={logoUrl} size={32} style={{ borderRadius: 999 }} /> : null}
          </div>
        ) : null}
        <div className="chat-v2-bubble-wrap">
          <div className={`chat-v2-bubble${isMine ? ' chat-v2-bubble--mine' : ' chat-v2-bubble--staff'}`}>
            <div className="chat-v2-bubble-text">{item.body}</div>
            {isMine ? (
              <div className="chat-v2-bubble-meta chat-v2-bubble-meta--mine">
                <span>{time}</span>
                <span className={`chat-v2-ticks${read ? ' chat-v2-ticks--read' : ''}`} aria-label={read ? t('chat.read') : t('chat.sent')}>
                  <CheckOutlined />
                  <CheckOutlined />
                </span>
              </div>
            ) : null}
          </div>
          {!isMine ? (
            <div className="chat-v2-bubble-foot">
              <span className="chat-v2-bubble-time">{time}</span>
              {showReaction ? (
                <span className="chat-v2-reaction" title={pharmacistName}>
                  <HeartFilled />
                </span>
              ) : null}
            </div>
          ) : null}
        </div>
      </div>
    </>
  );
}

export function ChatPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { branding } = useCustomerBranding();
  const accessToken = useAuthStore((s) => s.accessToken);
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);
  const [inputFocused, setInputFocused] = useState(false);
  const blurTimerRef = useRef<number | null>(null);
  const sendingRef = useRef(false);
  useVisualViewportInset();
  const { data: overview, isLoading, error, refetch } = useChatOverviewQuery();
  const [messages, setMessages] = useState<CustomerChatMessage[]>([]);
  const [sending, setSending] = useState(false);
  const [draft, setDraft] = useState('');
  const [enablingConsent, setEnablingConsent] = useState(false);
  const bottomRef = useRef<HTMLDivElement | null>(null);
  const listRef = useRef<HTMLDivElement | null>(null);

  const chatConsentGranted = overview?.chatConsentGranted ?? false;
  const hasMore = overview?.hasMore ?? false;
  const loading = isLoading && !overview;
  const loadError = error ? getApiErrorMessage(error, t('chat.loadFailed')) : null;

  const pharmacistName = useMemo(() => {
    const staff = [...messages].reverse().find((m) => m.senderType === STAFF_SENDER && m.senderName);
    return staff?.senderName?.trim() || t('chat.pharmacistName');
  }, [messages, t]);

  const staffRecentlyActive = useMemo(() => {
    const staff = [...messages].reverse().find((m) => m.senderType === STAFF_SENDER);
    return Boolean(staff && dayjs(staff.createdAt).isAfter(dayjs().subtract(24, 'hour')));
  }, [messages]);

  const lastStaffId = useMemo(() => {
    for (let i = messages.length - 1; i >= 0; i -= 1) {
      if (messages[i].senderType === STAFF_SENDER) return messages[i].id;
    }
    return null;
  }, [messages]);

  useEffect(() => {
    document.body.classList.add('customer-app--chat-route');
    return () => document.body.classList.remove('customer-app--chat-route');
  }, []);

  useEffect(() => {
    document.body.classList.toggle('customer-app--chat-typing', inputFocused);
    return () => document.body.classList.remove('customer-app--chat-typing');
  }, [inputFocused]);

  useEffect(
    () => () => {
      if (blurTimerRef.current != null) window.clearTimeout(blurTimerRef.current);
    },
    [],
  );

  useEffect(() => {
    if (overview) setMessages(overview.messages);
  }, [overview]);

  const scrollToBottom = () => {
    const el = listRef.current;
    if (!el) return;
    el.scrollTop = el.scrollHeight;
  };

  const resizeTextarea = () => {
    const el = textareaRef.current;
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = `${Math.min(el.scrollHeight, 120)}px`;
  };

  useEffect(() => {
    resizeTextarea();
  }, [draft]);

  useEffect(() => {
    const timer = window.setInterval(() => void refetch(), FALLBACK_POLL_MS);
    return () => window.clearInterval(timer);
  }, [refetch]);

  useEffect(() => {
    if (!accessToken) return;
    const url = buildCustomerChatEventsUrl(accessToken);
    return subscribeChatSse(url, () => void refetch());
  }, [accessToken, refetch]);

  useEffect(() => {
    scrollToBottom();
  }, [messages.length]);

  const onEnableChatConsent = async () => {
    setEnablingConsent(true);
    try {
      const saved = await upsertConsents([
        {
          channel: CUSTOMER_APP_CHAT_CONSENT.channel,
          purpose: CUSTOMER_APP_CHAT_CONSENT.purpose,
          granted: true,
        },
      ]);
      const chatConsent = saved.find(
        (c) =>
          c.channel === CUSTOMER_APP_CHAT_CONSENT.channel &&
          c.purpose === CUSTOMER_APP_CHAT_CONSENT.purpose,
      );
      queryClient.setQueryData<ChatOverview>(overviewQueryKeys.chat(), (current) =>
        current
          ? {
              ...current,
              chatConsentGranted: chatConsent?.granted ?? false,
              consents: saved,
            }
          : current,
      );
      message.success(t('chat.consentEnabled'));
    } catch (err) {
      message.error(getApiErrorMessage(err, t('chat.consentEnableFailed')));
    } finally {
      setEnablingConsent(false);
    }
  };

  const onSend = async () => {
    const text = draft.trim();
    if (!text || sendingRef.current) return;
    sendingRef.current = true;
    setSending(true);
    try {
      const created = await sendChatMessage(text);
      setMessages((prev) => [...prev, created]);
      setDraft('');
      scrollToBottom();
      textareaRef.current?.focus({ preventScroll: true });
      setInputFocused(true);
    } catch (err) {
      message.error(getApiErrorMessage(err, t('chat.sendFailed')));
    } finally {
      sendingRef.current = false;
      setSending(false);
    }
  };

  const clearBlurTimer = () => {
    if (blurTimerRef.current != null) {
      window.clearTimeout(blurTimerRef.current);
      blurTimerRef.current = null;
    }
  };

  const onCall = () => {
    const phone = branding.supportPhone?.replace(/\s+/g, '');
    if (phone) {
      window.location.href = `tel:${phone}`;
      return;
    }
    message.info(t('chat.callUnavailable'));
  };

  const chips = [
    { key: 'suggest', to: '/ai', icon: <MedicineBoxOutlined />, label: t('chat.chipSuggest'), tone: 'amber' },
    { key: 'find', to: '/pharmacy', icon: <EnvironmentOutlined />, label: t('chat.chipFind'), tone: 'orange' },
    { key: 'order', to: '/orders', icon: <ShoppingCartOutlined />, label: t('chat.chipOrder'), tone: 'rose' },
    { key: 'health', to: '/health', icon: <HeartOutlined />, label: t('chat.chipHealth'), tone: 'coral' },
  ] as const;

  const headerStyle = {
    background: `linear-gradient(135deg, ${branding.primaryColor}, ${branding.secondaryColor})`,
  };

  return (
    <div className="chat-v2">
      <div className="chat-v2-top">
        <header className="chat-v2-header" style={headerStyle}>
          <div className="chat-v2-header-inner">
            <div className="chat-v2-brand">
              <BrandingLogo logoUrl={branding.logoUrl} />
              <div className="chat-v2-brand-text">
                <div className="chat-v2-brand-title">
                  <span>{branding.appName}</span>
                  <CheckCircleFilled className="chat-v2-verified" aria-label={t('chat.verified')} />
                </div>
                <div className="chat-v2-brand-tagline">
                  {branding.tagline || t('chat.headerTagline')}
                </div>
              </div>
            </div>
            <div className="chat-v2-header-actions">
              <span className={`chat-v2-online${staffRecentlyActive ? '' : ' chat-v2-online--idle'}`}>
                {staffRecentlyActive ? <span className="chat-v2-online-dot" /> : null}
                {staffRecentlyActive ? t('chat.online') : t('chat.readyToHelp')}
              </span>
            </div>
          </div>
        </header>

        <section className="chat-v2-pharmacist" aria-label={t('chat.pharmacistCard')}>
          <div className="chat-v2-pharmacist-main">
            <div className="chat-v2-pharmacist-avatar-wrap">
              <BrandingLogo
                logoUrl={branding.logoUrl}
                size={52}
                style={{ borderRadius: 999, background: '#fff' }}
              />
              {staffRecentlyActive ? <span className="chat-v2-pharmacist-online" /> : null}
            </div>
            <div className="chat-v2-pharmacist-copy">
              <div className="chat-v2-pharmacist-name">{pharmacistName}</div>
              <div className="chat-v2-pharmacist-role">{t('chat.pharmacistRole')}</div>
              <div className="chat-v2-pharmacist-badges">
                <span className="chat-v2-badge">
                  <SafetyCertificateOutlined />
                  {t('chat.badgeVerified')}
                </span>
                <span className="chat-v2-badge">
                  <ClockCircleOutlined />
                  {t('chat.badgeReply')}
                </span>
              </div>
            </div>
          </div>
          <button type="button" className="chat-v2-call-btn" onClick={onCall}>
            <PhoneOutlined />
            {t('chat.call')}
          </button>
        </section>
      </div>

      {!chatConsentGranted ? (
        <Alert
          className="chat-v2-alert"
          type="warning"
          showIcon
          message={t('chat.consentRequired')}
          action={
            <div className="chat-v2-alert-actions">
              <Button size="small" type="primary" loading={enablingConsent} onClick={() => void onEnableChatConsent()}>
                {t('chat.enableNow')}
              </Button>
              <Link to="/profile">{t('chat.account')}</Link>
            </div>
          }
        />
      ) : null}

      {loadError ? (
        <Alert
          className="chat-v2-alert"
          type="warning"
          showIcon
          message={t('chat.loadErrorTitle')}
          description={loadError}
          action={
            <Button size="small" onClick={() => void refetch()}>
              {t('common.retry')}
            </Button>
          }
        />
      ) : null}

      <div ref={listRef} className="chat-v2-messages">
        {loading && messages.length === 0 ? (
          <div className="chat-v2-empty">
            <Spin />
          </div>
        ) : messages.length === 0 ? (
          <div className="chat-v2-empty">{t('chat.empty')}</div>
        ) : (
          messages.map((item, index) => {
            const isMine = item.senderType !== STAFF_SENDER;
            const prev = messages[index - 1];
            const showDate =
              !prev || !dayjs(item.createdAt).isSame(dayjs(prev.createdAt), 'day');
            const showAvatar =
              !isMine && (!prev || prev.senderType !== STAFF_SENDER || showDate);
            return (
              <ChatBubble
                key={item.id}
                item={item}
                isMine={isMine}
                showAvatar={showAvatar}
                showDate={showDate}
                showReaction={item.id === lastStaffId}
                logoUrl={branding.logoUrl}
                pharmacistName={pharmacistName}
              />
            );
          })
        )}
        {hasMore ? <div className="chat-v2-more-hint">{t('chat.longConversation')}</div> : null}
        <div ref={bottomRef} />
      </div>

      <div className="chat-v2-composer-dock" aria-label={t('chat.title')}>
        <div className="chat-v2-composer-panel">
          <div className="chat-v2-chips" role="list">
            {chips.map((chip) => (
              <Link key={chip.key} to={chip.to} className={`chat-v2-chip chat-v2-chip--${chip.tone}`} role="listitem">
                <span className="chat-v2-chip-icon">{chip.icon}</span>
                <span>{chip.label}</span>
              </Link>
            ))}
          </div>
          <div className="chat-v2-composer-row">
            <button type="button" className="chat-v2-round-btn" aria-label={t('chat.attach')} disabled={!chatConsentGranted}>
              <PlusOutlined />
            </button>
            <div className="chat-v2-input-shell">
              <textarea
                ref={textareaRef}
                className="chat-v2-input"
                rows={1}
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                onFocus={() => {
                  clearBlurTimer();
                  setInputFocused(true);
                }}
                onBlur={() => {
                  clearBlurTimer();
                  blurTimerRef.current = window.setTimeout(() => setInputFocused(false), 200);
                }}
                placeholder={chatConsentGranted ? t('chat.placeholder') : t('chat.placeholderDisabled')}
                disabled={!chatConsentGranted}
                enterKeyHint="send"
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    void onSend();
                  }
                }}
              />
              <span className="chat-v2-emoji" aria-hidden>
                <SmileOutlined />
              </span>
            </div>
            <button
              type="button"
              className="chat-v2-round-btn chat-v2-round-btn--send"
              aria-label={t('chat.send')}
              disabled={!chatConsentGranted || !draft.trim() || sending}
              onPointerDown={(e) => e.preventDefault()}
              onClick={(e) => {
                e.preventDefault();
                void onSend();
              }}
            >
              {sending ? <Spin size="small" /> : <SendOutlined />}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
