import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Button,
  Card,
  Col,
  Form,
  Input,
  Row,
  Space,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  createKitSalesProspect,
  fetchKitSalesLeads,
  fetchKitSalesPipelineSummary,
  type KitSalesLead,
  type KitSalesPipelineSummary,
} from '@/shared/api/kit-sales.api';

function formatDate(value?: string | null) {
  if (!value) return '—';
  const d = dayjs(value);
  return d.isValid() ? d.format('DD/MM/YYYY HH:mm') : String(value).slice(0, 16);
}

export function KitSalesDeskPage() {
  const { t } = useTranslation('kitSales');
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [leads, setLeads] = useState<KitSalesLead[]>([]);
  const [summary, setSummary] = useState<KitSalesPipelineSummary | null>(null);
  const [form] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [leadRows, pipeline] = await Promise.all([
        fetchKitSalesLeads({ limit: 100 }),
        fetchKitSalesPipelineSummary(),
      ]);
      setLeads(leadRows);
      setSummary(pipeline);
    } catch (error) {
      message.error(apiErrorMessage(error, t('create.failed')));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const statusLabel = (code: string) => {
    const key = `status.${code}` as const;
    return t(key, { defaultValue: code });
  };

  const temperatureLabel = (code: string) => {
    const key = `temperature.${code}` as const;
    return t(key, { defaultValue: code });
  };

  const temperatureColor = (code: string) => {
    if (code === 'hot') return 'red';
    if (code === 'warm') return 'orange';
    return 'default';
  };

  const columns = useMemo(
    () => [
      {
        title: t('leads.business'),
        dataIndex: 'businessName',
        key: 'businessName',
      },
      {
        title: t('leads.product'),
        dataIndex: 'productCode',
        key: 'productCode',
        width: 100,
      },
      {
        title: t('leads.status'),
        dataIndex: 'leadStatus',
        key: 'leadStatus',
        width: 120,
        render: (value: string) => <Tag>{statusLabel(value)}</Tag>,
      },
      {
        title: t('leads.temperature'),
        dataIndex: 'leadTemperature',
        key: 'leadTemperature',
        width: 100,
        render: (value: string) => (
          <Tag color={temperatureColor(value)}>{temperatureLabel(value)}</Tag>
        ),
      },
      {
        title: t('leads.score'),
        dataIndex: 'totalScore',
        key: 'totalScore',
        width: 72,
      },
      {
        title: t('leads.source'),
        dataIndex: 'source',
        key: 'source',
        width: 120,
        render: (value?: string | null) => value ?? '—',
      },
      {
        title: t('leads.updated'),
        dataIndex: 'updatedAt',
        key: 'updatedAt',
        width: 140,
        render: (value: string) => formatDate(value),
      },
    ],
    [t],
  );

  const onCreate = async () => {
    const values = await form.validateFields();
    setCreating(true);
    try {
      await createKitSalesProspect({
        businessName: values.businessName.trim(),
        province: values.province?.trim() || undefined,
        phone: values.phone?.trim() || undefined,
        source: values.source?.trim() || undefined,
        notes: values.notes?.trim() || undefined,
      });
      message.success(t('create.success'));
      form.resetFields();
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('create.failed')));
    } finally {
      setCreating(false);
    }
  };

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {t('title')}
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('subtitle')}{' '}
          <Link to="/success/growth">{t('growthDeskLink')}</Link>
        </Typography.Paragraph>
      </div>

      <Card
        title={t('pipeline.title')}
        extra={
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Reload
          </Button>
        }
      >
        <Typography.Text type="secondary">
          {t('pipeline.total', { count: summary?.totalLeads ?? 0 })}
        </Typography.Text>
        <Row gutter={[12, 12]} style={{ marginTop: 12 }}>
          {(summary?.byStatus ?? []).map((bucket) => (
            <Col key={bucket.status} xs={12} sm={8} md={6}>
              <Card size="small">
                <Typography.Text type="secondary">{statusLabel(bucket.status)}</Typography.Text>
                <div style={{ fontSize: 24, fontWeight: 600 }}>{bucket.count}</div>
              </Card>
            </Col>
          ))}
          {!loading && (summary?.byStatus?.length ?? 0) === 0 && (
            <Col span={24}>
              <Typography.Text type="secondary">{t('pipeline.empty')}</Typography.Text>
            </Col>
          )}
        </Row>
      </Card>

      <Card title={t('create.title')}>
        <Form form={form} layout="vertical" onFinish={() => void onCreate()}>
          <Row gutter={16}>
            <Col xs={24} md={12}>
              <Form.Item
                name="businessName"
                label={t('create.businessName')}
                rules={[{ required: true, message: t('create.businessName') }]}
              >
                <Input />
              </Form.Item>
            </Col>
            <Col xs={24} md={6}>
              <Form.Item name="province" label={t('create.province')}>
                <Input />
              </Form.Item>
            </Col>
            <Col xs={24} md={6}>
              <Form.Item name="phone" label={t('create.phone')}>
                <Input />
              </Form.Item>
            </Col>
            <Col xs={24} md={8}>
              <Form.Item name="source" label={t('create.source')}>
                <Input placeholder="referral, field, phc…" />
              </Form.Item>
            </Col>
            <Col xs={24} md={16}>
              <Form.Item name="notes" label={t('create.notes')}>
                <Input.TextArea rows={2} />
              </Form.Item>
            </Col>
          </Row>
          <Button type="primary" icon={<PlusOutlined />} htmlType="submit" loading={creating}>
            {t('create.submit')}
          </Button>
        </Form>
      </Card>

      <Card title={t('leads.title')}>
        <Table
          rowKey="id"
          loading={loading}
          dataSource={leads}
          columns={columns}
          pagination={{ pageSize: 20, showSizeChanger: false }}
          size="middle"
        />
      </Card>
    </Space>
  );
}
