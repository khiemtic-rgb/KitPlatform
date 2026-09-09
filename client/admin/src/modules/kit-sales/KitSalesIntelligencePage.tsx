import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, Col, Row, Space, Tag, Typography, message } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchKitSalesPainMarket,
  type KitSalesPainMarket,
} from '@/shared/api/kit-sales.api';

export function KitSalesIntelligencePage() {
  const { t } = useTranslation('kitSales');
  const [market, setMarket] = useState<KitSalesPainMarket | null>(null);

  useEffect(() => {
    void fetchKitSalesPainMarket()
      .then(setMarket)
      .catch((error) => message.error(apiErrorMessage(error, t('pain.failed'))));
  }, [t]);

  const ranked = useMemo(
    () =>
      [...(market?.byCategory ?? [])].sort((a, b) => b.signalRate - a.signalRate || b.approached - a.approached),
    [market],
  );
  const best = ranked.filter((row) => row.approached > 0).slice(0, 3);
  const next = ranked.find((row) => row.approached === 0) ?? ranked[ranked.length - 1];

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <div>
        <Space>
          <Typography.Title level={3} style={{ marginBottom: 0 }}>
            {t('intelligence.title')}
          </Typography.Title>
          {market && (
            <Tag color={market.mode === 'learn' ? 'green' : 'blue'}>
              {t(`pain.mode.${market.mode}`, { defaultValue: market.mode })}
            </Tag>
          )}
        </Space>
        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
          {t('intelligence.blurb')}
        </Typography.Paragraph>
      </div>

      {market && (
        <Typography.Text type="secondary">
          {t('pain.marketBlurb', { count: market.touchedLeads })}
        </Typography.Text>
      )}

      <Card size="small" title={t('intelligence.best')}>
        {best.length === 0 ? (
          <Typography.Text type="secondary">{t('intelligence.empty')}</Typography.Text>
        ) : (
          best.map((row, index) => (
            <div key={row.category} style={{ marginBottom: 8 }}>
              <Typography.Text strong>
                {index + 1}. {row.categoryLabel}
              </Typography.Text>
              <Typography.Text type="secondary">
                {' '}
                — {t('intelligence.replyRate', { rate: Math.round(row.signalRate * 100) })}
                {row.interested > 0
                  ? ` · ${row.interested}/${row.approached}`
                  : ` · ${row.approached} approached`}
              </Typography.Text>
            </div>
          ))
        )}
      </Card>

      {next && (
        <Card size="small" title={t('intelligence.next')}>
          <Typography.Text>{next.categoryLabel}</Typography.Text>
        </Card>
      )}

      <Row gutter={[12, 12]}>
        {ranked.map((row) => (
          <Col key={row.category} xs={12} sm={8} md={6}>
            <Card size="small">
              <Typography.Text type="secondary">{row.categoryLabel}</Typography.Text>
              <div style={{ fontWeight: 600 }}>
                {row.interested}/{row.approached}
              </div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('pain.signal', { rate: Math.round(row.signalRate * 100) })}
              </Typography.Text>
            </Card>
          </Col>
        ))}
      </Row>
    </Space>
  );
}
