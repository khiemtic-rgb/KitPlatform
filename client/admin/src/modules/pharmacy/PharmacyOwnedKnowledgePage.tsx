import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  App,
  Button,
  Checkbox,
  Drawer,
  Empty,
  Form,
  Input,
  Result,
  Select,
  Space,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  REVIEW_OVERDUE_LABEL,
  createOwnedKnowledge,
  getKnowledgeFeedbackSummary,
  getOwnedKnowledge,
  listOwnedKnowledge,
  transitionOwnedKnowledge,
  updateOwnedKnowledge,
  type KnowledgeFeedbackSummary,
  type OwnedKnowledgeDetail,
  type OwnedKnowledgeSummary,
  type OwnedKnowledgeWrite,
} from '@/shared/api/pharmacy-owned-knowledge.api';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useIsAdmin } from '@/shared/auth/usePermission';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import {
  allowsPharmacyKnowledgeVertical,
  composeOwnedKnowledgeBody,
  ownedKnowledgeSections,
  parseOwnedKnowledgeBody,
} from '@/modules/pharmacy/knowledge-experience';

export const ownedKnowledgeKinds = [
  { value: 'GUIDANCE', label: 'Hướng dẫn' },
  { value: 'OPERATING_PROCEDURE', label: 'Quy trình vận hành' },
  { value: 'OPERATIONAL_POLICY', label: 'Chính sách vận hành' },
  { value: 'PRODUCT_KNOWLEDGE', label: 'Kiến thức sản phẩm' },
  { value: 'CLINICAL_KNOWLEDGE', label: 'Tri thức lâm sàng' },
] as const;

const kindLabel = (kind: string) => ownedKnowledgeKinds.find((item) => item.value === kind)?.label ?? kind;

function statusLabel(status: string) {
  if (status === 'DRAFT') return 'Nháp';
  if (status === 'IN_REVIEW') return 'Chờ duyệt';
  if (status === 'APPROVED') return 'Đã duyệt';
  if (status === 'ACTIVE') return 'Đang dùng';
  if (status === 'STOPPED') return 'Đã ngừng';
  return status;
}

function actionLabel(action: string) {
  if (action === 'CREATED') return 'Đã tạo';
  if (action === 'UPDATED') return 'Đã sửa';
  if (action === 'SUBMITTED') return 'Đã gửi duyệt';
  if (action === 'RETURNED') return 'Đã trả lại';
  if (action === 'APPROVED') return 'Đã duyệt';
  return action;
}

function SectionText({ body }: { body: string }) {
  const sections = parseOwnedKnowledgeBody(body);
  const entries = Object.entries(sections);
  if (entries.length === 0) {
    return <Typography.Paragraph style={{ whiteSpace: 'pre-wrap' }}>{body || '—'}</Typography.Paragraph>;
  }
  return (
    <div>
      {entries.map(([heading, text]) => (
        <div key={heading}>
          <Typography.Text strong>{heading}</Typography.Text>
          <Typography.Paragraph style={{ whiteSpace: 'pre-wrap' }}>{text || '—'}</Typography.Paragraph>
        </div>
      ))}
    </div>
  );
}

function previewMode() {
  if (!import.meta.env.DEV) return false;
  return window.location.pathname.endsWith('/m1-ui-preview');
}

function blank(): OwnedKnowledgeWrite & { sections?: Record<string, string> } {
  return { kind: 'GUIDANCE', title: '', body: '', classification: '', sections: {} };
}

