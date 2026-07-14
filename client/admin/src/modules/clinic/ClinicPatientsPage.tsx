import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Button, Card, Drawer, Input, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined, HistoryOutlined, PlusOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { fetchCustomer, fetchCustomers } from '@/shared/api/customer-admin.api';
import type { CustomerAdminListItem, CustomerDetail } from '@/shared/api/customer-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchClinicVisits, type ClinicVisit } from '@/shared/api/clinic.api';
import { CustomerFormDrawer } from '@/modules/customer/CustomerFormDrawer';
import { useHasPermission } from '@/shared/auth/usePermission';

export function ClinicPatientsPage() {
  const { t } = useTranslation('clinic');
  const navigate = useNavigate();
  const canWrite = useHasPermission('clinic.write');
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<CustomerAdminListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<CustomerDetail | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyPatient, setHistoryPatient] = useState<CustomerAdminListItem | null>(null);
  const [historyVisits, setHistoryVisits] = useState<ClinicVisit[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await fetchCustomers({ search: search || undefined, page, pageSize });
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      setItems([]);
      setTotal(0);
      message.error(apiErrorMessage(error, t('patients.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, search, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const openHistory = async (row: CustomerAdminListItem) => {
    setHistoryPatient(row);
    setHistoryOpen(true);
    setHistoryLoading(true);
    try {
      setHistoryVisits(await fetchClinicVisits({ customerId: row.id }));
    } catch (error) {
      setHistoryVisits([]);
      message.error(apiErrorMessage(error, t('patients.historyFailed')));
    } finally {
      setHistoryLoading(false);
    }
  };

  const columns: ColumnsType<CustomerAdminListItem> = useMemo(
    () => [
      { title: t('patients.colCode'), dataIndex: 'customerCode', width: 120 },
      { title: t('patients.colName'), dataIndex: 'fullName' },
      { title: t('patients.colPhone'), dataIndex: 'phone', width: 130 },
      {
        title: t('patients.colStatus'),
        dataIndex: 'status',
        width: 100,
        render: (s: number) => (
          <Tag color={s === 1 ? 'green' : 'default'}>{s === 1 ? t('patients.active') : t('patients.inactive')}</Tag>
        ),
      },
      {
        title: t('patients.colActions'),
        width: 160,
        render: (_, row) => (
          <Space>
            <Button
              size="small"
              icon={<HistoryOutlined />}
              onClick={(e) => {
                e.stopPropagation();
                void openHistory(row);
              }}
            >
              {t('patients.history')}
            </Button>
            {canWrite ? (
              <Button
                size="small"
                icon={<EditOutlined />}
                onClick={(e) => {
                  e.stopPropagation();
                  void (async () => {
                    try {
                      setEditing(await fetchCustomer(row.id));
                      setDrawerOpen(true);
                    } catch (error) {
                      message.error(apiErrorMessage(error, t('patients.loadFailed')));
                    }
                  })();
                }}
              />
            ) : null}
          </Space>
        ),
      },
    ],
    [canWrite, t],
  );

  const historyColumns: ColumnsType<ClinicVisit> = [
    {
      title: t('visits.colWhen'),
      dataIndex: 'startedAt',
      width: 140,
      render: (v: string) => (v ? dayjs(v).format('DD/MM/YYYY HH:mm') : '—'),
    },
    {
      title: t('visits.colProvider'),
      render: (_, row) => row.providerDisplayName || '—',
    },
    {
      title: t('visits.colStatus'),
      dataIndex: 'visitStatus',
      width: 100,
      render: (s: string) => t(`visits.status.${s}`, { defaultValue: s }),
    },
    {
      title: t('patients.colActions'),
      width: 100,
      render: (_, row) => (
        <Button
          size="small"
          type="link"
          onClick={() => {
            setHistoryOpen(false);
            navigate(`/clinic/visits?open=${row.id}`);
          }}
        >
          {t('patients.openVisit')}
        </Button>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            {t('patients.title')}
          </Typography.Title>
          <Typography.Text type="secondary">{t('patients.subtitle')}</Typography.Text>
        </div>
        <Space wrap>
          <Input
            allowClear
            prefix={<SearchOutlined />}
            placeholder={t('patients.searchPlaceholder')}
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            onPressEnter={() => {
              setPage(1);
              setSearch(searchInput.trim());
            }}
            style={{ width: 220 }}
          />
          <Button
            icon={<SearchOutlined />}
            onClick={() => {
              setPage(1);
              setSearch(searchInput.trim());
            }}
          >
            {t('patients.search')}
          </Button>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            {t('patients.refresh')}
          </Button>
          {canWrite ? (
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => {
                setEditing(null);
                setDrawerOpen(true);
              }}
            >
              {t('patients.create')}
            </Button>
          ) : null}
        </Space>
      </div>

      <Card>
        <Table
          rowKey="id"
          loading={loading}
          columns={columns}
          dataSource={items}
          pagination={{
            current: page,
            pageSize,
            total,
            onChange: (p, ps) => {
              setPage(p);
              setPageSize(ps);
            },
          }}
          locale={{ emptyText: t('patients.empty') }}
        />
      </Card>

      <CustomerFormDrawer
        open={drawerOpen}
        editing={editing}
        onClose={() => setDrawerOpen(false)}
        onSaved={() => {
          setDrawerOpen(false);
          void load();
        }}
      />

      <Drawer
        title={
          historyPatient
            ? `${t('patients.historyTitle')} — ${historyPatient.fullName}`
            : t('patients.historyTitle')
        }
        open={historyOpen}
        onClose={() => setHistoryOpen(false)}
        width={560}
      >
        <Table
          rowKey="id"
          loading={historyLoading}
          columns={historyColumns}
          dataSource={historyVisits}
          pagination={{ pageSize: 10 }}
          locale={{ emptyText: t('patients.historyEmpty') }}
          size="small"
        />
      </Drawer>
    </Space>
  );
}
