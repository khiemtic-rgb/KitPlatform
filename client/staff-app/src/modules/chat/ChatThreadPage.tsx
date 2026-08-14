import { useCallback, useEffect, useRef, useState } from 'react';
import { App, Button, Input, Spin, Typography } from 'antd';
import { PhoneOutlined, SendOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useParams } from 'react-router-dom';
import {
  fetchChatMessages,
  fetchChatThreads,
  markChatRead,
  sendChatMessage,
} from '@/shared/api/chat.api';
import { CHAT_SENDER_CUSTOMER, type ChatMessage } from '@/shared/api/chat.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useAuthStore } from '@/shared/auth/auth.store';
import { buildChatEventsUrl, subscribeChatSse } from '@/shared/utils/chat-sse';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

function digitsOnly(value: string): string {
  return value.replace(/\D/g, '');
}

function hasUsablePhone(phone?: string | null): boolean {
  const d = digitsOnly(phone ?? '');
  return d.length >= 9 && d.length <= 12;
}

function MessageBubble({ item }: { item: ChatMessage }) {
  const isStaff = item.senderType !== CHAT_SENDER_CUSTOMER;
  return (
    <div className={`chat-bubble-row ${isStaff ? 'staff' : 'customer'}`}>
      <div className={`chat-bubble ${isStaff ? 'staff' : 'customer'}`}>
        <div style={{ whiteSpace: 'pre-wrap' }}>{item.body}</div>
        <Typography.Text className="chat-bubble-time">
          {dayjs(item.createdAt).format('DD/MM HH:mm')}
        </Typography.Text>
      </div>
    </div>
  );
}

export function ChatThreadPage() {
  const { message } = App.useApp();
  const { customerId = '' } = useParams();
  const accessToken = useAuthStore((s) => s.accessToken);
  const [title, setTitle] = useState('Chat');
  const [subtitle, setSubtitle] = useState<string | undefined>();
  const [phone, setPhone] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [draft, setDraft] = useState('');
  const [sending, setSending] = useState(false);
  const bottomRef = useRef<HTMLDivElement | null>(null);
  const listRef = useRef<HTMLElement | null>(null);

  const loadMessages = useCallback(
    async (silent = false) => {
      if (!customerId) return;
      if (!silent) setLoading(true);
      try {
        const page = await fetchChatMessages(customerId);
        setMessages(page.items);
        await markChatRead(customerId).catch(() => undefined);
      } catch (error) {
        if (!silent) message.error(apiErrorMessage(error, 'Không tải được tin nhắn'));
      } finally {
        if (!silent) setLoading(false);
      }
    },
    [customerId, message],
  );

  useEffect(() => {
    void (async () => {
      try {
        const threads = await fetchChatThreads();
        const thread = threads.find((t) => t.customerId === customerId);
        if (thread) {
          setTitle(thread.customerName);
          setPhone(thread.customerPhone);
          setSubtitle(
            hasUsablePhone(thread.customerPhone)
              ? thread.customerPhone ?? undefined
              : thread.customerCode || 'Chưa có SĐT',
          );
        }
      } catch {
        /* ignore */
      }
    })();
  }, [customerId]);

  useEffect(() => {
    if (!customerId) return;
    void loadMessages();
  }, [customerId, loadMessages]);

  useEffect(() => {
    if (!accessToken || !customerId) return;
    return subscribeChatSse(buildChatEventsUrl(accessToken), () => void loadMessages(true));
  }, [accessToken, customerId, loadMessages]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const submit = async () => {
    const body = draft.trim();
    if (!body || !customerId) return;
    setSending(true);
    try {
      const sent = await sendChatMessage(customerId, body);
      setMessages((prev) => [...prev, sent]);
      setDraft('');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không gửi được tin nhắn'));
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="staff-shell chat-thread-shell">
      <StaffPageHeader
        title={title}
        subtitle={subtitle}
        backTo="/chat"
        right={
          hasUsablePhone(phone) ? (
            <Button
              type="text"
              className="chat-header-call"
              icon={<PhoneOutlined />}
              href={`tel:${digitsOnly(phone ?? '')}`}
              aria-label="Gọi khách"
            />
          ) : null
        }
      />
      <main
        className="staff-body chat-thread-body"
        ref={(node) => {
          listRef.current = node;
        }}
      >
        {loading ? (
          <div style={{ textAlign: 'center', padding: 16 }}>
            <Spin />
          </div>
        ) : null}
        {!loading && messages.length === 0 ? (
          <Typography.Text type="secondary" style={{ display: 'block', textAlign: 'center', marginTop: 24 }}>
            Chưa có tin nhắn — gửi tin đầu tiên cho khách.
          </Typography.Text>
        ) : null}
        {messages.map((item) => (
          <MessageBubble key={item.id} item={item} />
        ))}
        <div ref={bottomRef} />
      </main>
      <footer className="staff-footer chat-compose-footer">
        <div className="chat-compose-row">
          <Input.TextArea
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            placeholder="Nhập tin nhắn… (Enter gửi, Shift+Enter xuống dòng)"
            autoSize={{ minRows: 1, maxRows: 4 }}
            onPressEnter={(e) => {
              if (!e.shiftKey) {
                e.preventDefault();
                void submit();
              }
            }}
          />
          <Button
            type="primary"
            className="chat-compose-send"
            icon={<SendOutlined />}
            loading={sending}
            disabled={!draft.trim()}
            onClick={() => void submit()}
            aria-label="Gửi"
          />
        </div>
      </footer>
    </div>
  );
}
