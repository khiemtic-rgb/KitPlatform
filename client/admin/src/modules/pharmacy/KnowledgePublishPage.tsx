import { useCallback, useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { Alert, Button, Card, DatePicker, Descriptions, Drawer, Empty, Form, Input, Popconfirm, Result, Select, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchKnowledgeVersions,
  fetchPublicationHistory,
  publishKnowledgeVersion,
  retireKnowledgeVersion,
  returnKnowledgeToDraft,
  submitKnowledgeReview,
  updateKnowledgeContent,
  verifyKnowledgeSource,
  type KnowledgePublicationEvent,
  type KnowledgeVersion,
} from '@/shared/api/pharmacy-knowledge.api';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useHasExplicitPermission } from '@/shared/auth/usePermission';
import {
  canOpenKnowledgeCatalog,
  knowledgeCatalogActions,
  publishBlockers,
  sameKnowledgeList,
  sourceAcceptanceError,
  splitKnowledgeList,
  type SourceVerificationInput,
} from '@/modules/pharmacy/knowledge-catalog-actions';

function day(value?: string | null) {
  return value ? value.slice(0, 10) : '—';
}

function blankToNull(value?: string | null) {
  const text = value?.trim();
  return text ? text : null;
}

function previewAccess() {
  if (!import.meta.env.DEV) return null;
  const params = new URLSearchParams(window.location.search);
  return params.get('k1preview') === '1' ? (params.get('access') ?? 'none') : null;
}

const previewRows: KnowledgeVersion[] = [
  row('k1syn-draft', 'DRAFT', 'UNVERIFIED'),
  row('k1syn-review', 'IN_REVIEW', 'UNVERIFIED'),
  row('k1syn-accepted', 'IN_REVIEW', 'ACCEPTED'),
  row('k1syn-published', 'PUBLISHED', 'ACCEPTED'),
  row('KR_OTHER', 'IN_REVIEW', 'ACCEPTED'),
];

function row(ruleCode: string, lifecycleStatus: string, sourceVerificationStatus: string): KnowledgeVersion {
  const accepted = sourceVerificationStatus === 'ACCEPTED';
  return {
    ruleCode,
    version: '1',
    lifecycleStatus,
    source: 'novixa',
    effectiveFrom: '2026-11-01',
    categoryCodes: ['HO_HAP'],
    keywords: ['synthetic'],
    excludeKeywords: [],
    reasonVi: 'synthetic preview',
    priority: 100,
    sourceVerificationStatus,
    referenceSource: accepted ? 'synthetic-citation' : null,
    sourceIdentity: accepted ? 'SYNTH-TEST' : null,
    sourceUrl: accepted ? 'https://example.test/k1-fixture' : null,
    issuingOrganization: accepted ? 'synthetic-org' : null,
    evidenceNote: accepted ? 'fixture section' : null,
  };
}

/** Dev-only harness. Production builds drop the route that renders this. It does not call the API. */
export function KnowledgePublishPreviewPage() {
  const access = new URLSearchParams(window.location.search).get('access') ?? 'none';
  const permissions =
    access === 'review' ? ['pharmacy.knowledge.review']
    : access === 'verify' ? ['pharmacy.knowledge.verify-source']
    : access === 'publish' ? ['platform.knowledge.publish']
    : access === 'all' ? ['pharmacy.knowledge.review', 'pharmacy.knowledge.verify-source', 'platform.knowledge.publish']
    : [];
  useAuthStore.setState({
    accessToken: 'k1-preview',
    refreshToken: null,
    user: {
      id: '00000000-0000-4000-8000-000000000001',
      tenantId: '00000000-0000-4000-8000-000000000002',
      tenantCode: 'K1_PREVIEW',
      username: 'k1-preview',
      email: 'k1-preview@example.test',
      roles: access === 'admin' ? ['ADMIN'] : ['STAFF'],
      permissions,
    },
  });
  return <KnowledgePublishPage />;
}

