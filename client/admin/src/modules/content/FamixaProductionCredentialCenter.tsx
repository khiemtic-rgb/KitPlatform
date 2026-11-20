import { useCallback, useEffect, useState } from 'react';
import { Alert, Button, Form, Input, Space, Table, Typography } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  addProductionCredentialVersion,
  listProductionCredentials,
  registerProductionCredential,
  type ProductionCredentialCatalogItem,
} from '@/shared/api/content.api';

const PROVIDER = 'runway';
const MODEL = 'gen4_turbo';

/**
 * Tenant-owned production credentials. The secret is write-only and is never rendered from the API.
 */
export function FamixaProductionCredentialCenter() {
  const [rows, setRows] = useState<ProductionCredentialCatalogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);
  const [rotatingId, setRotatingId] = useState<string | null>(null);
  const [addForm] = Form.useForm<{ secret: string; displayName?: string }>();
  const [rotateForm] = Form.useForm<{ secret: string }>();

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setRows(await listProductionCredentials());
    } catch (e) {
      setError(apiErrorMessage(e, 'Không tải được credential production'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const onAdd = async () => {
    const values = await addForm.validateFields();
    setAdding(true);
    setError(null);
    try {
      await registerProductionCredential({
        providerId: PROVIDER,
        modelId: MODEL,
        secret: values.secret,
        displayName: values.displayName?.trim() || PROVIDER,
      });
      addForm.resetFields();
      await load();
    } catch (e) {
      setError(apiErrorMessage(e, 'Không đăng ký được credential'));
    } finally {
      setAdding(false);
    }
  };

  const onRotate = async (credentialId: string) => {
    const values = await rotateForm.validateFields();
    setRotatingId(credentialId);
    setError(null);
    try {
      await addProductionCredentialVersion(credentialId, {
        providerId: PROVIDER,
        modelId: MODEL,
        secret: values.secret,
      });
      rotateForm.resetFields();
      setRotatingId(null);
      await load();
    } catch (e) {
      setError(apiErrorMessage(e, 'Không rotate được credential'));
      setRotatingId(null);
    }
  };

  return (
    <section data-production-credential-center="1" style={{ marginBottom: 24 }}>
      <Typography.Title level={5} style={{ marginTop: 0 }}>
        AI Credentials
      </Typography.Title>
      <Typography.Paragraph type="secondary">
        Credential production của tenant đang đăng nhập. Provider runway, model gen4_turbo. Secret chỉ ghi một chiều và không hiện lại.
      </Typography.Paragraph>
      {error ? <Alert type="error" showIcon style={{ marginBottom: 12 }} message={error} /> : null}
      <Table<ProductionCredentialCatalogItem>
        rowKey="credentialVersionId"
        size="small"
        loading={loading}
        pagination={false}
        dataSource={rows}
        locale={{ emptyText: 'Chưa có credential production' }}
        columns={[
          { title: 'Provider', dataIndex: 'providerId' },
          { title: 'Model', dataIndex: 'modelId' },
          { title: 'Status', dataIndex: 'status' },
          { title: 'Version', dataIndex: 'version', width: 90 },
          {
            title: 'Created',
            dataIndex: 'createdAt',
            render: (value: string) => (value ? new Date(value).toLocaleString() : ''),
          },
          {
            title: 'Rotate',
            width: 120,
            render: (_, row) => (
              <Button size="small" onClick={() => setRotatingId(row.credentialId)}>
                Rotate
              </Button>
            ),
          },
        ]}
      />
      <Form form={addForm} layout="inline" style={{ marginTop: 12 }} onFinish={() => void onAdd()}>
        <Form.Item name="displayName" label="Name">
          <Input autoComplete="off" placeholder="runway" style={{ width: 160 }} />
        </Form.Item>
        <Form.Item name="secret" label="Secret" rules={[{ required: true, message: 'Nhập secret một lần' }]}>
          <Input.Password autoComplete="new-password" visibilityToggle={false} style={{ width: 280 }} />
        </Form.Item>
        <Form.Item>
          <Button type="primary" htmlType="submit" loading={adding}>
            Add credential
          </Button>
        </Form.Item>
      </Form>
      {rotatingId ? (
        <Form form={rotateForm} layout="inline" style={{ marginTop: 12 }} onFinish={() => void onRotate(rotatingId)}>
          <Form.Item name="secret" label="New secret" rules={[{ required: true, message: 'Nhập secret mới' }]}>
            <Input.Password autoComplete="new-password" visibilityToggle={false} style={{ width: 280 }} />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">
                Rotate credential
              </Button>
              <Button onClick={() => setRotatingId(null)}>Huỷ</Button>
            </Space>
          </Form.Item>
        </Form>
      ) : null}
    </section>
  );
}
