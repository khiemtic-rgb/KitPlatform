import { useEffect, useState } from 'react';
import { App, Card, Space, Table, Tag, Typography } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchLearningGapReport,
  type LearningGapReport,
} from '@/shared/api/learning.api';
import { useCanLearningWrite } from '@/shared/auth/usePermission';

const SIGNAL_LABEL: Record<string, string> = {
  quiz_failed: 'Chưa đạt bài kiểm tra',
  observation_failed: 'Quan sát tại quầy chưa đạt',
};

function formatDate(value: string) {
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? value : d.toLocaleDateString('vi-VN');
}

/** Khoảng trống đào tạo 90 ngày: câu hay sai, ai cần kèm thêm, số đơn trước/sau khi đạt bài. */
export function LearningGapReportCard() {
  const { message } = App.useApp();
  const canWrite = useCanLearningWrite();
  const [data, setData] = useState<LearningGapReport | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!canWrite) return;
    let cancelled = false;
    setLoading(true);
    (async () => {
      try {
        const d = await fetchLearningGapReport();
        if (!cancelled) setData(d);
      } catch (e) {
        message.error(apiErrorMessage(e, 'Không tải được khoảng trống đào tạo'));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [canWrite, message]);

  if (!canWrite) return null;

  return (
    <Card
      title={`Khoảng trống đào tạo (${data?.days ?? 90} ngày)`}
      size="small"
      loading={loading}
    >
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <div>
          <Typography.Title level={5} style={{ marginTop: 0 }}>
            Ai cần kèm thêm
          </Typography.Title>
          <Table
            rowKey={(r) => `${r.employeeId}-${r.moduleId}-${r.signal}`}
            size="small"
            pagination={{ pageSize: 10, hideOnSinglePage: true }}
            dataSource={data?.employeeGaps ?? []}
            locale={{ emptyText: 'Chưa có tín hiệu yếu' }}
            columns={[
              { title: 'Nhân viên', dataIndex: 'employeeName' },
              { title: 'Bài', dataIndex: 'moduleTitle' },
              {
                title: 'Tín hiệu',
                dataIndex: 'signal',
                render: (s: string, r) => (
                  <Tag color="orange">
                    {SIGNAL_LABEL[s] ?? s} · {r.failCount} lần
                  </Tag>
                ),
              },
              { title: 'Gần nhất', dataIndex: 'lastAt', width: 110, render: formatDate },
              { title: 'Nên làm', dataIndex: 'suggestedAction' },
            ]}
          />
        </div>

        <div>
          <Typography.Title level={5} style={{ marginTop: 0 }}>
            Câu hỏi hay sai
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
            Từ 3 lượt trả lời, sai từ 40% trở lên. Nhiều người cùng sai thường là bài viết chưa rõ —
            xem lại nội dung bài trước khi nhắc nhân viên.
          </Typography.Paragraph>
          <Table
            rowKey="questionId"
            size="small"
            pagination={{ pageSize: 10, hideOnSinglePage: true }}
            dataSource={data?.weakQuestions ?? []}
            locale={{ emptyText: 'Chưa đủ dữ liệu' }}
            columns={[
              { title: 'Câu hỏi', dataIndex: 'prompt' },
              { title: 'Bài', dataIndex: 'moduleTitle' },
              {
                title: 'Sai',
                dataIndex: 'wrongPct',
                width: 120,
                render: (pct: number, r) => `${pct}% (${r.wrong}/${r.answered})`,
              },
            ]}
          />
        </div>

        <div>
          <Typography.Title level={5} style={{ marginTop: 0 }}>
            Trước / sau khi đạt bài
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
            Số đơn hoàn tất trung bình mỗi ngày, 30 ngày trước so với tối đa 30 ngày sau khi đạt
            (cần ít nhất 7 ngày). Chỉ là tín hiệu tham khảo — số đơn còn phụ thuộc ca làm và mùa vụ.
          </Typography.Paragraph>
          <Table
            rowKey={(r) => `${r.employeeId}-${r.moduleId}`}
            size="small"
            pagination={{ pageSize: 10, hideOnSinglePage: true }}
            dataSource={data?.outcomes ?? []}
            locale={{ emptyText: 'Chưa có bài đạt đủ 7 ngày' }}
            columns={[
              { title: 'Nhân viên', dataIndex: 'employeeName' },
              { title: 'Bài', dataIndex: 'moduleTitle' },
              { title: 'Đạt ngày', dataIndex: 'passedOn', width: 110, render: formatDate },
              { title: 'Đơn/ngày trước', dataIndex: 'ordersPerDayBefore', width: 120 },
              {
                title: 'Đơn/ngày sau',
                dataIndex: 'ordersPerDayAfter',
                width: 140,
                render: (v: number, r) => `${v} (${r.daysAfter} ngày)`,
              },
            ]}
          />
        </div>
      </Space>
    </Card>
  );
}
