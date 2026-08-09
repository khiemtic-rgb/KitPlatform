import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { App, Button, Card, DatePicker, Popconfirm, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchSalesPriceOverrides, syncPriceOverrideListPrice } from '@/shared/api/sales.api';
import type { SalesPriceOverrideLine } from '@/shared/api/sales.types';
import { useCanSalesPriceManage } from '@/shared/auth/usePermission';
import { formatDisplayMoney } from '@/shared/utils/money';

function catalogKey(row: SalesPriceOverrideLine): string {
  return `${row.productId}:${row.productUnitId}`;
}

export function SalesPriceOverrideReportPage() {
  const { t } = useTranslation('sales', { keyPrefix: 'priceOverrides' });
  const { message } = App.useApp();
  const canManage = useCanSalesPriceManage();
  const [range, setRange] = useState<[Dayjs, Dayjs]>([dayjs().startOf('month'), dayjs().endOf('day')]);
  const [loading, setLoading] = useState(false);
  const [rows, setRows] = useState<SalesPriceOverrideLine[]>([]);
  const [applyingKey, setApplyingKey] = useState<string | null>(null);
  const [appliedKeys, setAppliedKeys] = useState<Set<string>>(() => new Set());

  useEffect(() => {
    if (!canManage) return;
    let cancelled = false;
    setLoading(true);
    void fetchSalesPriceOverrides({
      from: range[0].startOf('day').toISOString(),
      to: range[1].endOf('day').toISOString(),
      limit: 300,
    })
      .then((data) => {
        if (!cancelled) setRows(data);
      })
      .catch((error) => {
        if (!cancelled) message.error(apiErrorMessage(error, t('loadError')));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [canManage, message, range, t]);

  const applyListPrice = async (row: SalesPriceOverrideLine) => {
    const key = catalogKey(row);
    setApplyingKey(key);
    try {
      await syncPriceOverrideListPrice({
        productId: row.productId,
        productUnitId: row.productUnitId,
        price: row.unitPrice,
      });
      setAppliedKeys((prev) => new Set(prev).add(key));
      message.success(
        t('applySuccess', {
          product: row.productName,
          unit: row.unitName,
          price: formatDisplayMoney(row.unitPrice),
        }),
      );
    } catch (error) {
      message.error(apiErrorMessage(error, t('applyError')));
    } finally {
      setApplyingKey(null);
    }
  };

  const columns: ColumnsType<SalesPriceOverrideLine> = [
    {
      title: t('columns.orderDate'),
      dataIndex: 'orderDate',
      width: 150,
      render: (v: string) => dayjs(v).format('DD/MM/YYYY HH:mm'),
    },
    {
      title: t('columns.orderNumber'),
      dataIndex: 'orderNumber',
      width: 120,
      render: (v: string, r) => (
        <Link to={`/sales/orders?orderId=${r.salesOrderId}`}>{v}</Link>
      ),
    },
    {
      title: t('columns.product'),
      dataIndex: 'productName',
      ellipsis: true,
      render: (_, r) => `${r.productCode} — ${r.productName}`,
    },
    { title: t('columns.qty'), dataIndex: 'quantity', width: 64, align: 'right' },
    {
      title: t('columns.listPrice'),
      dataIndex: 'listUnitPrice',
      width: 100,
      align: 'right',
      render: (v: number) => formatDisplayMoney(v),
    },
    {
      title: t('columns.soldPrice'),
      dataIndex: 'unitPrice',
      width: 100,
      align: 'right',
      render: (v: number) => formatDisplayMoney(v),
    },
    {
      title: t('columns.diff'),
      width: 96,
      align: 'right',
      render: (_, r) => formatDisplayMoney(r.unitPrice - r.listUnitPrice),
    },
    { title: t('columns.customer'), dataIndex: 'customerName', width: 120, ellipsis: true },
    { title: t('columns.soldBy'), dataIndex: 'soldByName', width: 160 },
    {
      title: t('columns.action'),
      key: 'action',
      width: 152,
      fixed: 'right',
      render: (_, r) => {
        const key = catalogKey(r);
        if (appliedKeys.has(key)) {
          return <Typography.Text type="success">{t('applied')}</Typography.Text>;
        }
        return (
          <Popconfirm
            title={t('applyConfirmTitle')}
            description={t('applyConfirm', {
              product: r.productName,
              unit: r.unitName,
              from: formatDisplayMoney(r.listUnitPrice),
              to: formatDisplayMoney(r.unitPrice),
            })}
            okText={t('applyOk')}
            cancelText={t('applyCancel')}
            onConfirm={() => void applyListPrice(r)}
          >
            <Button type="link" size="small" loading={applyingKey === key} style={{ paddingInline: 0 }}>
              {t('applyListPrice')}
            </Button>
          </Popconfirm>
        );
      },
    },
  ];

  if (!canManage) {
    return <Typography.Text type="secondary">{t('noAccess')}</Typography.Text>;
  }

  return (
    <Card
      title={t('title')}
      extra={
        <Space>
          <DatePicker.RangePicker
            value={range}
            allowClear={false}
            onChange={(v) => {
              if (v?.[0] && v[1]) setRange([v[0], v[1]]);
            }}
          />
        </Space>
      }
    >
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {t('hint')}
      </Typography.Paragraph>
      <Table
        rowKey="salesOrderItemId"
        loading={loading}
        columns={columns}
        dataSource={rows}
        size="small"
        scroll={{ x: 1280 }}
        pagination={{ pageSize: 50, showSizeChanger: true }}
        summary={() => {
          const diff = rows.reduce((s, r) => s + (r.unitPrice - r.listUnitPrice) * r.quantity, 0);
          return (
            <Table.Summary.Row>
              <Table.Summary.Cell index={0} colSpan={6}>
                <strong>{t('summary', { count: rows.length })}</strong>
              </Table.Summary.Cell>
              <Table.Summary.Cell index={1} align="right">
                <strong>{formatDisplayMoney(diff)}</strong>
              </Table.Summary.Cell>
              <Table.Summary.Cell index={2} colSpan={3} />
            </Table.Summary.Row>
          );
        }}
      />
    </Card>
  );
}