const previewLegacyDraft: OwnedKnowledgeDetail = {
  id: '00000000-0000-4000-8000-000000000031',
  kind: 'GUIDANCE',
  title: 'Ban hien tai',
  classification: 'Nhom hien tai',
  version: 1,
  lifecycleStatus: 'DRAFT',
  createdAt: '2026-01-01T00:00:00.000Z',
  updatedAt: '2026-01-02T00:00:00.000Z',
  body: 'Noi dung hien tai',
  history: [
    {
      id: '00000000-0000-4000-8000-000000000032',
      action: 'CREATED',
      version: 1,
      kind: 'GUIDANCE',
      title: 'Tieu de goc',
      body: 'NOI DUNG BACKFILL KHONG DUOC HIEN',
      classification: 'PHAN LOAI BACKFILL',
      snapshotFidelity: 'LEGACY_UNAVAILABLE',
      actorUserId: 'preview',
      createdAt: '2026-01-01T00:00:00.000Z',
    },
    {
      id: '00000000-0000-4000-8000-000000000033',
      action: 'UPDATED',
      version: 1,
      kind: 'GUIDANCE',
      title: 'Ban hien tai',
      body: 'Noi dung hien tai',
      classification: 'Nhom hien tai',
      snapshotFidelity: 'EXACT',
      actorUserId: 'preview',
      createdAt: '2026-01-02T00:00:00.000Z',
    },
  ],
};

export function PharmacyOwnedKnowledgePreviewPage() {
  const access = new URLSearchParams(window.location.search).get('access') ?? 'none';
  useAuthStore.setState({
    accessToken: 'm1-preview',
    refreshToken: null,
    user: {
      id: '00000000-0000-4000-8000-000000000021',
      tenantId: '00000000-0000-4000-8000-000000000022',
      tenantCode: 'M1_PREVIEW',
      username: 'm1-preview',
      email: 'm1-preview@example.test',
      roles: access === 'admin' ? ['ADMIN'] : ['STAFF'],
      permissions: [],
    },
  });
  return <PharmacyOwnedKnowledgePage />;
}

