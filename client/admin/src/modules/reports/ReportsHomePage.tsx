import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, Col, List, Row, Typography } from 'antd';
import { Link } from 'react-router-dom';
import {
  getReportCategoryLabel,
  getReportDefinitions,
  type ReportCategory,
} from '@/modules/reports/reports-catalog';

const categories: ReportCategory[] = ['sales', 'procurement', 'inventory'];

export function ReportsHomePage() {
  const { t } = useTranslation('reports', { keyPrefix: 'home' });
  const reports = useMemo(() => getReportDefinitions(), [t]);
  const favorites = reports.filter((r) => r.favorite);

  return (
    <div>
      <Typography.Title level={4} style={{ marginTop: 0 }}>
        {t('title')}
      </Typography.Title>
      <Typography.Paragraph type="secondary">{t('description')}</Typography.Paragraph>

      <Card size="small" title={t('favorites')} style={{ marginBottom: 16 }}>
        <Row gutter={[12, 12]}>
          {favorites.map((report) => (
            <Col xs={24} sm={12} lg={8} key={report.code}>
              <Link to={report.path}>
                <Card size="small" hoverable>
                  <Typography.Text strong style={{ display: 'block' }}>
                    {report.name}
                  </Typography.Text>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {report.description}
                  </Typography.Text>
                </Card>
              </Link>
            </Col>
          ))}
        </Row>
      </Card>

      {categories.map((category) => (
        <Card
          key={category}
          size="small"
          title={getReportCategoryLabel(category)}
          style={{ marginBottom: 16 }}
        >
          <List
            dataSource={reports.filter((r) => r.category === category)}
            renderItem={(item) => (
              <List.Item>
                <List.Item.Meta
                  title={<Link to={item.path}>{item.name}</Link>}
                  description={item.description}
                />
              </List.Item>
            )}
          />
        </Card>
      ))}
    </div>
  );
}
