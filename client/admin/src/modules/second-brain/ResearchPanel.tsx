import { useEffect, useState } from 'react';
import { Alert, Button, Card, Descriptions, Drawer, Space, Table, Tag, Typography } from 'antd';
import { researchApi, loadResearch } from './research.api';
import {
  classifyResearchError,
  researchPanelModel,
  sanitizeExperiment,
  type ResearchExperimentView,
  type ResearchLoadResult,
} from './research-view';

const catalogId = /^[A-Za-z0-9][A-Za-z0-9_-]{0,80}$/;

type DetailState =
  | { kind: 'closed' }
  | { kind: 'loading'; id: string }
  | { kind: 'absent' }
  | { kind: 'error'; message: string }
  | { kind: 'open'; experiment: ResearchExperimentView };

function flagText(value: boolean | null): string {
  if (value === true) return 'Nguồn đánh dấu đây là bằng chứng hiệu suất.';
  if (value === false) return 'Nguồn đánh dấu đây không phải bằng chứng hiệu suất.';
  return 'Chưa ghi nhận';
}

function boolText(value: boolean | null): string {
  if (value === true) return 'true';
  if (value === false) return 'false';
  return 'Chưa ghi nhận';
}

export function ResearchPanel() {
  const [result, setResult] = useState<ResearchLoadResult>({ kind: 'loading' });
  const [detail, setDetail] = useState<DetailState>({ kind: 'closed' });
  useEffect(() => {
    let cancelled = false;
    loadResearch().then((value) => { if (!cancelled) setResult(value); });
    return () => { cancelled = true; };
  }, []);
  const model = researchPanelModel(result);
  const tagColor = model.title === 'STALE' ? 'gold' : model.tone === 'error' ? 'red' : undefined;

  const open = (id: string) => {
    if (!catalogId.test(id)) {
      setDetail({ kind: 'absent' });
      return;
    }
    setDetail({ kind: 'loading', id });
    researchApi.experiment(id).then((body) => {
      if (!body.experiment || (body.availability !== 'FRESH' && body.availability !== 'STALE')) {
        setDetail({ kind: 'error', message: researchPanelModel({
          kind: 'ready', availability: body.availability, publication: null, experiments: [], status: null, report: null,
        }).detail });
        return;
      }
      setDetail({ kind: 'open', experiment: sanitizeExperiment(body.experiment) });
    }).catch((error) => {
      const classified = classifyResearchError(error);
      if (classified.kind === 'http' && classified.status === 404) setDetail({ kind: 'absent' });
      else setDetail({ kind: 'error', message: researchPanelModel(classified).detail });
    });
  };

  return (
    <Card title="Research Lab / Paper Ops" loading={result.kind === 'loading'}>
      <Space direction="vertical" size={12} style={{ width: '100%' }}>
        <Alert type={model.tone} showIcon message={<Tag color={tagColor}>{model.title}</Tag>} description={model.detail} />
        {model.labLine && <Alert type="info" showIcon message="Lab chưa có timestamp" description={model.labLine} />}
        {model.showBodies && (
          <>
            <Typography.Text type="secondary">
              Publication {model.publicationId ?? 'chưa ghi nhận'} · xuất bản {model.publishedAt ?? 'chưa ghi nhận'}
            </Typography.Text>
            {model.healthLine && <Typography.Paragraph style={{ marginBottom: 0 }}>{model.healthLine}</Typography.Paragraph>}
            <Typography.Text strong>Experiment</Typography.Text>
            <Table
              size="small"
              rowKey="experimentId"
              pagination={false}
              dataSource={model.experiments}
              locale={{ emptyText: 'Publication không có experiment.' }}
              columns={[
                { title: 'Experiment', dataIndex: 'experimentId' },
                { title: 'Họ', dataIndex: 'family', render: (value: string | null) => value ?? 'Chưa ghi nhận' },
                { title: 'Symbol', dataIndex: 'symbol', render: (value: string | null) => value ?? 'Chưa ghi nhận' },
                { title: 'Khung', dataIndex: 'timeframe', render: (value: string | null) => value ?? 'Chưa ghi nhận' },
                { title: 'Phê duyệt nguồn', dataIndex: 'approvalStatus', render: (value: string | null) => value ?? 'Chưa ghi nhận' },
                { title: 'Bằng chứng', dataIndex: 'evidenceStatus', render: (value: string | null) => value ?? 'Chưa ghi nhận' },
                { title: 'Tuyên bố hiệu suất', dataIndex: 'performanceClaim', render: (value: string | null) => value ?? 'Chưa ghi nhận' },
                { title: '', render: (_, row) => <Button type="link" onClick={() => open(row.experimentId)}>Xem</Button> },
              ]}
            />
            <Typography.Text strong>Paper Ops</Typography.Text>
            <Descriptions size="small" column={1}>
              {model.statusLines.map((line) => <Descriptions.Item key={line.label} label={line.label}>{line.value}</Descriptions.Item>)}
            </Descriptions>
            <Typography.Text strong>Báo cáo paper</Typography.Text>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
              Chuỗi PnL và profit factor là giá trị nguồn. Không phải khuyến nghị mua hoặc bán.
            </Typography.Paragraph>
            <Descriptions size="small" column={1}>
              {model.reportLines.map((line) => <Descriptions.Item key={line.label} label={line.label}>{line.value}</Descriptions.Item>)}
            </Descriptions>
            <Table
              size="small"
              rowKey={(row, index) => `${row.experimentId ?? 'strategy'}-${index}`}
              pagination={false}
              dataSource={model.strategies}
              locale={{ emptyText: 'Báo cáo không có strategy.' }}
              columns={[
                { title: 'Experiment', dataIndex: 'experimentId', render: (value: string | null) => value ?? 'Chưa ghi nhận' },
                { title: 'sampleWarning', dataIndex: 'sampleWarning', render: (value: string | null) => value ?? 'Chưa ghi nhận' },
                { title: 'Trong mẫu / ngoài mẫu', dataIndex: 'inSampleOutOfSample', render: (value: string | null) => value ?? 'Chưa ghi nhận' },
                { title: 'Khớp sàn', dataIndex: 'exchangeFill', render: (value: boolean | null) => boolText(value) },
                { title: 'PnL nguồn', dataIndex: 'netPnl', render: (value: string | null) => value ?? 'Chưa ghi nhận' },
                { title: 'Profit factor nguồn', dataIndex: 'profitFactor', render: (value: string | null) => value ?? 'Chưa ghi nhận' },
              ]}
            />
          </>
        )}
      </Space>
      <Drawer title="Experiment trong publication" open={detail.kind !== 'closed'} onClose={() => setDetail({ kind: 'closed' })} width={480}>
        {detail.kind === 'loading' && <Typography.Text>Đang đọc experiment.</Typography.Text>}
        {detail.kind === 'absent' && <Alert type="warning" showIcon message="Experiment không có trong publication." />}
        {detail.kind === 'error' && <Alert type="error" showIcon message={detail.message} />}
        {detail.kind === 'open' && <ExperimentDetail experiment={detail.experiment} />}
      </Drawer>
    </Card>
  );
}

