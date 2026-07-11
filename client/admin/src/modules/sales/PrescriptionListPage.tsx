import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Card, Input, Select, Space, Table, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EyeOutlined, ReloadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { fetchPrescriptions, type RxPrescriptionListItem } from '@/shared/api/rx.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { PrescriptionFormDrawer } from '@/modules/sales/PrescriptionFormDrawer';

const STATUS_COLORS: Record<string, string> = {
  draft: 'default',
  pending_verification: 'gold',
  verified: 'green',
  signed: 'blue',
  partially_dispensed: 'cyan',
  dispensed: 'purple',
  expired: 'default',
  cancelled: 'red',
};

const STATUS_KEYS = Object.keys(STATUS_COLORS);

export function PrescriptionListPage() {
  const { t } = useTranslation('rx');
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const canWrite = useHasPermission('rx.prescription.create') || useHasPermission('sales.write');
  const canVerify = useHasPermission('rx.prescription.verify');
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<RxPrescriptionListItem[]>([]);
  const [status, setStatus] = useState<string>();
  const [phoneSearch, setPhoneSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [total, setTotal] = useState(0);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [activeId, setActiveId] = useState<string>();
  const [readOnlyDrawer, setReadOnlyDrawer] = useState(false);

  const statusLabel = useCallback(
    (key: string) => t(`enums.prescriptionStatus.${key}`, { defaultValue: key }),
    [t],
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await fetchPrescriptions({
        status,
        phoneSearch,
        page,
        pageSize,
      });
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      message.error(apiErrorMessage(error, t('prescriptions.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [status, phoneSearch, page, pageSize, t]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    const rxId = searchParams.get('rx');
    if (!rxId) return;
    setActiveId(rxId);
    setReadOnlyDrawer(true);
    setDrawerOpen(true);
    setSearchParams({}, { replace: true });
  }, [searchParams, setSearchParams]);

  const openCreate = () => {
    setActiveId(undefined);
    setReadOnlyDrawer(false);
    setDrawerOpen(true);
  };

  const openView = (id: string) => {
    setActiveId(id);
    setReadOnlyDrawer(true);
    setDrawerOpen(true);
  };

  const openEdit = (id: string) => {
    setActiveId(id);
    setReadOnlyDrawer(false);
    setDrawerOpen(true);
  };

  const columns: ColumnsType<RxPrescriptionListItem> = useMemo(
    () => [
      {
        title: t('prescriptions.columns.code'),
        dataIndex: 'prescriptionCode',
        width: 140,
      },
      {
        title: t('prescriptions.columns.patient'),
        key: 'patient',
        render: (_, row) => (
          <div>
            <div>{row.patientName || '—'}</div>
            <small>{row.patientPhone || '—'}</small>
          </div>
        ),
      },
      {
        title: t('prescriptions.columns.prescriber'),
        dataIndex: 'prescriberName',
      },
      {
        title: t('prescriptions.columns.status'),
        dataIndex: 'status',
        width: 160,
        render: (value: string) => (
          <Tag color={STATUS_COLORS[value] || 'default'}>{statusLabel(value)}</Tag>
        ),
      },
      {
        title: t('prescriptions.columns.createdAt'),
        dataIndex: 'createdAt',
        width: 150,
        render: (value: string) => dayjs(value).format('DD/MM/YYYY HH:mm'),
      },
      {
        title: t('prescriptions.columns.actions'),
        key: 'actions',
        width: 280,
        render: (_, row) => (
          <Space size={4} onClick={(event) => event.stopPropagation()}>
            <Button type="link" size="small" icon={<EyeOutlined />} onClick={() => openView(row.id)}>
              {t('prescriptions.actions.view')}
            </Button>
            {canWrite ? (
              <Button type="link" size="small" onClick={() => openEdit(row.id)}>
                {t('prescriptions.actions.edit')}
              </Button>
            ) : null}
            {canVerify && row.status === 'pending_verification' ? (
              <Button type="link" size="small" onClick={() => openEdit(row.id)}>
                {t('prescriptions.actions.verify')}
              </Button>
            ) : null}
            <Button
              type="link"
              size="small"
              onClick={() => navigate(`/sales/pos?prescriptionId=${row.id}`)}
            >
              {t('prescriptions.actions.loadPos')}
            </Button>
          </Space>
        ),
      },
    ],
    [canWrite, canVerify, navigate, statusLabel, t],
  );

  return (
    <Card
      title={t('prescriptions.title')}
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {t('prescriptions.reload')}
          </Button>
          {canWrite ? (
            <Button type="primary" onClick={openCreate}>
              {t('prescriptions.create')}
            </Button>
          ) : null}
        </Space>
      }
    >
      <Space style={{ marginBottom: 12 }} wrap>
        <Select
          allowClear
          placeholder={t('prescriptions.filterStatus')}
          style={{ width: 200 }}
          value={status}
          onChange={(value) => {
            setStatus(value);
            setPage(1);
          }}
          options={STATUS_KEYS.map((key) => ({ value: key, label: statusLabel(key) }))}
        />
        <Input
          allowClear
          placeholder={t('prescriptions.filterPhone')}
          style={{ width: 250 }}
          value={phoneSearch}
          onChange={(event) => setPhoneSearch(event.target.value)}
          onPressEnter={() => {
            setPage(1);
            void load();
          }}
        />
        <Button
          onClick={() => {
            setPage(1);
            void load();
          }}
        >
          {t('prescriptions.filter')}
        </Button>
      </Space>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={items}
        columns={columns}
        onRow={(record) => ({
          onClick: () => openView(record.id),
          style: { cursor: 'pointer' },
        })}
        pagination={{
          current: page,
          pageSize,
          total,
          showSizeChanger: true,
          onChange: (nextPage, nextPageSize) => {
            setPage(nextPage);
            setPageSize(nextPageSize);
          },
        }}
      />

      <PrescriptionFormDrawer
        open={drawerOpen}
        prescriptionId={activeId}
        readOnly={readOnlyDrawer}
        onClose={() => setDrawerOpen(false)}
        onSaved={() => void load()}
      />
    </Card>
  );
}
