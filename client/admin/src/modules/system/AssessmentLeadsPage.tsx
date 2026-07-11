import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  App,
  Button,
  Card,
  Descriptions,
  Drawer,
  Dropdown,
  Popconfirm,
  Segmented,
  Select,
  Space,
  Spin,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import type { MenuProps } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  DownOutlined,
  EyeInvisibleOutlined,
  EyeOutlined,
  FilePdfOutlined,
  ReloadOutlined,
  UndoOutlined,
} from '@ant-design/icons';
import {
  archiveAssessmentSubmission,
  canViewAssessmentReport,
  fetchAssessmentReportPdf,
  fetchAssessmentSubmissionDetail,
  fetchAssessmentSubmissions,
  scoreTo100,
  unarchiveAssessmentSubmission,
  updateAssessmentLeadPipeline,
  type AssessmentSubmissionDetail,
  type AssessmentSubmissionListItem,
  type KapReportPdfKind,
} from '@/shared/api/assessment-admin.api';
import { fetchKapPartners, type KapPartnerListItem } from '@/shared/api/kap-admin.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatDisplayDateTime } from '@/shared/utils/date';
import {
  KAP_COMMISSION_OPTIONS,
  KAP_PIPELINE_OPTIONS,
} from '@/modules/kap/kap-labels';

const { Text } = Typography;

type LeadFilter = 'all' | 'withLead';
type ArchiveFilter = 'active' | 'archived';

const STATUS_COLORS: Record<string, string> = {
  draft: 'default',
  completed: 'blue',
  lead_captured: 'green',
  report_ready: 'cyan',
};

const BLOCKED_COMMISSION = new Set(['pending', 'approved', 'paid']);

