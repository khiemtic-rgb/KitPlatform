import { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Alert,
  Button,
  Drawer,
  Form,
  Input,
  Select,
  Space,
  Steps,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { TextAreaRef } from 'antd/es/input/TextArea';
import { ReloadOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  createLocalOsListing,
  fetchLocalOsListings,
  fetchLocalOsReports,
  fetchLocalOsSources,
  ingestLocalOsSource,
  publishLocalOsHomepage,
  rewriteLocalOsListing,
  setLocalOsListingStatus,
  updateLocalOsListing,
  type LocalListing,
  type LocalListingReport,
  type LocalSource,
} from '@/shared/api/local-os.api';
import { looksLikeRawDump, rewriteListingCopy } from './rewriteListingCopy';

const STATUS_COLOR: Record<string, string> = {
  NEEDS_REVIEW: 'gold',
  ACTIVE: 'green',
  HIDDEN: 'default',
  EXPIRED: 'red',
};

const STATUS_LABEL: Record<string, string> = {
  NEEDS_REVIEW: 'Chờ duyệt',
  ACTIVE: 'Đang đăng',
  HIDDEN: 'Ẩn',
  EXPIRED: 'Hết hạn',
};

const KIND_LABEL: Record<string, string> = {
  job: 'Việc',
  event: 'Sự kiện',
  room: 'Trọ',
};

const REPORT_LABEL: Record<string, string> = {
  wrong_phone: 'Sai số',
  gone: 'Hết phòng / hết việc',
  no_answer: 'Không liên lạc được',
  other: 'Tin không đúng',
};

const KIND_OPTIONS = [
  { value: 'job', label: 'Việc' },
  { value: 'event', label: 'Sự kiện' },
  { value: 'room', label: 'Trọ' },
];

function splitPosts(raw: string): string[] {
  const text = raw.replace(/\r/g, '').trim();
  if (!text) return [];
  const byDash = text.split(/\n-{3,}\n/).map((s) => s.trim()).filter((s) => s.length >= 12);
  if (byDash.length > 1) return byDash;
  return text.length >= 8 ? [text] : [];
}

function guessTitle(text: string): string {
  for (const line of text.replace(/\r/g, '').split('\n')) {
    const t = line.replace(/\s+/g, ' ').trim();
    if (t.length >= 8) return t.length <= 140 ? t : `${t.slice(0, 140)}…`;
  }
  const compact = text.replace(/\s+/g, ' ').trim();
  return compact.slice(0, 140) || 'Tin từ nhóm';
}

function guessPhone(text: string): string | undefined {
  const m = text.match(/(?:\+?84|0)(?:\s|\.|-)?[35789](?:\s|\.|-)?\d(?:\s|\.|-){0,2}\d{3}(?:\s|\.|-){0,2}\d{3,4}/);
  if (!m) return undefined;
  let digits = m[0].replace(/\D/g, '');
  if (digits.startsWith('84') && digits.length >= 11) digits = `0${digits.slice(2)}`;
  return digits.length >= 9 && digits.length <= 12 ? digits : undefined;
}

function guessSalary(text: string): string | undefined {
  const patterns = [
    /\d{1,3}(?:[.\s]\d{3})+\s*đ?\s*[-–~]\s*\d{1,3}(?:[.\s]\d{3})+\s*đ?\s*\/?\s*(?:giờ|gio|h)\b/i,
    /\d+(?:[.,]\d+)?\s*k\s*[-–~]\s*\d+(?:[.,]\d+)?\s*k(?:\s*\/?\s*(?:giờ|gio|h))?/i,
    /\d+(?:[.,]\d+)?\s*k\s*\/\s*(?:giờ|gio|h)\b/i,
    /\d+(?:[.,]\d+)?\s*tr(?:iệu)?\d?\s*[-–~]\s*\d+(?:[.,]\d+)?\s*tr(?:iệu)?/i,
    /\d+[.,]\d+\s*tr(?:iệu)?(?:\s*\/\s*tháng)?/i,
    /\d+\s*tr(?:iệu)?\d?(?:\s*\/\s*(?:tháng|khóa|khoa))?/i,
    /(?:lương|thu nhập|lcb)\s*[:：]\s*[^\n]{4,56}/i,
  ];
  let best: string | undefined;
  let bestAt = Number.POSITIVE_INFINITY;
  for (const p of patterns) {
    const m = p.exec(text);
    if (!m || m.index >= bestAt) continue;
    let s = m[0].replace(/\s+/g, ' ').trim();
    s = s.replace(/^(?:lương|thu nhập|lcb)\s*[:：]\s*/i, '').split(/\s+[•·|(]/)[0]?.trim() ?? s;
    if (s.length >= 3 && s.length <= 48) {
      best = s;
      bestAt = m.index;
    }
  }
  return best;
}

function guessPlace(text: string): string {
  if (/quyết thắng|quyet thang/i.test(text)) return 'Phường Quyết Thắng';
  if (/phan đình phùng|phan dinh phung/i.test(text)) return 'Phường Phan Đình Phùng';
  if (/thái nguyên|thai nguyen/i.test(text)) return 'TP. Thái Nguyên';
  return 'Thái Nguyên';
}

function reviewGaps(values: {
  kind?: string;
  title?: string;
  placeText?: string;
  contactPhone?: string;
}): string[] {
  const gaps: string[] = [];
  if (!values.kind) gaps.push('Chọn loại tin');
  if (!String(values.title ?? '').trim()) gaps.push('Thiếu tiêu đề');
  if (!String(values.placeText ?? '').trim()) gaps.push('Thiếu địa điểm');
  if (!String(values.contactPhone ?? '').trim()) gaps.push('Thiếu số điện thoại');
  return gaps;
}

export function LocalOsListingsPage() {
  const [items, setItems] = useState<LocalListing[]>([]);
  const [reports, setReports] = useState<LocalListingReport[]>([]);
  const [reportOf, setReportOf] = useState<LocalListing | null>(null);
  const [loading, setLoading] = useState(false);
  const [ingesting, setIngesting] = useState(false);
  const [polishing, setPolishing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState<string>('NEEDS_REVIEW');
  const [kind, setKind] = useState<string | undefined>();
  const [writing, setWriting] = useState<LocalListing | 'new' | null>(null);
  const [sources, setSources] = useState<LocalSource[]>([]);
  const [added, setAdded] = useState(0);
  const [lastError, setLastError] = useState<string | null>(null);
  const [form] = Form.useForm();
  const [editForm] = Form.useForm();
  const pasteRef = useRef<TextAreaRef>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [rows, inbox] = await Promise.all([
        fetchLocalOsListings({
          status: status === 'REPORTED' ? undefined : status || undefined,
          kind,
        }),
        fetchLocalOsReports().catch(() => [] as LocalListingReport[]),
      ]);
      setReports(inbox);
      const flagged = new Set(inbox.map((r) => r.listingId));
      setItems(status === 'REPORTED' ? rows.filter((row) => flagged.has(row.id)) : rows);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được danh sách.'));
    } finally {
      setLoading(false);
    }
  }, [status, kind]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    void fetchLocalOsSources()
      .then(setSources)
      .catch(() => undefined);
  }, []);

  const ingest = async () => {
    try {
      const values = await form.validateFields();
      const chunks = splitPosts(String(values.pastedText ?? ''));
      if (chunks.length === 0) {
        setLastError('Dán nội dung bài vào ô trên, rồi bấm Thêm.');
        return;
      }
      setLastError(null);
      setStatus('NEEDS_REVIEW');
      setIngesting(true);
      let created = 0;
      let skipped = 0;
      const sourceUrl = String(values.sourceUrl ?? '').trim();
      const kind = values.kind || 'job';
      for (const pastedText of chunks) {
        const written = rewriteListingCopy(pastedText, kind);
        if (sourceUrl) {
          const result = await ingestLocalOsSource({
            sourceUrl,
            pastedText: written.body,
            kind,
            sourceId: values.sourceId,
          });
          if (result.existing) skipped += 1;
          else {
            await updateLocalOsListing(result.listing.id, {
              kind,
              title: written.title,
              summary: written.body,
              placeText: written.place || guessPlace(pastedText),
              contactPhone: written.phone || guessPhone(pastedText),
              salaryText: kind === 'room' ? undefined : written.salary || guessSalary(pastedText),
              sourceKind: result.listing.sourceKind ?? 'group_paste',
              sourceUrl: result.listing.sourceUrl,
              trust: result.listing.trust,
              safetyFlag: result.listing.safetyFlag,
              status: result.listing.status,
            });
            created += 1;
          }
        } else {
          try {
            await createLocalOsListing({
              kind,
              title: written.title || guessTitle(pastedText),
              summary: written.body,
              placeText: written.place || guessPlace(pastedText),
              contactPhone: written.phone || guessPhone(pastedText),
              salaryText: kind === 'room' ? undefined : written.salary || guessSalary(pastedText),
            });
            created += 1;
          } catch (error) {
            const msg = apiErrorMessage(error, '');
            if (/trùng|đã có/i.test(msg)) skipped += 1;
            else throw error;
          }
        }
      }
      setAdded((n) => n + created);
      if (created > 0) message.success(`Đã thêm ${created} tin. Đang mở hàng chờ duyệt — dán tiếp được.`);
      if (skipped > 0) message.info(`${skipped} tin đã có — bỏ qua.`);
      form.setFieldsValue({ pastedText: '', sourceUrl: undefined });
      await load();
      window.setTimeout(() => pasteRef.current?.focus(), 50);
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) return;
      const msg = apiErrorMessage(error, 'Không thêm được tin. Kiểm tra API local (:5290) và đăng nhập ADMIN KIT_LOCAL.');
      setLastError(msg);
      message.error(msg);
    } finally {
      setIngesting(false);
    }
  };

  const applyRewrite = async (raw: string, kindValue: string) => {
    const local = rewriteListingCopy(raw, kindValue);
    try {
      const ai = await rewriteLocalOsListing({ text: raw, kind: kindValue });
      const drafted = {
        title: ai.title,
        body: ai.body,
        place: ai.place ?? undefined,
        phone: ai.phone ?? undefined,
        salary: kindValue === 'room' ? undefined : ai.salary ?? undefined,
      };
      if (looksLikeRawDump(drafted, raw)) {
        return { ...local, via: 'rules' as const, note: 'dump' };
      }
      return { ...drafted, via: 'ai' as const };
    } catch (error) {
      return { ...local, via: 'rules' as const, note: apiErrorMessage(error, '') };
    }
  };

  const polishBox = async () => {
    const raw = String(form.getFieldValue('pastedText') ?? '');
    if (raw.trim().length < 8) {
      message.warning('Dán nội dung bài trước, rồi bấm Viết lại.');
      return;
    }
    const kindValue = String(form.getFieldValue('kind') || 'job');
    setPolishing(true);
    try {
      const w = await applyRewrite(raw, kindValue);
      form.setFieldsValue({ pastedText: w.body });
      if (w.via === 'ai') message.success('AI đã viết lại. Đọc lại rồi thêm vào danh sách.');
      else if (w.note === 'dump') message.warning('AI trả bài thô — đã biên tập lại tại máy. Đọc lại trước khi thêm.');
      else message.warning('Chưa gọi được AI — đã biên tập tại máy. Đọc lại trước khi thêm.');
    } finally {
      setPolishing(false);
    }
  };

  const openWrite = (row: LocalListing) => {
    setWriting(row);
    editForm.setFieldsValue({
      kind: row.kind,
      title: row.title,
      summary: row.summary,
      placeText: row.placeText,
      salaryText: row.salaryText,
      contactPhone: row.contactPhone,
      contactName: row.contactName,
      workingTime: row.workingTime,
      requirements: row.requirements,
    });
  };

  const polishEdit = async () => {
    const kindValue = String(editForm.getFieldValue('kind') || 'job');
    const raw = [editForm.getFieldValue('title'), editForm.getFieldValue('summary')].filter(Boolean).join('\n');
    if (String(raw).trim().length < 8) return;
    setPolishing(true);
    try {
      const w = await applyRewrite(String(raw), kindValue);
      editForm.setFieldsValue({
        title: w.title,
        summary: w.body,
        placeText: w.place || editForm.getFieldValue('placeText'),
        contactPhone: w.phone || editForm.getFieldValue('contactPhone'),
        salaryText: kindValue === 'room' ? undefined : w.salary || editForm.getFieldValue('salaryText'),
      });
      if (w.via === 'ai') message.success('AI đã viết lại nội dung.');
      else if (w.note === 'dump') message.warning('AI trả bài thô — đã biên tập lại tại máy.');
      else message.warning('Chưa gọi được AI — đã biên tập tại máy.');
    } finally {
      setPolishing(false);
    }
  };

  const openNew = () => {
    setWriting('new');
    editForm.resetFields();
    editForm.setFieldsValue({ kind: 'job' });
  };

  const persistWrite = async (publish: boolean) => {
    const values = await editForm.validateFields();
    const gaps = reviewGaps(values);
    if (publish && gaps.length > 0) {
      message.warning(gaps.join(' · '));
      return;
    }
    setSaving(true);
    try {
      const body = {
        kind: values.kind as string,
        title: String(values.title).trim(),
        summary: values.summary,
        placeText: values.placeText,
        salaryText: values.kind === 'room' ? undefined : values.salaryText,
        contactPhone: values.contactPhone,
        contactName: values.contactName,
        workingTime: values.workingTime,
        requirements: values.requirements,
      };
      let id: string;
      if (writing === 'new') {
        const created = await createLocalOsListing(body);
        id = created.id;
      } else if (writing) {
        await updateLocalOsListing(writing.id, {
          ...body,
          sourceKind: writing.sourceKind ?? 'group_paste',
          sourceUrl: writing.sourceUrl,
          trust: writing.trust,
          safetyFlag: writing.safetyFlag,
          status: writing.status,
        });
        id = writing.id;
      } else {
        return;
      }
      if (publish) {
        await setLocalOsListingStatus(id, 'ACTIVE');
        message.success('Đã lên trang chủ Thái Nguyên Life.');
      } else {
        message.success('Đã lưu. Vẫn ở hàng chờ duyệt.');
      }
      setWriting(null);
      setStatus(publish ? 'ACTIVE' : 'NEEDS_REVIEW');
      await load();
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) return;
      message.error(apiErrorMessage(error, 'Không lưu được.'));
    } finally {
      setSaving(false);
    }
  };

  const setStatusOf = async (id: string, next: string) => {
    try {
      await setLocalOsListingStatus(id, next);
        message.success(next === 'ACTIVE' ? 'Đã lên trang chủ Thái Nguyên Life.' : 'Đã cập nhật trang chủ.');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không đổi được trạng thái.'));
    }
  };

  const columns: ColumnsType<LocalListing> = [
    {
      title: 'Loại',
      dataIndex: 'kind',
      width: 88,
      render: (v: string) => KIND_LABEL[v] ?? v,
    },
    {
      title: 'Tin',
      dataIndex: 'title',
      render: (_, row) => (
        <div>
          <div style={{ fontWeight: 600 }}>{row.title}</div>
          <Typography.Text type="secondary">
            {[row.placeText, row.salaryText, row.contactPhone].filter(Boolean).join(' · ')}
          </Typography.Text>
          {reports.filter((r) => r.listingId === row.id).length > 0 ? (
            <div>
              <Tag color="orange">
                {reports.filter((r) => r.listingId === row.id).length} báo cáo độc giả
              </Tag>
            </div>
          ) : null}
        </div>
      ),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 120,
      render: (v: string, row) => (
        <Space direction="vertical" size={0}>
          <Tag color={STATUS_COLOR[v] ?? 'default'}>{STATUS_LABEL[v] ?? v}</Tag>
          {row.safetyFlag ? <Tag color="red">Cần đọc kỹ</Tag> : null}
        </Space>
      ),
    },
    {
      title: '',
      width: 260,
      render: (_, row) => (
        <Space wrap>
          <Button size="small" type="primary" ghost onClick={() => openWrite(row)}>
            Viết / duyệt
          </Button>
          {row.status !== 'ACTIVE' ? (
            <Button size="small" onClick={() => void setStatusOf(row.id, 'ACTIVE')}>
              Đăng
            </Button>
          ) : null}
          {row.status !== 'HIDDEN' ? (
            <Button size="small" onClick={() => void setStatusOf(row.id, 'HIDDEN')}>
              Ẩn
            </Button>
          ) : null}
          {reports.some((r) => r.listingId === row.id) ? (
            <Button size="small" onClick={() => setReportOf(row)}>
              Xem báo cáo
            </Button>
          ) : null}
        </Space>
      ),
    },
  ];

  return (
    <div style={{ padding: 16 }}>
      <Typography.Title level={4} style={{ marginTop: 0 }}>
        Tin Thái Nguyên Life
      </Typography.Title>
      <Steps
        size="small"
        current={0}
        style={{ maxWidth: 720, marginBottom: 16 }}
        items={[
          { title: 'Dán vào danh sách' },
          { title: 'Viết bài' },
          { title: 'Duyệt' },
          { title: 'Đăng' },
        ]}
      />
      <Typography.Paragraph type="secondary">
        Copy bài từ nhóm quảng cáo, dán vào ô dưới — thêm liên tục, không cần link.
        Nhiều bài cách nhau bằng một dòng <code>---</code>. Sau đó mở tin → viết lại → duyệt → đăng.
        Học bổng / ưu đãi dán loại <strong>Sự kiện</strong> — không mở mục ưu đãi riêng.
        Site chỉ hiện tin đã đăng. Độc giả báo sai số / hết phòng — không tự ẩn; vào{' '}
        <strong>Có báo cáo</strong> rồi Ẩn hoặc sửa số.{' '}
        <Link to="/local-os/sources">Sổ nguồn</Link>
        {' · '}
        <Link to="/local-os/stats">Thống kê</Link>
      </Typography.Paragraph>
      {reports.length > 0 && status !== 'REPORTED' ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12, maxWidth: 760 }}
          message={`${new Set(reports.map((r) => r.listingId)).size} tin có báo cáo từ độc giả. Lọc «Có báo cáo» để xử lý.`}
        />
      ) : null}

      <Form
        form={form}
        layout="vertical"
        style={{ maxWidth: 760, marginBottom: 8 }}
        initialValues={{ kind: 'job' }}
      >
        <Space wrap style={{ width: '100%' }} align="start">
          <Form.Item name="kind" label="Loại" style={{ width: 160, marginBottom: 12 }}>
            <Select options={KIND_OPTIONS} />
          </Form.Item>
          <Form.Item name="sourceId" label="Nhóm / nguồn (nếu có)" style={{ minWidth: 280, flex: 1, marginBottom: 12 }}>
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder="Không bắt buộc"
              options={sources
                .filter((s) => s.status === 'active')
                .map((s) => ({ value: s.id, label: s.name }))}
            />
          </Form.Item>
        </Space>
        <Form.Item name="sourceUrl" label="Link bài (không bắt buộc)">
          <Input placeholder="Dán nếu có — bỏ trống cũng được" />
        </Form.Item>
        <Form.Item
          name="pastedText"
          label="Nội dung bài"
          extra="Dán bài thô → AI viết lại (bỏ tắt, cảm thán, câu hô) → đọc lại → Thêm. Không bịa lương / SĐT. Ctrl + Enter để thêm."
        >
          <Input.TextArea
            rows={8}
            placeholder="Dán nguyên bài vừa copy…"
            ref={pasteRef}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
                e.preventDefault();
                void ingest();
              }
            }}
          />
        </Form.Item>
        {lastError ? (
          <Alert type="error" showIcon style={{ marginBottom: 12 }} message={lastError} />
        ) : null}
        <Space>
          <Button htmlType="button" loading={polishing} onClick={() => void polishBox()}>
            AI viết lại cho chuẩn
          </Button>
          <Button type="primary" htmlType="button" loading={ingesting} onClick={() => void ingest()}>
            Thêm vào danh sách
          </Button>
          <Button onClick={openNew}>Viết tin mới</Button>
          {added > 0 ? (
            <Typography.Text type="secondary">Đã thêm {added} tin phiên này</Typography.Text>
          ) : null}
        </Space>
      </Form>

      <Space style={{ margin: '20px 0 12px' }} wrap>
        <Typography.Text strong>
          {status === 'NEEDS_REVIEW'
            ? `Chờ duyệt (${items.length})`
            : status === 'EXPIRED'
              ? 'Hết hạn — tin vừa thêm nằm ở Chờ duyệt'
              : status === 'REPORTED'
                ? `Có báo cáo độc giả (${items.length})`
                : `Danh sách (${items.length})`}
        </Typography.Text>
        <Select
          style={{ width: 160 }}
          value={status}
          onChange={setStatus}
          options={[
            { value: 'NEEDS_REVIEW', label: 'Chờ duyệt' },
            { value: 'ACTIVE', label: 'Đang đăng' },
            { value: 'HIDDEN', label: 'Ẩn' },
            { value: 'EXPIRED', label: 'Hết hạn' },
            { value: 'REPORTED', label: 'Có báo cáo' },
            { value: '', label: 'Tất cả' },
          ]}
        />
        <Select
          allowClear
          placeholder="Loại tin"
          style={{ width: 140 }}
          value={kind}
          onChange={setKind}
          options={KIND_OPTIONS}
        />
        <Button icon={<ReloadOutlined />} onClick={() => void load()}>
          Tải lại
        </Button>
        <Button
          onClick={() => {
            void (async () => {
              try {
                const r = await publishLocalOsHomepage();
                if (r.ok) message.success(r.message || `Đã lên trang chủ (${r.listingCount} tin).`);
                else message.error(r.message || 'Chưa lên được trang chủ.');
              } catch (error) {
                message.error(apiErrorMessage(error, 'Chưa lên được trang chủ.'));
              }
            })();
          }}
        >
          Cập nhật trang chủ
        </Button>
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        columns={columns}
        dataSource={items}
        pagination={{ pageSize: 20 }}
      />

      <Drawer
        title={writing === 'new' ? 'Viết tin mới' : 'Viết bài → duyệt → đăng'}
        width={560}
        open={!!writing}
        onClose={() => setWriting(null)}
        extra={
          <Space>
            <Button onClick={() => setWriting(null)}>Đóng</Button>
            <Button loading={saving} onClick={() => void persistWrite(false)}>
              Lưu
            </Button>
            <Button type="primary" loading={saving} onClick={() => void persistWrite(true)}>
              Duyệt &amp; đăng
            </Button>
          </Space>
        }
      >
        <Typography.Paragraph type="secondary">
          AI viết lại cho rõ — bỏ tắt, cảm thán, câu hô. Không bịa số. Bấm <strong>Duyệt &amp; đăng</strong> khi đủ tiêu đề, địa điểm và số điện thoại.
          Tin ACTIVE lên thainguyenlife.vn ngay, không cần deploy.
        </Typography.Paragraph>
        <Button style={{ marginBottom: 12 }} loading={polishing} onClick={() => void polishEdit()}>
          AI viết lại cho chuẩn
        </Button>
        <Form form={editForm} layout="vertical">
          <Form.Item name="kind" label="Loại" rules={[{ required: true }]}>
            <Select options={KIND_OPTIONS} />
          </Form.Item>
          <Form.Item name="title" label="Tiêu đề" rules={[{ required: true, message: 'Nhập tiêu đề' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="placeText" label="Địa điểm">
            <Input placeholder="Phường / gần trường / quán" />
          </Form.Item>
          <Form.Item name="contactName" label="Người liên hệ">
            <Input />
          </Form.Item>
          <Form.Item name="contactPhone" label="Số điện thoại">
            <Input />
          </Form.Item>
          <Form.Item name="workingTime" label="Thời gian (việc / sự kiện)">
            <Input />
          </Form.Item>
          <Form.Item name="salaryText" label="Thu nhập (việc — phòng để trống)">
            <Input />
          </Form.Item>
          <Form.Item name="summary" label="Nội dung">
            <Input.TextArea rows={8} />
          </Form.Item>
          <Form.Item name="requirements" label="Ghi chú thêm">
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      </Drawer>

      <Drawer
        title={reportOf ? `Báo cáo: ${reportOf.title}` : 'Báo cáo độc giả'}
        width={420}
        open={!!reportOf}
        onClose={() => setReportOf(null)}
        extra={
          reportOf && reportOf.status !== 'HIDDEN' ? (
            <Button
              danger
              onClick={() => {
                void setStatusOf(reportOf.id, 'HIDDEN');
                setReportOf(null);
              }}
            >
              Ẩn tin
            </Button>
          ) : null
        }
      >
        <Typography.Paragraph type="secondary">
          Một báo cáo ẩn danh không đủ để tự ẩn. Kiểm tra số / còn phòng rồi Ẩn hoặc sửa tin.
        </Typography.Paragraph>
        {reports
          .filter((r) => r.listingId === reportOf?.id)
          .map((r) => (
            <div key={r.id} style={{ marginBottom: 12 }}>
              <Tag>{REPORT_LABEL[r.reason] ?? r.reason}</Tag>
              <Typography.Text type="secondary">
                {' '}
                {new Date(r.createdAt).toLocaleString('vi-VN')}
              </Typography.Text>
              {r.note ? <div>{r.note}</div> : null}
            </div>
          ))}
      </Drawer>
    </div>
  );
}
