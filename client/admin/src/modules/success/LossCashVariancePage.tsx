import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Button,
  DatePicker,
  Select,
  Space,
  Spin,
  Table,
  Tabs,
  Tag,
  Typography,
  message,
} from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchBranches, fetchUsers } from '@/shared/api/identity-admin.api';
import type { BranchListItem } from '@/shared/api/identity-admin.types';
import {
  fetchLossAuditFeed,
  fetchLossCashVarianceToday,
  fetchLossEmployeeReports,
  type LossAuditFeed,
  type LossCashVarianceToday,
  type LossEmployeeReports,
} from '@/shared/api/success.api';
import { formatDisplayMoney } from '@/shared/utils/money';

const AUDIT_EVENT_TYPES = [
  'order_create',
  'order_edit',
  'order_cancel',
  'discount',
  'return',
  'stock_adjust',
  'internal_issue',
] as const;

export function LossCashVariancePage() {
  const { t } = useTranslation('success');
  const [tab, setTab] = useState('cash');
  const [cash, setCash] = useState<LossCashVarianceToday | null>(null);
  const [reports, setReports] = useState<LossEmployeeReports | null>(null);
  const [audit, setAudit] = useState<LossAuditFeed | null>(null);
  const [branches, setBranches] = useState<BranchListItem[]>([]);
  const [users, setUsers] = useState<{ id: string; username: string }[]>([]);
  const [branchId, setBranchId] = useState<string | undefined>();
  const [userId, setUserId] = useState<string | undefined>();
  const [eventType, setEventType] = useState<string | undefined>();
  const [range, setRange] = useState<[Dayjs, Dayjs] | null>(null);
  const [auditPage, setAuditPage] = useState(1);
  const [loading, setLoading] = useState(true);

  const rangeParams = useCallback(() => {
    if (range == null) return {};
    return {
      from: range[0].startOf('day').toISOString(),
      to: range[1].add(1, 'day').startOf('day').toISOString(),
    };
  }, [range]);

  const loadCash = useCallback(async () => {
    setCash(await fetchLossCashVarianceToday());
  }, []);

  const loadReports = useCallback(async () => {
    setReports(await fetchLossEmployeeReports({ ...rangeParams(), branchId }));
  }, [branchId, rangeParams]);

  const loadAudit = useCallback(async () => {
    setAudit(
      await fetchLossAuditFeed({
        ...rangeParams(),
        branchId,
        userId,
        eventType,
        page: auditPage,
        pageSize: 50,
      }),
    );
  }, [auditPage, branchId, eventType, rangeParams, userId]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      await Promise.all([
        loadCash(),
        loadReports(),
        loadAudit(),
        fetchBranches()
          .then(setBranches)
          .catch(() => setBranches([])),
        fetchUsers({ page: 1, pageSize: 200 })
          .then((res) =>
            setUsers(
              (res.items ?? []).map((u) => ({
                id: u.id,
                username: u.username,
              })),
            ),
          )
          .catch(() => setUsers([])),
      ]);
    } catch (error) {
      message.error(apiErrorMessage(error, t('loss.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [loadAudit, loadCash, loadReports, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const reportRangeLabel = useMemo(() => {
    if (!reports) return '—';
    const from = dayjs(reports.fromUtc).format('DD/MM/YYYY');
    const to = dayjs(reports.toUtc).subtract(1, 'millisecond').format('DD/MM/YYYY');
    return `${from} – ${to}`;
  }, [reports]);

  const filterBar = (
    <Space wrap style={{ marginTop: 16 }}>
      <DatePicker.RangePicker value={range} onChange={(v) => setRange(v as [Dayjs, Dayjs] | null)} allowClear />
      <Select
        allowClear
        placeholder={t('loss.branchAll')}
        style={{ minWidth: 220 }}
        value={branchId}
        options={branches.map((b) => ({
          value: b.id,
          label: b.branchCode ? `${b.branchName} (${b.branchCode})` : b.branchName,
        }))}
        onChange={(v) => setBranchId(v)}
      />
      {tab === 'audit' ? (
        <>
          <Select
            allowClear
            placeholder={t('loss.eventAll')}
            style={{ minWidth: 180 }}
            value={eventType}
            options={AUDIT_EVENT_TYPES.map((k) => ({
              value: k,
              label: t(`loss.events.${k}`),
            }))}
            onChange={(v) => {
              setEventType(v);
              setAuditPage(1);
            }}
          />
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('loss.userAll')}
            style={{ minWidth: 180 }}
            value={userId}
            options={users.map((u) => ({ value: u.id, label: u.username }))}
            onChange={(v) => {
              setUserId(v);
              setAuditPage(1);
            }}
          />
        </>
      ) : null}
      <Button
        type="primary"
        onClick={() => {
          void (async () => {
            setLoading(true);
            try {
              if (tab === 'audit') await loadAudit();
              else await loadReports();
            } catch (error) {
              message.error(apiErrorMessage(error, t('loss.loadFailed')));
            } finally {
              setLoading(false);
            }
          })();
        }}
      >
        {t('loss.applyFilter')}
      </Button>
      <Tag>{reportRangeLabel}</Tag>
    </Space>
  );

  if (loading && !cash && !reports && !audit) {
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
          <Typography.Text type="secondary">{t('loss.pageSubtitle')}</Typography.Text>
        </div>
        <Space>
          <Link to="/success/cockpit">{t('loss.backCockpit')}</Link>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {t('refresh')}
          </Button>
        </Space>
      </div>

      <Tabs
        style={{ marginTop: 16 }}
        activeKey={tab}
        onChange={setTab}
        items={[
          {
            key: 'cash',
            label: t('loss.tabs.cash'),
            children: (
              <>
                <Alert
                  type="info"
                  showIcon
                  message={t('loss.tip', { threshold: formatDisplayMoney(cash?.threshold ?? 0) })}
                />
                <Space wrap style={{ marginTop: 16 }}>
                  <Tag>
                    {t('loss.closedCount')}: {cash?.closedShiftCount ?? 0}
                  </Tag>
                  <Tag>
                    {t('loss.openCount')}: {cash?.openShiftCount ?? 0}
                  </Tag>
                  <Tag color={(cash?.alertCount ?? 0) > 0 ? 'error' : 'success'}>
                    {t('loss.alertCount')}: {cash?.alertCount ?? 0}
                  </Tag>
                  <Tag>
                    {t('loss.maxAbs')}: {formatDisplayMoney(cash?.maxAbsVariance ?? 0)}
                  </Tag>
                </Space>
                <Table
                  style={{ marginTop: 16 }}
                  rowKey="shiftId"
                  loading={loading}
                  dataSource={cash?.shifts ?? []}
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
                          <Typography.Text type={row.isAlert ? 'danger' : undefined}>
                            {formatDisplayMoney(v)}
                          </Typography.Text>
                        );
                      },
                    },
                  ]}
                />
                <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>
                  <Link to="/sales/shifts">{t('loss.linkShifts')}</Link>
                </Typography.Paragraph>
              </>
            ),
          },
          {
            key: 'audit',
            label: t('loss.tabs.audit'),
            children: (
              <>
                <Alert type="info" showIcon message={audit?.attributionNotes ?? t('loss.auditTip')} />
                {filterBar}
                <Table
                  style={{ marginTop: 16 }}
                  rowKey="id"
                  loading={loading}
                  dataSource={audit?.items ?? []}
                  pagination={{
                    current: audit?.page ?? auditPage,
                    pageSize: audit?.pageSize ?? 50,
                    total: audit?.total ?? 0,
                    showSizeChanger: false,
                    onChange: (p) => {
                      setAuditPage(p);
                      void (async () => {
                        setLoading(true);
                        try {
                          setAudit(
                            await fetchLossAuditFeed({
                              ...rangeParams(),
                              branchId,
                              userId,
                              eventType,
                              page: p,
                              pageSize: 50,
                            }),
                          );
                        } catch (error) {
                          message.error(apiErrorMessage(error, t('loss.loadFailed')));
                        } finally {
                          setLoading(false);
                        }
                      })();
                    },
                  }}
                  columns={[
                    {
                      title: t('loss.col.time'),
                      dataIndex: 'occurredAt',
                      width: 160,
                      render: (v: string) => dayjs(v).format('DD/MM/YYYY HH:mm'),
                    },
                    {
                      title: t('loss.col.event'),
                      dataIndex: 'eventType',
                      width: 140,
                      render: (v: string) => t(`loss.events.${v}`, { defaultValue: v }),
                    },
                    {
                      title: t('loss.col.actor'),
                      dataIndex: 'actorUsername',
                      render: (v: string | null | undefined) => v || '—',
                    },
                    { title: t('loss.col.summary'), dataIndex: 'summary' },
                    {
                      title: t('loss.col.branch'),
                      dataIndex: 'branchName',
                      render: (v: string | null | undefined) => v || '—',
                    },
                    {
                      title: t('loss.col.document'),
                      key: 'doc',
                      render: (_, row) =>
                        row.documentHref ? (
                          <Link to={row.documentHref}>{row.documentNumber || t('loss.openDoc')}</Link>
                        ) : (
                          row.documentNumber || '—'
                        ),
                    },
                  ]}
                />
              </>
            ),
          },
          {
            key: 'by-employee',
            label: t('loss.tabs.byEmployee'),
            children: (
              <>
                <Alert type="info" showIcon message={reports?.attributionNotes ?? t('loss.reportsTip')} />
                {filterBar}

                <Typography.Title level={5} style={{ marginTop: 24 }}>
                  {t('loss.reports.cancellations')}
                </Typography.Title>
                <Table
                  rowKey={(r) => r.employeeId ?? r.employeeName}
                  loading={loading}
                  dataSource={reports?.cancellations ?? []}
                  pagination={false}
                  columns={[
                    { title: t('loss.col.employee'), dataIndex: 'employeeName' },
                    { title: t('loss.col.cancelCount'), dataIndex: 'cancelCount', align: 'right' },
                    {
                      title: t('loss.col.cancelValue'),
                      dataIndex: 'cancelValue',
                      align: 'right',
                      render: (v: number) => formatDisplayMoney(v),
                    },
                  ]}
                />

                <Typography.Title level={5} style={{ marginTop: 24 }}>
                  {t('loss.reports.discounts')}
                </Typography.Title>
                <Table
                  rowKey={(r) => r.employeeId ?? r.employeeName}
                  loading={loading}
                  dataSource={reports?.discounts ?? []}
                  pagination={false}
                  columns={[
                    { title: t('loss.col.employee'), dataIndex: 'employeeName' },
                    { title: t('loss.col.orderCount'), dataIndex: 'orderCount', align: 'right' },
                    {
                      title: t('loss.col.orderDiscount'),
                      dataIndex: 'orderDiscountAmount',
                      align: 'right',
                      render: (v: number) => formatDisplayMoney(v),
                    },
                    {
                      title: t('loss.col.lineDiscount'),
                      dataIndex: 'lineDiscountAmount',
                      align: 'right',
                      render: (v: number) => formatDisplayMoney(v),
                    },
                    {
                      title: t('loss.col.totalDiscount'),
                      dataIndex: 'totalPosDiscount',
                      align: 'right',
                      render: (v: number) => formatDisplayMoney(v),
                    },
                  ]}
                />

                <Typography.Title level={5} style={{ marginTop: 24 }}>
                  {t('loss.reports.adjustments')}
                </Typography.Title>
                <Table
                  rowKey={(r) => r.employeeId ?? r.employeeName}
                  loading={loading}
                  dataSource={reports?.adjustments ?? []}
                  pagination={false}
                  columns={[
                    { title: t('loss.col.employee'), dataIndex: 'employeeName' },
                    { title: t('loss.col.adjustCount'), dataIndex: 'adjustmentCount', align: 'right' },
                    {
                      title: t('loss.col.absVariance'),
                      dataIndex: 'absVarianceValue',
                      align: 'right',
                      render: (v: number) => formatDisplayMoney(v),
                    },
                  ]}
                />
              </>
            ),
          },
        ]}
      />
    </div>
  );
}
