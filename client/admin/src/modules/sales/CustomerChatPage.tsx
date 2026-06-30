import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Badge, Button, Card, Empty, Input, List, Space, Spin, Typography, Alert, message } from 'antd';
import { SendOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  fetchChatThreads,
  fetchCustomerChatMessages,
  markStaffChatRead,
  sendStaffChatMessage,
  type AdminChatMessage,
  type AdminChatThread,
} from '@/shared/api/chat.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { CustomerDraftOrderDrawer } from '@/modules/sales/CustomerDraftOrderDrawer';
import { useAuthStore } from '@/shared/auth/auth.store';
import { buildAdminChatEventsUrl, subscribeChatSse } from '@/shared/utils/chat-sse';

const CUSTOMER_SENDER = 1;
const FALLBACK_POLL_MS = 30_000;
/** Viewport trừ header app, tab sales, card title, margin content */
const CHAT_PANEL_HEIGHT = 'calc(100vh - 228px)';

function MessageBubble({ item }: { item: AdminChatMessage }) {
  const isStaff = item.senderType !== CUSTOMER_SENDER;
  return (
    <div style={{ display: 'flex', justifyContent: isStaff ? 'flex-end' : 'flex-start', marginBottom: 8 }}>
      <div
        style={{
          maxWidth: '75%',
          padding: '8px 12px',
          borderRadius: 10,
          background: isStaff ? '#1677ff' : '#f5f5f5',
          color: isStaff ? '#fff' : '#000',
        }}
      >
        <div style={{ whiteSpace: 'pre-wrap' }}>{item.body}</div>
        <Typography.Text
          style={{ fontSize: 11, color: isStaff ? 'rgba(255,255,255,0.75)' : '#888' }}
        >
          {dayjs(item.createdAt).format('DD/MM HH:mm')}
        </Typography.Text>
      </div>
    </div>
  );
}

