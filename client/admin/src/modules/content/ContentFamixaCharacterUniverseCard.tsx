import { useState } from 'react';
import { Button, Card, Form, Input, InputNumber, Modal, Select, Space, Tag, Typography } from 'antd';
import { LockOutlined, PlusOutlined, ReloadOutlined, UnlockOutlined } from '@ant-design/icons';
import type { FamixaCharacterRecord } from './content-famixa-character-memory';
import { hasFrontRef } from './content-famixa-character-memory';
import { CHARACTER_TYPES, detectDuplicateCharacter, workspaceProgress } from './content-famixa-character-create';

export function ContentFamixaCharacterUniverseCard({
  rows,
  busy,
  onRefresh,
  onApprove,
  onLock,
  onUnlock,
  onOpenWorkspace,
  onCreate,
}: {
  rows: FamixaCharacterRecord[];
  busy?: string;
  onRefresh: () => void;
  onApprove: (code: string) => void;
  onLock: (code: string) => void;
  onUnlock: (code: string) => void;
  onOpenWorkspace: (code: string) => void;
  onCreate: (input: {
    name: string;
    role: string;
    gender: string;
    initialAge: number;
    universe: string;
    description: string;
    forceCreate?: boolean;
  }) => Promise<void>;
}) {
  const [createOpen, setCreateOpen] = useState(false);
  const [dup, setDup] = useState<ReturnType<typeof detectDuplicateCharacter>>();
  const [form] = Form.useForm();

  const submitCreate = async (forceCreate?: boolean) => {
    const v = await form.validateFields();
    const found = detectDuplicateCharacter(rows, { name: v.name, role: v.role });
    if (found.duplicate && !forceCreate) {
      setDup(found);
      return;
    }
    await onCreate({
      name: v.name,
      role: v.role,
      gender: v.gender,
      initialAge: v.initialAge,
      universe: v.universe,
      description: v.description ?? '',
      forceCreate,
    });
    setCreateOpen(false);
    setDup(undefined);
    form.resetFields();
  };

  return (
    <Card
      size="small"
      title="Character Universe"
      extra={
        <Space>
          <Button size="small" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
            Create Character
          </Button>
          <Button size="small" icon={<ReloadOutlined />} onClick={onRefresh}>
            Tải lại
          </Button>
        </Space>
      }
      style={{ marginBottom: 16 }}
    >
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        Minh trước: Style V1 → DNA V1 → Master → Director → LOCK ERA-01. Không LOCK từ ảnh hiện tại. Chưa làm Nam/Linh/An.
        Chưa LOCK không vào production.
      </Typography.Paragraph>
      {rows.length === 0 ? (
        <Typography.Text type="secondary">Chưa đọc được Registry (mig 330). Không đoán Canon.</Typography.Text>
      ) : (
        <div className="content-video-lab-cards">
          {rows.map((r) => {
            const front = hasFrontRef(r);
            const locked = r.lifecycle === 'locked';
            const approved = r.lifecycle === 'approved' || locked;
            const master = r.references.find((x) => x.kind.toUpperCase() === 'FRONT')?.path;
            const progress = workspaceProgress(r);
            return (
              <div key={r.characterCode} className="content-video-lab-card" style={{ cursor: 'default' }}>
                {master ? (
                  <img
                    src={master}
                    alt={r.name}
                    style={{ width: 96, height: 96, objectFit: 'cover', borderRadius: 8, display: 'block' }}
                  />
                ) : (
                  <Tag>Chưa FRONT</Tag>
                )}
                <Typography.Text strong style={{ display: 'block', marginTop: 8 }}>
                  {r.name}
                </Typography.Text>
                <Space wrap style={{ marginTop: 4 }}>
                  <Tag>{r.characterCode}</Tag>
                  <Tag>{r.currentEra}</Tag>
                  <Tag color={locked ? 'green' : approved ? 'blue' : 'gold'}>{r.lifecycle.toUpperCase()}</Tag>
                  <Tag color={front ? 'green' : r.visual === 'frame' ? 'gold' : undefined}>
                    {front ? 'Ref FRONT ✓' : r.visual === 'frame' ? 'Thiếu FRONT' : r.visual}
                  </Tag>
                  <Tag>v{r.version}</Tag>
                  {r.canon.brief?.status === 'BRIEF' ? (
                    <Tag color="purple">BRIEF</Tag>
                  ) : null}
                  <Tag>
                    {progress.completed} / {progress.total}
                  </Tag>
                </Space>
                <Space wrap style={{ marginTop: 8 }}>
                  <Button size="small" onClick={() => onOpenWorkspace(r.characterCode)}>
                    Workspace
                  </Button>
                  {r.lifecycle !== 'approved' && r.lifecycle !== 'locked' ? (
                    <Button size="small" loading={busy === r.characterCode} onClick={() => onApprove(r.characterCode)}>
                      Approve
                    </Button>
                  ) : null}
                  {locked ? (
                    <Button
                      size="small"
                      icon={<UnlockOutlined />}
                      loading={busy === r.characterCode}
                      onClick={() => onUnlock(r.characterCode)}
                    >
                      Unlock
                    </Button>
                  ) : (
                    <Button
                      size="small"
                      type="primary"
                      icon={<LockOutlined />}
                      disabled={!approved || (r.visual === 'frame' && !front)}
                      loading={busy === r.characterCode}
                      onClick={() => onLock(r.characterCode)}
                    >
                      Lock Canon
                    </Button>
                  )}
                </Space>
              </div>
            );
          })}
        </div>
      )}
      <Modal
        title="Create Character"
        open={createOpen}
        onCancel={() => {
          setCreateOpen(false);
          setDup(undefined);
        }}
        onOk={() => void submitCreate()}
        okText="Create"
      >
        {dup?.duplicate ? (
          <Typography.Paragraph type="warning">
            {dup.message} Ưu tiên USE EXISTING CHARACTER.
            <Button type="link" onClick={() => setDup(undefined)}>
              Sửa tên
            </Button>
            <Button type="link" onClick={() => void submitCreate(true)}>
              CREATE NEW CHARACTER
            </Button>
          </Typography.Paragraph>
        ) : null}
        <Form form={form} layout="vertical" initialValues={{ gender: 'male', initialAge: 11, universe: 'CORE' }}>
          <Form.Item name="name" label="Name" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="role" label="Role" rules={[{ required: true }]}>
            <Input placeholder="Main Child / Teacher / …" />
          </Form.Item>
          <Form.Item name="gender" label="Gender" rules={[{ required: true }]}>
            <Select options={['male', 'female', 'unspecified'].map((v) => ({ value: v, label: v }))} />
          </Form.Item>
          <Form.Item name="initialAge" label="Initial Age" rules={[{ required: true }]}>
            <InputNumber min={1} max={80} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="universe" label="Character Type" rules={[{ required: true }]}>
            <Select options={CHARACTER_TYPES.map((v) => ({ value: v, label: v }))} />
          </Form.Item>
          <Form.Item name="description" label="Short Description">
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  );
}
