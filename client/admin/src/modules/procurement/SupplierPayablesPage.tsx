import { useCallback, useEffect, useMemo, useState } from 'react';
import { Card, Descriptions, Drawer, Spin, Table, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { fetchSupplierPayables, fetchSupplierPayablesDetail } from '@/shared/api/procurement.api';
import type { SupplierPayablesDetail, SupplierPayablesRow } from '@/shared/api/procurement.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

function agingCell(value: number) {
  return value > 0.009 ? formatDisplayMoney(value) : '—';
}

export function SupplierPayablesPage() {
  const [loading, setLoading] = useState(false);
  const [rows, setRows] = useState<SupplierPayablesRow[]>([]);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detail, setDetail] = useState<SupplierPayablesDetail | null>(null);

  const loadSummary = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await fetchSupplierPayables());
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được báo cáo công nợ NCC'));
    } finally {
      setLoading(false);
    }
  }, []);

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
      message.error(apiErrorMessage(error, 'Không tải được chi tiết công nợ'));
      setDetailOpen(false);
    } finally {
      setDetailLoading(false);
    }
  };

  const columns: ColumnsType<SupplierPayablesRow> = [
    {
      title: 'Mã NCC',
      dataIndex: 'supplierCode',
      width: 110,
    },
    {
      title: 'Nhà cung cấp',
      dataIndex: 'supplierName',
      ellipsis: true,
    },
    {
      title: 'Hạn TT (ngày)',
      dataIndex: 'paymentTerms',
      width: 110,
      align: 'center',
    },
    {
      title: 'Còn phải trả',
      dataIndex: 'totalPayable',
      width: 140,
      align: 'right',
      render: (v: number) => formatDisplayMoney(v),
    },
    {
      title: '0–30 ngày',
      width: 120,
      align: 'right',
      render: (_, row) => agingCell(row.aging.current),
    },
    {
      title: '31–60',
      width: 110,
      align: 'right',
      render: (_, row) => agingCell(row.aging.days31To60),
    },
    {
      title: '61–90',
      width: 110,
      align: 'right',
      render: (_, row) => agingCell(row.aging.days61To90),
    },
    {
      title: '> 90',
      width: 110,
      align: 'right',
      render: (_, row) => agingCell(row.aging.over90),
    },
    {
      title: 'Phiếu mở',
      dataIndex: 'openDocumentCount',
      width: 90,
      align: 'center',
    },
  ];

  const detailColumns: ColumnsType<SupplierPayablesDetail['lines'][number]> = [
    { title: 'Phiếu nhập', dataIndex: 'grnNumber', width: 130 },
    {
      title: 'Ngày nhập',
      dataIndex: 'receiptDate',
      width: 120,
      render: (v: string) => formatDisplayDate(v),
    },
    {
      title: 'Giá trị GRN',
      dataIndex: 'grnTotal',
      align: 'right',
      render: (v: number) => formatDisplayMoney(v),
    },
    {
      title: 'Đã trả',
      dataIndex: 'paidAmount',
      align: 'right',
      render: (v: number) => formatDisplayMoney(v),
    },
    {
      title: 'Còn lại',
      dataIndex: 'outstanding',
      align: 'right',
      render: (v: number) => formatDisplayMoney(v),
    },
    {
      title: 'Tuổi nợ (ngày)',
      dataIndex: 'daysOutstanding',
      width: 120,
      align: 'center',
    },
  ];

  return (
    <Card title="Công nợ nhà cung cấp" bordered={false}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        Tính theo phiếu nhập đã hoàn tất trừ thanh toán đã ghi sổ. Thanh toán không gắn GRN được bù trừ theo thứ tự
        nhập cũ nhất.
      </Typography.Paragraph>

      <Table
        rowKey="supplierId"
        loading={loading}
        columns={columns}
        dataSource={rows}
        pagination={{ pageSize: 20, showTotal: (total) => `${total} NCC` }}
        scroll={{ x: 980 }}
        summary={() =>
          rows.length > 0 ? (
            <Table.Summary fixed>
              <Table.Summary.Row>
                <Table.Summary.Cell index={0} colSpan={3}>
                  <strong>Tổng</strong>
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
        title={detail ? `Công nợ — ${detail.supplierName}` : 'Chi tiết công nợ'}
        width={880}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        destroyOnClose
      >
        {detailLoading ? (
          <Spin tip="Đang tải chi tiết..." />
        ) : detail ? (
          <>
            <Descriptions column={2} size="small" bordered style={{ marginBottom: 16 }}>
              <Descriptions.Item label="Mã NCC">{detail.supplierCode}</Descriptions.Item>
              <Descriptions.Item label="Hạn thanh toán">{detail.paymentTerms} ngày</Descriptions.Item>
              <Descriptions.Item label="Còn phải trả">{formatDisplayMoney(detail.totalPayable)}</Descriptions.Item>
              <Descriptions.Item label="Tín dụng chưa phân bổ">
                {detail.unappliedCredit > 0.009 ? formatDisplayMoney(detail.unappliedCredit) : '—'}
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
