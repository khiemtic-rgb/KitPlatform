import { useQuery } from '@tanstack/react-query';
import { Table, Tag, Typography } from 'antd';
import dayjs from 'dayjs';
import { fetchLeads } from '@/shared/api/partner-portal.api';
import {
  KAP_COMMISSION_LABELS,
  KAP_PIPELINE_LABELS,
  KAP_SUBMISSION_STATUS_LABELS,
  kapLabel,
} from '@/shared/kap-labels';

const STATUS_COLORS: Record<string, string> = {
  draft: 'default',
  completed: 'blue',
  lead_captured: 'green',
  report_ready: 'cyan',
};

export function LeadsPage() {
  const { data, isLoading } = useQuery({ queryKey: ['partner-leads'], queryFn: fetchLeads });

  return (
    <div>
      <Typography.Title level={3}>Lead của bạn</Typography.Title>
      <Typography.Paragraph type="secondary">
        Các khảo sát gắn mã giới thiệu của bạn. Trạng thái pipeline do Novixa cập nhật.
      </Typography.Paragraph>
      <Table
        loading={isLoading}
        rowKey="id"
        dataSource={data ?? []}
        scroll={{ x: 900 }}
        columns={[
          {
            title: 'Nhà thuốc',
            dataIndex: 'orgName',
            ellipsis: true,
            render: (v, r) => v || r.contactName || '—',
          },
          { title: 'Liên hệ', dataIndex: 'contactName', width: 140, ellipsis: true },
          { title: 'SĐT', dataIndex: 'phone', width: 120 },
          {
            title: 'Trạng thái',
            dataIndex: 'status',
            width: 130,
            render: (v: string) => (
              <Tag color={STATUS_COLORS[v] ?? 'default'}>
                {kapLabel(KAP_SUBMISSION_STATUS_LABELS, v)}
              </Tag>
            ),
          },
          {
            title: 'Pipeline',
            dataIndex: 'leadPipelineStatus',
            width: 140,
            render: (v: string) => kapLabel(KAP_PIPELINE_LABELS, v),
          },
          {
            title: 'Hoa hồng',
            dataIndex: 'commissionStatus',
            width: 130,
            render: (v: string) => kapLabel(KAP_COMMISSION_LABELS, v),
          },
          {
            title: 'Điểm',
            dataIndex: 'overallPct',
            width: 80,
            align: 'right',
            render: (v) => (v == null ? '—' : Math.round(Number(v))),
          },
          {
            title: 'Ngày',
            dataIndex: 'startedAt',
            width: 120,
            render: (v) => dayjs(v).format('DD/MM/YYYY'),
          },
        ]}
      />
    </div>
  );
}