export function CustomerChatPage() {
  const { t } = useTranslation('sales', { keyPrefix: 'customerChat' });
  const accessToken = useAuthStore((s) => s.accessToken);
  const [threads, setThreads] = useState<AdminChatThread[]>([]);
  const [selectedId, setSelectedId] = useState<string>();
  const [messages, setMessages] = useState<AdminChatMessage[]>([]);
  const [loadingThreads, setLoadingThreads] = useState(true);
  const [threadsError, setThreadsError] = useState<string | null>(null);
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [sending, setSending] = useState(false);
  const [draft, setDraft] = useState('');
  const [draftDrawerOpen, setDraftDrawerOpen] = useState(false);
  const bottomRef = useRef<HTMLDivElement | null>(null);

  const selected = threads.find((thread) => thread.customerId === selectedId);

  const loadThreads = useCallback(async (silent = false) => {
    if (!silent) setLoadingThreads(true);
    setThreadsError(null);
    try {
      const data = await fetchChatThreads();
      setThreads(data);
      if (!selectedId && data.length > 0) {
        setSelectedId(data[0].customerId);
      }
    } catch (error) {
      const errMsg = apiErrorMessage(error, t('loadFailed'));
      setThreadsError(errMsg);
      if (!silent) message.error(errMsg);
    } finally {
      if (!silent) setLoadingThreads(false);
    }
  }, [selectedId, t]);

  const loadMessages = useCallback(
    async (customerId: string, silent = false) => {
      if (!silent) setLoadingMessages(true);
      try {
        const page = await fetchCustomerChatMessages(customerId);
        setMessages(page.items);
        await markStaffChatRead(customerId).catch(() => undefined);
        void loadThreads(true);
      } catch (error) {
        if (!silent) message.error(apiErrorMessage(error, t('messagesLoadFailed')));
      } finally {
        if (!silent) setLoadingMessages(false);
      }
    },
    [loadThreads, t],
  );

  useEffect(() => {
    void loadThreads();
    const timer = window.setInterval(() => void loadThreads(true), FALLBACK_POLL_MS);
    return () => window.clearInterval(timer);
  }, [loadThreads]);

  useEffect(() => {
    if (!selectedId) return;
    void loadMessages(selectedId);
    const timer = window.setInterval(() => void loadMessages(selectedId, true), FALLBACK_POLL_MS);
    return () => window.clearInterval(timer);
  }, [selectedId, loadMessages]);

  useEffect(() => {
    if (!accessToken) return;
    const url = buildAdminChatEventsUrl(accessToken);
    return subscribeChatSse(url, () => {
      void loadThreads(true);
      if (selectedId) void loadMessages(selectedId, true);
    });
  }, [accessToken, selectedId, loadThreads, loadMessages]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages.length, selectedId]);

  const onSend = async () => {
    if (!selectedId) return;
    const text = draft.trim();
    if (!text) return;
    setSending(true);
    try {
      const created = await sendStaffChatMessage(selectedId, text);
      setMessages((prev) => [...prev, created]);
      setDraft('');
      void loadThreads(true);
    } catch (error) {
      message.error(apiErrorMessage(error, t('sendFailed')));
    } finally {
      setSending(false);
    }
  };

  return (
    <Card
      title={t('title')}
      styles={{
        body: {
          padding: 0,
          height: CHAT_PANEL_HEIGHT,
          overflow: 'hidden',
          display: 'flex',
          flexDirection: 'column',
        },
      }}
    >
      {threadsError ? (
        <Alert
          type="warning"
          showIcon
          style={{ margin: '8px 12px 0', flexShrink: 0 }}
          message={t('loadFailedBanner')}
          description={threadsError}
          action={
            <Button size="small" onClick={() => void loadThreads()}>
              {t('retry')}
            </Button>
          }
        />
      ) : null}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: '280px 1fr',
          flex: 1,
          minHeight: 0,
          overflow: 'hidden',
        }}
      >
        <div
          style={{
            borderRight: '1px solid #f0f0f0',
            minHeight: 0,
            overflowY: 'auto',
            overflowX: 'hidden',
          }}
        >
          {loadingThreads ? (
            <div style={{ padding: 24, textAlign: 'center' }}>
              <Spin />
            </div>
          ) : threadsError ? (
            <Typography.Text type="secondary" style={{ display: 'block', padding: 24, textAlign: 'center' }}>
              {t('sidebarHint')}
            </Typography.Text>
          ) : threads.length === 0 ? (
            <Empty description={t('noThreads')} style={{ margin: 24 }} />
          ) : (
            <List
              dataSource={threads}
              renderItem={(item) => (
                <List.Item
                  style={{
                    cursor: 'pointer',
                    background: item.customerId === selectedId ? '#e6f4ff' : undefined,
                    padding: '12px 16px',
                  }}
                  onClick={() => setSelectedId(item.customerId)}
                >
                  <List.Item.Meta
                    title={
                      <Space>
                        <span>{item.customerName}</span>
                        {item.staffUnreadCount > 0 ? (
                          <Badge count={item.staffUnreadCount} size="small" />
                        ) : null}
                      </Space>
                    }
                    description={
                      <Typography.Text type="secondary" ellipsis style={{ maxWidth: 220 }}>
                        {item.lastMessagePreview ?? item.customerPhone ?? item.customerCode}
                      </Typography.Text>
                    }
                  />
                </List.Item>
              )}
            />
          )}
        </div>

        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            minHeight: 0,
            overflow: 'hidden',
          }}
        >
          {selected ? (
            <>
              <div
                style={{
                  padding: '10px 16px',
                  borderBottom: '1px solid #f0f0f0',
                  flexShrink: 0,
                }}
              >
                <Space wrap>
                  <Typography.Text strong>{selected.customerName}</Typography.Text>
                  <Typography.Text type="secondary">
                    {selected.customerPhone ?? selected.customerCode}
                  </Typography.Text>
                  <Button size="small" onClick={() => setDraftDrawerOpen(true)}>
                    {t('draftOrders')}
                  </Button>
                </Space>
              </div>
              <div
                style={{
                  flex: 1,
                  minHeight: 0,
                  overflowY: 'auto',
                  overflowX: 'hidden',
                  padding: 16,
                }}
              >
                {loadingMessages && messages.length === 0 ? (
                  <Spin />
                ) : messages.length === 0 ? (
                  <Empty description={t('noMessages')} />
                ) : (
                  messages.map((item) => <MessageBubble key={item.id} item={item} />)
                )}
                <div ref={bottomRef} />
              </div>
              <div
                style={{
                  padding: 12,
                  borderTop: '1px solid #f0f0f0',
                  display: 'flex',
                  gap: 8,
                  flexShrink: 0,
                }}
              >
                <Input.TextArea
                  value={draft}
                  onChange={(e) => setDraft(e.target.value)}
                  placeholder={t('replyPlaceholder')}
                  autoSize={{ minRows: 1, maxRows: 3 }}
                  onPressEnter={(e) => {
                    if (!e.shiftKey) {
                      e.preventDefault();
                      void onSend();
                    }
                  }}
                />
                <Button type="primary" icon={<SendOutlined />} loading={sending} onClick={() => void onSend()}>
                  {t('send')}
                </Button>
              </div>
            </>
          ) : (
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                flex: 1,
                minHeight: 0,
              }}
            >
              <Empty description={t('selectCustomer')} />
            </div>
          )}
        </div>
      </div>
      <CustomerDraftOrderDrawer
        open={draftDrawerOpen}
        customerId={selectedId}
        customerName={selected?.customerName}
        onClose={() => setDraftDrawerOpen(false)}
      />
    </Card>
  );
}
