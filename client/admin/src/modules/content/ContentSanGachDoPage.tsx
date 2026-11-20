import { useEffect, useState } from 'react';
import { Alert, Card, Col, Image, Row, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ContentPageHeader } from '@/modules/content/ContentPageHeader';
import { StepCard } from '@/modules/content/LinesheetAiSteps';
import {
  fetchSanGachDoDesk,
  fetchSanGachDoPreview,
  fetchSanGachDoSheet,
  type SanGachDoCharacter,
  type SanGachDoDesk,
} from '@/shared/api/san-gach-do.api';

const SHEET_LABEL: Record<string, string> = {
  goc: 'Góc người',
  mat: 'Mặt',
  ngoi: 'Ngồi',
  anh: 'Ảnh',
};

const MOTION = [
  ['Máy quay khóa', 'có'],
  ['Mặt ba phần tư', 'có'],
  ['Miệng ngậm', 'có'],
  ['Chớp mắt', 'mí nếp khít khe mắt, khép kín 0 điểm lộ'],
  ['Thở', 'nhẹ'],
  ['Nghiêng đầu nhẹ', 'có'],
] as const;

export function ContentSanGachDoPage() {
  const [desk, setDesk] = useState<SanGachDoDesk | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [picked, setPicked] = useState<SanGachDoCharacter | null>(null);
  const [sheets, setSheets] = useState<Record<string, string>>({});
  const [preview, setPreview] = useState<string | null>(null);

  useEffect(() => {
    let live = true;
    fetchSanGachDoDesk()
      .then((d) => {
        if (!live) return;
        setDesk(d);
        setPicked(d.characters.find((c) => c.id === 'Bo') ?? d.characters[0] ?? null);
        if (d.previewReady) {
          void fetchSanGachDoPreview().then((url) => {
            if (live) setPreview(url);
          });
        }
      })
      .catch(() => {
        if (live) setError('Chưa mở được bàn nhân vật.');
      });
    return () => {
      live = false;
    };
  }, []);

  useEffect(() => {
    if (!picked) return;
    let live = true;
    const urls: string[] = [];
    setSheets({});
    void Promise.all(
      picked.sheets.map(async (kind) => {
        const url = await fetchSanGachDoSheet(picked.id, kind);
        urls.push(url);
        return [kind, url] as const;
      }),
    )
      .then((pairs) => {
        if (!live) {
          pairs.forEach(([, url]) => URL.revokeObjectURL(url));
          return;
        }
        setSheets(Object.fromEntries(pairs));
      })
      .catch(() => {
        if (live) setError('Chưa đọc được ảnh mẫu.');
      });
    return () => {
      live = false;
      urls.forEach((u) => URL.revokeObjectURL(u));
    };
  }, [picked]);

  const columns: ColumnsType<SanGachDoCharacter> = [
    { title: 'Nhân vật', dataIndex: 'name', key: 'name' },
    {
      title: 'Loại',
      dataIndex: 'kind',
      key: 'kind',
      render: (kind: string) => (kind === 'prop' ? <Tag>Đạo cụ</Tag> : <Tag color="blue">Người</Tag>),
    },
    {
      title: 'Mẫu',
      dataIndex: 'sheets',
      key: 'sheets',
      render: (sheets: string[]) => sheets.map((s) => SHEET_LABEL[s] ?? s).join(' · '),
    },
    {
      title: 'So với mẫu',
      dataIndex: 'status',
      key: 'status',
      render: () => <Tag>Chưa đạt</Tag>,
    },
  ];

  const people = desk?.characters.length ?? 0;

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <ContentPageHeader
        title="Sân gạch đỏ"
        description="Mẫu nhân vật lấy từ thư mục Bo_Chuan. Bàn này chưa gọi Runway và chưa gọi Fal."
      />
      {error ? <Alert type="warning" showIcon message={error} /> : null}
      <StepCard n={1} title="Mẫu đã chốt" done={!!desk?.folderReady && people > 0}>
        <Typography.Text type="secondary">
          {desk?.folderReady
            ? `${people} mục trong ${desk.root}`
            : 'Chưa thấy thư mục mẫu.'}
        </Typography.Text>
      </StepCard>
      <StepCard n={2} title="Dựng trong Blender" done={!!desk?.blenderReady}>
        <Typography.Text type="secondary">
          {desk?.blenderReady
            ? 'Blender 4.5 đã có. Cảnh đầu là mặt Bố trên máy quay khóa. Toàn thân chưa được nặn lại.'
            : 'Blender chưa được cài. Chưa có file nhân vật để so.'}
        </Typography.Text>
      </StepCard>
      <StepCard n={3} title="Khớp mẫu góc, mặt, ngồi" done={desk?.matched === true}>
        <Typography.Text type="secondary">Chưa có ảnh dựng để đặt cạnh mẫu.</Typography.Text>
      </StepCard>
      <StepCard n={4} title="Chuyển động tối thiểu như Lệch Tông" done={false}>
        <Space wrap>
          {MOTION.map(([item, state]) => (
            <Tag key={item}>{item}: {desk?.previewReady ? state : 'chưa có'}</Tag>
          ))}
        </Space>
        {preview ? <Image src={preview} alt="Bố khung 1" style={{ marginTop: 12, maxWidth: 480 }} /> : null}
      </StepCard>
      <Card size="small" title="Nhân vật">
        <Table
          rowKey="id"
          size="small"
          pagination={false}
          columns={columns}
          dataSource={desk?.characters ?? []}
          onRow={(row) => ({ onClick: () => setPicked(row) })}
          rowClassName={(row) => (row.id === picked?.id ? 'ant-table-row-selected' : '')}
        />
      </Card>
      {picked ? (
        <Card size="small" title={`${picked.name} · mẫu`}>
          <Row gutter={[12, 12]}>
            {picked.sheets.map((kind) => (
              <Col key={kind} xs={24} md={12} xl={8}>
                <Typography.Text type="secondary">{SHEET_LABEL[kind] ?? kind}</Typography.Text>
                {sheets[kind] ? <Image src={sheets[kind]} alt={`${picked.name} ${kind}`} /> : null}
              </Col>
            ))}
          </Row>
        </Card>
      ) : null}
    </Space>
  );
}