export function KnowledgePublishPage() {
  const location = useLocation();
  const governance = location.pathname.endsWith('/xu-ly') || location.pathname.endsWith('/k1-ui-preview');
  const review = useHasExplicitPermission('pharmacy.knowledge.review');
  const verifySource = useHasExplicitPermission('pharmacy.knowledge.verify-source');
  const publishAllowed = useHasExplicitPermission('platform.knowledge.publish');
  const access = { review, verifySource, publish: publishAllowed };
  const allowed = canOpenKnowledgeCatalog(access);
  const preview = previewAccess();
  const [loading, setLoading] = useState(false);
  const [rows, setRows] = useState<KnowledgeVersion[]>([]);
  const [open, setOpen] = useState<KnowledgeVersion | null>(null);
  const [history, setHistory] = useState<KnowledgePublicationEvent[]>([]);
  const [form] = Form.useForm<SourceVerificationInput & { sourcePublishedOn?: Dayjs | null }>();
  const [contentForm] = Form.useForm<{ reasonVi: string; keywordsText: string; excludeText: string; categoryText: string }>();
  const [codeQuery, setCodeQuery] = useState('');
  const [lifecycleFilter, setLifecycleFilter] = useState<string | undefined>();
  const [sourceFilter, setSourceFilter] = useState<string | undefined>();
  const [loadError, setLoadError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (preview) {
      setRows(previewRows);
      return;
    }
    setLoading(true);
    setLoadError(null);
    try {
      setRows(await fetchKnowledgeVersions());
    } catch (error) {
      const text = apiErrorMessage(error, 'Không tải được danh sách phiên bản');
      setLoadError(text);
      message.error(text);
    } finally {
      setLoading(false);
    }
  }, [preview]);

  useEffect(() => {
    if (allowed) void load();
  }, [allowed, load]);

  const fillContent = (row: KnowledgeVersion) => {
    contentForm.setFieldsValue({
      reasonVi: row.reasonVi,
      keywordsText: row.keywords.join(', '),
      excludeText: row.excludeKeywords.join(', '),
      categoryText: row.categoryCodes.join(', '),
    });
  };

  const show = async (row: KnowledgeVersion) => {
    setOpen(row);
    fillContent(row);
    if (preview) {
      setHistory([]);
      form.setFieldsValue({
        referenceSource: row.referenceSource ?? '',
        sourceIdentity: row.sourceIdentity ?? '',
        sourceUrl: row.sourceUrl ?? '',
        documentRef: row.documentRef ?? '',
        issuingOrganization: row.issuingOrganization ?? '',
        evidenceNote: row.evidenceNote ?? '',
        sourcePublishedOn: row.sourcePublishedOn ? dayjs(row.sourcePublishedOn) : null,
      });
      return;
    }
    form.setFieldsValue({
      referenceSource: row.referenceSource ?? '',
      sourceIdentity: row.sourceIdentity ?? '',
      sourceUrl: row.sourceUrl ?? '',
      documentRef: row.documentRef ?? '',
      issuingOrganization: row.issuingOrganization ?? '',
      evidenceNote: row.evidenceNote ?? '',
      sourcePublishedOn: row.sourcePublishedOn ? dayjs(row.sourcePublishedOn) : null,
    });
    try {
      const events = await fetchPublicationHistory(row.ruleCode);
      setHistory(events.filter((item) => item.version === row.version));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được lịch sử xuất bản'));
    }
  };

  const replaceOpen = (next: KnowledgeVersion) => {
    setOpen(next);
    setRows((current) => current.map((row) => (
      row.ruleCode === next.ruleCode && row.version === next.version ? next : row
    )));
  };

  const submit = async (row: KnowledgeVersion) => {
    if (preview) {
      replaceOpen({ ...row, lifecycleStatus: 'IN_REVIEW' });
      message.success('Đã gửi xét duyệt');
      return;
    }
    try {
      replaceOpen(await submitKnowledgeReview(row.ruleCode, row.version));
      message.success('Đã gửi xét duyệt');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không gửi xét duyệt được'));
    }
  };

  const returnDraft = async (row: KnowledgeVersion) => {
    if (preview) {
      replaceOpen({ ...row, lifecycleStatus: 'DRAFT' });
      message.success('Đã trả phiên bản về nháp');
      return;
    }
    try {
      replaceOpen(await returnKnowledgeToDraft(row.ruleCode, row.version));
      message.success('Đã trả phiên bản về nháp');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không trả về nháp được'));
    }
  };

  const verify = async (row: KnowledgeVersion, status: 'ACCEPTED' | 'REJECTED') => {
    const values = form.getFieldsValue();
    if (status === 'ACCEPTED') {
      const error = sourceAcceptanceError(values);
      if (error) {
        message.error(error);
        return;
      }
    }
    const publishedOn = values.sourcePublishedOn;
    if (preview) {
      replaceOpen({
        ...row,
        sourceVerificationStatus: status,
        referenceSource: blankToNull(values.referenceSource),
        sourceIdentity: blankToNull(values.sourceIdentity),
        sourceUrl: blankToNull(values.sourceUrl),
        documentRef: blankToNull(values.documentRef),
        issuingOrganization: blankToNull(values.issuingOrganization),
        evidenceNote: blankToNull(values.evidenceNote),
        sourcePublishedOn: publishedOn ? publishedOn.format('YYYY-MM-DD') : null,
        verifiedBy: 'k1-preview',
        verifiedAt: new Date().toISOString(),
      });
      message.success(status === 'ACCEPTED' ? 'Đã chấp nhận nguồn' : 'Đã từ chối nguồn');
      return;
    }
    try {
      replaceOpen(await verifyKnowledgeSource(row.ruleCode, row.version, {
        status,
        referenceSource: blankToNull(values.referenceSource),
        sourceIdentity: blankToNull(values.sourceIdentity),
        sourceUrl: blankToNull(values.sourceUrl),
        documentRef: blankToNull(values.documentRef),
        issuingOrganization: blankToNull(values.issuingOrganization),
        evidenceNote: blankToNull(values.evidenceNote),
        sourcePublishedOn: publishedOn ? publishedOn.format('YYYY-MM-DD') : null,
      }));
      message.success(status === 'ACCEPTED' ? 'Đã chấp nhận nguồn' : 'Đã từ chối nguồn');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không ghi được kết quả kiểm chứng nguồn'));
    }
  };

  const saveContent = async (row: KnowledgeVersion) => {
    const values = await contentForm.validateFields();
    const reasonVi = values.reasonVi.trim();
    const keywords = splitKnowledgeList(values.keywordsText ?? '');
    const excludeKeywords = splitKnowledgeList(values.excludeText ?? '');
    const categoryCodes = splitKnowledgeList(values.categoryText ?? '');
    const clears = reasonVi !== row.reasonVi
      || !sameKnowledgeList(keywords, row.keywords)
      || !sameKnowledgeList(excludeKeywords, row.excludeKeywords)
      || !sameKnowledgeList(categoryCodes, row.categoryCodes);
    if (preview) {
      replaceOpen({
        ...row,
        reasonVi,
        keywords,
        excludeKeywords,
        categoryCodes,
        sourceVerificationStatus: clears ? 'UNVERIFIED' : row.sourceVerificationStatus,
        verifiedBy: clears ? null : row.verifiedBy,
        verifiedAt: clears ? null : row.verifiedAt,
      });
      message.success(clears ? 'Đã lưu. Nguồn về UNVERIFIED vì nội dung đổi.' : 'Đã lưu nội dung');
      return;
    }
    try {
      replaceOpen(await updateKnowledgeContent(row.ruleCode, row.version, {
        reasonVi,
        keywords,
        excludeKeywords,
        categoryCodes,
      }));
      message.success('Đã lưu nội dung');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được nội dung'));
    }
  };

  const publish = async (row: KnowledgeVersion) => {
    try {
      await publishKnowledgeVersion(row.ruleCode, row.version);
      message.success('Đã xuất bản phiên bản');
      setOpen(null);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không xuất bản được'));
    }
  };

  const retire = async (row: KnowledgeVersion) => {
    try {
      await retireKnowledgeVersion(row.ruleCode, row.version);
      message.success('Đã thu hồi phiên bản');
      setOpen(null);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không thu hồi được'));
    }
  };

  if (!allowed) {
    return (
      <Result
        status="403"
        title="Không có quyền thao tác tri thức dùng chung"
        subTitle="Cần một trong các quyền pharmacy.knowledge.review, pharmacy.knowledge.verify-source hoặc platform.knowledge.publish. Vai trò ADMIN không thay cho các quyền này. Ghim nhà thuốc dùng pharmacy.knowledge.approve và nằm ở màn khác."
      />
    );
  }

  const columns: ColumnsType<KnowledgeVersion> = [
    { title: 'Mã', dataIndex: 'ruleCode' },
    { title: 'Phiên bản', dataIndex: 'version', width: 110 },
    {
      title: 'Vòng đời',
      dataIndex: 'lifecycleStatus',
      width: 140,
      render: (value: string) => <Tag>{value}</Tag>,
    },
    {
      title: 'Nguồn',
      dataIndex: 'sourceVerificationStatus',
      width: 140,
      render: (value?: string) => <Tag>{value ?? 'UNVERIFIED'}</Tag>,
    },
    { title: 'Hiệu lực', render: (_, row) => `${day(row.effectiveFrom)} → ${day(row.effectiveTo)}` },
    {
      title: '',
      width: 120,
      render: (_, row) => (
        <Button type="link" onClick={() => void show(row)}>
          Xem
        </Button>
      ),
    },
  ];

  const actions = open
    ? knowledgeCatalogActions(open, governance ? access : { review: false, verifySource: false, publish: false })
    : null;

  const visibleRows = rows.filter((row) => {
    if (codeQuery && !row.ruleCode.toLowerCase().includes(codeQuery.trim().toLowerCase())) return false;
    if (lifecycleFilter && row.lifecycleStatus !== lifecycleFilter) return false;
    if (sourceFilter && (row.sourceVerificationStatus ?? 'UNVERIFIED') !== sourceFilter) return false;
    return true;
  });
  const blockers = open ? publishBlockers(open) : [];

  return (
    <Card title={governance ? 'Kiểm duyệt và xuất bản tri thức nền tảng' : 'Kho tri thức nền tảng'}>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Xuất bản không làm nhà thuốc tự áp dụng"
        description="PUBLISHED chỉ cho phép nhà thuốc ghim đúng phiên bản. Copilot chưa dùng phiên bản cho đến khi nhà thuốc ghim."
      />
      <Typography.Paragraph type="secondary">
        {governance
          ? 'Xét duyệt nội dung, kiểm chứng nguồn và xuất bản là ba quyền riêng. Vai trò ADMIN không thay các quyền này. Nháp, chờ duyệt, đã xuất bản và thu hồi là bộ lọc của màn này.'
          : 'Đây là danh mục nền tảng để xem. Xét duyệt, kiểm chứng nguồn và xuất bản nằm ở mục Kiểm duyệt và xuất bản.'}
      </Typography.Paragraph>
      <Space wrap style={{ marginBottom: 16 }}>
        <Input
          allowClear
          placeholder="Lọc mã"
          value={codeQuery}
          onChange={(event) => setCodeQuery(event.target.value)}
          style={{ width: 180 }}
        />
        <Select
          allowClear
          placeholder="Vòng đời"
          value={lifecycleFilter}
          onChange={setLifecycleFilter}
          style={{ width: 160 }}
          options={['DRAFT', 'IN_REVIEW', 'PUBLISHED', 'RETIRED'].map((value) => ({ value, label: value }))}
        />
        <Select
          allowClear
          placeholder="Nguồn"
          value={sourceFilter}
          onChange={setSourceFilter}
          style={{ width: 160 }}
          options={['UNVERIFIED', 'ACCEPTED', 'REJECTED'].map((value) => ({ value, label: value }))}
        />
      </Space>
      {loadError ? <Alert type="error" showIcon style={{ marginBottom: 16 }} message={loadError} /> : null}
      <Table
        rowKey={(row) => `${row.ruleCode}:${row.version}`}
        loading={loading}
        columns={columns}
        dataSource={visibleRows}
        pagination={{ pageSize: 20 }}
        locale={{ emptyText: <Empty description={rows.length === 0 ? 'Chưa có phiên bản tri thức.' : 'Không có phiên bản khớp bộ lọc.'} /> }}
      />
      <Drawer
        title={open ? `${open.ruleCode} · ${open.version}` : ''}
        open={open !== null}
        width={560}
        onClose={() => setOpen(null)}
      >
        {open && actions ? (
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Descriptions column={1} size="small">
              <Descriptions.Item label="Vòng đời">{open.lifecycleStatus}</Descriptions.Item>
              <Descriptions.Item label="Kiểm chứng nguồn">{open.sourceVerificationStatus ?? 'UNVERIFIED'}</Descriptions.Item>
              <Descriptions.Item label="Người xác minh">{open.verifiedBy || '—'}</Descriptions.Item>
              <Descriptions.Item label="Thời điểm xác minh">{open.verifiedAt ? day(open.verifiedAt) : '—'}</Descriptions.Item>
              <Descriptions.Item label="Trích dẫn">{open.referenceSource || '—'}</Descriptions.Item>
              <Descriptions.Item label="Định danh">{open.sourceIdentity || '—'}</Descriptions.Item>
              <Descriptions.Item label="URL">{open.sourceUrl || '—'}</Descriptions.Item>
              <Descriptions.Item label="Mã hồ sơ">{open.documentRef || '—'}</Descriptions.Item>
              <Descriptions.Item label="Cơ quan">{open.issuingOrganization || '—'}</Descriptions.Item>
              <Descriptions.Item label="Ngày văn bản">{day(open.sourcePublishedOn)}</Descriptions.Item>
              <Descriptions.Item label="Vị trí bằng chứng">{open.evidenceNote || '—'}</Descriptions.Item>
              <Descriptions.Item label="Nhãn catalog">{open.source || '—'}</Descriptions.Item>
              <Descriptions.Item label="Hiệu lực">{`${day(open.effectiveFrom)} → ${day(open.effectiveTo)}`}</Descriptions.Item>
              <Descriptions.Item label="Triệu chứng">{open.symptomCode ?? '—'}</Descriptions.Item>
              <Descriptions.Item label="Nhóm hàng">{open.categoryCodes.join(', ') || '—'}</Descriptions.Item>
              <Descriptions.Item label="Từ khóa">{open.keywords.join(', ') || '—'}</Descriptions.Item>
              <Descriptions.Item label="Từ loại trừ">{open.excludeKeywords.join(', ') || '—'}</Descriptions.Item>
              <Descriptions.Item label="Lý do">{open.reasonVi}</Descriptions.Item>
              <Descriptions.Item label="Ưu tiên">{open.priority}</Descriptions.Item>
            </Descriptions>
            {actions.editContent ? (
              <Form
                key={`content:${open.ruleCode}:${open.version}`}
                form={contentForm}
                layout="vertical"
                initialValues={{
                  reasonVi: open.reasonVi,
                  keywordsText: open.keywords.join(', '),
                  excludeText: open.excludeKeywords.join(', '),
                  categoryText: open.categoryCodes.join(', '),
                }}
              >
                <Typography.Text strong>Sửa nội dung chưa xuất bản</Typography.Text>
                <Typography.Paragraph type="secondary">
                  Triệu chứng đang gắn: {open.symptomCode ?? '—'}. Lưu không đổi triệu chứng. Đổi lý do, từ khóa, từ loại trừ hoặc nhóm hàng đưa nguồn ACCEPTED về UNVERIFIED.
                </Typography.Paragraph>
                <Form.Item label="Lý do" name="reasonVi" rules={[{ required: true, whitespace: true, message: 'Thiếu lý do.' }]}>
                  <Input.TextArea rows={3} />
                </Form.Item>
                <Form.Item label="Từ khóa, cách nhau bởi dấu phẩy" name="keywordsText">
                  <Input />
                </Form.Item>
                <Form.Item label="Từ loại trừ, cách nhau bởi dấu phẩy" name="excludeText">
                  <Input />
                </Form.Item>
                <Form.Item label="Nhóm hàng, cách nhau bởi dấu phẩy" name="categoryText">
                  <Input />
                </Form.Item>
                <Button onClick={() => void saveContent(open)}>Lưu nội dung</Button>
              </Form>
            ) : null}
            {actions.verifySource ? (
              <Form
                key={`${open.ruleCode}:${open.version}`}
                form={form}
                layout="vertical"
                initialValues={{
                  referenceSource: open.referenceSource ?? '',
                  sourceIdentity: open.sourceIdentity ?? '',
                  sourceUrl: open.sourceUrl ?? '',
                  documentRef: open.documentRef ?? '',
                  issuingOrganization: open.issuingOrganization ?? '',
                  evidenceNote: open.evidenceNote ?? '',
                  sourcePublishedOn: open.sourcePublishedOn ? dayjs(open.sourcePublishedOn) : null,
                }}
              >
                <Typography.Text type="secondary">
                  Nhãn catalog không phải nguồn. Không có giá trị nào được điền sẵn để chấp nhận.
                </Typography.Text>
                <Form.Item label="Trích dẫn" name="referenceSource">
                  <Input.TextArea rows={3} />
                </Form.Item>
                <Form.Item label="Định danh nguồn" name="sourceIdentity">
                  <Input />
                </Form.Item>
                <Form.Item label="URL" name="sourceUrl">
                  <Input />
                </Form.Item>
                <Form.Item label="Mã hồ sơ" name="documentRef">
                  <Input />
                </Form.Item>
                <Form.Item label="Cơ quan ban hành" name="issuingOrganization">
                  <Input />
                </Form.Item>
                <Form.Item label="Vị trí bằng chứng" name="evidenceNote">
                  <Input.TextArea rows={2} />
                </Form.Item>
                <Form.Item label="Ngày văn bản" name="sourcePublishedOn">
                  <DatePicker style={{ width: '100%' }} />
                </Form.Item>
              </Form>
            ) : null}
            {publishAllowed && open.lifecycleStatus === 'IN_REVIEW' && blockers.length > 0 ? (
              <Alert
                type="warning"
                showIcon
                message="Chưa xuất bản được"
                description={(
                  <ul style={{ margin: 0, paddingLeft: 18 }}>
                    {blockers.map((item) => <li key={item}>{item}</li>)}
                  </ul>
                )}
              />
            ) : null}
            <Space wrap>
              {actions.submitReview ? (
                <Button onClick={() => void submit(open)}>Gửi xét duyệt</Button>
              ) : null}
              {actions.returnToDraft ? (
                <Button onClick={() => void returnDraft(open)}>Trả về nháp</Button>
              ) : null}
              {actions.verifySource ? (
                <Button type="primary" onClick={() => void verify(open, 'ACCEPTED')}>Chấp nhận nguồn</Button>
              ) : null}
              {actions.verifySource ? (
                <Button danger onClick={() => void verify(open, 'REJECTED')}>Từ chối nguồn</Button>
              ) : null}
              {actions.publish ? (
                <Popconfirm
                  title="Xuất bản không làm nhà thuốc tự áp dụng"
                  description="Nhà thuốc chỉ dùng phiên bản này sau khi tự ghim đúng version."
                  okText="Xuất bản"
                  cancelText="Hủy"
                  onConfirm={() => publish(open)}
                >
                  <Button type="primary">Xuất bản</Button>
                </Popconfirm>
              ) : null}
              {actions.retire ? (
                <Popconfirm
                  title="Thu hồi phiên bản đã xuất bản?"
                  description="Runtime ngừng dùng phiên bản này. Pin không tự chuyển sang phiên bản khác."
                  okText="Thu hồi"
                  cancelText="Hủy"
                  onConfirm={() => retire(open)}
                >
                  <Button danger>Thu hồi</Button>
                </Popconfirm>
              ) : null}
            </Space>
            <Typography.Text strong>Lịch sử xuất bản</Typography.Text>
            {history.length === 0 ? <Typography.Text type="secondary">Chưa có lần xuất bản hoặc thu hồi.</Typography.Text> : null}
            {history.map((item) => (
              <Typography.Paragraph key={`${item.createdAt}-${item.toStatus}`} style={{ marginBottom: 8 }}>
                {day(item.createdAt)} · {item.fromStatus} → {item.toStatus}
                <br />
                <Typography.Text type="secondary">{item.actorUserId}</Typography.Text>
              </Typography.Paragraph>
            ))}
          </Space>
        ) : null}
      </Drawer>
    </Card>
  );
}
