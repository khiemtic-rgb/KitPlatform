import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, Card, Col, Row, Spin, Typography } from 'antd';

type TrafficStats = {
  since?: string;
  total: number;
  today: number;
  week: number;
  days: { date: string; total: number }[];
  top: { path: string; label: string; count: number }[];
};

const LIVE = 'https://thainguyenlife.vn/api/stats';

export function LocalOsStatsPage() {
  const [stats, setStats] = useState<TrafficStats | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void fetch(LIVE)
      .then(async (res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        setStats((await res.json()) as TrafficStats);
      })
      .catch(() => setError('Chưa đọc được số từ site live. Thử lại sau hoặc kiểm tra Worker.'));
  }, []);

  return (
    <div style={{ maxWidth: 880 }}>
      <Typography.Title level={3} style={{ marginTop: 0 }}>
        Thống kê truy cập
      </Typography.Title>
      <Typography.Paragraph type="secondary">
        Lượt mở trang thật trên thainguyenlife.vn. Chỉ xem trong quản trị — không hiện trên site công khai.
        Số bắt đầu từ lúc bật đếm — không bịa, không lấy Google Analytics.
      </Typography.Paragraph>
      <Typography.Paragraph>
        <Link to="/local-os/listings">Hàng chờ tin</Link>
        {' · '}
        <Link to="/local-os/sources">Sổ nguồn</Link>
      </Typography.Paragraph>
      {error && <Alert type="warning" showIcon message={error} style={{ marginBottom: 16 }} />}
      {!stats && !error && <Spin />}
      {stats && (
        <>
          <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
            <Col xs={24} sm={8}>
              <Card size="small" title="Hôm nay">
                <Typography.Title level={2} style={{ margin: 0 }}>
                  {stats.today}
                </Typography.Title>
              </Card>
            </Col>
            <Col xs={24} sm={8}>
              <Card size="small" title="7 ngày">
                <Typography.Title level={2} style={{ margin: 0 }}>
                  {stats.week}
                </Typography.Title>
              </Card>
            </Col>
            <Col xs={24} sm={8}>
              <Card size="small" title={`Từ ${stats.since || 'khi bật'}`}>
                <Typography.Title level={2} style={{ margin: 0 }}>
                  {stats.total}
                </Typography.Title>
              </Card>
            </Col>
          </Row>
          <Card size="small" title="Trang xem nhiều" style={{ marginBottom: 12 }}>
            {stats.top.length === 0 && <Typography.Text type="secondary">Chưa có lượt xem.</Typography.Text>}
            {stats.top.map((row) => (
              <div key={row.path} style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0' }}>
                <span>{row.label}</span>
                <strong>{row.count}</strong>
              </div>
            ))}
          </Card>
        </>
      )}
    </div>
  );
}
