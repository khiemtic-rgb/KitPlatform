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
import { ReloadOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  createLocalOsListing,
  fetchLocalOsListings,
  fetchLocalOsSources,
  ingestLocalOsSource,
  setLocalOsListingStatus,
  updateLocalOsListing,
  type LocalListing,
  type LocalSource,
} from '@/shared/api/local-os.api';
import { rewriteListingCopy } from './rewriteListingCopy';

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
  const [loading, setLoading] = useState(false);
  const [ingesting, setIngesting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState<string>('NEEDS_REVIEW');
  const [kind, setKind] = useState<string | undefined>();
  const [writing, setWriting] = useState<LocalListing | 'new' | null>(null);
  const [sources, setSources] = useState<LocalSource[]>([]);
  const [added, setAdded] = useState(0);
  const [lastError, setLastError] = useState<string | null>(null);
  const [form] = Form.useForm();
  const [editForm] = Form.useForm();
  const pasteRef = useRef<{ focus: () => void } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchLocalOsListings({ status: status || undefined, kind }));
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

  const polishBox = () => {
    const raw = String(form.getFieldValue('pastedText') ?? '');
    if (raw.trim().length < 8) {
      message.warning('Dán nội dung bài trước, rồi bấm Viết lại.');
      return;
    }
    const kind = String(form.getFieldValue('kind') || 'job');
    form.setFieldsValue({ pastedText: rewriteListingCopy(raw, kind).body });
    message.success('Đã viết lại cho chuẩn. Đọc lại rồi thêm vào danh sách.');
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

  const polishEdit = () => {
    const kind = String(editForm.getFieldValue('kind') || 'job');
    const raw = [editForm.getFieldValue('title'), editForm.getFieldValue('summary')].filter(Boolean).join('\n');
    if (String(raw).trim().length < 8) return;
    const w = rewriteListingCopy(String(raw), kind);
    editForm.setFieldsValue({
      title: w.title,
      summary: w.body,
      placeText: w.place || editForm.getFieldValue('placeText'),
      contactPhone: w.phone || editForm.getFieldValue('contactPhone'),
      salaryText: kind === 'room' ? undefined : w.salary || editForm.getFieldValue('salaryText'),
    });
    message.success('Đã viết lại nội dung.');
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
        message.success('Đã đăng trong danh sách. Trang chủ mạng chỉ cập nhật sau khi deploy.');
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
        message.success(
          next === 'ACTIVE'
            ? 'Đã đăng trong danh sách. Trang chủ mạng chỉ cập nhật sau khi deploy.'
            : 'Đã cập nhật.',
        );
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
        Site chỉ hiện tin đã đăng.{' '}
        <Link to="/local-os/sources">Sổ nguồn</Link>
        {' · '}
        <Link to="/local-os/stats">Thống kê</Link>
      </Typography.Paragraph>

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
          extra="Dán bài thô → Viết lại cho chuẩn → Thêm. Ctrl + Enter để thêm rồi dán tiếp."
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
          <Button htmlType="button" onClick={polishBox}>
            Viết lại cho chuẩn
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
          Viết lại cho rõ. Bấm <strong>Duyệt &amp; đăng</strong> khi đủ tiêu đề, địa điểm và số điện thoại.
          Site mới hiện tin.
        </Typography.Paragraph>
        <Button style={{ marginBottom: 12 }} onClick={polishEdit}>
          Viết lại cho chuẩn
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
    </div>
  );
}