function ExperimentDetail({ experiment }: { experiment: ResearchExperimentView }) {
  const lines = ([
    ['Experiment', experiment.experimentId],
    ['Chiến lược', experiment.strategyId ?? 'Chưa ghi nhận'],
    ['Phiên bản', experiment.strategyVersion ?? 'Chưa ghi nhận'],
    ['Họ', experiment.family ?? 'Chưa ghi nhận'],
    ['Symbol', experiment.symbol ?? 'Chưa ghi nhận'],
    ['Khung', experiment.timeframe ?? 'Chưa ghi nhận'],
    ['Nguồn dữ liệu', experiment.dataSource ?? 'Chưa ghi nhận'],
    ['Snapshot', experiment.snapshotSha256 ?? 'Chưa ghi nhận'],
    ['Phê duyệt nguồn', experiment.approvalStatus ?? 'Chưa ghi nhận'],
    ['Bằng chứng', experiment.evidenceStatus ?? 'Chưa ghi nhận'],
    ['Tuyên bố hiệu suất', experiment.performanceClaim ?? 'Chưa ghi nhận'],
    ['Giới hạn risk', experiment.riskLimitsApproval ?? 'Chưa ghi nhận'],
    ['Chọn theo PnL', boolText(experiment.selectedByPnl)],
    ['Tham số chọn trên ngoài mẫu', boolText(experiment.parametersChosenOnOutOfSample)],
    ['Live', boolText(experiment.liveOrders)],
    ['Lệnh gửi sàn theo nguồn', experiment.exchangeOrdersSent == null ? 'Chưa ghi nhận' : String(experiment.exchangeOrdersSent)],
    ['Win rate trong mẫu', flagText(experiment.development.winRateIsPerformanceEvidence)],
    ['Profit factor trong mẫu', flagText(experiment.development.profitFactorIsPerformanceEvidence)],
    ['Win rate ngoài mẫu', flagText(experiment.outOfSample.winRateIsPerformanceEvidence)],
    ['Profit factor ngoài mẫu', flagText(experiment.outOfSample.profitFactorIsPerformanceEvidence)],
  ] as const).map(([label, value]) => ({ label, value }));
  return (
    <Space direction="vertical">
      <Alert type="info" showIcon message="Trạng thái nguồn không phải kết luận đã xác nhận và không phải khuyến nghị mua hoặc bán." />
      <Descriptions column={1} size="small">
        {lines.map((line) => <Descriptions.Item key={line.label} label={line.label}>{line.value}</Descriptions.Item>)}
      </Descriptions>
    </Space>
  );
}
