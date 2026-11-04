import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, App, Button, Card, Descriptions, Drawer, Empty, Form, Input, InputNumber, Result, Space, Switch, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { apiErrorMessage } from '@/shared/api/api-error';
import { http } from '@/shared/api/http';
import {
  fetchKnowledgeApprovalHistory,
  fetchPharmacyKnowledgeCatalog,
  fetchPharmacyKnowledgeVersion,
  pinKnowledgeVersion,
  retireKnowledgePin,
  saveKnowledgePolicy,
  type KnowledgeAdoption,
  type KnowledgeApprovalEvent,
  type KnowledgeVersion,
} from '@/shared/api/pharmacy-knowledge.api';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useCanApproveTenantKnowledge } from '@/shared/auth/usePermission';
import {
  activePin,
  groupPharmacyVersions,
  localKnowledgeToday,
  pinBlockers,
  pinsMissingPublishedVersion,
  runtimeUsesVersion,
  pharmacyListGroup,
} from '@/modules/pharmacy/knowledge-pharmacy-groups';

function day(value?: string | null) {
  return value ? value.slice(0, 10) : '—';
}

function previewAccess() {
  if (!import.meta.env.DEV) return null;
  const params = new URLSearchParams(window.location.search);
  const onPreviewRoute = window.location.pathname.endsWith('/k2a-ui-preview');
  if (!onPreviewRoute && params.get('k2preview') !== '1') return null;
  return params.get('access') ?? 'none';
}

function previewVersion(
  ruleCode: string,
  version: string,
  sourceVerificationStatus: string,
  effectiveTo: string | null,
  reasonVi: string,
): KnowledgeVersion {
  const accepted = sourceVerificationStatus === 'ACCEPTED';
  return {
    ruleCode,
    version,
    lifecycleStatus: 'PUBLISHED',
    source: 'novixa',
    effectiveFrom: '2020-01-01',
    effectiveTo,
    symptomCode: 'cough',
    categoryCodes: ['HO_HAP'],
    keywords: ['synthetic'],
    excludeKeywords: [],
    reasonVi,
    priority: 100,
    sourceVerificationStatus,
    referenceSource: accepted ? 'synthetic-citation' : null,
    sourceIdentity: accepted ? 'SYNTH-TEST' : null,
    sourceUrl: accepted ? 'https://example.test/k2-fixture' : null,
    issuingOrganization: accepted ? 'synthetic-org' : null,
    evidenceNote: accepted ? 'fixture section' : null,
    verifiedBy: accepted ? 'k2-preview' : null,
    verifiedAt: accepted ? '2026-11-01T00:00:00Z' : null,
  };
}

const previewVersions: KnowledgeVersion[] = [
  previewVersion('k2syn-used', '1', 'ACCEPTED', null, 'Ho khan, bản đang ghim'),
  previewVersion('k2syn-used', '2', 'ACCEPTED', null, 'Ho khan, bản mới chưa ghim'),
  previewVersion('k2syn-open', '1', 'ACCEPTED', null, 'Sổ mũi, chưa ghim'),
  previewVersion('k2syn-stale', '1', 'ACCEPTED', '2020-01-02', 'Pin đã hết hiệu lực'),
  previewVersion('k2syn-gap', '1', 'UNVERIFIED', null, 'Thiếu nguồn, không ghim'),
  previewVersion('KR_OTHER', '1', 'ACCEPTED', null, 'Không dùng tại quầy'),
];

const previewPins: KnowledgeAdoption[] = [
  { ruleCode: 'k2syn-used', ruleVersion: '1', status: 'APPROVED', referenceSource: 'ghi chú pin', productMatchingEnabled: true, extraExcludeKeywords: [], priorityOverride: null },
  { ruleCode: 'k2syn-stale', ruleVersion: '1', status: 'APPROVED', referenceSource: 'ghi chú pin', productMatchingEnabled: true, extraExcludeKeywords: [], priorityOverride: null },
  { ruleCode: 'k2syn-missing', ruleVersion: '1', status: 'APPROVED', referenceSource: 'pin mất bản publish', productMatchingEnabled: true, extraExcludeKeywords: [], priorityOverride: null },
];

type PreviewCall = { method?: string; url?: string; data?: unknown };

