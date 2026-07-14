import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Alert, Button, Space, Spin, Table, Tag, Typography, message } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchLossCashVarianceToday, type LossCashVarianceToday } from '@/shared/api/success.api';
import { formatDisplayMoney } from '@/shared/utils/money';

export function LossCashVariancePage() {
  const { t } = useTranslation('success');
  const [data, setData] = useState<LossCashVarianceToday | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await fetchLossCashVarianceToday());
    } catch (error) {
      message.error(apiErrorMessage(error, t('loss.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading && !data) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
        <Spin size="large" />
      </div>
    );
  }

  return (
    <div className="dashboard-page">
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>
            {t('loss.title')}
          </Typography.Title>
          <Typography.Text type="secondary">
            {t('loss.subtitle', { date: data?.businessDate ?? '—' })}
          </Typography.Text>
        </div>
        <Space>
          <Link to="/success/cockpit">{t('loss.backCockpit')}</Link>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {t('refresh')}
          </Button>
        </Space>
      </div>

      <Alert
        type="info"
        showIcon
        style={{ marginTop: 16 }}
        message={t('loss.tip', { threshold: formatDisplayMoney(data?.threshold ?? 0) })}
      />

      <Space wrap style={{ marginTop: 16 }}>
        <Tag>
          {t('loss.closedCount')}: {data?.closedShiftCount ?? 0}
        </Tag>
        <Tag>
          {t('loss.openCount')}: {data?.openShiftCount ?? 0}
        </Tag>
        <Tag color={(data?.alertCount ?? 0) > 0 ? 'error' : 'success'}>
          {t('loss.alertCount')}: {data?.alertCount ?? 0}
        </Tag>
        <Tag>
          {t('loss.maxAbs')}: {formatDisplayMoney(data?.maxAbsVariance ?? 0)}
        </Tag>
      </Space>

      <Table
        style={{ marginTop: 16 }}
        rowKey="shiftId"
        loading={loading}
        dataSource={data?.shifts ?? []}
        pagination={false}
        columns={[
          {
            title: t('loss.col.shift'),
            dataIndex: 'shiftNumber',
            render: (v: string, row) => (
              <Space>
                <span>{v}</span>
                {row.isAlert ? <Tag color="error">{t('loss.alert')}</Tag> : null}
                {row.status === 'open' ? <Tag>{t('loss.open')}</Tag> : null}
              </Space>
            ),
          },
          { title: t('loss.col.branch'), dataIndex: 'branchName' },
          { title: t('loss.col.warehouse'), dataIndex: 'warehouseName' },
          {
            title: t('loss.col.expected'),
            dataIndex: 'expectedCash',
            align: 'right',
            render: (v: number | null | undefined) => (v == null ? '—' : formatDisplayMoney(v)),
          },
          {
            title: t('loss.col.closing'),
            dataIndex: 'closingCash',
            align: 'right',
            render: (v: number | null | undefined) => (v == null ? '—' : formatDisplayMoney(v)),
          },
          {
            title: t('loss.col.variance'),
            dataIndex: 'cashVariance',
            align: 'right',
            render: (v: number | null | undefined, row) => {
              if (row.status === 'open' || v == null) return '—';
              return (
                <Typography.Text type={row.isAlert ? 'danger' : undefined}>{formatDisplayMoney(v)}</Typography.Text>
              );
            },
          },
        ]}
      />

      <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>
        <Link to="/sales/shifts">{t('loss.linkShifts')}</Link>
      </Typography.Paragraph>
    </div>
  );
}
