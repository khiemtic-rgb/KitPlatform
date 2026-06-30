import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, Descriptions, Drawer, Spin, Table, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { CreditCardOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { fetchSupplierPayables, fetchSupplierPayablesDetail } from '@/shared/api/procurement.api';
import type { SupplierPayablesDetail, SupplierPayablesDetailLine, SupplierPayablesRow } from '@/shared/api/procurement.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { buildSupplierPaymentCreateUrl } from '@/modules/procurement/supplier-payment-nav';
import { useProcurementWrite } from '@/shared/auth/usePermission';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

export function SupplierPayablesPage() {
  const { t } = useTranslation('procurement', { keyPrefix: 'supplierPayables' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const canWrite = useProcurementWrite();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [rows, setRows] = useState<SupplierPayablesRow[]>([]);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detail, setDetail] = useState<SupplierPayablesDetail | null>(null);
  const emDash = tShared('emDash');

  const agingCell = useCallback(
    (value: number) => (value > 0.009 ? formatDisplayMoney(value) : emDash),
    [emDash],
  );

  const loadSummary = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await fetchSupplierPayables());
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadSummary();
  }, [loadSummary]);

  const totals = useMemo(
    () =>
      rows.reduce(
        (acc, row) => ({
          payable: acc.payable + row.totalPayable,
          current: acc.current + row.aging.current,
          days31To60: acc.days31To60 + row.aging.days31To60,
          days61To90: acc.days61To90 + row.aging.days61To90,
          over90: acc.over90 + row.aging.over90,
        }),
        { payable: 0, current: 0, days31To60: 0, days61To90: 0, over90: 0 },
      ),
    [rows],
  );

  const openDetail = async (supplierId: string) => {
    setDetailOpen(true);
    setDetailLoading(true);
    setDetail(null);
    try {
      setDetail(await fetchSupplierPayablesDetail(supplierId));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
      setDetailOpen(false);
    } finally {
      setDetailLoading(false);
    }
  };

  const goToPayment = (prefill: { supplierId: string; goodsReceiptId?: string; amount?: number }) => {
    navigate(buildSupplierPaymentCreateUrl(prefill));
  };

  const detailColumns: ColumnsType<SupplierPayablesDetailLine> = useMemo(() => {
    const base: ColumnsType<SupplierPayablesDetailLine> = [
      { title: tShared('columns.grnNumber'), dataIndex: 'grnNumber', width: 130 },
      {
        title: tShared('columns.receiptDate'),
        dataIndex: 'receiptDate',
        width: 120,
        render: (v: string) => formatDisplayDate(v),
      },
      {
        title: t('columns.grnValue'),
        dataIndex: 'grnTotal',
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('columns.paid'),
        dataIndex: 'paidAmount',
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('columns.outstanding'),
        dataIndex: 'outstanding',
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('columns.agingDays'),
        dataIndex: 'daysOutstanding',
        width: 120,
        align: 'center',
      },
    ];

    if (!canWrite) return base;

    return [
      ...base,
      {
        title: '',
        width: 110,
        render: (_, line) =>
          line.outstanding > 0.009 && detail ? (
            <Button
              type="link"
              size="small"
              icon={<CreditCardOutlined />}
              onClick={(e) => {
                e.stopPropagation();
                goToPayment({
                  supplierId: detail.supplierId,
                  goodsReceiptId: line.goodsReceiptId,
                  amount: line.outstanding,
                });
              }}
            >
              {t('pay')}
            </Button>
          ) : null,
      },
    ];
  }, [canWrite, detail, navigate, t, tShared]);

  const columns: ColumnsType<SupplierPayablesRow> = useMemo(
    () => [
      {
        title: tShared('columns.supplierCode'),
        dataIndex: 'supplierCode',
        width: 110,
      },
      {
        title: tShared('columns.supplierName'),
        dataIndex: 'supplierName',
        width: 280,
        ellipsis: { showTitle: true },
      },
      {
        title: tShared('columns.paymentTermsDays'),
        dataIndex: 'paymentTerms',
        width: 110,
        align: 'center',
      },
      {
        title: t('columns.totalPayable'),
        dataIndex: 'totalPayable',
        width: 140,
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('columns.aging0To30'),
        width: 120,
        align: 'right',
        render: (_, row) => agingCell(row.aging.current),
      },
      {
        title: t('columns.aging31To60'),
        width: 110,
        align: 'right',
        render: (_, row) => agingCell(row.aging.days31To60),
      },
      {
        title: t('columns.aging61To90'),
        width: 110,
        align: 'right',
        render: (_, row) => agingCell(row.aging.days61To90),
      },
      {
        title: t('columns.agingOver90'),
        width: 110,
        align: 'right',
        render: (_, row) => agingCell(row.aging.over90),
      },
      {
        title: t('columns.openDocuments'),
        dataIndex: 'openDocumentCount',
        width: 90,
        align: 'center',
      },
    ],
    [agingCell, t, tShared],
  );

  return (
    <Card title={t('title')} bordered={false}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {t('intro')}
      </Typography.Paragraph>

      <Table
        rowKey="supplierId"
        loading={loading}
        columns={columns}
        dataSource={rows}
        pagination={{ pageSize: 20, showTotal: (total) => tShared('pagination.suppliers', { count: total }) }}
        scroll={{ x: 1180 }}
        summary={() =>
          rows.length > 0 ? (
            <Table.Summary fixed>
              <Table.Summary.Row>
                <Table.Summary.Cell index={0} colSpan={3}>
                  <strong>{tShared('columns.total')}</strong>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={1} align="right">
                  <strong>{formatDisplayMoney(totals.payable)}</strong>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={2} align="right">
                  {agingCell(totals.current)}
                </Table.Summary.Cell>
                <Table.Summary.Cell index={3} align="right">
                  {agingCell(totals.days31To60)}
                </Table.Summary.Cell>
                <Table.Summary.Cell index={4} align="right">
                  {agingCell(totals.days61To90)}
                </Table.Summary.Cell>
                <Table.Summary.Cell index={5} align="right">
                  {agingCell(totals.over90)}
                </Table.Summary.Cell>
                <Table.Summary.Cell index={6} />
              </Table.Summary.Row>
            </Table.Summary>
          ) : null
        }
        onRow={(record) => ({
          onClick: () => void openDetail(record.supplierId),
          style: { cursor: 'pointer' },
        })}
      />

      <Drawer
        title={detail ? t('detailDrawerWithName', { supplierName: detail.supplierName }) : t('detailDrawer')}
        width={880}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        destroyOnClose
        extra={
          detail && canWrite && detail.totalPayable > 0.009 ? (
            <Button
              type="primary"
              icon={<CreditCardOutlined />}
              onClick={() =>
                goToPayment({
                  supplierId: detail.supplierId,
                  amount: detail.totalPayable,
                })
              }
            >
              {t('createPayment')}
            </Button>
          ) : undefined
        }
      >
        {detailLoading ? (
          <Spin tip={tShared('messages.loadingDetail')} />
        ) : detail ? (
          <>
            <Descriptions column={2} size="small" bordered style={{ marginBottom: 16 }}>
              <Descriptions.Item label={tShared('columns.supplierCode')}>{detail.supplierCode}</Descriptions.Item>
              <Descriptions.Item label={tShared('columns.paymentTermsFull')}>
                {t('paymentTermsDays', { days: detail.paymentTerms })}
              </Descriptions.Item>
              <Descriptions.Item label={t('columns.totalPayable')}>{formatDisplayMoney(detail.totalPayable)}</Descriptions.Item>
              <Descriptions.Item label={t('columns.unappliedCredit')}>
                {detail.unappliedCredit > 0.009 ? formatDisplayMoney(detail.unappliedCredit) : emDash}
              </Descriptions.Item>
            </Descriptions>
            <Table
              rowKey="goodsReceiptId"
              size="small"
              pagination={false}
              dataSource={detail.lines.filter((line) => line.outstanding > 0.009)}
              columns={detailColumns}
            />
          </>
        ) : null}
      </Drawer>
    </Card>
  );
}
