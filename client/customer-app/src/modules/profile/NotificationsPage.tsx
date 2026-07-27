import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Spin, message } from 'antd';
import {
  BellOutlined,
  CalendarOutlined,
  CheckCircleOutlined,
  MedicineBoxOutlined,
  RightOutlined,
  SafetyCertificateOutlined,
  ShoppingCartOutlined,
  TeamOutlined,
  LockOutlined,
} from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import {
  fetchServerNotifications,
  getApiErrorMessage,
  markAllServerNotificationsRead,
  markServerNotificationRead,
} from '@/shared/api/customer-app.api';
import type { ServerNotification } from '@/shared/api/customer-app.types';
import { useCustomerLabels } from '@/shared/i18n/useCustomerLabels';
import { notifyServerNotificationsChanged } from '@/shared/hooks/useCustomerNotificationCount';
import {
  listCustomerNotifications,
  markAllCustomerNotificationsRead,
  markCustomerNotificationRead,
  subscribeCustomerNotifications,
  type CustomerNotification,
} from '@/shared/notifications/customer-notifications';
import './NotificationsPage.css';

type DisplayNotification = {
  id: string;
  source: 'server' | 'local';
  category?: string;
  title: string;
  body: string;
  href?: string | null;
  createdAt: string;
  read: boolean;
};

function toDisplayFromServer(item: ServerNotification): DisplayNotification {
  return {
    id: item.id,
    source: 'server',
    category: item.category,
    title: item.title,
    body: item.body,
    href: item.href,
    createdAt: item.createdAt,
    read: item.isRead,
  };
}

function toDisplayFromLocal(item: CustomerNotification): DisplayNotification {
  return {
    id: item.id,
    source: 'local',
    category: item.kind === 'draft_order' ? 'order' : item.kind,
    title: item.title,
    body: item.body,
    href: item.href,
    createdAt: item.createdAt,
    read: item.read,
  };
}

function isAlertNotification(item: DisplayNotification) {
  const haystack = `${item.title} ${item.body}`.toLowerCase();
  return (
    haystack.includes('missed') ||
    haystack.includes('bỏ liều') ||
    haystack.includes('quên') ||
    haystack.includes('no doses')
  );
}

function iconMeta(item: DisplayNotification): {
  icon: ReactNode;
  tone: 'default' | 'alert' | 'care' | 'order';
  alertDot?: boolean;
} {
  if (isAlertNotification(item)) {
    return { icon: <BellOutlined />, tone: 'alert', alertDot: true };
  }
  switch (item.category) {
    case 'medication':
      return { icon: <MedicineBoxOutlined />, tone: 'default' };
    case 'care':
      return { icon: <CalendarOutlined />, tone: 'care' };
    case 'order':
      return { icon: <ShoppingCartOutlined />, tone: 'order' };
    case 'family':
      return { icon: <TeamOutlined />, tone: 'care' };
    default:
      return { icon: <BellOutlined />, tone: 'default' };
  }
}

