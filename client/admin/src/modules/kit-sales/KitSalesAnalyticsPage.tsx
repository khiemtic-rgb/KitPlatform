import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, Col, Row, Space, Typography, message } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchKitSalesDeskKpi,
  fetchKitSalesJourneyDue,
  fetchKitSalesLeads,
  fetchKitSalesOwnerKpi,
  fetchKitSalesPipelineSummary,
  type KitSalesDeskKpi,
  type KitSalesOwnerKpi,
  type KitSalesJourneyPlan,
  type KitSalesLead,
  type KitSalesPipelineSummary,
} from '@/shared/api/kit-sales.api';
import { KIT_SALES_PIPELINE_STATUSES } from './kit-sales.constants';
import { classifyKitSalesToday } from './kit-sales.helpers';

export function KitSalesAnalyticsPage() {
  const { t } = useTranslation('kitSales');
  const navigate = useNavigate();
  const [summary, setSummary] = useState<KitSalesPipelineSummary | null>(null);
  const [leads, setLeads] = useState<KitSalesLead[]>([]);
  const [due, setDue] = useState<KitSalesJourneyPlan[]>([]);
  const [owner, setOwner] = useState<KitSalesOwnerKpi | null>(null);
  const [desk, setDesk] = useState<KitSalesDeskKpi | null>(null);
  const [kpiNote, setKpiNote] = useState<string | null>(null);

  useEffect(() => {
    void Promise.all([
      fetchKitSalesPipelineSummary(),
      fetchKitSalesLeads({ limit: 200 }),
      fetchKitSalesJourneyDue(20).catch(() => [] as KitSalesJourneyPlan[]),
      fetchKitSalesOwnerKpi().then(
        (value) => ({ value, error: null as string | null }),
        (error) => ({ value: null, error: apiErrorMessage(error, 'KPI chưa đọc được.') }),
      ),
      fetchKitSalesDeskKpi().then(
        (value) => value,
        () => null,
      ),
    ])
      .then(([pipeline, rows, journeys, ownerKpi, deskKpi]) => {
        setSummary(pipeline);
        setLeads(rows);
        setDue(journeys);
        setOwner(ownerKpi.value);
        setDesk(deskKpi);
        setKpiNote(ownerKpi.error);
      })
      .catch((error) => message.error(apiErrorMessage(error, t('create.failed'))));
  }, [t]);

  const today = useMemo(() => classifyKitSalesToday(leads, due), [due, leads]);
  const buckets = useMemo(() => {
    const counts = Object.fromEntries((summary?.byStatus ?? []).map((b) => [b.status, b.count]));
    return KIT_SALES_PIPELINE_STATUSES.map((status) => ({ status, count: counts[status] ?? 0 }));
  }, [summary]);
  const demoCount = buckets.find((row) => row.status === 'demo')?.count ?? 0;

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {t('analytics.title')}
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('analytics.blurb')}
        </Typography.Paragraph>
      </div>

      <Typography.Text>
        {t('workspace.strip', {
          due: today.due.length,
          followup: today.followup.length,
          phc: today.phc.length,
          demo: demoCount,
        })}
      </Typography.Text>

      {kpiNote && (
        <Typography.Text type="danger">KPI cohort chưa hiện: {kpiNote}</Typography.Text>
      )}
      {desk && (
        <Card size="small" title="Việc của nhân viên">
          <Typography.Text>
            Đến hạn {desk.tasksDueToday} · Draft {desk.draftsWaiting} · Chưa quyết định {desk.leadsWithoutDecision} · Chờ PHC {desk.phcPending} · Khung vắng khách {desk.nextQuietWindow}
          </Typography.Text>
        </Card>
      )}
      {owner && (
        <Card size="small" title="KPI chủ KIT">
          <Typography.Paragraph>
            Cohort đủ tuổi {owner.eligible}. Chưa đến hạn {owner.notYetMature}. Quyết định qualification{' '}
            {owner.qualificationDecisionRate ?? '—'}. Đã xử lý (quyết định hoặc lost) {owner.closureRate ?? '—'}. Lost{' '}
            {owner.lostDispositions}. Người xác nhận paid {owner.humanPaid}. Doanh thu đối soát:             {owner.revenueLabel}. Tin chưa duyệt {owner.unapprovedSends}. Chi phí AI {owner.aiCost}. Dwell đang mở{' '}
            {owner.openDwellLeads} lead.
          </Typography.Paragraph>
          <Typography.Paragraph>
            Facebook trong kỳ: đã gửi {owner.outboundSent} · không rõ {owner.outboundUnknown} · lỗi{' '}
            {owner.outboundFailed} · bị chặn {owner.outboundBlocked}.
          </Typography.Paragraph>
          {owner.stageRates.map((row) => (
            <div key={`${row.fromStatus}-${row.toStatus}`}>
              {row.fromStatus} → {row.toStatus}: {row.reached}/{row.cohort} {row.label}
              {row.rate == null ? '' : ` (${row.rate})`}
              {' · '}
              {row.flowLabel} {row.flowReached}/{row.flowCohort}
              {row.flowRate == null ? '' : ` (${row.flowRate})`}
            </div>
          ))}
        </Card>
      )}

      <Card size="small" title={t('analytics.funnel')}>
        <Row gutter={[12, 12]}>
          {buckets.map((bucket) => (
            <Col key={bucket.status} xs={12} sm={8} md={6}>
              <Typography.Text type="secondary">
                {t(`status.${bucket.status}`, { defaultValue: bucket.status })}
              </Typography.Text>
              <div style={{ fontSize: 22, fontWeight: 600 }}>{bucket.count}</div>
            </Col>
          ))}
        </Row>
      </Card>

      <Button onClick={() => navigate('/kit-sales/intelligence')}>{t('analytics.openIntel')}</Button>
    </Space>
  );
}
