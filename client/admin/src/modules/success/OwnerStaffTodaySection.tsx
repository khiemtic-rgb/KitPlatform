import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Card, Col, Empty, Row, Space, Spin, Tag, Typography } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchOwnerStaffToday,
  type OwnerStaffToday,
  type StaffChecklistKind,
} from '@/shared/api/success.api';
import { formatDisplayMoney } from '@/shared/utils/money';

function checklistTagColor(status: string) {
  if (status === 'completed') return 'success';
  if (status === 'in_progress') return 'processing';
  return 'warning';
}

function ChecklistLine({ label, kind }: { label: string; kind: StaffChecklistKind }) {
  const { t } = useTranslation('success');
  return (
    <div style={{ marginTop: 4 }}>
      <Space size={4} wrap>
        <Typography.Text>{label}:</Typography.Text>
        <Tag color={checklistTagColor(kind.status)}>
          {t(`staffToday.checklistStatus.${kind.status}`, { defaultValue: t('staffToday.checklistStatus.not_started') })}
        </Tag>
        {kind.missingRequiredCount > 0 ? (
          <Tag color="error">{t('staffToday.missingRequired', { count: kind.missingRequiredCount })}</Tag>
        ) : null}
      </Space>
      {kind.tickedByNames.length > 0 ? (
        <div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('staffToday.tickedBy', { names: kind.tickedByNames.join(', ') })}
          </Typography.Text>
        </div>
      ) : null}
    </div>
  );
}

export function OwnerStaffTodaySection() {
  const { t } = useTranslation('success');
  const [data, setData] = useState<OwnerStaffToday | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await fetchOwnerStaffToday());
      setError(null);
    } catch (err) {
      setError(apiErrorMessage(err, t('staffToday.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading && !data) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 24 }}>
        <Spin />
      </div>
    );
  }

  if (error && !data) {
    return <Typography.Text type="secondary">{error}</Typography.Text>;
  }

  const checklists = data?.checklists ?? [];
  const tasks = data?.tasksByAssignee ?? [];
  const cash = data?.cashVarianceByCloser7d ?? [];
  const withoutCredential = data?.employeesWithoutCredentialCount;

  return (
    <Row gutter={[12, 12]}>
      <Col xs={24} lg={12}>
        <Card size="small" title={t('staffToday.checklistTitle')} extra={<Link to="/success/shift-checklist">{t('staffToday.open')}</Link>}>
          {checklists.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('staffToday.noBranches')} />
          ) : (
            checklists.map((b) => (
              <div key={b.branchId} style={{ marginBottom: 8 }}>
                <Typography.Text strong>{b.branchName}</Typography.Text>
                <ChecklistLine label={t('staffToday.openShift')} kind={b.open} />
                <ChecklistLine label={t('staffToday.closeShift')} kind={b.close} />
              </div>
            ))
          )}
        </Card>
      </Col>
      <Col xs={24} lg={12}>
        <Card size="small" title={t('staffToday.tasksTitle')}>
          {tasks.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('staffToday.noTasks')} />
          ) : (
            tasks.map((a) => (
              <div key={a.employeeId} style={{ display: 'flex', justifyContent: 'space-between', gap: 8, marginBottom: 4 }}>
                <Typography.Text>{a.assigneeName}</Typography.Text>
                <Space size={4}>
                  <Tag>{t('staffToday.openTasks', { count: a.openCount })}</Tag>
                  {a.overdueCount > 0 ? (
                    <Tag color="red">{t('staffToday.overdueTasks', { count: a.overdueCount })}</Tag>
                  ) : null}
                </Space>
              </div>
            ))
          )}
        </Card>
      </Col>
      <Col xs={24} lg={12}>
        <Card size="small" title={t('staffToday.cashTitle')} extra={<Link to="/success/loss">{t('staffToday.open')}</Link>}>
          {cash.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('staffToday.noCash')} />
          ) : (
            cash.map((c) => (
              <div
                key={c.closerUserId ?? c.closerName}
                style={{ display: 'flex', justifyContent: 'space-between', gap: 8, marginBottom: 4 }}
              >
                <Typography.Text>{c.closerName}</Typography.Text>
                <Typography.Text type={c.absVarianceTotal > 0 ? 'danger' : undefined}>
                  {t('staffToday.cashLine', { count: c.shiftCount, amount: formatDisplayMoney(c.absVarianceTotal) })}
                </Typography.Text>
              </div>
            ))
          )}
        </Card>
      </Col>
      <Col xs={24} lg={12}>
        <Card size="small" title={t('staffToday.learningTitle')}>
          {withoutCredential == null ? (
            <Typography.Text type="secondary">{t('staffToday.learningUnavailable')}</Typography.Text>
          ) : (
            <Space direction="vertical" size={0}>
              <Typography.Text style={{ fontSize: 20 }} type={withoutCredential > 0 ? 'warning' : undefined}>
                {withoutCredential} / {data?.activeEmployeeCount ?? 0}
              </Typography.Text>
              <Typography.Text type="secondary">{t('staffToday.learningHint')}</Typography.Text>
            </Space>
          )}
        </Card>
      </Col>
    </Row>
  );
}
