import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { App, Alert, Badge, Button, Empty, Input, Segmented, Spin, Typography } from 'antd';
import { MessageOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import { fetchChatThreads, sumUnreadThreads } from '@/shared/api/chat.api';
import type { ChatThread } from '@/shared/api/chat.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useAuthStore } from '@/shared/auth/auth.store';
import { buildChatEventsUrl, subscribeChatSse } from '@/shared/utils/chat-sse';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

type ChatFilter = 'all' | 'unread';

function digitsOnly(value: string): string {
  return value.replace(/\D/g, '');
}

function hasUsablePhone(phone?: string | null): boolean {
  const d = digitsOnly(phone ?? '');
  return d.length >= 9 && d.length <= 12;
}

function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0] ?? ''}${parts[parts.length - 1][0] ?? ''}`.toUpperCase();
}

function formatChatTime(iso: string | null): string {
  if (!iso) return '';
  const d = dayjs(iso);
  if (!d.isValid()) return '';
  const now = dayjs();
  const mins = now.diff(d, 'minute');
  if (mins < 1) return 'Vừa xong';
  if (mins < 60) return `${mins} phút`;
  if (now.isSame(d, 'day')) return d.format('HH:mm');
  if (now.subtract(1, 'day').isSame(d, 'day')) return `Hôm qua ${d.format('HH:mm')}`;
  if (now.diff(d, 'day') < 7) return d.format('DD/MM HH:mm');
  return d.format('DD/MM/YY');
}

function sortThreads(items: ChatThread[]): ChatThread[] {
  return [...items].sort((a, b) => {
    const unreadDiff = (b.staffUnreadCount > 0 ? 1 : 0) - (a.staffUnreadCount > 0 ? 1 : 0);
    if (unreadDiff !== 0) return unreadDiff;
    const ta = a.lastMessageAt ? dayjs(a.lastMessageAt).valueOf() : 0;
    const tb = b.lastMessageAt ? dayjs(b.lastMessageAt).valueOf() : 0;
    if (tb !== ta) return tb - ta;
    return a.customerName.localeCompare(b.customerName, 'vi');
  });
}

export function ChatListPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const accessToken = useAuthStore((s) => s.accessToken);
  const [threads, setThreads] = useState<ChatThread[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState<ChatFilter>('all');
  const requestIdRef = useRef(0);

  const load = useCallback(
    async (mode: 'full' | 'silent' | 'refresh' = 'full') => {
      const requestId = ++requestIdRef.current;
      if (mode === 'full') {
        setLoading(true);
        setLoadError(null);
      }
      if (mode === 'refresh') setRefreshing(true);
      try {
        const rows = await fetchChatThreads();
        if (requestId !== requestIdRef.current) return;
        setThreads(rows);
        setLoadError(null);
      } catch (error) {
        if (requestId !== requestIdRef.current) return;
        const text = apiErrorMessage(error, 'Không tải được hội thoại');
        if (mode === 'full') {
          setThreads([]);
          setLoadError(text);
        } else if (mode === 'refresh') {
          message.error(text);
        }
      } finally {
        if (requestId !== requestIdRef.current) return;
        if (mode === 'full') setLoading(false);
        if (mode === 'refresh') setRefreshing(false);
      }
    },
    [message],
  );

  useEffect(() => {
    void load('full');
  }, [load]);

  useEffect(() => {
    if (!accessToken) return;
    return subscribeChatSse(buildChatEventsUrl(accessToken), () => void load('silent'));
  }, [accessToken, load]);

  const unreadTotal = useMemo(() => sumUnreadThreads(threads), [threads]);

  const visible = useMemo(() => {
    const q = query.trim().toLowerCase();
    const qDigits = digitsOnly(query);
    let rows = sortThreads(threads);
    if (filter === 'unread') {
      rows = rows.filter((t) => t.staffUnreadCount > 0);
    }
    if (q || qDigits.length >= 3) {
      rows = rows.filter((t) => {
        const name = t.customerName.toLowerCase();
        const code = t.customerCode.toLowerCase();
        const phone = (t.customerPhone ?? '').toLowerCase();
        const phoneDigits = digitsOnly(t.customerPhone ?? '');
        return (
          name.includes(q) ||
          code.includes(q) ||
          phone.includes(q) ||
          (qDigits.length >= 3 && phoneDigits.includes(qDigits))
        );
      });
    }
    return rows;
  }, [threads, query, filter]);

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Chat khách"
        subtitle={
          loadError
            ? 'Không tải được danh sách'
            : unreadTotal > 0
              ? `${unreadTotal} tin chưa đọc · ${threads.length} hội thoại`
              : `${threads.length} hội thoại`
        }
        backTo="/"
        right={
          <Button
            type="text"
            className="chat-header-refresh"
            icon={<ReloadOutlined spin={refreshing || loading} />}
            aria-label="Tải lại"
            onClick={() => void load(loadError ? 'full' : 'refresh')}
          />
        }
      />
      <main className="staff-body">
        {loadError ? (
          <Alert
            type="error"
            showIcon
            message="Không tải được hội thoại"
            description={loadError}
            action={
              <Button size="small" type="primary" loading={loading} onClick={() => void load('full')}>
                Thử lại
              </Button>
            }
            style={{ marginBottom: 12 }}
          />
        ) : null}

        <Input
          size="large"
          allowClear
          prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
          placeholder="Tìm tên, SĐT, mã khách…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          disabled={Boolean(loadError) && threads.length === 0}
        />
        <div className="chat-list-toolbar">
          <Segmented
            size="middle"
            value={filter}
            onChange={(v) => setFilter(v as ChatFilter)}
            disabled={Boolean(loadError) && threads.length === 0}
            options={[
              { label: `Tất cả (${threads.length})`, value: 'all' },
              {
                label: unreadTotal > 0 ? `Chưa đọc (${unreadTotal})` : 'Chưa đọc',
                value: 'unread',
              },
            ]}
          />
        </div>

        {loading ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : loadError && threads.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description="Kiểm tra mạng / quyền chi nhánh rồi bấm Thử lại"
          />
        ) : visible.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={
              filter === 'unread'
                ? 'Không có hội thoại chưa đọc'
                : query.trim()
                  ? 'Không tìm thấy hội thoại khớp'
                  : 'Chưa có tin nhắn từ app khách'
            }
          />
        ) : (
          visible.map((thread) => {
            const unread = thread.staffUnreadCount > 0;
            const phoneLabel = hasUsablePhone(thread.customerPhone)
              ? thread.customerPhone
              : thread.customerCode || 'Chưa có SĐT';
            return (
              <button
                key={thread.customerId}
                type="button"
                className={`chat-thread-hit${unread ? ' is-unread' : ''}`}
                onClick={() => navigate(`/chat/${thread.customerId}`)}
              >
                <span className={`chat-thread-avatar${unread ? ' is-unread' : ''}`}>
                  {initialsOf(thread.customerName)}
                </span>
                <span className="chat-thread-main">
                  <span className="chat-thread-top">
                    <Typography.Text strong className="chat-thread-name" ellipsis>
                      {thread.customerName}
                    </Typography.Text>
                    <span className="chat-thread-time">
                      {formatChatTime(thread.lastMessageAt) || '—'}
                    </span>
                  </span>
                  <span className="chat-thread-phone">
                    {phoneLabel}
                    {!hasUsablePhone(thread.customerPhone) ? (
                      <span className="chat-thread-phone-warn"> · thiếu SĐT</span>
                    ) : null}
                  </span>
                  <span className="chat-thread-preview">
                    <MessageOutlined className="chat-thread-preview-icon" />
                    <Typography.Text
                      type="secondary"
                      ellipsis
                      className={unread ? 'chat-thread-preview-text is-unread' : 'chat-thread-preview-text'}
                    >
                      {thread.lastMessagePreview?.trim() || 'Chưa có tin nhắn'}
                    </Typography.Text>
                    {unread ? <Badge count={thread.staffUnreadCount} size="small" /> : null}
                  </span>
                </span>
              </button>
            );
          })
        )}
      </main>
    </div>
  );
}
