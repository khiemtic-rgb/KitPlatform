import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import {
  Alert,
  Button,
  Card,
  Col,
  Popconfirm,
  Progress,
  Row,
  Space,
  Statistic,
  Typography,
  message,
} from 'antd';
import {
  CheckCircleOutlined,
  DownloadOutlined,
  ReloadOutlined,
  UnorderedListOutlined,
  UserSwitchOutlined,
} from '@ant-design/icons';
import {
  bulkMarkPharmacyMembers,
  fetchAllCustomersForExport,
  fetchModeAReadiness,
} from '@/shared/api/customer-admin.api';
import type { CustomerModeAReadinessSummary } from '@/shared/api/customer-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { downloadCsv } from '@/shared/utils/download-csv';
import { useHasPermission } from '@/shared/auth/usePermission';

function emptySummary(): CustomerModeAReadinessSummary {
  return {
    prospect: 0,
    member: 0,
    revoked: 0,
    total: 0,
    hasAppAccount: 0,
    validVnMobile: 0,
    phoneNeedsFix: 0,
    duplicatePhoneGroups: 0,
    customersInDuplicateGroups: 0,
    modeAReady: 0,
    eligibleToPromote: 0,
  };
}

export function CustomerModeAReadinessCard() {
  const { t } = useTranslation('sales', { keyPrefix: 'receiptSettings.modeAReadiness' });
  const navigate = useNavigate();
  const canWrite = useHasPermission('sales.write');
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [promoting, setPromoting] = useState(false);
  const [summary, setSummary] = useState<CustomerModeAReadinessSummary>(emptySummary);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setSummary(await fetchModeAReadiness());
    } catch (error) {
      setSummary(emptySummary());
      message.error(apiErrorMessage(error, t('loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const readyPct =
    summary.total > 0 ? Math.round((summary.modeAReady / summary.total) * 1000) / 10 : 0;
  const tone =
    summary.total === 0
      ? 'info'
      : readyPct >= 80
        ? 'success'
        : readyPct >= 50
          ? 'warning'
          : 'error';

  const openList = (phoneReadiness: string) => {
    navigate(`/customer/list?phoneReadiness=${encodeURIComponent(phoneReadiness)}`);
  };

  const exportNeedsFix = async () => {
    setExporting(true);
    try {
      const rows = await fetchAllCustomersForExport({ phoneReadiness: 'needs_fix' });
      downloadCsv(
        `sdt-can-sua-${new Date().toISOString().slice(0, 10)}.csv`,
        [
          t('csv.code'),
          t('csv.name'),
          t('csv.phone'),
          t('csv.relation'),
          t('csv.status'),
          t('csv.appAccount'),
        ],
        rows.map((r) => [
          r.customerCode,
          r.fullName,
          r.phone,
          r.pharmacyRelation ?? '',
          String(r.status),
          r.hasAppAccount ? '1' : '0',
        ]),
      );
      message.success(t('exportSuccess', { count: rows.length }));
    } catch (error) {
      message.error(apiErrorMessage(error, t('exportFailed')));
    } finally {
      setExporting(false);
    }
  };

  const promoteMembers = async () => {
    setPromoting(true);
    try {
      const result = await bulkMarkPharmacyMembers('staff_mark');
      if (result.updated === 0) {
        message.info(t('promoteNothing'));
      } else {
        message.success(
          t('promoteSuccess', {
            updated: result.updated,
            skipped: result.skipped + result.alreadyMember,
          }),
        );
      }
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('promoteFailed')));
    } finally {
      setPromoting(false);
    }
  };

  return (
    <Card
      title={t('title')}
      loading={loading}
      extra={
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          {t('reload')}
        </Button>
      }
    >
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {t('intro')}
      </Typography.Paragraph>

      <Alert
        type={tone}
        showIcon
        icon={readyPct >= 80 ? <CheckCircleOutlined /> : undefined}
        style={{ marginBottom: 16 }}
        message={t('readyBanner', {
          ready: summary.modeAReady,
          total: summary.total,
          pct: readyPct,
        })}
        description={t('readyHint')}
      />

      <Progress
        percent={readyPct}
        status={readyPct >= 80 ? 'success' : readyPct >= 50 ? 'active' : 'exception'}
        style={{ marginBottom: 16 }}
      />

      <Row gutter={[16, 16]}>
        <Col xs={12} sm={8} md={6}>
          <Statistic title={t('stats.modeAReady')} value={summary.modeAReady} />
        </Col>
        <Col xs={12} sm={8} md={6}>
          <Statistic title={t('stats.needsFix')} value={summary.phoneNeedsFix} />
        </Col>
        <Col xs={12} sm={8} md={6}>
          <Statistic title={t('stats.validVn')} value={summary.validVnMobile} />
        </Col>
        <Col xs={12} sm={8} md={6}>
          <Statistic title={t('stats.eligiblePromote')} value={summary.eligibleToPromote} />
        </Col>
        <Col xs={12} sm={8} md={6}>
          <Statistic title={t('stats.hasApp')} value={summary.hasAppAccount} />
        </Col>
        <Col xs={12} sm={8} md={6}>
          <Statistic title={t('stats.member')} value={summary.member} />
        </Col>
        <Col xs={12} sm={8} md={6}>
          <Statistic title={t('stats.prospect')} value={summary.prospect} />
        </Col>
        <Col xs={12} sm={8} md={6}>
          <Statistic
            title={t('stats.duplicates')}
            value={summary.customersInDuplicateGroups}
            suffix={
              summary.duplicatePhoneGroups > 0
                ? t('stats.duplicateGroupsSuffix', { groups: summary.duplicatePhoneGroups })
                : undefined
            }
          />
        </Col>
        <Col xs={12} sm={8} md={6}>
          <Statistic title={t('stats.total')} value={summary.total} />
        </Col>
      </Row>

      <Space wrap style={{ marginTop: 16 }}>
        {canWrite ? (
          summary.eligibleToPromote > 0 ? (
            <Popconfirm
              title={t('actions.promoteConfirmTitle')}
              description={t('actions.promoteConfirmBody')}
              okText={t('actions.promoteMembers')}
              onConfirm={() => void promoteMembers()}
            >
              <Button
                type="primary"
                icon={<UserSwitchOutlined />}
                loading={promoting}
              >
                {t('actions.promoteMembers')} ({summary.eligibleToPromote})
              </Button>
            </Popconfirm>
          ) : (
            <Button type="primary" icon={<CheckCircleOutlined />} disabled>
              {t('actions.promoteMembers')} (0)
            </Button>
          )
        ) : null}
        <Button icon={<UnorderedListOutlined />} onClick={() => openList('needs_fix')}>
          {t('actions.viewNeedsFix')}
        </Button>
        <Button icon={<UnorderedListOutlined />} onClick={() => openList('mode_a_ready')}>
          {t('actions.viewReady')}
        </Button>
        {summary.customersInDuplicateGroups > 0 ? (
          <Button icon={<UnorderedListOutlined />} onClick={() => openList('duplicate')}>
            {t('actions.viewDuplicates')}
          </Button>
        ) : null}
        <Button
          icon={<DownloadOutlined />}
          loading={exporting}
          disabled={summary.phoneNeedsFix === 0}
          onClick={() => void exportNeedsFix()}
        >
          {t('actions.exportNeedsFix')}
        </Button>
      </Space>

      {canWrite && summary.eligibleToPromote === 0 ? (
        <Alert
          type="success"
          showIcon
          style={{ marginTop: 12 }}
          message={t('promoteDoneHint', { needsFix: summary.phoneNeedsFix })}
        />
      ) : null}
    </Card>
  );
}
