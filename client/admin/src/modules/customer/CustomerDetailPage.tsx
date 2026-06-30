import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Button,
  Card,
  Descriptions,
  Space,
  Spin,
  Tabs,
  Tag,
  Typography,
  message,
} from 'antd';
import {
  ArrowLeftOutlined,
  CommentOutlined,
  EditOutlined,
  FormOutlined,
  MedicineBoxOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { fetchCustomer } from '@/shared/api/customer-admin.api';
import type { CustomerDetail } from '@/shared/api/customer-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { isProductFeatureEnabled } from '@/shared/product/product-phases';
import { CustomerConsentPanel } from '@/modules/customer/CustomerConsentPanel';
import { CustomerFormDrawer } from '@/modules/customer/CustomerFormDrawer';
import { CustomerLoyaltyPanel } from '@/modules/customer/CustomerLoyaltyPanel';
import { CustomerOrdersPanel } from '@/modules/customer/CustomerOrdersPanel';
import { useCustomerEnums } from '@/shared/i18n/use-customer-enums';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

type DetailTab = 'profile' | 'consents' | 'loyalty' | 'orders';

export function CustomerDetailPage() {
  const { t } = useTranslation('customer', { keyPrefix: 'detailPage' });
  const { t: tc } = useTranslation('common');
  const { customerStatusLabel, customerGenderLabel } = useCustomerEnums();
  const { customerId = '' } = useParams();
  const navigate = useNavigate();
  const canWrite = useHasPermission('sales.write');
  const showReservations = isProductFeatureEnabled('sales.customerReservations');
  const showChat = isProductFeatureEnabled('sales.chat');
  const [detail, setDetail] = useState<CustomerDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<DetailTab>('profile');
  const [drawerOpen, setDrawerOpen] = useState(false);

  const load = useCallback(async () => {
    if (!customerId) return;
    setLoading(true);
    try {
      setDetail(await fetchCustomer(customerId));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
      navigate('/customer/list');
    } finally {
      setLoading(false);
    }
  }, [customerId, navigate, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const tabItems = useMemo(
    () => [
      {
        key: 'profile' as const,
        label: t('tabs.profile'),
        children: detail ? (
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Descriptions bordered size="small" column={1}>
              <Descriptions.Item label={t('fields.customerCode')}>{detail.customerCode}</Descriptions.Item>
              <Descriptions.Item label={t('fields.fullName')}>{detail.fullName}</Descriptions.Item>
              <Descriptions.Item label={t('fields.phone')}>{detail.phone}</Descriptions.Item>
              <Descriptions.Item label={t('fields.email')}>{detail.email ?? '—'}</Descriptions.Item>
              <Descriptions.Item label={t('fields.dateOfBirth')}>
                {detail.dateOfBirth ? formatDisplayDate(detail.dateOfBirth) : '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('fields.gender')}>
                {detail.gender != null ? customerGenderLabel(detail.gender) : '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('fields.allowCredit')}>
                {detail.allowCredit ? tc('actions.yes') : tc('actions.no')}
              </Descriptions.Item>
              {detail.allowCredit ? (
                <Descriptions.Item label={t('fields.creditLimit')}>
                  {detail.creditLimit != null && detail.creditLimit > 0
                    ? formatDisplayMoney(detail.creditLimit)
                    : t('fields.unlimitedCredit')}
                </Descriptions.Item>
              ) : null}
              <Descriptions.Item label={t('fields.createdAt')}>
                {formatDisplayDate(detail.createdAt)}
              </Descriptions.Item>
              <Descriptions.Item label={t('fields.appAccount')}>
                {detail.hasAppAccount
                  ? t('appAccount.registered', {
                      lastLogin: detail.appLastLoginAt
                        ? dayjs(detail.appLastLoginAt).format('DD/MM/YYYY HH:mm')
                        : '—',
                    })
                  : t('appAccount.notRegistered')}
              </Descriptions.Item>
            </Descriptions>

            <Card size="small" title={t('appActivity.title')}>
              <Space wrap>
                <Link to="/sales/customer-drafts">
                  <Button icon={<FormOutlined />}>{t('appActivity.draftOrders')}</Button>
                </Link>
                {showReservations && (
                  <Link to="/sales/customer-reservations">
                    <Button icon={<MedicineBoxOutlined />}>{t('appActivity.reservations')}</Button>
                  </Link>
                )}
                {showChat && (
                  <Link to="/sales/chat">
                    <Button icon={<CommentOutlined />}>{t('appActivity.chat')}</Button>
                  </Link>
                )}
              </Space>
            </Card>
          </Space>
        ) : null,
      },
      {
        key: 'consents' as const,
        label: t('tabs.consents'),
        children: detail ? <CustomerConsentPanel customerId={detail.id} /> : null,
      },
      {
        key: 'loyalty' as const,
        label: t('tabs.loyalty'),
        children: detail ? <CustomerLoyaltyPanel customerId={detail.id} /> : null,
      },
      {
        key: 'orders' as const,
        label: t('tabs.orders'),
        children: detail ? <CustomerOrdersPanel customerId={detail.id} /> : null,
      },
    ],
    [customerGenderLabel, detail, showChat, showReservations, t, tc],
  );

  if (loading || !detail) {
    return (
      <div style={{ padding: 48, textAlign: 'center' }}>
        <Spin tip={t('loadingTip')} />
      </div>
    );
  }

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Space wrap>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/customer/list')}>
          {t('backToList')}
        </Button>
        <Button icon={<ReloadOutlined />} onClick={() => void load()}>
          {tc('actions.reload')}
        </Button>
        {canWrite && detail ? (
          <Button icon={<EditOutlined />} onClick={() => setDrawerOpen(true)}>
            {tc('actions.edit')}
          </Button>
        ) : null}
      </Space>

      <Card size="small">
        <Space direction="vertical" size={4}>
          <Typography.Title level={4} style={{ margin: 0 }}>
            {detail.fullName}
          </Typography.Title>
          <Typography.Text type="secondary">
            {detail.customerCode} · {detail.phone}
            {detail.email ? ` · ${detail.email}` : ''}
          </Typography.Text>
          <Space wrap>
            <Tag color={detail.status === 1 ? 'green' : 'default'}>
              {customerStatusLabel(detail.status)}
            </Tag>
            {detail.hasAppAccount ? (
              <Tag color={detail.appVerified ? 'blue' : 'gold'}>
                {detail.appVerified ? t('appAccount.verified') : t('appAccount.unverified')}
              </Tag>
            ) : (
              <Tag>{t('appAccount.none')}</Tag>
            )}
          </Space>
        </Space>
      </Card>

      <Tabs activeKey={activeTab} onChange={(key) => setActiveTab(key as DetailTab)} items={tabItems} />

      {detail ? (
        <CustomerFormDrawer
          open={drawerOpen}
          editing={detail}
          onClose={() => setDrawerOpen(false)}
          onSaved={(customer) => setDetail(customer)}
        />
      ) : null}
    </Space>
  );
}