export function PharmacyOwnedKnowledgePage() {
  const { modal } = App.useApp();
  const allowed = useIsAdmin();
  const preview = previewMode();
  const platformLoaded = useTenantPlatformStore((s) => s.loaded);
  const vertical = useTenantPlatformStore((s) => s.settings?.vertical);
  const pharmacyTenant = preview
    ? new URLSearchParams(window.location.search).get('vertical') !== 'marketing'
    : platformLoaded && allowsPharmacyKnowledgeVertical(vertical);
  const [kind, setKind] = useState<string | undefined>();
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState<string | undefined>();
  const [classification, setClassification] = useState('');
  const [leftVersion, setLeftVersion] = useState<string | undefined>();
  const [rightVersion, setRightVersion] = useState<string | undefined>();
  const [rows, setRows] = useState<OwnedKnowledgeSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState<OwnedKnowledgeDetail | null>(null);
  const [creating, setCreating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [drafts, setDrafts] = useState<OwnedKnowledgeDetail[]>(() => (preview ? [previewLegacyDraft] : []));
  const [overdueOnly, setOverdueOnly] = useState(false);
  const [feedback, setFeedback] = useState<KnowledgeFeedbackSummary | null>(null);
  const [form] = Form.useForm<OwnedKnowledgeWrite & { sections?: Record<string, string> }>();
  const visibleRows = overdueOnly ? rows.filter((row) => row.reviewOverdue) : rows;

  useEffect(() => {
    if (!allowed || preview) return;
    getKnowledgeFeedbackSummary().then(setFeedback).catch(() => setFeedback(null));
  }, [allowed, preview]);

  const load = useCallback(async () => {
    if (preview) {
      const needle = query.trim().toLowerCase();
      const group = classification.trim().toLowerCase();
      setRows(drafts.filter((row) => {
        if (kind && row.kind !== kind) return false;
        if (status && row.lifecycleStatus !== status) return false;
        if (needle && !row.title.toLowerCase().includes(needle)) return false;
        if (group && !(row.classification || '').toLowerCase().includes(group)) return false;
        return true;
      }));
      return;
    }
    setLoading(true);
    try {
      setRows(await listOwnedKnowledge({ kind, q: query, status, classification }));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được tri thức của nhà thuốc'));
    } finally {
      setLoading(false);
    }
  }, [classification, drafts, kind, preview, query, status]);

  useEffect(() => {
    if (allowed) void load();
  }, [allowed, load]);

  const columns: ColumnsType<OwnedKnowledgeSummary> = useMemo(() => [
    { title: 'Tiêu đề', dataIndex: 'title' },
    { title: 'Loại', dataIndex: 'kind', render: (value: string) => kindLabel(value) },
    { title: 'Phân loại', dataIndex: 'classification', render: (value?: string | null) => value || '—' },
    { title: 'Phiên bản', dataIndex: 'version', width: 110, render: (value: number) => `Phiên bản ${value}` },
    {
      title: 'Trạng thái',
      dataIndex: 'lifecycleStatus',
      width: 120,
      render: (value: string, row) => (
        <Space direction="vertical" size={0}>
          <Tag>{statusLabel(value)}</Tag>
          {row.applicationStatus ? <Tag>{statusLabel(row.applicationStatus)}</Tag> : null}
          {row.reviewOverdue ? <Tag color="orange">{REVIEW_OVERDUE_LABEL}</Tag> : null}
        </Space>
      ),
    },
    {
      title: 'Phản hồi',
      width: 150,
      render: (_value, row) => {
        const count = feedback?.items.find((item) => item.knowledgeId === row.id);
        if (!count) return '—';
        return (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {count.openCount} lượt mở · {count.notHelpfulCount} chưa giúp được
          </Typography.Text>
        );
      },
    },
    {
      title: '',
      width: 90,
      render: (_value, row) => (
        <Button
          type="link"
          onClick={() => {
            void openRow(row.id);
          }}
        >
          Xem
        </Button>
      ),
    },
  ], [drafts, feedback, preview]);

  async function openRow(id: string) {
    if (preview) {
      setOpen(drafts.find((row) => row.id === id) ?? null);
      return;
    }
    try {
      setOpen(await getOwnedKnowledge(id));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không mở được bản nháp'));
    }
  }

  function startCreate() {
    form.setFieldsValue(blank());
    setCreating(true);
    setOpen(null);
  }

  function startEdit(row: OwnedKnowledgeDetail) {
    form.setFieldsValue({
      kind: row.kind,
      title: row.title,
      body: row.body,
      classification: row.classification ?? '',
      sections: parseOwnedKnowledgeBody(row.body),
    });
    setCreating(true);
  }

  async function save(values: OwnedKnowledgeWrite & { sections?: Record<string, string> }) {
    const composed = composeOwnedKnowledgeBody(values.sections ?? {});
    const payload = {
      kind: values.kind,
      title: values.title.trim(),
      body: composed || values.body?.trim() || '',
      classification: values.classification?.trim() || null,
    };
    setSaving(true);
    try {
      if (preview) {
        const now = new Date().toISOString();
        if (open) {
          const next: OwnedKnowledgeDetail = {
            ...open,
            ...payload,
            updatedAt: now,
            history: [
              ...open.history,
              {
                id: crypto.randomUUID(),
                action: 'UPDATED',
                version: open.version,
                kind: payload.kind,
                title: payload.title,
                body: payload.body,
                classification: payload.classification,
                snapshotFidelity: 'EXACT',
                actorUserId: 'preview',
                createdAt: now,
              },
            ],
          };
          setDrafts((current) => current.map((row) => (row.id === open.id ? next : row)));
          setOpen(next);
        } else {
          const created: OwnedKnowledgeDetail = {
            id: crypto.randomUUID(),
            ...payload,
            version: 1,
            lifecycleStatus: 'DRAFT',
            createdAt: now,
            updatedAt: now,
            history: [
              {
                id: crypto.randomUUID(),
                action: 'CREATED',
                version: 1,
                kind: payload.kind,
                title: payload.title,
                body: payload.body,
                classification: payload.classification,
                snapshotFidelity: 'EXACT',
                actorUserId: 'preview',
                createdAt: now,
              },
            ],
          };
          setDrafts((current) => [created, ...current]);
          setOpen(created);
        }
        setCreating(false);
        message.success('Đã lưu nháp');
        return;
      }
      const saved = open
        ? await updateOwnedKnowledge(open.id, payload)
        : await createOwnedKnowledge(payload);
      setOpen(saved);
      setCreating(false);
      message.success('Đã lưu nháp');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được bản nháp'));
    } finally {
      setSaving(false);
    }
  }

  function confirmTransition(action: 'approve' | 'activate') {
    let reviewDueOn: string | null = null;
    modal.confirm({
      title: action === 'approve' ? 'Duyệt nội dung này?' : 'Áp dụng trong nhà thuốc?',
      content: (
        <Space direction="vertical" style={{ width: '100%' }}>
          <Typography.Text>
            {action === 'approve'
              ? 'Sau khi duyệt, nội dung không sửa tại chỗ. Muốn sửa phải tạo phiên bản mới. Người soạn hoặc gửi duyệt không tự duyệt được.'
              : 'Nội dung chỉ dùng trong nhà thuốc. Chưa đưa vào gợi ý bán hàng.'}
          </Typography.Text>
          <Typography.Text type="secondary">Hạn rà soát (để trống = 12 tháng tới)</Typography.Text>
          <Input type="date" onChange={(event) => { reviewDueOn = event.target.value || null; }} />
        </Space>
      ),
      okText: action === 'approve' ? 'Duyệt' : 'Áp dụng',
      cancelText: 'Đóng',
      onOk: () => transition(action, reviewDueOn),
    });
  }

  async function transition(
    action: 'submit' | 'return' | 'approve' | 'activate' | 'stop' | 'resume' | 'next-draft',
    reviewDueOn: string | null = null,
  ) {
    if (!open) return;
    const version = open.versions?.find((item) => item.version === open.version) ?? open.versions?.slice(-1)[0];
    const note = action === 'return' || action === 'stop' || action === 'resume'
      ? window.prompt('Ghi chú') ?? ''
      : null;
    if ((action === 'return' || action === 'stop' || action === 'resume') && !note?.trim()) return;
    if (preview) {
      const nextStatus = action === 'submit'
        ? 'IN_REVIEW'
        : action === 'return' || action === 'next-draft'
          ? 'DRAFT'
          : 'APPROVED';
      const next = {
        ...open,
        lifecycleStatus: action === 'next-draft' ? 'DRAFT' : nextStatus,
        activations: action === 'activate'
          ? [{ versionId: version?.versionId ?? open.id, version: open.version, status: 'ACTIVE' }]
          : open.activations,
      };
      setOpen(next);
      setDrafts((current) => current.map((row) => (row.id === open.id ? next : row)));
      return;
    }
    if (!version) {
      message.error('Chưa có phiên bản để chuyển');
      return;
    }
    try {
      const saved = await transitionOwnedKnowledge(open.id, action, {
        versionId: version.versionId,
        expectedUpdatedAt: version.updatedAt,
        note,
        reviewDueOn: action === 'approve' || action === 'activate' ? reviewDueOn : null,
      });
      setOpen(saved);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không chuyển được trạng thái'));
    }
  }

  if (!pharmacyTenant) {
    return (
      <Result
        status="403"
        title="Tri thức nhà thuốc không thuộc tenant này"
        subTitle="Mục này chỉ mở khi tenant đang vận hành nhà thuốc. Quyền nền tảng không suy ra từ vai trò quản trị tenant."
      />
    );
  }

  if (!allowed) {
    return (
      <Result
        status="403"
        title="Không có quyền soạn tri thức của nhà thuốc"
        subTitle="Chỉ quản trị của đúng nhà thuốc mới tạo và sửa bản nháp."
      />
    );
  }

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>Tài liệu vận hành nội bộ</Typography.Title>
        <Typography.Text type="secondary">
          Quy trình, chính sách và hướng dẫn nội bộ của nhà thuốc. Không phải hồ sơ khách hàng và không phải kho đơn thuốc tham khảo. Bản nháp chưa đưa ra quầy.
        </Typography.Text>
      </div>
      <Space wrap>
        <Input
          allowClear
          placeholder="Tìm tiêu đề hoặc nội dung"
          style={{ width: 240 }}
          value={query}
          onChange={(event) => setQuery(event.target.value)}
        />
        <Select
          allowClear
          placeholder="Lọc theo loại"
          style={{ width: 220 }}
          value={kind}
          options={ownedKnowledgeKinds.map((item) => ({ value: item.value, label: item.label }))}
          onChange={(value) => setKind(value)}
        />
        <Select
          allowClear
          placeholder="Trạng thái"
          style={{ width: 160 }}
          value={status}
          options={[
            { value: 'DRAFT', label: 'Nháp' },
            { value: 'IN_REVIEW', label: 'Chờ duyệt' },
            { value: 'APPROVED', label: 'Đã duyệt' },
          ]}
          onChange={(value) => setStatus(value)}
        />
        <Input
          allowClear
          placeholder="Phân loại"
          style={{ width: 180 }}
          value={classification}
          onChange={(event) => setClassification(event.target.value)}
        />
        <Checkbox checked={overdueOnly} onChange={(event) => setOverdueOnly(event.target.checked)}>
          Chỉ {REVIEW_OVERDUE_LABEL.toLowerCase()}
        </Checkbox>
        <Button type="primary" onClick={startCreate}>Tạo tri thức</Button>
      </Space>
      {feedback && feedback.recentMissing.length > 0 ? (
        <div>
          <Typography.Text strong>Quầy báo còn thiếu</Typography.Text>
          <div>
            {feedback.recentMissing.slice(0, 8).map((item) => (
              <Tag key={`${item.createdAt}-${item.query}`} style={{ marginTop: 4 }}>{item.query}</Tag>
            ))}
          </div>
        </div>
      ) : null}
      <Table
        rowKey="id"
        loading={loading}
        columns={columns}
        dataSource={visibleRows}
        locale={{ emptyText: <Empty description="Chưa có tri thức nháp" /> }}
        pagination={false}
      />
      <Drawer
        title={open ? open.title : 'Tạo tri thức'}
        open={creating || open !== null}
        width={720}
        onClose={() => {
          setCreating(false);
          setOpen(null);
        }}
        extra={open && !creating && open.lifecycleStatus === 'DRAFT' ? <Button onClick={() => startEdit(open)}>Sửa nháp</Button> : null}
      >
        {creating ? (
          <Form form={form} layout="vertical" onFinish={(values) => { void save(values); }}>
            <Form.Item name="kind" label="Loại" rules={[{ required: true, message: 'Chọn loại' }]}>
              <Select options={ownedKnowledgeKinds.map((item) => ({ value: item.value, label: item.label }))} />
            </Form.Item>
            <Form.Item name="title" label="Tiêu đề" rules={[{ required: true, message: 'Nhập tiêu đề' }]}>
              <Input maxLength={200} />
            </Form.Item>
            <Form.Item name="classification" label="Phân loại">
              <Input maxLength={200} />
            </Form.Item>
            <Form.Item shouldUpdate noStyle>
              {() => {
                const selected = form.getFieldValue('kind') || 'GUIDANCE';
                const sections = ownedKnowledgeSections[selected] ?? ownedKnowledgeSections.GUIDANCE;
                return (
                  <>
                    {selected === 'CLINICAL_KNOWLEDGE' ? (
                      <Typography.Paragraph type="warning">
                        Tri thức lâm sàng chỉ lưu trong nhà thuốc. Không dùng để tư vấn, bán hàng, hoặc đưa vào gợi ý.
                      </Typography.Paragraph>
                    ) : (
                      <Typography.Paragraph type="secondary">
                        Điền các mục của loại đã chọn. Bản đã duyệt không sửa tại chỗ; sửa sau duyệt sẽ thành phiên bản mới.
                      </Typography.Paragraph>
                    )}
                    {sections.map((section) => (
                      <Form.Item
                        key={section.heading}
                        name={['sections', section.heading]}
                        label={section.heading}
                        rules={section.required ? [{ required: true, message: `Nhập ${section.heading}` }] : undefined}
                      >
                        <Input.TextArea rows={section.heading.includes('bước') ? 6 : 3} maxLength={8000} />
                      </Form.Item>
                    ))}
                  </>
                );
              }}
            </Form.Item>
            <Button type="primary" htmlType="submit" loading={saving}>Lưu nháp</Button>
          </Form>
        ) : open ? (
          <Space direction="vertical" size={12} style={{ width: '100%' }}>
            <Typography.Text>Loại: {kindLabel(open.kind)}</Typography.Text>
            <Typography.Text>Phân loại: {open.classification || '—'}</Typography.Text>
            <Typography.Text>Phiên bản {open.version} · {statusLabel(open.lifecycleStatus)}</Typography.Text>
            {open.kind === 'CLINICAL_KNOWLEDGE' ? (
              <Typography.Text type="warning">Bản lâm sàng không được áp dụng để tư vấn hoặc bán hàng.</Typography.Text>
            ) : null}
            <Space wrap>
              {open.lifecycleStatus === 'DRAFT' ? <Button onClick={() => { void transition('submit'); }}>Gửi duyệt</Button> : null}
              {open.lifecycleStatus === 'IN_REVIEW' ? <Button type="primary" onClick={() => confirmTransition('approve')}>Duyệt</Button> : null}
              {open.lifecycleStatus === 'IN_REVIEW' ? <Button onClick={() => { void transition('return'); }}>Trả lại</Button> : null}
              {open.lifecycleStatus === 'APPROVED' ? <Button onClick={() => { void transition('next-draft'); }}>Tạo bản nháp mới</Button> : null}
              {open.lifecycleStatus === 'APPROVED' && open.kind !== 'CLINICAL_KNOWLEDGE' && open.activations?.find((item) => item.version === open.version)?.status !== 'ACTIVE' ? <Button onClick={() => confirmTransition('activate')}>Áp dụng</Button> : null}
              {open.activations?.find((item) => item.version === open.version)?.status === 'ACTIVE' ? <Button onClick={() => { void transition('stop'); }}>Ngừng áp dụng</Button> : null}
              {open.activations?.find((item) => item.version === open.version)?.status === 'STOPPED' ? <Button onClick={() => { void transition('resume'); }}>Áp dụng lại</Button> : null}
            </Space>
            {open.kind === 'CLINICAL_KNOWLEDGE' ? (
              <Typography.Text type="secondary">
                Bản lâm sàng chỉ lưu trong nhà thuốc. Không có đường đưa ra quầy.
              </Typography.Text>
            ) : null}
            <SectionText body={open.body} />
            {(open.versions?.length ?? 0) > 1 ? (
              <Space direction="vertical" style={{ width: '100%' }}>
                <Typography.Title level={5}>So sánh phiên bản</Typography.Title>
                <Space wrap>
                  <Select
                    placeholder="Phiên bản trái"
                    style={{ width: 180 }}
                    value={leftVersion}
                    options={(open.versions ?? []).map((item) => ({ value: item.versionId, label: `Phiên bản ${item.version}` }))}
                    onChange={setLeftVersion}
                  />
                  <Select
                    placeholder="Phiên bản phải"
                    style={{ width: 180 }}
                    value={rightVersion}
                    options={(open.versions ?? []).map((item) => ({ value: item.versionId, label: `Phiên bản ${item.version}` }))}
                    onChange={setRightVersion}
                  />
                </Space>
                <Space align="start" style={{ width: '100%' }}>
                  <SectionText body={open.versions?.find((item) => item.versionId === leftVersion)?.body || ''} />
                  <SectionText body={open.versions?.find((item) => item.versionId === rightVersion)?.body || ''} />
                </Space>
              </Space>
            ) : null}
            <Typography.Title level={5}>Lịch sử</Typography.Title>
            {open.history.map((event) => {
              const exact = event.snapshotFidelity === 'EXACT';
              return (
                <Typography.Paragraph key={event.id}>
                  {actionLabel(event.action)} phiên bản {event.version}: {event.title}
                  <br />
                  {kindLabel(event.kind)}
                  {exact ? (
                    <>
                      {' · '}
                      {event.classification || '—'}
                      <br />
                      {event.body}
                    </>
                  ) : (
                    <>
                      <br />
                      Nội dung lịch sử gốc không còn khả dụng.
                    </>
                  )}
                </Typography.Paragraph>
              );
            })}
          </Space>
        ) : null}
      </Drawer>
    </Space>
  );
}
