import { Card, Col, List, Row, Typography } from 'antd';
import { Link } from 'react-router-dom';
import {
  REPORT_CATEGORY_LABELS,
  REPORT_DEFINITIONS,
  type ReportCategory,
} from '@/modules/reports/reports-catalog';

const categories: ReportCategory[] = ['sales', 'procurement', 'inventory'];

export function ReportsHomePage() {
  const favorites = REPORT_DEFINITIONS.filter((r) => r.favorite);

  return (
    <div>
      <Typography.Title level={4} style={{ marginTop: 0 }}>
        Báo cáo — Thống kê
      </Typography.Title>
      <Typography.Paragraph type="secondary">
        Wave 1: doanh thu, ca làm việc, nhập hàng, công nợ NCC, tồn kho và cảnh báo HSD. Chọn báo cáo để lọc kỳ,
        xuất CSV hoặc in.
      </Typography.Paragraph>

      <Card size="small" title="Thường dùng" style={{ marginBottom: 16 }}>
        <Row gutter={[12, 12]}>
          {favorites.map((report) => (
            <Col xs={24} sm={12} lg={8} key={report.code}>
              <Link to={report.path}>
                <Card size="small" hoverable>
                  <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                    {report.code}
                  </Typography.Text>
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
          title={REPORT_CATEGORY_LABELS[category]}
          style={{ marginBottom: 16 }}
        >
          <List
            dataSource={REPORT_DEFINITIONS.filter((r) => r.category === category)}
            renderItem={(item) => (
              <List.Item>
                <List.Item.Meta
                  title={<Link to={item.path}>{item.name}</Link>}
                  description={
                    <>
                      <Typography.Text code>{item.code}</Typography.Text> — {item.description}
                    </>
                  }
                />
              </List.Item>
            )}
          />
        </Card>
      ))}
    </div>
  );
}