function installPreviewTransport() {
  const target = window as Window & { __k2previewCalls?: PreviewCall[]; __k2previewTransport?: boolean };
  if (target.__k2previewTransport) return;
  target.__k2previewTransport = true;
  target.__k2previewCalls = [];
  http.interceptors.request.use((config) => {
    if (!window.location.pathname.endsWith('/k2a-ui-preview')) return config;
    config.adapter = async (cfg) => {
      target.__k2previewCalls?.push({
        method: cfg.method,
        url: cfg.url,
        data: typeof cfg.data === 'string' ? JSON.parse(cfg.data) : cfg.data,
      });
      return { data: {}, status: 200, statusText: 'OK', headers: {}, config: cfg };
    };
    return config;
  });
}

/** Dev-only harness. Production builds drop the route. Agree records the API call and does not reach a tenant. */
export function KnowledgeApprovalPreviewPage() {
  installPreviewTransport();
  const access = new URLSearchParams(window.location.search).get('access') ?? 'none';
  const permissions = access === 'approve' ? ['pharmacy.knowledge.approve'] : [];
  useAuthStore.setState({
    accessToken: 'k2-preview',
    refreshToken: null,
    user: {
      id: '00000000-0000-4000-8000-000000000011',
      tenantId: '00000000-0000-4000-8000-000000000012',
      tenantCode: 'K2_PREVIEW',
      username: 'k2-preview',
      email: 'k2-preview@example.test',
      roles: access === 'admin' ? ['ADMIN'] : ['STAFF'],
      permissions,
    },
  });
  return <KnowledgeApprovalPage />;
}

