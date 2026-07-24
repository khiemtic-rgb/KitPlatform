import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Button, Card, Input, Popconfirm, Radio, Space, Table, Tabs, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined, PlusOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import {
  fetchCustomers,
  fetchCustomer,
  fetchSimilarCustomerClusters,
  mergeCustomers,
} from '@/shared/api/customer-admin.api';
import type {
  CustomerAdminListItem,
  CustomerDetail,
  SimilarCustomerCluster,
} from '@/shared/api/customer-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useCanSalesCustomers } from '@/shared/auth/usePermission';
import { CustomerFormDrawer } from '@/modules/customer/CustomerFormDrawer';
import { CustomerImportCard } from '@/modules/customer/CustomerImportCard';
import { useCustomerEnums } from '@/shared/i18n/use-customer-enums';
import { formatDisplayDate } from '@/shared/utils/date';

type ListTab = 'list' | 'similar';

type SimilarRow = {
  key: string;
  clusterKey: string;
  matchKind: string;
  displayLabel: string;
  maxSimilarity?: number | null;
  id: string;
  customerCode: string;
  fullName: string;
  phone: string;
  email?: string;
  status: number;
  createdAt: string;
  orderCount: number;
  isFirstInCluster: boolean;
  clusterSize: number;
};

function suggestKeeperId(cluster: SimilarCustomerCluster): string {
  const sorted = [...cluster.customers].sort((a, b) => {
    const orderDiff = (b.orderCount ?? 0) - (a.orderCount ?? 0);
    if (orderDiff !== 0) return orderDiff;
    return String(a.createdAt).localeCompare(String(b.createdAt));
  });
  return sorted[0]?.id ?? cluster.customers[0]?.id ?? '';
}

function toSimilarRows(clusters: SimilarCustomerCluster[]): SimilarRow[] {
  const rows: SimilarRow[] = [];
  for (const cluster of clusters) {
    cluster.customers.forEach((c, index) => {
      rows.push({
        key: `${cluster.clusterKey}:${c.id}`,
        clusterKey: cluster.clusterKey,
        matchKind: cluster.matchKind,
        displayLabel: cluster.displayLabel,
        maxSimilarity: cluster.maxSimilarity,
        id: c.id,
        customerCode: c.customerCode,
        fullName: c.fullName,
        phone: c.phone,
        email: c.email,
        status: c.status,
        createdAt: c.createdAt,
        orderCount: c.orderCount ?? 0,
        isFirstInCluster: index === 0,
        clusterSize: cluster.customers.length,
      });
    });
  }
  return rows;
}