export function AssessmentLeadsPage() {
  const { t } = useTranslation('system', { keyPrefix: 'assessmentLeads' });
  const { message } = App.useApp();
  const [leadFilter, setLeadFilter] = useState<LeadFilter>('all');
  const [archiveFilter, setArchiveFilter] = useState<ArchiveFilter>('active');
  const [partnerId, setPartnerId] = useState<string | undefined>();
  const [pipelineFilter, setPipelineFilter] = useState<string | undefined>();
  const [partners, setPartners] = useState<KapPartnerListItem[]>([]);
  const [items, setItems] = useState<AssessmentSubmissionListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [loading, setLoading] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detail, setDetail] = useState<AssessmentSubmissionDetail | null>(null);
  const [reportLoadingId, setReportLoadingId] = useState<string | null>(null);
  const [archivingId, setArchivingId] = useState<string | null>(null);
  const [savingFieldId, setSavingFieldId] = useState<string | null>(null);

  const pipelineOptions = useMemo(
    () =>
      KAP_PIPELINE_OPTIONS.map((o) => ({
        value: o.value,
        label: t(`pipeline.${o.value}`, o.label),
      })),
    [t],
  );

  const commissionOptions = useMemo(
    () =>
      KAP_COMMISSION_OPTIONS.map((o) => ({
        value: o.value,
        label: t(`commission.${o.value}`, o.label),
      })),
    [t],
  );

  const openReportPdf = useCallback(
    async (id: string, kind: KapReportPdfKind = 'consulting') => {
      setReportLoadingId(id);
      try {
        const blob = await fetchAssessmentReportPdf(id, kind);
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank', 'noopener,noreferrer');
        window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
      } catch (error) {
        message.error(apiErrorMessage(error, t('messages.reportFailed')));
      } finally {
        setReportLoadingId(null);
      }
    },
    [t, message],
  );

  const reportMenuItems: MenuProps['items'] = useMemo(
    () => [
      { key: 'consulting', label: t('actions.reportConsulting') },
      { key: 'executive', label: t('actions.reportExecutive') },
      { key: 'appendix', label: t('actions.reportAppendix') },
    ],
    [t],
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await fetchAssessmentSubmissions({
        page,
        pageSize,
        hasLead: leadFilter === 'withLead' ? true : undefined,
        partnerId,
        leadPipelineStatus: pipelineFilter,
        archived: archiveFilter === 'archived',
      });
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [leadFilter, archiveFilter, partnerId, pipelineFilter, page, pageSize, t, message]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    void fetchKapPartners()
      .then(setPartners)
      .catch(() => setPartners([]));
  }, []);

  const patchItem = useCallback((id: string, patch: Partial<AssessmentSubmissionListItem>) => {
    setItems((prev) => prev.map((row) => (row.id === id ? { ...row, ...patch } : row)));
  }, []);

  const handlePipelineChange = useCallback(
    async (row: AssessmentSubmissionListItem, next: string) => {
      const prev = row.leadPipelineStatus || 'new';
      patchItem(row.id, { leadPipelineStatus: next });
      setSavingFieldId(row.id);
      try {
        await updateAssessmentLeadPipeline(row.id, {
          leadPipelineStatus: next,
          commissionStatus: row.commissionStatus,
        });
        message.success(t('messages.pipelineUpdated'));
      } catch (error) {
        patchItem(row.id, { leadPipelineStatus: prev });
        message.error(apiErrorMessage(error, t('messages.pipelineFailed')));
      } finally {
        setSavingFieldId(null);
      }
    },
    [message, patchItem, t],
  );

  const handleCommissionChange = useCallback(
    async (row: AssessmentSubmissionListItem, next: string) => {
      const prev = row.commissionStatus || 'none';
      patchItem(row.id, { commissionStatus: next });
      setSavingFieldId(row.id);
      try {
        await updateAssessmentLeadPipeline(row.id, {
          leadPipelineStatus: row.leadPipelineStatus || 'new',
          commissionStatus: next,
        });
        message.success(t('messages.commissionUpdated'));
      } catch (error) {
        patchItem(row.id, { commissionStatus: prev });
        message.error(apiErrorMessage(error, t('messages.commissionFailed')));
      } finally {
        setSavingFieldId(null);
      }
    },
    [message, patchItem, t],
  );

  const handleArchive = useCallback(
    async (row: AssessmentSubmissionListItem) => {
      const commission = (row.commissionStatus || 'none').toLowerCase();
      if (BLOCKED_COMMISSION.has(commission)) {
        message.warning(t('archive.blockedCommission'));
        return;
      }
      setArchivingId(row.id);
      try {
        await archiveAssessmentSubmission(row.id);
        message.success(t('messages.archived'));
        setItems((prev) => prev.filter((x) => x.id !== row.id));
        setTotal((n) => Math.max(0, n - 1));
      } catch (error) {
        message.error(apiErrorMessage(error, t('messages.archiveFailed')));
      } finally {
        setArchivingId(null);
      }
    },
    [message, t],
  );

  const handleUnarchive = useCallback(
    async (row: AssessmentSubmissionListItem) => {
      setArchivingId(row.id);
      try {
        await unarchiveAssessmentSubmission(row.id);
        message.success(t('messages.unarchived'));
        setItems((prev) => prev.filter((x) => x.id !== row.id));
        setTotal((n) => Math.max(0, n - 1));
      } catch (error) {
        message.error(apiErrorMessage(error, t('messages.unarchiveFailed')));
      } finally {
        setArchivingId(null);
      }
    },
    [message, t],
  );

  const openDetail = useCallback(
    async (id: string) => {
      setDetailOpen(true);
      setDetailLoading(true);
      setDetail(null);
      try {
        setDetail(await fetchAssessmentSubmissionDetail(id));
      } catch (error) {
        message.error(apiErrorMessage(error, t('messages.detailFailed')));
        setDetailOpen(false);
      } finally {
        setDetailLoading(false);
      }
    },
    [t, message],
  );

  const columns: ColumnsType<AssessmentSubmissionListItem> = useMemo(
    () => [
      {
        title: t('columns.startedAt'),
        dataIndex: 'startedAt',
        width: 148,
        fixed: 'left',
        render: (v: string) => formatDisplayDateTime(v),
      },
      {
        title: t('columns.status'),
        dataIndex: 'status',
        width: 118,
        render: (status: string) => (
          <Tag color={STATUS_COLORS[status] ?? 'default'} style={{ marginInlineEnd: 0 }}>
            {t(`status.${status}`, status)}
          </Tag>
        ),
      },
      {
        title: t('columns.org'),
        dataIndex: 'respondentOrgName',
        width: 180,
        ellipsis: true,
        render: (v: string | null, row) =>
          v ?? (row.respondentPhone ? '—' : <Text type="secondary">{t('noLead')}</Text>),
      },
      {
        title: t('columns.contact'),
        key: 'contact',
        width: 150,
        ellipsis: true,
        render: (_, row) =>
          row.respondentPhone ? (
            <div style={{ lineHeight: 1.35, minWidth: 0 }}>
              <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {row.respondentName ?? '—'}
              </div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {row.respondentPhone}
              </Typography.Text>
            </div>
          ) : (
            <Typography.Text type="secondary">—</Typography.Text>
          ),
      },
      {
        title: t('columns.score'),
        key: 'score',
        width: 72,
        align: 'right',
        render: (_, row) => {
          const s = scoreTo100(row.overallPct);
          return s != null ? <strong>{s}</strong> : '—';
        },
      },
      {
        title: t('columns.progress'),
        key: 'progress',
        width: 72,
        align: 'center',
        render: (_, row) => String(row.responseCount),
      },
      {
        title: t('columns.partner'),
        dataIndex: 'partnerCode',
        width: 88,
        ellipsis: true,
        render: (v: string | null | undefined) => v || '—',
      },
      {
        title: t('columns.pipeline'),
        dataIndex: 'leadPipelineStatus',
        width: 148,
        render: (v: string | undefined, row) => (
          <Select
            size="small"
            style={{ width: '100%' }}
            value={v || 'new'}
            options={pipelineOptions}
            loading={savingFieldId === row.id}
            popupMatchSelectWidth={180}
            onChange={(next) => void handlePipelineChange(row, next)}
          />
        ),
      },
      {
        title: t('columns.commission'),
        dataIndex: 'commissionStatus',
        width: 140,
        render: (v: string | undefined, row) => (
          <Select
            size="small"
            style={{ width: '100%' }}
            value={v || 'none'}
            options={commissionOptions}
            loading={savingFieldId === row.id}
            popupMatchSelectWidth={180}
            onChange={(next) => void handleCommissionChange(row, next)}
          />
        ),
      },
      {
        title: t('columns.actions'),
        key: 'actions',
        width: 200,
        fixed: 'right',
        render: (_, row) => {
          const commission = (row.commissionStatus || 'none').toLowerCase();
          const archiveBlocked = BLOCKED_COMMISSION.has(commission);
          const isArchived = archiveFilter === 'archived' || Boolean(row.archivedAt);
          const moreItems: MenuProps['items'] = canViewAssessmentReport(row.status)
            ? reportMenuItems
            : [];

          return (
            <Space size={0} wrap={false} style={{ whiteSpace: 'nowrap' }}>
              <Tooltip title={t('actions.detail')}>
                <Button
                  type="link"
                  size="small"
                  icon={<EyeOutlined />}
                  onClick={() => openDetail(row.id)}
                >
                  {t('actions.detail')}
                </Button>
              </Tooltip>
              {canViewAssessmentReport(row.status) && (
                <Dropdown
                  menu={{
                    items: moreItems,
                    onClick: ({ key }) => void openReportPdf(row.id, key as KapReportPdfKind),
                  }}
                >
                  <Button
                    type="link"
                    size="small"
                    icon={<FilePdfOutlined />}
                    loading={reportLoadingId === row.id}
                  >
                    {t('actions.viewReport')} <DownOutlined />
                  </Button>
                </Dropdown>
              )}
              {isArchived ? (
                <Popconfirm
                  title={t('archive.unarchiveTitle')}
                  description={t('archive.unarchiveBody')}
                  okText={t('archive.unarchiveOk')}
                  cancelText={t('archive.cancel')}
                  onConfirm={() => handleUnarchive(row)}
                >
                  <Button
                    type="link"
                    size="small"
                    icon={<UndoOutlined />}
                    loading={archivingId === row.id}
                  >
                    {t('actions.unarchive')}
                  </Button>
                </Popconfirm>
              ) : archiveBlocked ? (
                <Tooltip title={t('archive.blockedCommission')}>
                  <Button type="link" size="small" danger disabled icon={<EyeInvisibleOutlined />}>
                    {t('actions.archive')}
                  </Button>
                </Tooltip>
              ) : (
                <Popconfirm
                  title={t('archive.confirmTitle')}
                  description={t('archive.confirmBody')}
                  okText={t('archive.ok')}
                  okButtonProps={{ danger: true }}
                  cancelText={t('archive.cancel')}
                  onConfirm={() => handleArchive(row)}
                >
                  <Button
                    type="link"
                    size="small"
                    danger
                    icon={<EyeInvisibleOutlined />}
                    loading={archivingId === row.id}
                  >
                    {t('actions.archive')}
                  </Button>
                </Popconfirm>
              )}
            </Space>
          );
        },
      },
    ],
    [
      t,
      openDetail,
      openReportPdf,
      reportLoadingId,
      reportMenuItems,
      archiveFilter,
      archivingId,
      savingFieldId,
      pipelineOptions,
      commissionOptions,
      handlePipelineChange,
      handleCommissionChange,
      handleArchive,
      handleUnarchive,
    ],
  );

  return (
    <>
      <Card
        title={t('title')}
        extra={
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {t('refresh')}
          </Button>
        }
      >
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
          <Space wrap size="middle">
            <Segmented
              value={leadFilter}
              onChange={(v) => {
                setPage(1);
                setLeadFilter(v as LeadFilter);
              }}
              options={[
                { label: t('filters.all'), value: 'all' },
                { label: t('filters.withLead'), value: 'withLead' },
              ]}
            />
            <Segmented
              value={archiveFilter}
              onChange={(v) => {
                setPage(1);
                setArchiveFilter(v as ArchiveFilter);
              }}
              options={[
                { label: t('filters.visible'), value: 'active' },
                { label: t('filters.archived'), value: 'archived' },
              ]}
            />
            <Select
              allowClear
              placeholder={t('filters.partner')}
              style={{ minWidth: 200 }}
              value={partnerId}
              options={partners.map((p) => ({
                value: p.id,
                label: `${p.code} — ${p.name}`,
              }))}
              onChange={(v) => {
                setPage(1);
                setPartnerId(v);
              }}
              showSearch
              optionFilterProp="label"
            />
            <Select
              allowClear
              placeholder={t('filters.pipeline')}
              style={{ minWidth: 180 }}
              value={pipelineFilter}
              options={pipelineOptions}
              onChange={(v) => {
                setPage(1);
                setPipelineFilter(v);
              }}
            />
          </Space>
          <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
            {t('hint')}
          </Typography.Paragraph>
          <Table
            rowKey="id"
            size="middle"
            loading={loading}
            columns={columns}
            dataSource={items}
            scroll={{ x: 1320 }}
            pagination={{
              current: page,
              pageSize,
              total,
              showSizeChanger: true,
              onChange: (p, ps) => {
                setPage(p);
                setPageSize(ps);
              },
            }}
          />
        </Space>
      </Card>

      <Drawer
        title={t('detailTitle')}
        width={560}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        extra={
          detail && canViewAssessmentReport(detail.status) ? (
            <Dropdown.Button
              type="primary"
              loading={reportLoadingId === detail.id}
              icon={<DownOutlined />}
              menu={{
                items: reportMenuItems,
                onClick: ({ key }) => void openReportPdf(detail.id, key as KapReportPdfKind),
              }}
              onClick={() => void openReportPdf(detail.id, 'consulting')}
            >
              {t('actions.viewReport')}
            </Dropdown.Button>
          ) : null
        }
      >
        {detailLoading ? (
          <Spin style={{ display: 'block', margin: '48px auto' }} />
        ) : (
          detail && (
            <Space direction="vertical" size="large" style={{ width: '100%' }}>
              <Descriptions column={1} size="small" bordered>
                <Descriptions.Item label={t('columns.status')}>
                  <Tag color={STATUS_COLORS[detail.status] ?? 'default'}>
                    {t(`status.${detail.status}`, detail.status)}
                  </Tag>
                </Descriptions.Item>
                <Descriptions.Item label={t('detail.template')}>
                  {detail.templateCode} v{detail.templateVersion}
                </Descriptions.Item>
                <Descriptions.Item label={t('columns.startedAt')}>
                  {formatDisplayDateTime(detail.startedAt)}
                </Descriptions.Item>
                {detail.completedAt && (
                  <Descriptions.Item label={t('detail.completedAt')}>
                    {formatDisplayDateTime(detail.completedAt)}
                  </Descriptions.Item>
                )}
                <Descriptions.Item label={t('columns.score')}>
                  {scoreTo100(detail.overallPct) != null
                    ? `${scoreTo100(detail.overallPct)}/100`
                    : '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('columns.progress')}>
                  {detail.responseCount}/{detail.requiredCount} {t('detail.answers')}
                </Descriptions.Item>
              </Descriptions>

              {detail.respondentPhone && (
                <>
                  <Typography.Title level={5}>{t('detail.leadSection')}</Typography.Title>
                  <Descriptions column={1} size="small" bordered>
                    <Descriptions.Item label={t('detail.org')}>
                      {detail.respondentOrgName ?? '—'}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('detail.name')}>
                      {detail.respondentName ?? '—'}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('detail.phone')}>
                      {detail.respondentPhone}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('detail.email')}>
                      {detail.respondentEmail ?? '—'}
                    </Descriptions.Item>
                    {detail.respondentNote && (
                      <Descriptions.Item label={t('detail.note')}>
                        {detail.respondentNote}
                      </Descriptions.Item>
                    )}
                    <Descriptions.Item label={t('detail.consent')}>
                      {detail.consentMarketing ? t('detail.consentYes') : t('detail.consentNo')}
                    </Descriptions.Item>
                  </Descriptions>
                </>
              )}

              {detail.categoryScores.length > 0 && (
                <>
                  <Typography.Title level={5}>{t('detail.scores')}</Typography.Title>
                  {detail.categoryScores.map((c) => (
                    <div key={c.code} style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <span>{c.name}</span>
                      <strong>{scoreTo100(c.scorePct)}/100</strong>
                    </div>
                  ))}
                </>
              )}

              {detail.insights.length > 0 && (
                <>
                  <Typography.Title level={5}>{t('detail.insights')}</Typography.Title>
                  {detail.insights.map((i) => (
                    <Card key={i.title} size="small" type="inner">
                      <Text strong>{i.title}</Text>
                      <div>{i.body}</div>
                    </Card>
                  ))}
                </>
              )}

              {detail.recommendations.length > 0 && (
                <>
                  <Typography.Title level={5}>{t('detail.recommendations')}</Typography.Title>
                  {detail.recommendations.map((r) => (
                    <Card key={r.title} size="small" type="inner">
                      <Text strong>{r.title}</Text>
                      <div>{r.body}</div>
                    </Card>
                  ))}
                </>
              )}
            </Space>
          )
        )}
      </Drawer>
    </>
  );
}
