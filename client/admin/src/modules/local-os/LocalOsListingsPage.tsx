import { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Alert,
  App,
  Button,
  Col,
  Drawer,
  Form,
  Input,
  Row,
  Select,
  Space,
  Steps,
  Table,
  Tag,
  Typography,
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
import { looksLikeRawDump, rewriteListingCopy, type ListingRewrite } from './rewriteListingCopy';

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

const EMPLOY_OPTIONS = [
  { value: 'part_time', label: 'Bán thời gian' },
  { value: 'full_time', label: 'Toàn thời gian' },
  { value: 'internship', label: 'Thực tập' },
  { value: 'weekend', label: 'Cuối tuần' },
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

function pick(ai?: string | null, local?: string): string | undefined {
  const a = (ai ?? '').trim();
  if (a) return a;
  const b = (local ?? '').trim();
  return b || undefined;
}

function draftToForm(kind: string, w: ListingRewrite) {
  return {
    kind,
    title: w.title,
    summary: w.body,
    placeText: w.place,
    contactPhone: w.phone,
    contactName: w.contactName,
    workingTime: w.workingTime,
    salaryText: kind === 'room' ? undefined : w.salary,
    requirements: w.requirements,
    organizationName: w.organizationName,
    employmentType: w.employmentType,
  };
}

function reviewGaps(values: {
  kind?: string;
  title?: string;
  contactPhone?: string;
}): string[] {
  const gaps: string[] = [];
  const kind = String(values.kind ?? '');
  if (!kind) gaps.push('Chọn loại tin');
  if (!String(values.title ?? '').trim()) gaps.push('Thiếu tiêu đề');
  if ((kind === 'job' || kind === 'room') && !String(values.contactPhone ?? '').trim()) {
    gaps.push('Thiếu số điện thoại');
  }
  return gaps;
}

export function LocalOsListingsPage() {
  const { message } = App.useApp();
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
  const [formHint, setFormHint] = useState<string | null>(null);
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
              contactName: written.contactName,
              workingTime: written.workingTime,
              requirements: written.requirements,
              organizationName: written.organizationName,
              employmentType: written.employmentType,
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
              contactName: written.contactName,
              workingTime: written.workingTime,
              requirements: written.requirements,
              organizationName: written.organizationName,
              employmentType: written.employmentType,
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
      const drafted: ListingRewrite = {
        title: pick(ai.title, local.title) || local.title,
        body: pick(ai.body, local.body) || local.body,
        place: pick(ai.place, local.place),
        phone: pick(ai.phone, local.phone),
        salary: kindValue === 'room' ? undefined : pick(ai.salary, local.salary),
        contactName: pick(ai.contactName, local.contactName),
        workingTime: pick(ai.workingTime, local.workingTime),
        requirements: pick(ai.requirements, local.requirements),
        organizationName: pick(ai.organizationName, local.organizationName),
        employmentType: pick(ai.employmentType, local.employmentType),
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
      setWriting('new');
      setFormHint(null);
      editForm.setFieldsValue({
        ...draftToForm(kindValue, w),
        placeText: w.place || guessPlace(raw),
        contactPhone: w.phone || guessPhone(raw),
      });
      if (w.via === 'ai') message.success('Đã điền form bên phải. Sửa rồi Duyệt & đăng.');
      else if (w.note === 'dump') message.warning('AI trả bài thô — đã tách trường tại máy. Kiểm tra form rồi duyệt.');
      else message.warning('Chưa gọi được AI — đã tách trường tại máy. Kiểm tra form rồi duyệt.');
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
      organizationName: row.organizationName,
      employmentType: row.employmentType,
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const persistWrite = async (publish: boolean) => {
    let values: Record<string, unknown>;
    try {
      values = await editForm.validateFields();
    } catch {
      setFormHint('Thiếu tiêu đề hoặc loại tin.');
      message.error('Thiếu tiêu đề hoặc loại tin.');
      return;
    }
    const pasted = String(form.getFieldValue('pastedText') ?? '');
    const contactPhone =
      String(values.contactPhone ?? '').trim() || guessPhone(pasted) || '';
    const placeText = String(values.placeText ?? '').trim() || guessPlace(pasted);
    editForm.setFieldsValue({ contactPhone, placeText });
    const merged = { ...values, contactPhone, placeText };
    const gaps = reviewGaps(merged);
    if (publish && gaps.length > 0) {
      setFormHint(gaps.join(' · '));
      message.warning(gaps.join(' · '));
      if (gaps.some((g) => g.includes('điện thoại'))) {
        editForm.setFields([{ name: 'contactPhone', errors: ['Cần số để đăng việc / trọ'] }]);
      }
      return;
    }
    setFormHint(null);
    setSaving(true);
    try {
      const body = {
        kind: String(values.kind ?? ''),
        title: String(values.title ?? '').trim(),
        summary: values.summary == null ? undefined : String(values.summary),
        placeText,
        salaryText: String(values.kind ?? '') === 'room' ? undefined : values.salaryText == null ? undefined : String(values.salaryText),
        contactPhone,
        contactName: values.contactName == null ? undefined : String(values.contactName),
        workingTime: values.workingTime == null ? undefined : String(values.workingTime),
        requirements: values.requirements == null ? undefined : String(values.requirements),
        organizationName: values.organizationName == null ? undefined : String(values.organizationName),
        employmentType: values.employmentType == null ? undefined : String(values.employmentType),
      };
      let id: string;
      if (writing && writing !== 'new') {
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
        const created = await createLocalOsListing(body);
        id = created.id;
      }
      if (publish) {
        await setLocalOsListingStatus(id, 'ACTIVE');
        if (import.meta.env.DEV) {
          message.warning(
            'Đã đăng trên máy local. thainguyenlife.vn lấy tin từ api.novixa.vn — chưa lên trang chủ mạng.',
          );
        } else {
          try {
            const r = await publishLocalOsHomepage();
            if (r.ok && !r.skipped) message.success(r.message || 'Đã lên trang chủ Thái Nguyên Life.');
            else message.warning(r.message || 'Đã đăng. Trang chủ mạng chưa cập nhật.');
          } catch (error) {
            message.warning(apiErrorMessage(error, 'Đã đăng. Chưa đẩy được trang chủ — bấm Cập nhật trang chủ.'));
          }
        }
      } else {
        message.success('Đã lưu. Vẫn ở hàng chờ duyệt.');
      }
      setFormHint(null);
      setWriting(null);
      editForm.resetFields();
      editForm.setFieldsValue({ kind: form.getFieldValue('kind') || 'job' });
      form.setFieldsValue({ pastedText: '' });
      setStatus(publish ? 'ACTIVE' : 'NEEDS_REVIEW');
      await load();
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) {
        setFormHint('Thiếu tiêu đề hoặc loại tin.');
        return;
      }
      const msg = apiErrorMessage(error, 'Không lưu được.');
      setFormHint(msg);
      message.error(msg);
    } finally {
      setSaving(false);
    }
  };

  const setStatusOf = async (id: string, next: string) => {
    try {
      await setLocalOsListingStatus(id, next);
      if (next === 'ACTIVE' && import.meta.env.DEV) {
        message.warning('Đã đăng trên máy local. thainguyenlife.vn chưa có tin này.');
      } else {
        message.success(next === 'ACTIVE' ? 'Đã đăng.' : 'Đã cập nhật.');
      }
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
            {[row.organizationName, row.placeText, row.salaryText, row.contactName, row.contactPhone]
              .filter(Boolean)
              .join(' · ')}
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
      {import.meta.env.DEV ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          message="Đang đăng trên máy local chưa lên thainguyenlife.vn. Trang chủ mạng lấy tin từ api.novixa.vn (admin.novixa.vn)."
        />
      ) : null}
      <Steps
        size="small"
        current={0}
        style={{ maxWidth: 720, marginBottom: 16 }}
        items={[
          { title: 'Dán bài' },
          { title: 'AI điền form cạnh' },
          { title: 'Sửa & duyệt' },
          { title: 'Đăng' },
        ]}
      />
      <Typography.Paragraph type="secondary">
        Copy bài từ nhóm, dán bên trái → AI điền form bên phải → sửa SĐT/tên → Duyệt & đăng.
        Học bổng / ưu đãi dán loại <strong>Sự kiện</strong> — không mở mục ưu đãi riêng.
        Site chỉ hiện tin đã đăng. Độc giả báo sai số / hết phòng — không tự ẩn; vào{' '}
        <strong>Có báo cáo</strong> rồi Ẩn hoặc sửa số.{' '}
        <Link to="/local-os/duyet">Duyệt trên điện thoại</Link>
        {' · '}
        <Link to="/local-os/sources">Sổ nguồn</Link>
        {' · '}
        <Link to="/local-os/stats">Thống kê</Link>
      </Typography.Paragraph>
      {reports.length > 0 && status !== 'REPORTED' ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          message={`${new Set(reports.map((r) => r.listingId)).size} tin có báo cáo từ độc giả. Lọc «Có báo cáo» để xử lý.`}
        />
      ) : null}

      <Row gutter={[24, 24]} align="top">
        <Col xs={24} xl={12}>
          <Form
            form={form}
            layout="vertical"
            initialValues={{ kind: 'job' }}
          >
            <Space wrap style={{ width: '100%' }} align="start">
              <Form.Item name="kind" label="Loại" style={{ width: 160, marginBottom: 12 }}>
                <Select
                  options={KIND_OPTIONS}
                  onChange={(v) => editForm.setFieldsValue({ kind: v })}
                />
              </Form.Item>
              <Form.Item name="sourceId" label="Nhóm / nguồn (nếu có)" style={{ minWidth: 220, flex: 1, marginBottom: 12 }}>
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
              extra="Giữ bài gốc bên trái để đối chiếu. AI điền form bên phải — không xóa ô này."
            >
              <Input.TextArea
                rows={16}
                placeholder="Dán nguyên bài vừa copy…"
                ref={pasteRef}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
                    e.preventDefault();
                    void polishBox();
                  }
                }}
              />
            </Form.Item>
            {lastError ? (
              <Alert type="error" showIcon style={{ marginBottom: 12 }} message={lastError} />
            ) : null}
            <Space wrap>
              <Button type="primary" htmlType="button" loading={polishing} onClick={() => void polishBox()}>
                AI viết lại &amp; điền form
              </Button>
              <Button htmlType="button" loading={ingesting} onClick={() => void ingest()}>
                Thêm vào danh sách
              </Button>
              {added > 0 ? (
                <Typography.Text type="secondary">Đã thêm {added} tin phiên này</Typography.Text>
              ) : null}
            </Space>
          </Form>
        </Col>
        <Col xs={24} xl={12}>
          <div
            style={{
              border: '1px solid #f0f0f0',
              borderRadius: 8,
              padding: 16,
              background: '#fafafa',
            }}
          >
            <Typography.Title level={5} style={{ marginTop: 0 }}>
              {writing && writing !== 'new' ? 'Sửa tin đang chọn' : 'Form duyệt'}
            </Typography.Title>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
              Sửa SĐT, tên, lương rồi <strong>Duyệt &amp; đăng</strong>. Việc / phòng cần số điện thoại.
            </Typography.Paragraph>
            {formHint ? (
              <Alert type="warning" showIcon style={{ marginBottom: 12 }} message={formHint} />
            ) : null}
            <Space wrap style={{ marginBottom: 12 }}>
              <Button htmlType="button" loading={saving} onClick={() => void persistWrite(false)}>
                Lưu chờ duyệt
              </Button>
              <Button htmlType="button" type="primary" loading={saving} onClick={() => void persistWrite(true)}>
                Duyệt &amp; đăng
              </Button>
              <Button
                onClick={() => {
                  setWriting(null);
                  editForm.resetFields();
                  editForm.setFieldsValue({ kind: form.getFieldValue('kind') || 'job' });
                }}
              >
                Xóa form
              </Button>
            </Space>
            <Form form={editForm} layout="vertical" initialValues={{ kind: 'job' }}>
              <Form.Item name="kind" label="Loại" rules={[{ required: true }]}>
                <Select options={KIND_OPTIONS} />
              </Form.Item>
              <Form.Item name="title" label="Tiêu đề" rules={[{ required: true, message: 'Nhập tiêu đề' }]}>
                <Input />
              </Form.Item>
              <Form.Item name="contactPhone" label="Số điện thoại">
                <Input placeholder="09xxxxxxxx" />
              </Form.Item>
              <Form.Item name="placeText" label="Địa điểm">
                <Input placeholder="Phường / gần trường / quán" />
              </Form.Item>
              <Form.Item name="organizationName" label="Quán / công ty">
                <Input placeholder="Vert, TokyoLife, Sen Hồ…" />
              </Form.Item>
              <Form.Item name="contactName" label="Người liên hệ">
                <Input placeholder="chị Hoa, anh Minh…" />
              </Form.Item>
              <Form.Item name="employmentType" label="Hình thức">
                <Select allowClear options={EMPLOY_OPTIONS} placeholder="Bán thời gian / toàn thời gian" />
              </Form.Item>
              <Form.Item name="workingTime" label="Thời gian (việc / sự kiện)">
                <Input placeholder="17h–22h, T2–CN" />
              </Form.Item>
              <Form.Item name="salaryText" label="Thu nhập (việc — phòng để trống)">
                <Input placeholder="22.000đ/giờ hoặc 7–10 triệu/tháng" />
              </Form.Item>
              <Form.Item name="summary" label="Nội dung">
                <Input.TextArea rows={6} />
              </Form.Item>
              <Form.Item name="requirements" label="Yêu cầu" style={{ marginBottom: 0 }}>
                <Input.TextArea rows={2} placeholder="Tuổi, kinh nghiệm, giới tính…" />
              </Form.Item>
            </Form>
          </div>
        </Col>
      </Row>

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
                if (r.ok && !r.skipped) message.success(r.message || `Đã lên trang chủ (${r.listingCount} tin).`);
                else if (r.skipped) message.warning(r.message || 'Máy local không đẩy trang chủ mạng.');
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
