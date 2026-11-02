import { useCallback, useEffect, useState } from 'react';
import { Alert, App, Button, Card, Select, Space, Switch, Table, Tag, Typography } from 'antd';
import { SaveOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  credentialsForModel,
  fetchTenantAiConfig,
  modelKey,
  saveTenantAiConfig,
  type TenantAiCapability,
  type TenantAiCapabilitySettings,
} from '@/shared/api/tenant-ai-config.api';

const CAPABILITY_LABELS: Record<TenantAiCapability, string> = {
  TEXT: 'Văn bản',
  IMAGE: 'Hình ảnh',
  VIDEO: 'Video',
  VOICE: 'Giọng nói',
  LIPSYNC: 'Lip Sync',
};

type Draft = { model?: string; credentialId?: string | null; enabled: boolean };

function draftFrom(row: TenantAiCapabilitySettings): Draft {
  return {
    model: row.config ? modelKey(row.config) : undefined,
    credentialId: row.config?.credentialId ?? null,
    enabled: row.config?.enabled ?? true,
  };
}

export function TenantAiConfigurationCard({ reloadKey }: { reloadKey?: number }) {
  const { message } = App.useApp();
  const [rows, setRows] = useState<TenantAiCapabilitySettings[]>([]);
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState<TenantAiCapability | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchTenantAiConfig();
      setRows(data);
      setDrafts(Object.fromEntries(data.map((r) => [r.capability, draftFrom(r)])));
    } catch (e) {
      setError(apiErrorMessage(e, 'Không thể tải cấu hình AI của tenant'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, reloadKey]);

  const patch = (capability: TenantAiCapability, next: Partial<Draft>) =>
    setDrafts((d) => ({ ...d, [capability]: { ...d[capability], ...next } }));

  const onSave = async (row: TenantAiCapabilitySettings) => {
    const draft = drafts[row.capability];
    const model = row.catalog.find((m) => modelKey(m) === draft?.model);
    if (!draft || !model) return;
    setSaving(row.capability);
    try {
      await saveTenantAiConfig(row.capability, {
        providerId: model.providerId,
        modelId: model.modelId,
        credentialId: model.requiresCredential ? draft.credentialId ?? null : null,
        enabled: draft.enabled,
      });
      message.success(`Đã lưu cấu hình ${CAPABILITY_LABELS[row.capability]}`);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không lưu được cấu hình AI'));
    } finally {
      setSaving(null);
    }
  };

  return (
    <Card
      data-tenant-ai-configuration="1"
      style={{ marginBottom: 16 }}
      title="Cấu hình AI · Tenant hiện tại"
      loading={loading}
    >
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        Mặc định của tenant cho Production. Shot và build có thể ghi đè. Không có cấu hình hợp lệ thì Production bị chặn.
        Chỉ hiện model đang chạy được và credential của tenant này.
      </Typography.Paragraph>
      {error ? (
        <Alert type="error" showIcon message={error} action={<Button size="small" onClick={() => void load()}>Thử lại</Button>} />
      ) : (
        <Table<TenantAiCapabilitySettings>
          rowKey="capability"
          size="small"
          pagination={false}
          dataSource={rows}
          columns={[
            {
              title: 'Capability',
              dataIndex: 'capability',
              width: 120,
              render: (cap: TenantAiCapability) => CAPABILITY_LABELS[cap] ?? cap,
            },
            {
              title: 'Provider / Model',
              render: (_, row) =>
                row.catalog.length === 0 ? (
                  <Typography.Text type="secondary">Chưa có model Production chạy được cho tenant</Typography.Text>
                ) : (
                  <Select
                    style={{ width: '100%', minWidth: 220 }}
                    placeholder="Chọn model"
                    value={drafts[row.capability]?.model}
                    onChange={(value: string) => patch(row.capability, { model: value, credentialId: null })}
                    options={row.catalog.map((m) => ({
                      value: modelKey(m),
                      label: `${m.displayName} · ${m.providerId}`,
                    }))}
                  />
                ),
            },
            {
              title: 'Credential',
              render: (_, row) => {
                const model = row.catalog.find((m) => modelKey(m) === drafts[row.capability]?.model);
                if (!model) return null;
                if (!model.requiresCredential) return <Tag>Không cần</Tag>;
                const options = credentialsForModel(row, model);
                if (options.length === 0)
                  return <Typography.Text type="warning">Chưa có credential. Thêm ở danh sách Model AI bên dưới.</Typography.Text>;
                return (
                  <Select
                    style={{ width: '100%', minWidth: 200 }}
                    placeholder="Chọn credential"
                    value={drafts[row.capability]?.credentialId ?? undefined}
                    onChange={(value: string) => patch(row.capability, { credentialId: value })}
                    options={options.map((c) => ({ value: c.credentialId, label: `${c.displayName} · v${c.version}` }))}
                  />
                );
              },
            },
            {
              title: 'Bật',
              width: 70,
              render: (_, row) =>
                row.catalog.length === 0 ? null : (
                  <Switch
                    size="small"
                    checked={drafts[row.capability]?.enabled ?? true}
                    onChange={(checked) => patch(row.capability, { enabled: checked })}
                  />
                ),
            },
            {
              title: '',
              width: 110,
              render: (_, row) => {
                if (row.catalog.length === 0) return null;
                const draft = drafts[row.capability];
                const model = row.catalog.find((m) => modelKey(m) === draft?.model);
                const ready = !!model && (!model.requiresCredential || !!draft?.credentialId);
                return (
                  <Space>
                    <Button
                      size="small"
                      type="primary"
                      icon={<SaveOutlined />}
                      disabled={!ready}
                      loading={saving === row.capability}
                      onClick={() => void onSave(row)}
                    >
                      Lưu
                    </Button>
                  </Space>
                );
              },
            },
          ]}
        />
      )}
    </Card>
  );
}
