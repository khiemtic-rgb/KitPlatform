import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import {
  Alert,
  Badge,
  Button,
  Card,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Tooltip,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  CheckOutlined,
  CloseOutlined,
  EyeOutlined,
  PlusOutlined,
  ReloadOutlined,
  ShoppingCartOutlined,
  SearchOutlined,
  FilterOutlined,
  FileProtectOutlined,
  BellOutlined,
  ClockCircleOutlined,
  UserOutlined,
  BankOutlined,
  ShopOutlined,
  FlagOutlined,
  NumberOutlined,
  MedicineBoxOutlined,
  InboxOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  consumeConnectStatusEvent,
  createConnectStatusEvent,
  dismissConnectStatusEvent,
  fetchConnectOrgLinks,
  fetchConnectOrgProfile,
  fetchConnectRxHandoff,
  fetchConnectRxHandoffs,
  fetchConnectStatusEvents,
  type ConnectOrgLink,
  type ConnectOrgProfile,
  type ConnectRxHandoff,
  type ConnectStatusEvent,
} from '@/shared/api/connect.api';

const STATUS_COLOR: Record<string, string> = {
  pending_pharmacy: 'orange',
  consumed: 'green',
  dismissed: 'default',
};

const HANDOFF_COLOR: Record<string, string> = {
  pending_pharmacy: 'orange',
  consumed: 'green',
  dismissed: 'default',
};

const EVENT_STATUSES = ['pending_pharmacy', 'consumed', 'dismissed'] as const;

type CreateForm = {
  pharmacyTenantId: string;
  patientDisplayName?: string;
  patientPhone?: string;
  summary?: string;
};

type TabFilters = {
  search: string;
  status?: string;
  partnerId?: string;
};

const DEFAULT_FILTERS: TabFilters = {
  search: '',
  status: 'pending_pharmacy',
  partnerId: undefined,
};

function colTitle(icon: ReactNode, label: string) {
  return (
    <Space size={6}>
      <span style={{ color: 'rgba(0,0,0,0.45)', display: 'inline-flex' }}>{icon}</span>
      <span>{label}</span>
    </Space>
  );
}

function matchesText(haystack: Array<string | undefined | null>, q: string): boolean {
  if (!q.trim()) return true;
  const hay = haystack.filter(Boolean).join(' ').toLowerCase();
  return hay.includes(q.trim().toLowerCase());
}