export function NotificationsPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { notificationCategory } = useCustomerLabels();
  const [loading, setLoading] = useState(true);
  const [markingAll, setMarkingAll] = useState(false);
  const [serverItems, setServerItems] = useState<ServerNotification[]>([]);
  const [serverUnreadTotal, setServerUnreadTotal] = useState(0);
  const [localItems, setLocalItems] = useState<CustomerNotification[]>(() => listCustomerNotifications());

  const loadServer = useCallback(async () => {
    setLoading(true);
    try {
      const result = await fetchServerNotifications(50);
      setServerItems(result.items);
      setServerUnreadTotal(result.unreadCount);
    } catch (error) {
      message.error(getApiErrorMessage(error, t('notifications.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadServer();
  }, [loadServer]);

  useEffect(() => {
    const refreshLocal = () => setLocalItems(listCustomerNotifications());
    return subscribeCustomerNotifications(refreshLocal);
  }, []);

  const items = useMemo(() => {
    const server = serverItems.map(toDisplayFromServer);
    const serverKeys = new Set(server.map((item) => `${item.title}|${item.body}`));
    const local = localItems
      .filter((item) => !serverKeys.has(`${item.title}|${item.body}`))
      .map(toDisplayFromLocal);
    return [...server, ...local].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  }, [localItems, serverItems]);

  const localOnlyUnread = useMemo(() => {
    const serverKeys = new Set(serverItems.map((item) => `${item.title}|${item.body}`));
    return localItems.filter(
      (item) => !item.read && !serverKeys.has(`${item.title}|${item.body}`),
    ).length;
  }, [localItems, serverItems]);

  const unreadCount = serverUnreadTotal + localOnlyUnread;

  const openNotification = async (item: DisplayNotification) => {
    try {
      if (item.source === 'server' && !item.read) {
        setServerItems((prev) =>
          prev.map((row) =>
            row.id === item.id ? { ...row, isRead: true, readAt: new Date().toISOString() } : row,
          ),
        );
        setServerUnreadTotal((count) => Math.max(0, count - 1));
        await markServerNotificationRead(item.id);
        notifyServerNotificationsChanged();
      } else if (item.source === 'local' && !item.read) {
        markCustomerNotificationRead(item.id);
      }
    } catch (error) {
      message.error(getApiErrorMessage(error, t('notifications.markReadFailed')));
      if (item.source === 'server') {
        void loadServer();
      }
      return;
    }
    if (item.href) {
      navigate(item.href);
    }
  };

  const markAllRead = async () => {
    setMarkingAll(true);
    const previousItems = serverItems;
    const previousUnread = serverUnreadTotal;
    setServerItems((prev) =>
      prev.map((row) => ({ ...row, isRead: true, readAt: row.readAt ?? new Date().toISOString() })),
    );
    setServerUnreadTotal(0);
    markAllCustomerNotificationsRead();
    try {
      await markAllServerNotificationsRead();
      notifyServerNotificationsChanged();
    } catch (error) {
      setServerItems(previousItems);
      setServerUnreadTotal(previousUnread);
      message.error(getApiErrorMessage(error, t('notifications.markAllReadFailed')));
    } finally {
      setMarkingAll(false);
    }
  };

  return (
    <div className="notif-page">
      <header className="notif-hero">
        <div className="notif-hero-copy">
          <h1 className="notif-hero-title">{t('notifications.title')}</h1>
          <p className="notif-hero-intro">{t('notifications.subtitle')}</p>
        </div>
        <div className="notif-hero-bell" aria-hidden>
          <BellOutlined />
          {unreadCount > 0 ? (
            <span className="notif-hero-count">{unreadCount > 99 ? '99+' : unreadCount}</span>
          ) : null}
        </div>
      </header>

      {unreadCount > 0 ? (
        <button
          type="button"
          className="notif-mark-all"
          disabled={markingAll}
          onClick={() => void markAllRead()}
        >
          <CheckCircleOutlined />
          {t('notifications.markAllRead')}
        </button>
      ) : null}

      {loading ? (
        <div className="notif-loading">
          <Spin />
        </div>
      ) : items.length === 0 ? (
        <div className="notif-empty">
          <BellOutlined />
          {t('notifications.empty')}
        </div>
      ) : (
        <div className="notif-list">
          {items.map((item) => {
            const clickable = Boolean(item.href);
            const meta = iconMeta(item);
            const className = [
              'notif-card',
              item.read ? 'notif-card--read' : 'notif-card--unread',
              clickable ? 'notif-card--clickable' : '',
            ]
              .filter(Boolean)
              .join(' ');
            const iconTone = meta.tone === 'default' ? '' : ` notif-card-icon--${meta.tone}`;

            return (
              <article
                key={`${item.source}-${item.id}`}
                className={className}
                role={clickable ? 'button' : undefined}
                tabIndex={clickable ? 0 : undefined}
                onClick={() => void openNotification(item)}
                onKeyDown={(event) => {
                  if (!clickable) return;
                  if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    void openNotification(item);
                  }
                }}
              >
                <span className={`notif-card-icon${iconTone}`} aria-hidden>
                  {meta.icon}
                  {meta.alertDot ? <span className="notif-card-alert-dot" /> : null}
                </span>

                <div className="notif-card-main">
                  <div className="notif-card-top">
                    <h2 className="notif-card-title">{item.title}</h2>
                    <div className="notif-card-badges">
                      {!item.read ? (
                        <span className="notif-badge notif-badge--new">{t('common.new')}</span>
                      ) : null}
                      {item.category ? (
                        <span className="notif-badge notif-badge--cat">
                          {notificationCategory(item.category)}
                        </span>
                      ) : null}
                    </div>
                  </div>
                  <p className="notif-card-body">{item.body}</p>
                  <p className="notif-card-time">
                    <CalendarOutlined />
                    {dayjs(item.createdAt).format('DD/MM/YYYY HH:mm')}
                  </p>
                </div>

                {clickable ? <RightOutlined className="notif-card-chevron" /> : null}
              </article>
            );
          })}
        </div>
      )}

      <div className="notif-privacy">
        <span className="notif-privacy-icon" aria-hidden>
          <SafetyCertificateOutlined />
        </span>
        <div className="notif-privacy-copy">
          <div className="notif-privacy-title">{t('notifications.privacyTitle')}</div>
          <div className="notif-privacy-sub">{t('notifications.privacySub')}</div>
        </div>
        <LockOutlined className="notif-privacy-lock" aria-hidden />
      </div>
    </div>
  );
}