export function KnowledgeApprovalPage() {
  const allowed = useCanApproveTenantKnowledge();
  const preview = previewAccess();
  const today = localKnowledgeToday();
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [versions, setVersions] = useState<KnowledgeVersion[]>([]);
  const [pins, setPins] = useState<KnowledgeAdoption[]>([]);
  const [open, setOpen] = useState<KnowledgeVersion | null>(null);
  const [history, setHistory] = useState<KnowledgeApprovalEvent[]>([]);
  const [source, setSource] = useState('');
  const [codeQuery, setCodeQuery] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const submitLock = useRef(false);
  const confirmLock = useRef(false);
  const [form] = Form.useForm();
  const { modal } = App.useApp();

  const load = useCallback(async () => {
    if (preview) {
      setVersions(previewVersions);
      setPins(previewPins);
      setLoadError(null);
      return;
    }
    setLoading(true);
    setLoadError(null);
    try {
      const catalog = await fetchPharmacyKnowledgeCatalog();
      setVersions(catalog.publishedVersions);
      setPins(catalog.adoptions);
    } catch (error) {
      const text = apiErrorMessage(error, 'Không tải được tri thức đã xuất bản');
      setLoadError(text);
      message.error(text);
    } finally {
      setLoading(false);
    }
  }, [preview]);

  useEffect(() => {
    if (allowed) void load();
  }, [allowed, load]);

  const pinFor = useCallback(
    (ruleCode: string) => pins.find((item) => item.ruleCode === ruleCode),
    [pins],
  );

  const show = async (row: KnowledgeVersion) => {
    const pin = activePin(pins, row.ruleCode) ?? pinFor(row.ruleCode);
    form.setFieldsValue({
      productMatchingEnabled: pin?.productMatchingEnabled ?? true,
      extraExcludeKeywords: (pin?.extraExcludeKeywords ?? []).join(', '),
      priorityOverride: pin?.priorityOverride ?? null,
    });
    if (preview) {
      setOpen(row);
      setHistory([{
        ruleCode: row.ruleCode,
        version: pin?.ruleVersion ?? null,
        fromStatus: 'NONE',
        toStatus: pin?.status ?? 'NONE',
        actorUserId: 'k2-preview',
        referenceSource: pin?.referenceSource,
        createdAt: '2026-11-01T00:00:00Z',
      }]);
      return;
    }
    try {
      const detail = await fetchPharmacyKnowledgeVersion(row.ruleCode, row.version);
      setOpen(detail);
      setHistory(await fetchKnowledgeApprovalHistory(row.ruleCode));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không xem được phiên bản'));
    }
  };

  const applyPreviewPin = (ruleCode: string, version: string, status: 'APPROVED' | 'RETIRED', note: string) => {
    setPins((current) => {
      const existing = current.find((item) => item.ruleCode === ruleCode);
      const next: KnowledgeAdoption = {
        ruleCode,
        ruleVersion: version,
        status,
        referenceSource: note || existing?.referenceSource || null,
        productMatchingEnabled: existing?.productMatchingEnabled ?? true,
        extraExcludeKeywords: existing?.extraExcludeKeywords ?? [],
        priorityOverride: existing?.priorityOverride ?? null,
      };
      return [...current.filter((item) => item.ruleCode !== ruleCode), next];
    });
  };

  const pin = async () => {
    if (!open || submitLock.current) return;
    const note = source.trim();
    submitLock.current = true;
    setSubmitting(true);
    try {
      await pinKnowledgeVersion(open.ruleCode, open.version, note);
      if (preview) applyPreviewPin(open.ruleCode, open.version, 'APPROVED', note);
      message.success('Đã ghim phiên bản cho nhà thuốc này');
      setSource('');
      setOpen(null);
      if (!preview) await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không ghim được'));
    } finally {
      submitLock.current = false;
      setSubmitting(false);
    }
  };

  const confirmPin = () => {
    if (!open || submitting || confirmLock.current) return;
    const current = activePin(pins, open.ruleCode);
    confirmLock.current = true;
    modal.confirm({
      title: 'Ghim đúng phiên bản này?',
      content: current
        ? `Nhà thuốc sẽ dùng ${open.ruleCode} phiên bản ${open.version} thay cho phiên bản ${current.ruleVersion}. Pin không tự nâng khi có bản mới. Nhà thuốc khác không đổi.`
        : `Nhà thuốc sẽ dùng ${open.ruleCode} phiên bản ${open.version}. Không ghim thì runtime không dùng rule này, và không có fallback sang catalog chung.`,
      okText: 'Ghim',
      cancelText: 'Hủy',
      onCancel: () => { confirmLock.current = false; },
      onOk: async () => {
        try { await pin(); } finally { confirmLock.current = false; }
      },
    });
  };

  const retirePin = async (ruleCode: string, version: string) => {
    if (submitLock.current) return;
    submitLock.current = true;
    setSubmitting(true);
    try {
      await retireKnowledgePin(ruleCode, version);
      if (preview) applyPreviewPin(ruleCode, version, 'RETIRED', '');
      message.success('Đã thu hồi pin');
      setOpen(null);
      if (!preview) await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không thu hồi được pin'));
    } finally {
      submitLock.current = false;
      setSubmitting(false);
    }
  };

  const confirmRetire = (ruleCode: string, version: string) => {
    if (submitting || confirmLock.current) return;
    confirmLock.current = true;
    modal.confirm({
      title: 'Thu hồi pin?',
      content: 'Quầy nhà thuốc này sẽ không còn dùng rule. Không có pin đang hiệu lực thì runtime không dùng rule, và không có fallback sang tenant khác.',
      okText: 'Thu hồi pin',
      cancelText: 'Hủy',
      okButtonProps: { danger: true },
      onCancel: () => { confirmLock.current = false; },
      onOk: async () => {
        try { await retirePin(ruleCode, version); } finally { confirmLock.current = false; }
      },
    });
  };

  const savePolicy = async () => {
    if (!open) return;
    const values = await form.validateFields();
    const words = String(values.extraExcludeKeywords ?? '')
      .split(',')
      .map((item: string) => item.trim())
      .filter(Boolean);
    if (preview) {
      setPins((current) => current.map((item) => (
        item.ruleCode === open.ruleCode
          ? {
              ...item,
              productMatchingEnabled: values.productMatchingEnabled !== false,
              extraExcludeKeywords: words,
              priorityOverride: values.priorityOverride ?? null,
            }
          : item
      )));
      message.success('Đã lưu chính sách thu hẹp');
      return;
    }
    try {
      await saveKnowledgePolicy(open.ruleCode, {
        productMatchingEnabled: values.productMatchingEnabled !== false,
        extraExcludeKeywords: words,
        priorityOverride: values.priorityOverride ?? null,
      });
      message.success('Đã lưu chính sách thu hẹp');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được chính sách'));
    }
  };

  const groups = useMemo(
    () => groupPharmacyVersions(versions, pins, today),
    [versions, pins, today],
  );
  const runtimeRows = useMemo(
    () => versions.filter((row) => runtimeUsesVersion(row, activePin(pins, row.ruleCode), today)),
    [versions, pins, today],
  );
  const missingPins = useMemo(() => pinsMissingPublishedVersion(versions, pins), [versions, pins]);

  if (!allowed) {
    return (
      <Result
        status="403"
        title="Không có quyền duyệt tri thức"
        subTitle="Cần vai trò ADMIN của đúng nhà thuốc, hoặc pharmacy.knowledge.approve. Quyền này không mở review, xác minh nguồn hay xuất bản."
      />
    );
  }

  const matches = (row: KnowledgeVersion) =>
    !codeQuery || row.ruleCode.toLowerCase().includes(codeQuery.trim().toLowerCase());

  const columns: ColumnsType<KnowledgeVersion> = [
    { title: 'Mã', dataIndex: 'ruleCode', width: 160 },
    { title: 'Mô tả', dataIndex: 'reasonVi', ellipsis: true },
    { title: 'Phiên bản', dataIndex: 'version', width: 110 },
    {
      title: 'Xuất bản',
      dataIndex: 'lifecycleStatus',
      width: 120,
      render: (value: string) => <Tag>{value}</Tag>,
    },
    {
      title: 'Nguồn',
      width: 220,
      render: (_, row) => (
        <span>
          <Tag>{row.sourceVerificationStatus ?? 'UNVERIFIED'}</Tag>
          {' '}
          {row.referenceSource || 'Chưa có trích dẫn'}
        </span>
      ),
    },
    {
      title: 'Áp dụng',
      width: 180,
      render: (_, row) => {
        const pin = activePin(pins, row.ruleCode);
        const group = pharmacyListGroup(row, pin, today);
        if (group === 'inUse') return <Tag color="green">Đang dùng {row.version}</Tag>;
        if (group === 'newer') return <Tag color="gold">Mới hơn pin {pin?.ruleVersion}</Tag>;
        if (group === 'inactivePin') return <Tag>Pin không còn hiệu lực</Tag>;
        if (group === 'older') return <Tag>Cũ hơn pin {pin?.ruleVersion}</Tag>;
        return <Tag>Chưa ghim</Tag>;
      },
    },
    {
      title: '',
      width: 90,
      render: (_, row) => (
        <Button type="link" onClick={() => void show(row)}>Xem</Button>
      ),
    },
  ];

  const currentPin = open ? activePin(pins, open.ruleCode) : undefined;
  const pinnedThisVersion = currentPin?.ruleVersion === open?.version;
  const blockers = open ? pinBlockers(open, today) : [];
  const runtimeHere = open ? runtimeUsesVersion(open, currentPin, today) : false;

  const section = (title: string, hint: string, rows: KnowledgeVersion[], empty: string) => {
    const data = rows.filter(matches);
    return (
      <div style={{ marginBottom: 24 }}>
        <Typography.Title level={5} style={{ marginBottom: 4 }}>{title}</Typography.Title>
        <Typography.Paragraph type="secondary">{hint}</Typography.Paragraph>
        {data.length === 0 ? (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={empty} />
        ) : (
          <Table
            rowKey={(row) => `${title}:${row.ruleCode}:${row.version}`}
            loading={loading}
            columns={columns}
            dataSource={data}
            pagination={false}
          />
        )}
      </div>
    );
  };

  return (
    <Card title="Tiếp nhận tri thức nền tảng">
      <Typography.Paragraph type="secondary">
        Nhà thuốc chọn phiên bản nền tảng đã xuất bản để dùng. Đây không phải duyệt tri thức nội bộ của nhà thuốc.
      </Typography.Paragraph>
      <Alert
        type={runtimeRows.length === 0 ? 'warning' : 'info'}
        showIcon
        style={{ marginBottom: 16 }}
        message={runtimeRows.length === 0
          ? 'Không có pin đang hiệu lực'
          : `Runtime đang dùng ${runtimeRows.map((row) => `${row.ruleCode} phiên bản ${row.version}`).join(', ')}`}
        description="Có phiên bản mới không tự đổi pin. Không có pin thì runtime không dùng rule, và không lấy rule của nhà thuốc khác."
      />
      <Space wrap style={{ marginBottom: 16 }}>
        <Input
          allowClear
          placeholder="Lọc mã"
          value={codeQuery}
          onChange={(event) => setCodeQuery(event.target.value)}
          style={{ width: 180 }}
        />
      </Space>
      {loadError ? <Alert type="error" showIcon style={{ marginBottom: 16 }} message={loadError} /> : null}
      {missingPins.map((pin) => (
        <Alert
          key={`${pin.ruleCode}:${pin.ruleVersion}`}
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          message={`${pin.ruleCode} đang ghim phiên bản ${pin.ruleVersion} nhưng bản đó không còn PUBLISHED`}
          description="Runtime không dùng pin này. Không tự chuyển sang phiên bản khác."
        />
      ))}
      {!loading && !loadError && versions.length === 0 ? (
        <Empty description="Chưa có phiên bản đã xuất bản. Không tạo pin để minh họa." />
      ) : (
        <>
          {section(
            'Đang áp dụng',
            'Pin APPROVED, đúng phiên bản PUBLISHED còn hiệu lực.',
            groups.inUse,
            codeQuery ? 'Không có phiên bản khớp bộ lọc.' : 'Không có pin đang hiệu lực.',
          )}
          {groups.inactivePin.filter(matches).length > 0 ? section(
            'Pin không còn hiệu lực',
            'Nhà thuốc đã ghim, nhưng phiên bản không còn trong thời gian hiệu lực hoặc không còn là bản runtime dùng.',
            groups.inactivePin,
            '',
          ) : null}
          {section(
            'Chưa áp dụng',
            'Đã PUBLISHED. Nhà thuốc chưa ghim.',
            groups.notAdopted,
            'Không có rule đã xuất bản mà chưa ghim.',
          )}
          {section(
            'Có phiên bản mới',
            'Đã có bản PUBLISHED mới hơn pin. Pin hiện tại giữ nguyên cho đến khi nhà thuốc ghim bản mới.',
            groups.newer,
            'Không có phiên bản mới hơn pin.',
          )}
          {groups.older.filter(matches).length > 0 ? section(
            'Phiên bản cũ hơn pin',
            'Vẫn xuất bản, không phải bản runtime đang dùng.',
            groups.older,
            '',
          ) : null}
        </>
      )}
      <Drawer
        title={open ? `${open.ruleCode} · phiên bản ${open.version}` : ''}
        open={open !== null}
        width={560}
        onClose={() => setOpen(null)}
      >
        {open ? (
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Alert
              type={runtimeHere ? 'success' : 'warning'}
              showIcon
              message={runtimeHere
                ? 'Runtime của nhà thuốc này đang dùng đúng phiên bản này.'
                : currentPin && pharmacyListGroup(open, currentPin, today) === 'newer'
                  ? `Runtime vẫn dùng phiên bản ${currentPin.ruleVersion}. Bản này chưa được ghim.`
                  : 'Không có pin đang hiệu lực cho phiên bản này. Runtime không dùng rule từ dòng này.'}
            />
            <Descriptions column={1} size="small">
              <Descriptions.Item label="Mã">{open.ruleCode}</Descriptions.Item>
              <Descriptions.Item label="Phiên bản">{open.version}</Descriptions.Item>
              <Descriptions.Item label="Xuất bản">{open.lifecycleStatus}</Descriptions.Item>
              <Descriptions.Item label="Áp dụng">{pinnedThisVersion ? `APPROVED · ${open.version}` : currentPin ? `Pin đang ở ${currentPin.ruleVersion}` : 'Chưa ghim'}</Descriptions.Item>
              <Descriptions.Item label="Lý do">{open.reasonVi}</Descriptions.Item>
              <Descriptions.Item label="Triệu chứng">{open.symptomCode ?? '—'}</Descriptions.Item>
              <Descriptions.Item label="Từ khóa">{open.keywords.join(', ') || '—'}</Descriptions.Item>
              <Descriptions.Item label="Nhóm hàng">{open.categoryCodes.join(', ') || '—'}</Descriptions.Item>
              <Descriptions.Item label="Từ loại trừ sẵn">{open.excludeKeywords.join(', ') || '—'}</Descriptions.Item>
              <Descriptions.Item label="Ưu tiên">{open.priority}</Descriptions.Item>
              <Descriptions.Item label="Hiệu lực">{`${day(open.effectiveFrom)} → ${day(open.effectiveTo)}`}</Descriptions.Item>
              <Descriptions.Item label="Nguồn">{open.sourceVerificationStatus ?? 'UNVERIFIED'}</Descriptions.Item>
              <Descriptions.Item label="Trích dẫn">{open.referenceSource || '—'}</Descriptions.Item>
              <Descriptions.Item label="Định danh">{open.sourceIdentity || '—'}</Descriptions.Item>
              <Descriptions.Item label="URL">{open.sourceUrl || '—'}</Descriptions.Item>
              <Descriptions.Item label="Mã hồ sơ">{open.documentRef || '—'}</Descriptions.Item>
              <Descriptions.Item label="Cơ quan">{open.issuingOrganization || '—'}</Descriptions.Item>
              <Descriptions.Item label="Ngày văn bản">{day(open.sourcePublishedOn)}</Descriptions.Item>
              <Descriptions.Item label="Vị trí bằng chứng">{open.evidenceNote || '—'}</Descriptions.Item>
              <Descriptions.Item label="Người xác minh">{open.verifiedBy || '—'}</Descriptions.Item>
              <Descriptions.Item label="Thời điểm xác minh">{open.verifiedAt ? day(open.verifiedAt) : '—'}</Descriptions.Item>
            </Descriptions>
            {pinnedThisVersion ? (
              <Button danger loading={submitting} disabled={submitting} onClick={() => confirmRetire(open.ruleCode, open.version)}>Thu hồi pin</Button>
            ) : (
              <Space direction="vertical" style={{ width: '100%' }}>
                {blockers.length > 0 ? (
                  <Alert
                    type="warning"
                    showIcon
                    message="Chưa ghim được"
                    description={(
                      <ul style={{ margin: 0, paddingLeft: 18 }}>
                        {blockers.map((item) => <li key={item}>{item}</li>)}
                      </ul>
                    )}
                  />
                ) : (
                  <Input
                    placeholder="Ghi chú khi ghim"
                    value={source}
                    onChange={(event) => setSource(event.target.value)}
                  />
                )}
                <Button
                  type="primary"
                  loading={submitting}
                  disabled={blockers.length > 0 || source.trim().length === 0 || submitting}
                  onClick={confirmPin}
                >
                  Ghim phiên bản này
                </Button>
              </Space>
            )}
            {pinnedThisVersion ? (
              <Form form={form} layout="vertical">
                <Typography.Text type="secondary">
                  Chỉ được thu hẹp: tắt gợi ý sản phẩm, thêm từ loại trừ, hoặc hạ ưu tiên. Không thêm từ khóa hay nhóm hàng.
                </Typography.Text>
                <Form.Item name="productMatchingEnabled" label="Gợi ý sản phẩm" valuePropName="checked">
                  <Switch />
                </Form.Item>
                <Form.Item name="extraExcludeKeywords" label="Từ loại trừ thêm, cách nhau bởi dấu phẩy">
                  <Input />
                </Form.Item>
                <Form.Item
                  name="priorityOverride"
                  label={`Độ ưu tiên sau khi hạ (số hiện tại ${open.priority}, số lớn hơn là kém ưu tiên hơn)`}
                >
                  <InputNumber min={open.priority} style={{ width: '100%' }} />
                </Form.Item>
                <Button onClick={() => void savePolicy()}>Lưu chính sách thu hẹp</Button>
              </Form>
            ) : null}
            <Typography.Text strong>Lịch sử áp dụng của nhà thuốc này</Typography.Text>
            {history.length === 0 ? <Typography.Text type="secondary">Chưa có lần ghim hoặc thu hồi.</Typography.Text> : null}
            {history.map((item) => (
              <Typography.Paragraph key={`${item.createdAt}-${item.toStatus}-${item.version ?? ''}`} style={{ marginBottom: 8 }}>
                {day(item.createdAt)} · {item.fromStatus} → {item.toStatus} · phiên bản {item.version ?? '—'}
                <br />
                <Typography.Text type="secondary">{item.referenceSource ?? item.actorUserId}</Typography.Text>
              </Typography.Paragraph>
            ))}
          </Space>
        ) : null}
      </Drawer>
    </Card>
  );
}