export function CustomerListPage() {
  const { t } = useTranslation('customer', { keyPrefix: 'listPage' });
  const { t: tc } = useTranslation('common');
  const { customerStatusLabel } = useCustomerEnums();
  const navigate = useNavigate();
  const canWrite = useCanSalesCustomers();
  const [activeTab, setActiveTab] = useState<ListTab>('list');
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<CustomerAdminListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<CustomerDetail | null>(null);

  const [similarLoading, setSimilarLoading] = useState(false);
  const [similarClusters, setSimilarClusters] = useState<SimilarCustomerCluster[]>([]);
  const [similarClusterCount, setSimilarClusterCount] = useState(0);
  const [similarCustomerCount, setSimilarCustomerCount] = useState(0);
  const [keepers, setKeepers] = useState<Record<string, string>>({});
  const [mergingId, setMergingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await fetchCustomers({ search: search || undefined, page, pageSize });
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      setItems([]);
      setTotal(0);
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, search, t]);

  const loadSimilar = useCallback(async () => {
    setSimilarLoading(true);
    try {
      const result = await fetchSimilarCustomerClusters(0.8);
      setSimilarClusters(result.clusters);
      setSimilarClusterCount(result.clusterCount);
      setSimilarCustomerCount(result.customerCount);
      setKeepers((prev) => {
        const next = { ...prev };
        for (const cluster of result.clusters) {
          const current = next[cluster.clusterKey];
          const stillInCluster = cluster.customers.some((c) => c.id === current);
          if (!stillInCluster) next[cluster.clusterKey] = suggestKeeperId(cluster);
        }
        return next;
      });
    } catch (error) {
      setSimilarClusters([]);
      setSimilarClusterCount(0);
      setSimilarCustomerCount(0);
      message.error(apiErrorMessage(error, t('messages.similarLoadFailed')));
    } finally {
      setSimilarLoading(false);
    }
  }, [t]);

  useEffect(() => {
    if (activeTab === 'list') void load();
  }, [activeTab, load]);

  useEffect(() => {
    if (activeTab === 'similar') void loadSimilar();
  }, [activeTab, loadSimilar]);

  /** Live search while typing — debounce so API is not hit on every keystroke. */
  useEffect(() => {
    const next = searchInput.trim();
    const timer = window.setTimeout(() => {
      setPage(1);
      setSearch((prev) => (prev === next ? prev : next));
    }, 300);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const openCreate = () => {
    setEditing(null);
    setDrawerOpen(true);
  };

  const openEdit = async (row: { id: string }, event: React.MouseEvent) => {
    event.stopPropagation();
    try {
      setEditing(await fetchCustomer(row.id));
      setDrawerOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadCustomerFailed')));
    }
  };

  const handleMerge = async (row: SimilarRow) => {
    const keeperId = keepers[row.clusterKey];
    if (!keeperId || keeperId === row.id) {
      message.warning(t('pickKeeperFirst'));
      return;
    }
    setMergingId(row.id);
    try {
      const result = await mergeCustomers({
        keeperCustomerId: keeperId,
        sourceCustomerId: row.id,
      });
      message.success(
        t('mergeSuccess', {
          source: row.customerCode,
          orders: result.ordersMoved,
          payments: result.paymentsMoved,
        }),
      );
      await loadSimilar();
    } catch (error) {
      message.error(apiErrorMessage(error, t('mergeFailed')));
    } finally {
      setMergingId(null);
    }
  };

  const similarRows = useMemo(() => toSimilarRows(similarClusters), [similarClusters]);

  const columns: ColumnsType<CustomerAdminListItem> = useMemo(
    () => [
      {
        title: t('columns.customerCode'),
        dataIndex: 'customerCode',
        width: 120,
      },
      {
        title: t('columns.fullName'),
        dataIndex: 'fullName',
      },
      {
        title: t('columns.phone'),
        dataIndex: 'phone',
        width: 130,
      },
      {
        title: t('columns.group'),
        dataIndex: 'customerGroupName',
        width: 140,
        render: (v?: string | null, row?: CustomerAdminListItem) => {
          if (!v) return '—';
          const pct = Number(row?.groupDiscountPercent ?? 0);
          return pct > 0 ? `${v} (−${pct}%)` : v;
        },
      },
      {
        title: t('columns.email'),
        dataIndex: 'email',
        render: (v?: string) => v ?? '—',
      },
      {
        title: t('columns.status'),
        dataIndex: 'status',
        width: 110,
        render: (status: number) => (
          <Tag color={status === 1 ? 'green' : 'default'}>{customerStatusLabel(status)}</Tag>
        ),
      },
      {
        title: t('columns.createdAt'),
        dataIndex: 'createdAt',
        width: 120,
        render: (v: string) => formatDisplayDate(v),
      },
      ...(canWrite
        ? [
            {
              title: '',
              width: 72,
              render: (_: unknown, row: CustomerAdminListItem) => (
                <Button
                  type="link"
                  size="small"
                  icon={<EditOutlined />}
                  onClick={(event) => void openEdit(row, event)}
                >
                  {tc('actions.edit')}
                </Button>
              ),
            } as ColumnsType<CustomerAdminListItem>[number],
          ]
        : []),
    ],
    [canWrite, customerStatusLabel, t, tc],
  );

  const similarColumns: ColumnsType<SimilarRow> = useMemo(
    () => [
      {
        title: t('columns.match'),
        width: 140,
        render: (_: unknown, row: SimilarRow) =>
          row.isFirstInCluster ? (
            <Space direction="vertical" size={0}>
              <Tag color={row.matchKind === 'phone' ? 'orange' : 'blue'}>
                {row.matchKind === 'phone' ? t('matchPhone') : t('matchName')}
              </Tag>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {row.clusterSize} KH
              </Typography.Text>
            </Space>
          ) : null,
      },
      {
        title: t('columns.similarity'),
        width: 90,
        render: (_: unknown, row: SimilarRow) => {
          if (!row.isFirstInCluster) return null;
          if (row.matchKind === 'phone') return '100%';
          const pct = Math.round((row.maxSimilarity ?? 0) * 100);
          return pct > 0 ? t('similarityPct', { pct }) : '—';
        },
      },
      ...(canWrite
        ? [
            {
              title: t('keep'),
              width: 72,
              render: (_: unknown, row: SimilarRow) => (
                <Radio
                  checked={keepers[row.clusterKey] === row.id}
                  onClick={(event) => event.stopPropagation()}
                  onChange={() =>
                    setKeepers((prev) => ({ ...prev, [row.clusterKey]: row.id }))
                  }
                />
              ),
            } as ColumnsType<SimilarRow>[number],
          ]
        : []),
      {
        title: t('columns.customerCode'),
        dataIndex: 'customerCode',
        width: 110,
      },
      {
        title: t('columns.fullName'),
        dataIndex: 'fullName',
      },
      {
        title: t('columns.phone'),
        dataIndex: 'phone',
        width: 120,
      },
      {
        title: t('columns.hasOrders'),
        dataIndex: 'orderCount',
        width: 120,
        render: (count: number) =>
          count > 0 ? (
            <Tag color="green">{t('hasOrdersYes', { count })}</Tag>
          ) : (
            <Tag>{t('hasOrdersNo')}</Tag>
          ),
      },
      {
        title: t('columns.createdAt'),
        dataIndex: 'createdAt',
        width: 110,
        render: (v: string) => formatDisplayDate(v),
      },
      ...(canWrite
        ? [
            {
              title: '',
              width: 140,
              render: (_: unknown, row: SimilarRow) => {
                const isKeeper = keepers[row.clusterKey] === row.id;
                return (
                  <Space size={4} onClick={(event) => event.stopPropagation()}>
                    {!isKeeper ? (
                      <Popconfirm
                        title={t('mergeConfirmTitle')}
                        description={t('mergeConfirmBody')}
                        okText={t('merge')}
                        onConfirm={() => void handleMerge(row)}
                      >
                        <Button
                          type="link"
                          size="small"
                          danger
                          loading={mergingId === row.id}
                          disabled={mergingId != null && mergingId !== row.id}
                        >
                          {t('merge')}
                        </Button>
                      </Popconfirm>
                    ) : (
                      <Tag color="processing">{t('keep')}</Tag>
                    )}
                    <Button
                      type="link"
                      size="small"
                      icon={<EditOutlined />}
                      onClick={(event) => void openEdit(row, event)}
                    >
                      {tc('actions.edit')}
                    </Button>
                  </Space>
                );
              },
            } as ColumnsType<SimilarRow>[number],
          ]
        : []),
    ],
    [canWrite, keepers, mergingId, t, tc],
  );

  return (
    <>
      <Card
        title={t('title')}
        extra={
          canWrite ? (
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('addCustomer')}
            </Button>
          ) : null
        }
      >
        <Tabs
          activeKey={activeTab}
          onChange={(key) => setActiveTab(key as ListTab)}
          items={[
            {
              key: 'list',
              label: t('tabs.list'),
              children: (
                <>
                  {canWrite ? (
                    <Card size="small" title={t('importTitle')} style={{ marginBottom: 16 }}>
                      <CustomerImportCard onImported={load} />
                    </Card>
                  ) : null}
                  <Space wrap style={{ marginBottom: 16 }}>
                    <Input
                      allowClear
                      style={{ width: 320 }}
                      placeholder={t('searchPlaceholder')}
                      prefix={<SearchOutlined />}
                      value={searchInput}
                      onChange={(e) => setSearchInput(e.target.value)}
                      onPressEnter={() => {
                        setPage(1);
                        setSearch(searchInput.trim());
                      }}
                    />
                    <Button
                      type="primary"
                      icon={<SearchOutlined />}
                      onClick={() => {
                        setPage(1);
                        setSearch(searchInput.trim());
                      }}
                    >
                      {tc('actions.search')}
                    </Button>
                    <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
                      {tc('actions.reload')}
                    </Button>
                  </Space>

                  <Table
                    rowKey="id"
                    size="small"
                    loading={loading}
                    columns={columns}
                    dataSource={items}
                    onRow={(row) => ({
                      style: { cursor: 'pointer' },
                      onClick: () => navigate(`/customer/${row.id}`),
                    })}
                    pagination={{
                      current: page,
                      pageSize,
                      total,
                      showSizeChanger: true,
                      onChange: (nextPage, nextSize) => {
                        setPage(nextPage);
                        setPageSize(nextSize);
                      },
                    }}
                  />
                </>
              ),
            },
            {
              key: 'similar',
              label: t('tabs.similar'),
              children: (
                <>
                  <Alert
                    type="warning"
                    showIcon
                    style={{ marginBottom: 16 }}
                    message={t('similarGuideTitle')}
                    description={
                      <ol style={{ margin: 0, paddingLeft: 18 }}>
                        <li>{t('similarGuide1')}</li>
                        <li>{t('similarGuide2')}</li>
                        <li>{t('similarGuide3')}</li>
                      </ol>
                    }
                  />
                  {!canWrite ? (
                    <Alert
                      type="info"
                      showIcon
                      style={{ marginBottom: 16 }}
                      message={t('needCustomersWrite')}
                    />
                  ) : null}
                  <Space wrap style={{ marginBottom: 12 }}>
                    <Typography.Text type="secondary">
                      {t('similarSummary', {
                        clusters: similarClusterCount,
                        customers: similarCustomerCount,
                      })}
                    </Typography.Text>
                    <Button
                      icon={<ReloadOutlined />}
                      onClick={() => void loadSimilar()}
                      loading={similarLoading}
                    >
                      {tc('actions.reload')}
                    </Button>
                  </Space>
                  <Table
                    rowKey="key"
                    size="small"
                    loading={similarLoading}
                    columns={similarColumns}
                    dataSource={similarRows}
                    onRow={(row) => ({
                      style: {
                        cursor: 'pointer',
                        background: row.isFirstInCluster ? undefined : 'rgba(0,0,0,0.02)',
                      },
                      onClick: () => navigate(`/customer/${row.id}`),
                    })}
                    pagination={{ pageSize: 50, hideOnSinglePage: true }}
                    locale={{ emptyText: t('similarEmpty') }}
                  />
                </>
              ),
            },
          ]}
        />
      </Card>

      <CustomerFormDrawer
        open={drawerOpen}
        editing={editing}
        onClose={() => setDrawerOpen(false)}
        onSaved={(customer) => {
          if (activeTab === 'similar') void loadSimilar();
          else void load();
          if (!editing) navigate(`/customer/${customer.id}`);
        }}
      />
    </>
  );
}
