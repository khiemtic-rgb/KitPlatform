import { useEffect, useState } from 'react';
import {
  Alert,
  App,
  Card,
  Col,
  List,
  Progress,
  Row,
  Space,
  Statistic,
  Table,
  Typography,
} from 'antd';
import {
  CheckSquareOutlined,
  ReadOutlined,
  RiseOutlined,
  StarOutlined,
  TrophyOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchPeopleDashboard,
  type LearningPeopleDashboard,
} from '@/shared/api/learning.api';
import { PeoplePageShell, PeopleNavButton } from '@/modules/learning/people-ui';
import { LearningGapReportCard } from '@/modules/learning/LearningGapReportCard';

/** Dashboard chi tiết (quản lý) — bổ sung cho Tổng quan chính. */
export function LearningPeopleDashboardPage() {
  const { message } = App.useApp();
  const [data, setData] = useState<LearningPeopleDashboard | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const d = await fetchPeopleDashboard();
        if (!cancelled) setData(d);
      } catch (e) {
        message.error(apiErrorMessage(e, 'Không tải được tổng quan đội ngũ'));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [message]);

  return (
    <PeoplePageShell
      title="Tổng quan đội ngũ"
      hint="Làm lần lượt vài việc bên dưới — không cần nhớ hết menu. Không khóa bán hàng."
    >
      <Card title="Việc nên làm trước" loading={loading}>
        <List
          dataSource={data?.actionItems ?? []}
          locale={{ emptyText: 'Không có việc chờ' }}
          renderItem={(item, idx) => (
            <List.Item>
              <Space align="start">
                <Typography.Text strong>{idx + 1}.</Typography.Text>
                <Typography.Text>{item}</Typography.Text>
              </Space>
            </List.Item>
          )}
        />
        <Space wrap style={{ marginTop: 12 }}>
          <PeopleNavButton to="/people/enrollments" icon={<ReadOutlined />}>
            Tiến độ học
          </PeopleNavButton>
          <PeopleNavButton to="/people/evaluations" icon={<StarOutlined />}>
            Đánh giá tháng
          </PeopleNavButton>
          <PeopleNavButton to="/people/grow" icon={<RiseOutlined />}>
            Phát triển nghề
          </PeopleNavButton>
          <PeopleNavButton to="/people/recognize" icon={<TrophyOutlined />}>
            Ghi nhận
          </PeopleNavButton>
          <PeopleNavButton to="/success/shift-checklist" icon={<CheckSquareOutlined />}>
            Checklist ca
          </PeopleNavButton>
        </Space>
      </Card>

      <Row gutter={[12, 12]}>
        <Col xs={12} md={6}>
          <Card loading={loading} size="small">
            <Statistic title="Tỷ lệ bài đã đạt" value={data?.trainingCompletionPct ?? 0} suffix="%" />
            <Progress
              percent={data?.trainingCompletionPct ?? 0}
              size="small"
              style={{ marginTop: 8 }}
              format={() =>
                `${data?.modulesPassedTotal ?? 0}/${data?.modulesTotalAssigned ?? 0}`
              }
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card loading={loading} size="small">
            <Statistic
              title="Chưa học bán hàng cơ bản tại quầy"
              value={data?.missingPosBasicCount ?? 0}
            />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Năng lực đã ghi nhận: {data?.credentialCount ?? 0}
            </Typography.Text>
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card loading={loading} size="small">
            <Statistic title="Chưa đánh giá tháng này" value={data?.unevaluatedThisMonth ?? 0} />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Điểm trung bình đã chấm: {data?.avgEvaluateScore ?? '—'}
            </Typography.Text>
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card loading={loading} size="small">
            <Statistic title="Đủ điều kiện lên bậc" value={data?.eligiblePromotionCount ?? 0} />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Ghi nhận 30 ngày: {data?.recognitionCount30d ?? 0}
            </Typography.Text>
          </Card>
        </Col>
      </Row>

      <Row gutter={[12, 12]}>
        <Col xs={12} md={6}>
          <Card loading={loading} size="small">
            <Statistic
              title="Chờ nhân viên phản hồi"
              value={data?.pendingFeedbackCount ?? 0}
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card loading={loading} size="small">
            <Statistic
              title="Chi nhánh chưa đóng ca hôm nay"
              value={data?.missingCloseChecklistBranchesToday ?? 0}
            />
          </Card>
        </Col>
      </Row>

      {(data?.missingPosBasicCount ?? 0) > 0 ||
      (data?.unevaluatedThisMonth ?? 0) > 0 ||
      (data?.pendingFeedbackCount ?? 0) > 0 ||
      (data?.missingCloseChecklistBranchesToday ?? 0) > 0 ? (
        <Alert
          type="warning"
          showIcon
          message="Gợi ý làm trong 5 phút (không ép buộc)"
          description="(1) Gán / nhắc học bán hàng cơ bản cho nhân viên còn thiếu. (2) Đánh giá tháng dựa trên bằng chứng. (3) Nhắc nhân viên phản hồi đánh giá. (4) Tick checklist đóng ca tại Vận hành ca."
        />
      ) : null}

      <LearningGapReportCard />

      <Card title="Phân bố bậc nghề" size="small" loading={loading}>
        <Table
          rowKey="levelCode"
          pagination={false}
          size="small"
          dataSource={data?.careerLevelCounts ?? []}
          columns={[
            { title: 'Bậc', dataIndex: 'levelTitle' },
            { title: 'Mã bậc', dataIndex: 'levelCode', width: 100 },
            { title: 'Số nhân viên', dataIndex: 'employeeCount', width: 120 },
          ]}
        />
      </Card>
    </PeoplePageShell>
  );
}