export function ConnectStatusPage() {
  const { t } = useTranslation('connect');
  const navigate = useNavigate();
  const [profile, setProfile] = useState<ConnectOrgProfile | null>(null);
  const [items, setItems] = useState<ConnectStatusEvent[]>([]);
  const [handoffs, setHandoffs] = useState<ConnectRxHandoff[]>([]);
  const [partners, setPartners] = useState<ConnectOrgLink[]>([]);
  const [loading, setLoading] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<CreateForm>();
  const [rxOpen, setRxOpen] = useState(false);
  const [rxLoading, setRxLoading] = useState(false);
  const [rxDetail, setRxDetail] = useState<ConnectRxHandoff | null>(null);
  const [activeTab, setActiveTab] = useState<string>();
  const [rxFilters, setRxFilters] = useState<TabFilters>(DEFAULT_FILTERS);
  const [queueFilters, setQueueFilters] = useState<TabFilters>(DEFAULT_FILTERS);
  const isClinic = profile?.orgKind === 'clinic';
  const isPharmacy = profile?.orgKind === 'pharmacy';

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const me = await fetchConnectOrgProfile();
      setProfile(me);
      const [events, rxList] = await Promise.all([
        fetchConnectStatusEvents(),
        fetchConnectRxHandoffs(),
      ]);
      setItems(events);
      setHandoffs(rxList);
      if (me?.orgKind === 'clinic') {
        const links = await fetchConnectOrgLinks('active');
        setPartners(links.filter((l) => l.partnerOrgRole === 'pharmacy'));
      }
      setActiveTab((prev) => {
        if (prev) return prev;
        return me?.orgKind === 'pharmacy' ? 'rx' : 'queue';
      });
    } catch (error) {
      message.error(apiErrorMessage(error, t('status.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  /** Signal queue: hide clinic_rx for pharmacy (those live on Rx→POS tab). Clinic sees all outbound. */
  const signalBase = useMemo(() => {
    if (isPharmacy) {
      return items.filter((row) => row.sourceType !== 'clinic_rx');
    }
    return items;
  }, [items, isPharmacy]);

  const pendingHandoffCount = useMemo(
    () => handoffs.filter((r) => r.handoffStatus === 'pending_pharmacy').length,
    [handoffs],
  );
  const pendingSignalCount = useMemo(
    () => signalBase.filter((r) => r.eventStatus === 'pending_pharmacy').length,
    [signalBase],
  );

  const filteredHandoffs = useMemo(() => {
    return handoffs.filter((row) => {
      if (rxFilters.status && row.handoffStatus !== rxFilters.status) return false;
      if (rxFilters.partnerId) {
        const partnerId = isPharmacy ? row.clinicTenantId : row.pharmacyTenantId;
        if (partnerId !== rxFilters.partnerId) return false;
      }
      return matchesText(
        [
          row.patientDisplayName,
          row.patientPhone,
          row.prescriptionCode,
          row.providerDisplayName,
          row.clinicTenantName,
          row.clinicTenantCode,
          row.pharmacyTenantName,
          row.pharmacyTenantCode,
          row.diagnosisText,
        ],
        rxFilters.search,
      );
    });
  }, [handoffs, rxFilters, isPharmacy]);

  const filteredItems = useMemo(() => {
    return signalBase.filter((row) => {
      if (queueFilters.status && row.eventStatus !== queueFilters.status) return false;
      if (queueFilters.partnerId) {
        const partnerId = isPharmacy ? row.clinicTenantId : row.pharmacyTenantId;
        if (partnerId !== queueFilters.partnerId) return false;
      }
      return matchesText(
        [
          row.patientDisplayName,
          row.patientPhone,
          row.summary,
          row.sourceType,
          row.clinicTenantName,
          row.clinicTenantCode,
          row.pharmacyTenantName,
          row.pharmacyTenantCode,
        ],
        queueFilters.search,
      );
    });
  }, [signalBase, queueFilters, isPharmacy]);

  const partnerOptions = useMemo(() => {
    const map = new Map<string, string>();
    if (isPharmacy) {
      for (const row of [...handoffs, ...items]) {
        if (row.clinicTenantId) {
          map.set(row.clinicTenantId, row.clinicTenantName || row.clinicTenantCode);
        }
      }
    } else {
      for (const row of items) {
        map.set(row.pharmacyTenantId, row.pharmacyTenantName || row.pharmacyTenantCode);
      }
      for (const row of handoffs) {
        map.set(row.pharmacyTenantId, row.pharmacyTenantName || row.pharmacyTenantCode);
      }
    }
    return [...map.entries()]
      .map(([value, label]) => ({ value, label }))
      .sort((a, b) => a.label.localeCompare(b.label, 'vi'));
  }, [handoffs, items, isPharmacy]);

  const onCreate = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      await createConnectStatusEvent({
        pharmacyTenantId: values.pharmacyTenantId,
        patientDisplayName: values.patientDisplayName,
        patientPhone: values.patientPhone,
        summary: values.summary,
      });
      message.success(t('status.createSuccess'));
      setCreateOpen(false);
      form.resetFields();
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('status.createFailed')));
    } finally {
      setSaving(false);
    }
  };

  const onViewRx = async (sourceId?: string) => {
    if (!sourceId) return;
    setRxOpen(true);
    setRxLoading(true);
    setRxDetail(null);
    try {
      setRxDetail(await fetchConnectRxHandoff(sourceId));
    } catch (error) {
      message.error(apiErrorMessage(error, t('status.rxEmpty')));
    } finally {
      setRxLoading(false);
    }
  };

  const openPos = (handoffId?: string) => {
    setRxOpen(false);
    if (handoffId) {
      navigate(`/sales/pos?connectHandoffId=${encodeURIComponent(handoffId)}`);
    } else {
      navigate('/sales/pos');
    }
  };

  const onConsume = async (row: ConnectStatusEvent) => {
    try {
      await consumeConnectStatusEvent(row.id);
      message.success(t('status.consumeSuccess'));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('status.actionFailed')));
    }
  };

  const onDismiss = async (id: string) => {
    try {
      await dismissConnectStatusEvent(id);
      message.success(t('status.dismissSuccess'));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('status.actionFailed')));
    }
  };

  const columns: ColumnsType<ConnectStatusEvent> = [
    {
      title: colTitle(<ClockCircleOutlined />, t('status.colWhen')),
      dataIndex: 'createdAt',
      width: '12%',
      render: (v: string) => (v ? dayjs(v).format('DD/MM/YYYY HH:mm') : '—'),
    },
    {
      title: colTitle(<UserOutlined />, t('status.colPatient')),
      key: 'patient',
      width: '16%',
      ellipsis: true,
      render: (_v, row) => (
        <div style={{ minWidth: 0 }}>
          <Typography.Text strong ellipsis style={{ display: 'block', maxWidth: '100%' }}>
            {row.patientDisplayName || '—'}
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }} ellipsis>
            {row.patientPhone || '—'}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: colTitle(<FlagOutlined />, t('status.colStatus')),
      dataIndex: 'eventStatus',
      width: '12%',
      align: 'center',
      render: (s: string) => (
        <Tag color={STATUS_COLOR[s] ?? 'default'} style={{ marginInlineEnd: 0 }}>
          {t(`status.status.${s}`, { defaultValue: s })}
        </Tag>
      ),
    },
    {
      title: t('status.colSource'),
      key: 'source',
      width: '12%',
      align: 'center',
      render: (_, row) => (
        <Tag style={{ marginInlineEnd: 0 }}>
          {t(`status.sourceTypes.${row.sourceType}`, { defaultValue: row.sourceType })}
        </Tag>
      ),
    },
    {
      title: colTitle(
        isPharmacy ? <BankOutlined /> : <ShopOutlined />,
        isPharmacy ? t('status.colClinic') : t('status.colPharmacy'),
      ),
      key: 'partner',
      width: '16%',
      ellipsis: true,
      render: (_, row) => {
        const name = isPharmacy ? row.clinicTenantName : row.pharmacyTenantName;
        const code = isPharmacy ? row.clinicTenantCode : row.pharmacyTenantCode;
        return (
          <Tooltip title={`${name} (${code})`}>
            <Typography.Text ellipsis style={{ display: 'block', maxWidth: '100%' }}>
              {name}
            </Typography.Text>
          </Tooltip>
        );
      },
    },
    {
      title: t('status.colSummary'),
      dataIndex: 'summary',
      width: '18%',
      ellipsis: true,
      render: (v?: string) => v || '—',
    },
    {
      title: t('status.colActions'),
      key: 'actions',
      width: '14%',
      align: 'right',
      render: (_, row) => {
        const actions: ReactNode[] = [];
        if (row.sourceType === 'clinic_rx' && row.sourceId) {
          actions.push(
            <Tooltip key="view" title={t('status.viewRx')}>
              <Button
                size="small"
                icon={<EyeOutlined />}
                onClick={() => void onViewRx(row.sourceId)}
              />
            </Tooltip>,
          );
        }
        if (isPharmacy && row.eventStatus === 'pending_pharmacy') {
          actions.push(
            <Tooltip key="consume" title={t('status.consume')}>
              <Button
                size="small"
                type="primary"
                icon={<CheckOutlined />}
                onClick={() => void onConsume(row)}
              />
            </Tooltip>,
            <Tooltip key="dismiss" title={t('status.dismiss')}>
              <Button
                size="small"
                icon={<CloseOutlined />}
                onClick={() => void onDismiss(row.id)}
              />
            </Tooltip>,
          );
        }
        if (actions.length === 0) {
          return <Typography.Text type="secondary">—</Typography.Text>;
        }
        return (
          <Space size={4} style={{ justifyContent: 'flex-end' }}>
            {actions}
          </Space>
        );
      },
    },
  ];

  const handoffColumns: ColumnsType<ConnectRxHandoff> = [
    {
      title: colTitle(<ClockCircleOutlined />, t('status.colWhen')),
      dataIndex: 'createdAt',
      width: '12%',
      render: (v: string) => (v ? dayjs(v).format('DD/MM/YYYY HH:mm') : '—'),
    },
    {
      title: colTitle(<MedicineBoxOutlined />, t('status.colRxCode')),
      dataIndex: 'prescriptionCode',
      width: '12%',
      ellipsis: true,
    },
    {
      title: colTitle(<UserOutlined />, t('status.colPatient')),
      key: 'patient',
      width: '18%',
      ellipsis: true,
      render: (_, row) => (
        <div style={{ minWidth: 0 }}>
          <Typography.Text strong ellipsis style={{ display: 'block', maxWidth: '100%' }}>
            {row.patientDisplayName || '—'}
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }} ellipsis>
            {row.patientPhone || '—'}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: colTitle(<FlagOutlined />, t('status.colStatus')),
      dataIndex: 'handoffStatus',
      width: '12%',
      align: 'center',
      render: (s: string) => (
        <Tag color={HANDOFF_COLOR[s] ?? 'default'} style={{ marginInlineEnd: 0 }}>
          {t(`status.handoff.${s}`, { defaultValue: s })}
        </Tag>
      ),
    },
    {
      title: colTitle(
        isPharmacy ? <BankOutlined /> : <ShopOutlined />,
        isPharmacy ? t('status.colClinic') : t('status.colPharmacy'),
      ),
      key: 'partner',
      width: '16%',
      ellipsis: true,
      render: (_, row) => {
        const name = isPharmacy ? row.clinicTenantName : row.pharmacyTenantName;
        const code = isPharmacy ? row.clinicTenantCode : row.pharmacyTenantCode;
        return (
          <Tooltip title={`${name} (${code})`}>
            <Typography.Text ellipsis style={{ display: 'block', maxWidth: '100%' }}>
              {name}
            </Typography.Text>
          </Tooltip>
        );
      },
    },
    {
      title: colTitle(<NumberOutlined />, t('status.colLines')),
      key: 'lines',
      width: '8%',
      align: 'center',
      render: (_, row) => row.lines?.length ?? 0,
    },
    {
      title: t('status.colActions'),
      key: 'actions',
      width: '22%',
      align: 'right',
      render: (_, row) => (
        <Space size={4} style={{ justifyContent: 'flex-end' }}>
          {isPharmacy ? (
            <Button
              size="small"
              type="primary"
              icon={<ShoppingCartOutlined />}
              onClick={() => openPos(row.id)}
            >
              {t('status.openPos')}
            </Button>
          ) : null}
          <Tooltip title={t('status.viewRx')}>
            <Button size="small" icon={<EyeOutlined />} onClick={() => void onViewRx(row.id)} />
          </Tooltip>
        </Space>
      ),
    },
  ];

  const renderFilterBar = (
    filters: TabFilters,
    setFilters: (next: TabFilters) => void,
    statusNs: 'status.status' | 'status.handoff',
  ) => (
    <Space wrap style={{ width: '100%', marginBottom: 4 }}>
      <Input
        allowClear
        prefix={<SearchOutlined />}
        placeholder={t('status.searchPlaceholder')}
        value={filters.search}
        onChange={(e) => setFilters({ ...filters, search: e.target.value })}
        style={{ width: 240 }}
      />
      <Select
        allowClear
        placeholder={t('status.filterStatus')}
        suffixIcon={<FilterOutlined />}
        style={{ minWidth: 150 }}
        value={filters.status}
        onChange={(value) => setFilters({ ...filters, status: value })}
        options={EVENT_STATUSES.map((key) => ({
          value: key,
          label: t(`${statusNs}.${key}`),
        }))}
      />
      <Select
        allowClear
        showSearch
        optionFilterProp="label"
        placeholder={t('status.filterPartner')}
        suffixIcon={isPharmacy ? <BankOutlined /> : <ShopOutlined />}
        style={{ minWidth: 200 }}
        value={filters.partnerId}
        onChange={(value) => setFilters({ ...filters, partnerId: value })}
        options={partnerOptions}
      />
    </Space>
  );

  const pharmacyTabs = [
    {
      key: 'rx',
      label: (
        <Space size={8}>
          <FileProtectOutlined />
          <span>{t('status.rxInboxTitle')}</span>
          <Badge
            count={pendingHandoffCount}
            showZero
            color={activeTab === 'rx' ? '#1677ff' : '#8c8c8c'}
            overflowCount={999}
          />
        </Space>
      ),
      children: (
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('status.rxInboxHint')}
          </Typography.Text>
          {renderFilterBar(rxFilters, setRxFilters, 'status.handoff')}
          <Table
            rowKey="id"
            loading={loading}
            columns={handoffColumns}
            dataSource={filteredHandoffs}
            tableLayout="fixed"
            pagination={{ pageSize: 10 }}
            locale={{
              emptyText:
                handoffs.length > 0 && filteredHandoffs.length === 0
                  ? t('status.filterEmpty')
                  : t('status.rxInboxEmpty'),
            }}
          />
        </Space>
      ),
    },
    {
      key: 'queue',
      label: (
        <Space size={8}>
          <BellOutlined />
          <span>{t('status.queueTitle')}</span>
          <Badge
            count={pendingSignalCount}
            showZero
            color={activeTab === 'queue' ? '#1677ff' : '#8c8c8c'}
            overflowCount={999}
          />
        </Space>
      ),
      children: (
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('status.queueHint')}
          </Typography.Text>
          {renderFilterBar(queueFilters, setQueueFilters, 'status.status')}
          <Table
            rowKey="id"
            loading={loading}
            columns={columns}
            dataSource={filteredItems}
            tableLayout="fixed"
            pagination={{ pageSize: 20 }}
            locale={{
              emptyText:
                signalBase.length > 0 && filteredItems.length === 0
                  ? t('status.filterEmpty')
                  : t('status.empty'),
            }}
          />
        </Space>
      ),
    },
  ];

  const clinicTabs = [
    {
      key: 'queue',
      label: (
        <Space size={8}>
          <BellOutlined />
          <span>{t('status.clinicSignalsTitle')}</span>
          <Badge
            count={pendingSignalCount}
            showZero
            color={activeTab === 'queue' ? '#1677ff' : '#8c8c8c'}
            overflowCount={999}
          />
        </Space>
      ),
      children: (
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('status.clinicSignalsHint')}
          </Typography.Text>
          {renderFilterBar(queueFilters, setQueueFilters, 'status.status')}
          <Table
            rowKey="id"
            loading={loading}
            columns={columns}
            dataSource={filteredItems}
            tableLayout="fixed"
            pagination={{ pageSize: 20 }}
            locale={{
              emptyText:
                signalBase.length > 0 && filteredItems.length === 0
                  ? t('status.filterEmpty')
                  : t('status.empty'),
            }}
          />
        </Space>
      ),
    },
    {
      key: 'sentRx',
      label: (
        <Space size={8}>
          <FileProtectOutlined />
          <span>{t('status.clinicSentRxTitle')}</span>
          <Badge
            count={pendingHandoffCount}
            showZero
            color={activeTab === 'sentRx' ? '#1677ff' : '#8c8c8c'}
            overflowCount={999}
          />
        </Space>
      ),
      children: (
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('status.clinicSentRxHint')}
          </Typography.Text>
          {renderFilterBar(rxFilters, setRxFilters, 'status.handoff')}
          <Table
            rowKey="id"
            loading={loading}
            columns={handoffColumns}
            dataSource={filteredHandoffs}
            tableLayout="fixed"
            pagination={{ pageSize: 10 }}
            locale={{
              emptyText:
                handoffs.length > 0 && filteredHandoffs.length === 0
                  ? t('status.filterEmpty')
                  : t('status.clinicSentRxEmpty'),
            }}
          />
        </Space>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            <Space size={8}>
              <InboxOutlined />
              {t('status.title')}
            </Space>
          </Typography.Title>
          <Typography.Text type="secondary">
            {isClinic ? t('status.clinicSubtitle') : t('status.subtitle')}
          </Typography.Text>
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            {t('status.refresh')}
          </Button>
          {isClinic ? (
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => {
                form.resetFields();
                setCreateOpen(true);
              }}
            >
              {t('status.create')}
            </Button>
          ) : null}
        </Space>
      </div>

      {isPharmacy ? <Alert type="info" showIcon message={t('status.pharmacyHint')} /> : null}
      {isClinic ? <Alert type="info" showIcon message={t('status.clinicHint')} /> : null}

      <Card styles={{ body: { paddingTop: 12 } }}>
        {activeTab ? (
          <Tabs
            type="card"
            activeKey={activeTab}
            onChange={setActiveTab}
            items={isPharmacy ? pharmacyTabs : clinicTabs}
          />
        ) : null}
      </Card>

      <Modal
        title={t('status.createTitle')}
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        onOk={() => void onCreate()}
        confirmLoading={saving}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="pharmacyTenantId"
            label={t('status.pharmacy')}
            rules={[{ required: true, message: t('status.pharmacyRequired') }]}
          >
            <Select
              options={partners.map((p) => ({
                value: p.partnerTenantId,
                label: `${p.partnerTenantName} (${p.partnerTenantCode})`,
              }))}
              placeholder={t('status.pharmacyPlaceholder')}
              showSearch
              optionFilterProp="label"
            />
          </Form.Item>
          <Form.Item name="patientDisplayName" label={t('status.patientName')}>
            <Input />
          </Form.Item>
          <Form.Item name="patientPhone" label={t('status.patientPhone')}>
            <Input />
          </Form.Item>
          <Form.Item name="summary" label={t('status.summary')}>
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={t('status.rxTitle')}
        open={rxOpen}
        onCancel={() => setRxOpen(false)}
        footer={
          <Space>
            {isPharmacy ? (
              <Button
                type="primary"
                icon={<ShoppingCartOutlined />}
                onClick={() => openPos(rxDetail?.id)}
              >
                {t('status.openPos')}
              </Button>
            ) : null}
            <Button onClick={() => setRxOpen(false)}>{t('status.rxClose')}</Button>
          </Space>
        }
        destroyOnClose
        width={520}
      >
        {rxLoading ? (
          <Typography.Text type="secondary">…</Typography.Text>
        ) : rxDetail ? (
          <Space direction="vertical" style={{ width: '100%' }} size={12}>
            <div>
              <Typography.Text strong>{rxDetail.prescriptionCode}</Typography.Text>
              <Tag
                color={HANDOFF_COLOR[rxDetail.handoffStatus] ?? 'default'}
                style={{ marginInlineStart: 8 }}
              >
                {t(`status.handoff.${rxDetail.handoffStatus}`, {
                  defaultValue: rxDetail.handoffStatus,
                })}
              </Tag>
            </div>
            <div>
              {rxDetail.patientDisplayName || '—'}
              {rxDetail.patientPhone ? ` · ${rxDetail.patientPhone}` : ''}
            </div>
            <div>
              {t('status.rxProvider')}: {rxDetail.providerDisplayName || '—'}
            </div>
            {rxDetail.diagnosisText ? (
              <div>
                {t('status.rxDiagnosis')}: {rxDetail.diagnosisText}
              </div>
            ) : null}
            <Typography.Text type="secondary">{t('status.rxLinesTitle')}</Typography.Text>
            {rxDetail.lines.map((line, idx) => (
              <div key={`${line.drugName}-${idx}`}>
                {idx + 1}. {line.drugName}
                {line.strength ? ` (${line.strength})` : ''} — {line.quantity}
                {line.unit ? ` ${line.unit}` : ''}
                {line.dosageInstruction ? (
                  <Typography.Text type="secondary"> · {line.dosageInstruction}</Typography.Text>
                ) : null}
              </div>
            ))}
            {isPharmacy ? <Alert type="info" showIcon message={t('status.rxNotPos')} /> : null}
          </Space>
        ) : (
          <Typography.Text type="secondary">{t('status.rxEmpty')}</Typography.Text>
        )}
      </Modal>
    </Space>
  );
}
