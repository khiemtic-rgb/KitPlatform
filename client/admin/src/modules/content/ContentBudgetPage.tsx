import { useCallback, useEffect, useState } from 'react';
import { App, Card, Col, Progress, Row, Statistic, Table, Typography } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { Button } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchContentBudget, type ContentBudgetSnapshot } from '@/shared/api/content.api';

function money(n: number) {
  return n.toLocaleString('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 2 });
}

export function ContentBudgetPage() {
  const { message } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [budget, setBudget] = useState<ContentBudgetSnapshot | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setBudget(await fetchContentBudget());
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được giới hạn chi phí'));
    } finally {
      setLoading(false);
    }
  }, [message]);

  useEffect(() => {
    void load();
  }, [load]);

  const usedPct =
    budget && budget.globalCeilingUsd > 0
      ? Math.min(100, Math.round((budget.globalSpendUsd / budget.globalCeilingUsd) * 100))
      : 0;

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            Giới hạn chi phí AI
          </Typography.Title>
          <Typography.Text type="secondary">
            Theo dõi tiền ước tính khi nhờ AI viết / tạo ảnh trong tháng. Vượt mức → không cho tạo ảnh mới
            (tránh cháy ngân sách).
          </Typography.Text>
        </div>
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          Tải lại
        </Button>
      </div>

      <Row gutter={[16, 16]}>
        <Col xs={24} md={8}>
          <Card loading={loading}>
            <Statistic title="Mức tối đa / tháng" value={budget?.globalCeilingUsd ?? 0} precision={2} prefix="$" />
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card loading={loading}>
            <Statistic title="Đã dùng (ước tính)" value={budget?.globalSpendUsd ?? 0} precision={2} prefix="$" />
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card loading={loading}>
            <Statistic title="Còn lại" value={budget?.globalRemainingUsd ?? 0} precision={2} prefix="$" />
          </Card>
        </Col>
      </Row>

      <Card style={{ marginTop: 16 }} loading={loading} title="Mức đã dùng trong tháng">
        <Progress percent={usedPct} status={usedPct >= 90 ? 'exception' : 'active'} />
        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
          Chất lượng ảnh mặc định:{' '}
          <strong>
            {budget?.defaultImageTier === 'lean'
              ? 'Tiết kiệm'
              : budget?.defaultImageTier === 'premium'
                ? 'Cao cấp'
                : 'Cân bằng'}
          </strong>
        </Typography.Paragraph>
      </Card>

      <Card style={{ marginTop: 16 }} title="Theo từng thương hiệu">
        <Table
          rowKey="brandId"
          loading={loading}
          pagination={false}
          dataSource={budget?.brands ?? []}
          columns={[
            { title: 'Thương hiệu', dataIndex: 'brandName', render: (v, r) => `${v} (${r.brandCode})` },
            { title: 'Mức tối đa', dataIndex: 'effectiveCeilingUsd', render: money },
            { title: 'Đã dùng', dataIndex: 'spendUsd', render: money },
            { title: 'Còn lại', dataIndex: 'remainingUsd', render: money },
            {
              title: 'Chất lượng ảnh',
              dataIndex: 'effectiveImageTier',
              render: (v: string) =>
                v === 'lean' ? 'Tiết kiệm' : v === 'premium' ? 'Cao cấp' : 'Cân bằng',
            },
            {
              title: 'Tự dừng khi hết',
              dataIndex: 'pauseWhenExceeded',
              render: (v: boolean) => (v ? 'Có' : 'Không'),
            },
          ]}
        />
      </Card>
    </div>
  );
}
