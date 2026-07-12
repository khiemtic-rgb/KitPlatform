import { useEffect, useState } from 'react';
import { Alert, Card, Col, Row, Space, Tag, Typography, message } from 'antd';
import { useTranslation } from 'react-i18next';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchConnectOverview, type ConnectOverview } from '@/shared/api/connect.api';

export function ConnectOverviewPage() {
  const { t } = useTranslation('connect', { keyPrefix: 'overview' });
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState<ConnectOverview | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    void fetchConnectOverview()
      .then((overview) => {
        if (!cancelled) setData(overview);
      })
      .catch((error) => {
        if (!cancelled) message.error(apiErrorMessage(error, t('loadFailed')));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [t]);

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ margin: 0 }}>
          {t('title')}
        </Typography.Title>
        <Typography.Text type="secondary">{t('subtitle')}</Typography.Text>
      </div>

      <Alert type="info" showIcon message={t('legalBanner')} />

      <Row gutter={[12, 12]}>
        <Col xs={24} md={12}>
          <Card title={t('packCard')} loading={loading && !data}>
            <Space direction="vertical" size={8}>
              <div>
                <Typography.Text type="secondary">{t('packCode')}</Typography.Text>
                <div>
                  <Tag color="blue">{data?.packCode ?? '—'}</Tag>
                </div>
              </div>
              <div>
                <Typography.Text type="secondary">{t('phase')}</Typography.Text>
                <div>
                  <Tag>{data?.phase ?? '—'}</Tag>
                </div>
              </div>
              <Typography.Paragraph style={{ marginBottom: 0 }}>
                {data?.legalBoundary ?? t('legalFallback')}
              </Typography.Paragraph>
            </Space>
          </Card>
        </Col>
        <Col xs={24} md={12}>
          <Card title={t('capabilitiesCard')} loading={loading && !data}>
            <Space wrap>
              {(data?.enabledCapabilities ?? []).map((cap) => (
                <Tag key={cap} color="green">
                  {cap}
                </Tag>
              ))}
            </Space>
            <Typography.Title level={5} style={{ marginTop: 16 }}>
              {t('nonGoals')}
            </Typography.Title>
            <Space wrap>
              {(data?.explicitNonGoals ?? []).map((item) => (
                <Tag key={item}>{item}</Tag>
              ))}
            </Space>
          </Card>
        </Col>
      </Row>

      <Card title={t('nextCard')}>
        <Typography.Paragraph style={{ marginBottom: 0 }}>{t('nextBody')}</Typography.Paragraph>
      </Card>
    </Space>
  );
}
