import { useCallback, useEffect, useState } from 'react';
import { Card, Empty, Progress, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import {
  fetchCustomerLoyaltySummary,
  fetchCustomerLoyaltyTransactions,
} from '@/shared/api/customer-admin.api';
import type { LoyaltyProgramSummary, LoyaltyTransaction } from '@/shared/api/customer-admin.types';
import { LOYALTY_TX_LABELS } from '@/shared/api/customer-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatDisplayQuantity } from '@/shared/utils/money';

interface CustomerLoyaltyPanelProps {
  customerId: string;
}

function formatPoints(value: number): string {
  return Number.isInteger(value)
    ? value.toLocaleString('vi-VN')
    : value.toLocaleString('vi-VN', { maximumFractionDigits: 4 });
}

export function CustomerLoyaltyPanel({ customerId }: CustomerLoyaltyPanelProps) {
  const [programs, setPrograms] = useState<LoyaltyProgramSummary[]>([]);
  const [transactions, setTransactions] = useState<LoyaltyTransaction[]>([]);
  const [txTotal, setTxTotal] = useState(0);
  const [txPage, setTxPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [txLoading, setTxLoading] = useState(false);

  const loadSummary = useCallback(async () => {
    setLoading(true);
    try {
      const summary = await fetchCustomerLoyaltySummary(customerId);
      setPrograms(summary.programs);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được tích điểm'));
    } finally {
      setLoading(false);
    }
  }, [customerId]);

  const loadTransactions = useCallback(
    async (page: number) => {
      setTxLoading(true);
      try {
        const result = await fetchCustomerLoyaltyTransactions(customerId, page, 10);
        setTransactions(result.items);
        setTxTotal(result.total);
        setTxPage(page);
      } catch (error) {
        message.error(apiErrorMessage(error, 'Không tải được lịch sử điểm'));
      } finally {
        setTxLoading(false);
      }
    },
    [customerId],
  );

  useEffect(() => {
    void loadSummary();
    void loadTransactions(1);
  }, [loadSummary, loadTransactions]);

  const txColumns: ColumnsType<LoyaltyTransaction> = [
    {
      title: 'Thời gian',
      dataIndex: 'createdAt',
      width: 140,
      render: (v: string) => dayjs(v).format('DD/MM/YYYY HH:mm'),
    },
    {
      title: 'Loại',
      dataIndex: 'transactionType',
      width: 120,
      render: (v: number) => LOYALTY_TX_LABELS[v] ?? v,
    },
    {
      title: 'Điểm',
      dataIndex: 'points',
      width: 100,
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums', color: v >= 0 ? '#389e0d' : '#cf1322' }}>
          {v >= 0 ? '+' : ''}
          {formatPoints(v)}
        </span>
      ),
    },
    {
      title: 'Ghi chú',
      dataIndex: 'notes',
      render: (v?: string) => v ?? '—',
    },
  ];

  if (loading) {
    return <Card loading />;
  }

  if (programs.length === 0) {
    return <Empty description="Khách chưa tham gia chương trình tích điểm" />;
  }

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      {programs.map((program) => {
        const tierProgress =
          program.nextTier && program.currentTier
            ? Math.min(
                100,
                Math.round(
                  ((program.pointsBalance - program.currentTier.minPoints) /
                    Math.max(1, program.nextTier.minPoints - program.currentTier.minPoints)) *
                    100,
                ),
              )
            : 100;

        return (
          <Card key={program.programId} size="small" title={program.programName}>
            <Space direction="vertical" size="small" style={{ width: '100%' }}>
              <Typography.Text>
                Số dư: <strong>{formatPoints(program.pointsBalance)}</strong> điểm · Tích lũy:{' '}
                {formatDisplayQuantity(program.lifetimePoints)}
              </Typography.Text>
              {program.currentTier ? (
                <Tag color="blue">
                  {program.currentTier.tierName} (−{program.currentTier.discountPercent}%)
                </Tag>
              ) : (
                <Tag>Chưa có hạng</Tag>
              )}
              {program.nextTier ? (
                <>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Còn {formatPoints(program.nextTier.minPoints - program.pointsBalance)} điểm đến{' '}
                    {program.nextTier.tierName}
                  </Typography.Text>
                  <Progress percent={tierProgress} size="small" showInfo={false} />
                </>
              ) : null}
            </Space>
          </Card>
        );
      })}

      <Card size="small" title="Lịch sử điểm">
        <Table
          rowKey="id"
          size="small"
          loading={txLoading}
          columns={txColumns}
          dataSource={transactions}
          pagination={{
            current: txPage,
            pageSize: 10,
            total: txTotal,
            showSizeChanger: false,
            onChange: (page) => void loadTransactions(page),
          }}
        />
      </Card>
    </Space>
  );
}
