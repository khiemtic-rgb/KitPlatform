import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Button,
  Card,
  Col,
  Form,
  Input,
  Modal,
  Popconfirm,
  Row,
  Select,
  Space,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import { DeleteOutlined, EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  createKitSalesProspect,
  deleteKitSalesLead,
  fetchKitSalesLeadFilters,
  fetchKitSalesLeads,
  importKitSalesProspects,
  updateKitSalesLead,
  type KitSalesLead,
  type KitSalesLeadFilters,
} from '@/shared/api/kit-sales.api';
import {
  KIT_SALES_FACEBOOK_KINDS,
  KIT_SALES_PROVINCES,
  KIT_SALES_STATUSES,
  KIT_SALES_TEMPERATURES,
  kitSalesActionI18nKey,
} from './kit-sales.constants';
import { formatKitSalesDate, kitSalesTemperatureColor } from './kit-sales.helpers';

export function KitSalesLeadsPage() {
  const { t } = useTranslation('kitSales');
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [importing, setImporting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [leads, setLeads] = useState<KitSalesLead[]>([]);
  const [editing, setEditing] = useState<KitSalesLead | null>(null);
  const [provinceFilter, setProvinceFilter] = useState<string | null>('Thái Nguyên');
  const [facebookKindFilter, setFacebookKindFilter] = useState<string | null>(null);
  const [filters, setFilters] = useState<KitSalesLeadFilters | null>(null);
  const [form] = Form.useForm();
  const [bulkForm] = Form.useForm();
  const [editForm] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [leadRows, leadFilters] = await Promise.all([
        fetchKitSalesLeads({
          limit: 200,
          province: provinceFilter ?? undefined,
          facebookKind: facebookKindFilter ?? undefined,
        }),
        fetchKitSalesLeadFilters(),
      ]);
      setLeads(leadRows);
      setFilters(leadFilters);
    } catch (error) {
      message.error(apiErrorMessage(error, t('create.failed')));
    } finally {
      setLoading(false);
    }
  }, [facebookKindFilter, provinceFilter, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const statusLabel = (code: string) => t(`status.${code}`, { defaultValue: code });
  const temperatureLabel = (code: string) => t(`temperature.${code}`, { defaultValue: code });
  const actionLabel = (code: string) => t(kitSalesActionI18nKey(code), { defaultValue: code });
  const facebookKindLabel = (code?: string | null) =>
    code === 'page' || code === 'profile' ? t(`filter.${code}`) : t('filter.allKinds');

  const provinceOptions = useMemo(() => {
    const extra = (filters?.provinces ?? []).filter(
      (name) => !KIT_SALES_PROVINCES.includes(name as (typeof KIT_SALES_PROVINCES)[number]),
    );
    return [...KIT_SALES_PROVINCES, ...extra].map((name) => ({ value: name, label: name }));
  }, [filters]);

  const kindCounts = useMemo(() => {
    const counts = Object.fromEntries((filters?.byFacebookKind ?? []).map((b) => [b.status, b.count]));
    return { profile: counts.profile ?? 0, page: counts.page ?? 0 };
  }, [filters]);

  const openEdit = (lead: KitSalesLead) => {
    setEditing(lead);
    editForm.setFieldsValue({
      businessName: lead.businessName,
      province: lead.province ?? '',
      phone: lead.phone ?? '',
      source: lead.facebookUrl ?? lead.source ?? '',
      facebookKind: lead.facebookKind ?? undefined,
      leadStatus: lead.leadStatus,
      leadTemperature: lead.leadTemperature,
      notes: lead.notes ?? '',
    });
  };

  const onSaveEdit = async () => {
    if (!editing) return;
    const values = await editForm.validateFields();
    setSaving(true);
    try {
      await updateKitSalesLead(editing.id, {
        businessName: values.businessName.trim(),
        province: values.province?.trim() || undefined,
        phone: values.phone?.trim() || undefined,
        source: values.source?.trim() || undefined,
        facebookKind: values.facebookKind || undefined,
        leadStatus: values.leadStatus,
        leadTemperature: values.leadTemperature,
        notes: values.notes?.trim() || undefined,
      });
      message.success(t('leads.editSuccess'));
      setEditing(null);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('leads.editFailed')));
    } finally {
      setSaving(false);
    }
  };

  const onDelete = async (lead: KitSalesLead) => {
    setDeletingId(lead.id);
    try {
      await deleteKitSalesLead(lead.id);
      message.success(t('leads.deleteSuccess'));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('leads.deleteFailed')));
    } finally {
      setDeletingId(null);
    }
  };

  const columns = useMemo(
    () => [
      {
        title: t('leads.business'),
        dataIndex: 'businessName',
        key: 'businessName',
      },
      {
        title: t('create.province'),
        dataIndex: 'province',
        key: 'province',
        width: 120,
        render: (value?: string | null) => value || '—',
      },
      {
        title: t('leads.facebook'),
        key: 'facebook',
        width: 180,
        render: (_: unknown, row: KitSalesLead) =>
          row.facebookKind || row.facebookUrl ? (
            <Space size={4} wrap>
              {row.facebookKind && (
                <Tag color={row.facebookKind === 'page' ? 'blue' : 'default'}>
                  {facebookKindLabel(row.facebookKind)}
                </Tag>
              )}
              {row.facebookUrl && (
                <Typography.Link
                  href={row.facebookUrl}
                  target="_blank"
                  rel="noreferrer"
                  onClick={(event) => event.stopPropagation()}
                >
                  {t('leads.openFacebook')}
                </Typography.Link>
              )}
            </Space>
          ) : (
            '—'
          ),
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
          <Tag color={kitSalesTemperatureColor(value)}>{temperatureLabel(value)}</Tag>
        ),
      },
      {
        title: t('leads.nextAction'),
        key: 'nextAction',
        width: 160,
        render: (_: unknown, row: KitSalesLead) =>
          row.nextActionCode
            ? `${actionLabel(row.nextActionCode)}${row.nextActionAt ? ` · ${formatKitSalesDate(row.nextActionAt)}` : ''}`
            : '—',
      },
      {
        title: t('leads.updated'),
        dataIndex: 'updatedAt',
        key: 'updatedAt',
        width: 140,
        render: (value: string) => formatKitSalesDate(value),
      },
      {
        title: t('leads.actions'),
        key: 'actions',
        width: 120,
        fixed: 'right' as const,
        render: (_: unknown, row: KitSalesLead) => (
          <Space size={4}>
            <Button
              type="link"
              size="small"
              icon={<EditOutlined />}
              onClick={(event) => {
                event.stopPropagation();
                openEdit(row);
              }}
            >
              {t('leads.edit')}
            </Button>
            <Popconfirm
              title={t('leads.deleteConfirm', { name: row.businessName })}
              okText={t('leads.delete')}
              okButtonProps={{ danger: true }}
              onConfirm={() => void onDelete(row)}
            >
              <Button
                type="link"
                size="small"
                danger
                icon={<DeleteOutlined />}
                loading={deletingId === row.id}
                onClick={(event) => event.stopPropagation()}
              >
                {t('leads.delete')}
              </Button>
            </Popconfirm>
          </Space>
        ),
      },
    ],
    [actionLabel, deletingId, t],
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
        facebookKind: values.facebookKind || undefined,
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

  const onImport = async () => {
    const values = await bulkForm.validateFields();
    setImporting(true);
    try {
      const result = await importKitSalesProspects({
        text: values.text,
        province: values.province?.trim() || undefined,
        facebookKind: values.facebookKind || undefined,
      });
      if (result.errors.length > 0) {
        message.warning(
          `${t('create.bulkSuccess', { created: result.created, skipped: result.skipped })} ${result.errors[0]}`,
        );
      } else {
        message.success(t('create.bulkSuccess', { created: result.created, skipped: result.skipped }));
      }
      if (result.created > 0) bulkForm.setFieldValue('text', '');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('create.bulkFailed')));
    } finally {
      setImporting(false);
    }
  };

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Space wrap>
        <Typography.Title level={3} style={{ marginBottom: 0 }}>
          {t('leads.title')}
        </Typography.Title>
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          {t('pipeline.reload')}
        </Button>
      </Space>

      <Card size="small">
        <Row gutter={16}>
          <Col xs={24} md={10}>
            <Typography.Text type="secondary">{t('filter.province')}</Typography.Text>
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={t('filter.allProvinces')}
              value={provinceFilter}
              onChange={(value) => setProvinceFilter(value ?? null)}
              options={provinceOptions}
              style={{ width: '100%', marginTop: 8 }}
            />
          </Col>
          <Col xs={24} md={14}>
            <Typography.Text type="secondary">{t('filter.kind')}</Typography.Text>
            <Space wrap style={{ marginTop: 8 }}>
              <Button
                size="small"
                type={facebookKindFilter === null ? 'primary' : 'default'}
                onClick={() => setFacebookKindFilter(null)}
              >
                {t('filter.allKinds')}
              </Button>
              {KIT_SALES_FACEBOOK_KINDS.map((kind) => (
                <Button
                  key={kind}
                  size="small"
                  type={facebookKindFilter === kind ? 'primary' : 'default'}
                  onClick={() => setFacebookKindFilter(facebookKindFilter === kind ? null : kind)}
                >
                  {facebookKindLabel(kind)} · {kindCounts[kind]}
                </Button>
              ))}
            </Space>
          </Col>
        </Row>
      </Card>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={leads}
        columns={columns}
        pagination={{ pageSize: 20, showSizeChanger: false }}
        size="middle"
        scroll={{ x: 1100 }}
        onRow={(row) => ({
          onClick: () => navigate(`/kit-sales/leads/${row.id}`),
          style: { cursor: 'pointer' },
        })}
      />

      <Card title={t('create.bulkTitle')} size="small">
        <Form
          form={bulkForm}
          layout="vertical"
          initialValues={{ province: 'Thái Nguyên' }}
          onFinish={() => void onImport()}
        >
          <Form.Item name="text" rules={[{ required: true, message: t('create.sourceHint') }]}>
            <Input.TextArea rows={4} placeholder={t('create.bulkHint')} />
          </Form.Item>
          <Row gutter={16}>
            <Col xs={24} md={12}>
              <Form.Item name="province" label={t('filter.province')}>
                <Select allowClear showSearch optionFilterProp="label" options={provinceOptions} />
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item name="facebookKind" label={t('create.facebookKind')}>
                <Select
                  allowClear
                  placeholder={t('create.facebookKindAuto')}
                  options={KIT_SALES_FACEBOOK_KINDS.map((kind) => ({
                    value: kind,
                    label: facebookKindLabel(kind),
                  }))}
                />
              </Form.Item>
            </Col>
          </Row>
          <Button type="primary" htmlType="submit" loading={importing}>
            {t('create.bulkSubmit')}
          </Button>
        </Form>
      </Card>

      <Card title={t('create.title')} size="small">
        <Form
          form={form}
          layout="vertical"
          initialValues={{ province: 'Thái Nguyên' }}
          onFinish={() => void onCreate()}
        >
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
              <Form.Item name="province" label={t('filter.province')}>
                <Select allowClear showSearch optionFilterProp="label" options={provinceOptions} />
              </Form.Item>
            </Col>
            <Col xs={24} md={6}>
              <Form.Item name="phone" label={t('create.phone')}>
                <Input />
              </Form.Item>
            </Col>
            <Col xs={24} md={14}>
              <Form.Item name="source" label={t('create.source')}>
                <Input placeholder={t('create.sourceHint')} />
              </Form.Item>
            </Col>
            <Col xs={24} md={10}>
              <Form.Item name="facebookKind" label={t('create.facebookKind')}>
                <Select
                  allowClear
                  placeholder={t('create.facebookKindAuto')}
                  options={KIT_SALES_FACEBOOK_KINDS.map((kind) => ({
                    value: kind,
                    label: facebookKindLabel(kind),
                  }))}
                />
              </Form.Item>
            </Col>
            <Col xs={24}>
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

      <Modal
        title={t('leads.editTitle')}
        open={Boolean(editing)}
        onCancel={() => setEditing(null)}
        onOk={() => void onSaveEdit()}
        confirmLoading={saving}
        okText={t('leads.save')}
        destroyOnClose
        width={640}
      >
        <Form form={editForm} layout="vertical">
          <Form.Item
            name="businessName"
            label={t('create.businessName')}
            rules={[{ required: true, message: t('create.businessName') }]}
          >
            <Input />
          </Form.Item>
          <Row gutter={16}>
            <Col span={12}>
              <Form.Item name="province" label={t('filter.province')}>
                <Select allowClear showSearch optionFilterProp="label" options={provinceOptions} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="phone" label={t('create.phone')}>
                <Input />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="leadStatus" label={t('leads.status')}>
                <Select
                  options={KIT_SALES_STATUSES.map((code) => ({
                    value: code,
                    label: statusLabel(code),
                  }))}
                />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="leadTemperature" label={t('leads.temperature')}>
                <Select
                  options={KIT_SALES_TEMPERATURES.map((code) => ({
                    value: code,
                    label: temperatureLabel(code),
                  }))}
                />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="source" label={t('create.source')}>
            <Input placeholder={t('create.sourceHint')} />
          </Form.Item>
          <Form.Item name="facebookKind" label={t('create.facebookKind')}>
            <Select
              allowClear
              placeholder={t('create.facebookKindAuto')}
              options={KIT_SALES_FACEBOOK_KINDS.map((kind) => ({
                value: kind,
                label: facebookKindLabel(kind),
              }))}
            />
          </Form.Item>
          <Form.Item name="notes" label={t('create.notes')}>
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
