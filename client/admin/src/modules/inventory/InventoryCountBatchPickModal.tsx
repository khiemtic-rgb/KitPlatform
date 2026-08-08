import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Button, Input, Modal, Space, Switch, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { StockBatch } from '@/shared/api/inventory.types';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayQuantity } from '@/shared/utils/money';
import {
  expiryToneColor,
  getExpiryTone,
  NEAR_EXPIRY_DAYS,
  sortBatchesForCount,
} from '@/modules/inventory/inventory-count-batch-sort';

interface InventoryCountBatchPickModalProps {
  open: boolean;
  productLabel: string;
  batches: StockBatch[];
  initialSelectedIds?: string[];
  onCancel: () => void;
  /** Trả về các lô đã chọn (đã sort FEFO). */
  onConfirm: (selected: StockBatch[]) => void;
}

export function InventoryCountBatchPickModal({
  open,
  productLabel,
  batches,
  initialSelectedIds,
  onCancel,
  onConfirm,
}: InventoryCountBatchPickModalProps) {
  const { t } = useTranslation('inventory', { keyPrefix: 'inventoryCountBatchPick' });
  const [search, setSearch] = useState('');
  const [hideZero, setHideZero] = useState(true);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);

  useEffect(() => {
    if (!open) return;
    setSearch('');
    setHideZero(true);
    const initial = (initialSelectedIds ?? []).filter((id) => batches.some((b) => b.id === id));
    setSelectedIds(initial.length > 0 ? initial : []);
  }, [open, batches, initialSelectedIds]);

  const sorted = useMemo(() => sortBatchesForCount(batches), [batches]);

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    return sorted.filter((b) => {
      if (hideZero && b.quantityAvailable <= 0) return false;
      if (!q) return true;
      return b.batchNumber.toLowerCase().includes(q);
    });
  }, [sorted, hideZero, search]);

  const selectedBatches = useMemo(() => {
    const map = new Map(batches.map((b) => [b.id, b]));
    return selectedIds.map((id) => map.get(id)).filter((b): b is StockBatch => Boolean(b));
  }, [batches, selectedIds]);

  const expiredSelected = selectedBatches.filter((b) => getExpiryTone(b.expiryDate) === 'expired');

  const columns: ColumnsType<StockBatch> = [
    {
      title: t('columns.batchNumber'),
      dataIndex: 'batchNumber',
      width: 120,
      render: (v: string) => <Typography.Text strong>{v}</Typography.Text>,
    },
    {
      title: t('columns.expiry'),
      dataIndex: 'expiryDate',
      width: 130,
      render: (v?: string) => {
        if (!v) return '—';
        const tone = getExpiryTone(v);
        return (
          <span style={{ color: expiryToneColor(tone), fontWeight: tone === 'ok' ? 400 : 600 }}>
            {formatDisplayDate(v)}
          </span>
        );
      },
    },
    {
      title: t('columns.stock'),
      dataIndex: 'quantityAvailable',
      width: 100,
      align: 'right',
      render: (v: number) => (
        <span style={{ color: v <= 0 ? '#8c8c8c' : undefined }}>
          {formatDisplayQuantity(v)}
          {v <= 0 ? (
            <Tag style={{ marginLeft: 6 }} color="default">
              {t('zeroStock')}
            </Tag>
          ) : null}
        </span>
      ),
    },
  ];

  return (
    <Modal
      open={open}
      title={t('title')}
      onCancel={onCancel}
      width={720}
      centered
      destroyOnClose
      styles={{
        body: {
          maxHeight: 'calc(100vh - 180px)',
          overflow: 'hidden',
          display: 'flex',
          flexDirection: 'column',
          paddingTop: 12,
        },
      }}
      footer={
        <Space style={{ width: '100%', justifyContent: 'space-between' }}>
          <Tag color="blue">{t('selectedCount', { count: selectedIds.length })}</Tag>
          <Space>
            <Button onClick={onCancel}>{t('exit')}</Button>
            <Button
              type="primary"
              disabled={selectedIds.length === 0}
              onClick={() => onConfirm(sortBatchesForCount(selectedBatches))}
            >
              {t('confirm')}
            </Button>
          </Space>
        </Space>
      }
    >
      <Space direction="vertical" size="middle" style={{ width: '100%', flexShrink: 0 }}>
        <Typography.Text type="secondary">{productLabel}</Typography.Text>

        <Space wrap size={[12, 8]} align="center">
          <Input.Search
            allowClear
            placeholder={t('searchPlaceholder')}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ width: 260 }}
          />
          <Space size={6}>
            <Switch checked={hideZero} onChange={setHideZero} size="small" />
            <span>{t('hideZeroStock')}</span>
          </Space>
        </Space>

        <Space wrap size={[8, 4]}>
          <Typography.Text type="secondary">{t('legendTitle')}</Typography.Text>
          <Tag>{t('legendOk')}</Tag>
          <Tag color="gold">{t('legendNear', { days: NEAR_EXPIRY_DAYS })}</Tag>
          <Tag color="red">{t('legendExpired')}</Tag>
        </Space>

        {expiredSelected.length > 0 ? (
          <Alert
            type="warning"
            showIcon
            message={t('expiredWarning', { count: expiredSelected.length })}
          />
        ) : null}
      </Space>

      <div style={{ flex: 1, minHeight: 0, marginTop: 12, overflow: 'hidden' }}>
        <Table
          rowKey="id"
          size="small"
          pagination={{ pageSize: 20, hideOnSinglePage: true, showSizeChanger: false }}
          columns={columns}
          dataSource={visible}
          rowSelection={{
            selectedRowKeys: selectedIds,
            onChange: (keys) => setSelectedIds(keys.map(String)),
            preserveSelectedRowKeys: true,
          }}
          locale={{ emptyText: t('empty') }}
          scroll={{ y: 'max(160px, calc(100vh - 420px))' }}
        />
      </div>
    </Modal>
  );
}
