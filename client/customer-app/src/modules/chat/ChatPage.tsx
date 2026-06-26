import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, Button, Input, Space, Spin, Typography, message } from 'antd';
import { SendOutlined } from '@ant-design/icons';
import { Link } from 'react-router-dom';
import dayjs from 'dayjs';
import {
  fetchChatMessages,
  fetchConsents,
  getApiErrorMessage,
  markChatRead,
  sendChatMessage,
  upsertConsents,
} from '@/shared/api/customer-app.api';
import type { CustomerChatMessage } from '@/shared/api/customer-app.types';
import { CUSTOMER_APP_CHAT_CONSENT } from '@/shared/api/customer-app.types';
import { buildCustomerChatEventsUrl, subscribeChatSse } from '@/shared/hooks/chat-sse';
import { useAuthStore } from '@/shared/auth/auth.store';

const STAFF_SENDER = 2;
const FALLBACK_POLL_MS = 30_000;

function ChatBubble({ item, isMine }: { item: CustomerChatMessage; isMine: boolean }) {
  return (
    <div
      style={{
        display: 'flex',
        justifyContent: isMine ? 'flex-end' : 'flex-start',
        marginBottom: 10,
      }}
    >
      <div
        style={{
          maxWidth: '82%',
          padding: '10px 12px',
          borderRadius: isMine ? '14px 14px 4px 14px' : '14px 14px 14px 4px',
          background: isMine ? '#0f766e' : '#fff',
          color: isMine ? '#fff' : '#134e4a',
          border: isMine ? 'none' : '1px solid #ccfbf1',
          boxShadow: '0 1px 2px rgba(15,118,110,0.08)',
        }}
      >
        {!isMine && item.senderName ? (
          <Typography.Text
            style={{ display: 'block', fontSize: 11, color: '#0f766e', marginBottom: 4 }}
          >
            {item.senderName}
          </Typography.Text>
        ) : null}
        <div style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>{item.body}</div>
        <Typography.Text
          style={{
            display: 'block',
            fontSize: 10,
            marginTop: 6,
            color: isMine ? 'rgba(255,255,255,0.75)' : '#64748b',
          }}
        >
          {dayjs(item.createdAt).format('DD/MM HH:mm')}
        </Typography.Text>
      </div>
    </div>
  );
}

export function ChatPage() {
  const accessToken = useAuthStore((s) => s.accessToken);
  const [messages, setMessages] = useState<CustomerChatMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const [draft, setDraft] = useState('');
  const [hasMore, setHasMore] = useState(false);
  const [chatConsentGranted, setChatConsentGranted] = useState(false);
  const [enablingConsent, setEnablingConsent] = useState(false);
  const bottomRef = useRef<HTMLDivElement | null>(null);
  const listRef = useRef<HTMLDivElement | null>(null);

  const scrollToBottom = () => {
    const el = listRef.current;
    if (!el) return;
    el.scrollTop = el.scrollHeight;
  };

  const loadMessages = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    if (!silent) setLoadError(null);
    try {
      const [consents, page] = await Promise.all([fetchConsents(), fetchChatMessages()]);
      const chatConsent = consents.find(
        (c) =>
          c.channel === CUSTOMER_APP_CHAT_CONSENT.channel &&
          c.purpose === CUSTOMER_APP_CHAT_CONSENT.purpose,
      );
      setChatConsentGranted(chatConsent?.granted ?? false);
      setMessages(page.items);
      setHasMore(page.hasMore);
      await markChatRead().catch(() => undefined);
    } catch (error) {
      const errMsg = getApiErrorMessage(error, 'Không tải được tin nhắn');
      if (!silent) setLoadError(errMsg);
      if (!silent) message.error(errMsg);
    } finally {
      if (!silent) setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadMessages();
    const timer = window.setInterval(() => void loadMessages(true), FALLBACK_POLL_MS);
    return () => window.clearInterval(timer);
  }, [loadMessages]);

  useEffect(() => {
    if (!accessToken) return;
    const url = buildCustomerChatEventsUrl(accessToken);
    return subscribeChatSse(url, () => void loadMessages(true));
  }, [accessToken, loadMessages]);

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
      setChatConsentGranted(chatConsent?.granted ?? false);
      message.success('Đã bật chat dược sĩ — bạn có thể gửi tin nhắn');
    } catch (error) {
      message.error(getApiErrorMessage(error, 'Không bật được đồng ý chat'));
    } finally {
      setEnablingConsent(false);
    }
  };

  const onSend = async () => {
    const text = draft.trim();
    if (!text) return;
    setSending(true);
    try {
      const created = await sendChatMessage(text);
      setMessages((prev) => [...prev, created]);
      setDraft('');
      scrollToBottom();
    } catch (error) {
      message.error(getApiErrorMessage(error, 'Không gửi được tin nhắn'));
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="customer-chat-page">
      <div className="customer-chat-toolbar">
        <Typography.Title level={5} style={{ marginTop: 0, marginBottom: 4 }}>
          Chat dược sĩ
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 13 }}>
          Hỏi đáp trực tiếp với nhà thuốc.
        </Typography.Paragraph>

        {!chatConsentGranted ? (
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 8 }}
            message="Cần đồng ý chat dược sĩ trước khi gửi tin nhắn."
            action={
              <Space size={8} wrap>
                <Button size="small" type="primary" loading={enablingConsent} onClick={() => void onEnableChatConsent()}>
                  Bật ngay
                </Button>
                <Link to="/profile" style={{ whiteSpace: 'nowrap' }}>
                  Tài khoản
                </Link>
              </Space>
            }
          />
        ) : null}

        {loadError ? (
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 8 }}
            message="Không tải được chat"
            description={loadError}
            action={
              <Button size="small" onClick={() => void loadMessages()}>
                Thử lại
              </Button>
            }
          />
        ) : null}
      </div>

      <div ref={listRef} className="customer-chat-messages">
        {loading && messages.length === 0 ? (
          <div style={{ textAlign: 'center', padding: 48 }}>
            <Spin />
          </div>
        ) : messages.length === 0 ? (
          <Typography.Text type="secondary">
            Chưa có tin nhắn. Gửi câu hỏi đầu tiên cho dược sĩ nhé.
          </Typography.Text>
        ) : (
          messages.map((item) => (
            <ChatBubble key={item.id} item={item} isMine={item.senderType !== STAFF_SENDER} />
          ))
        )}
        {hasMore ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            (Cuộc trò chuyện dài — hiển thị tin gần nhất)
          </Typography.Text>
        ) : null}
        <div ref={bottomRef} />
      </div>

      <div className="customer-chat-composer">
        <Input.TextArea
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          placeholder={chatConsentGranted ? 'Nhập câu hỏi...' : 'Cần đồng ý chat trước'}
          autoSize={{ minRows: 1, maxRows: 4 }}
          disabled={!chatConsentGranted || sending}
          onPressEnter={(e) => {
            if (!e.shiftKey) {
              e.preventDefault();
              void onSend();
            }
          }}
        />
        <Button
          type="primary"
          icon={<SendOutlined />}
          loading={sending}
          disabled={!chatConsentGranted || !draft.trim()}
          onClick={() => void onSend()}
          style={{ alignSelf: 'flex-end' }}
        />
      </div>
    </div>
  );
}
