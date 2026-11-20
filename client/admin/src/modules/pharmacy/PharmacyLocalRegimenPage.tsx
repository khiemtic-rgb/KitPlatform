import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, Button, Checkbox, Drawer, Form, Input, Result, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchProducts } from '@/shared/api/catalog.api';
import {
  createLocalRegimen,
  getLocalRegimen,
  listLocalRegimens,
  transitionLocalRegimen,
  updateLocalRegimen,
  type LocalRegimenDetail,
  type LocalRegimenItem,
  type LocalRegimenSummary,
  type LocalRegimenWrite,
} from '@/shared/api/pharmacy-local-regimen.api';
import { REVIEW_OVERDUE_LABEL } from '@/shared/api/pharmacy-owned-knowledge.api';
import { useIsAdmin } from '@/shared/auth/usePermission';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import { allowsPharmacyKnowledgeVertical } from '@/modules/pharmacy/knowledge-experience';

function statusLabel(status: string) {
  if (status === 'DRAFT') return 'Nháp';
  if (status === 'IN_REVIEW') return 'Chờ duyệt';
  if (status === 'APPROVED') return 'Đã duyệt';
  if (status === 'ACTIVE') return 'Đang dùng';
  if (status === 'STOPPED') return 'Đã ngừng';
  return status;
}

const emptyItem = (): LocalRegimenItem => ({ ingredient: '', matchStatus: 'UNMATCHED' });

type ProductOption = { value: string; label: string };

function productOptionLabel(code: string, name: string) {
  return code ? `${code} — ${name}` : name;
}

function RegimenProductPicker({
  value,
  label,
  disabled,
  onPick,
  onClear,
}: {
  value?: string | null;
  label?: string;
  disabled?: boolean;
  onPick: (product: ProductOption) => void;
  onClear: () => void;
}) {
  const [text, setText] = useState('');
  const [items, setItems] = useState<ProductOption[]>([]);
  const [loading, setLoading] = useState(false);
  const seq = useRef(0);

  async function onType(next: string) {
    setText(next);
    const current = ++seq.current;
    if (!next.trim()) {
      setItems([]);
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const page = await fetchProducts({ search: next.trim(), page: 1, pageSize: 8 });
      if (current !== seq.current) return;
      setItems(page.items.map((item) => ({
        value: item.id,
        label: productOptionLabel(item.productCode, item.productName),
      })));
    } catch {
      if (current !== seq.current) return;
      setItems([]);
      message.error('Không tìm được sản phẩm');
    } finally {
      if (current === seq.current) setLoading(false);
    }
  }

  if (value) {
    return (
      <Space>
        <Tag color="green">{label || 'Đã gắn sản phẩm'}</Tag>
        {disabled ? null : <Button type="link" onClick={onClear}>Bỏ sản phẩm</Button>}
      </Space>
    );
  }

  if (disabled) return <Typography.Text type="secondary">Chưa gắn sản phẩm</Typography.Text>;

  return (
    <div style={{ width: '100%' }}>
      <Input
        value={text}
        allowClear
        placeholder="Gõ mã hoặc tên thuốc đang bán"
        onChange={(event) => { void onType(event.target.value); }}
      />
      {loading ? <Typography.Text type="secondary">Đang tìm...</Typography.Text> : null}
      {!loading && text.trim() && items.length === 0 ? (
        <Typography.Text type="secondary">Không thấy sản phẩm này trong danh mục.</Typography.Text>
      ) : null}
      {items.map((item) => (
        <Button
          key={item.value}
          block
          style={{ textAlign: 'left', marginTop: 4 }}
          onClick={() => {
            onPick(item);
            setText('');
            setItems([]);
          }}
        >
          {item.label}
        </Button>
      ))}
    </div>
  );
}

export function PharmacyLocalRegimenPage() {
  const platformLoaded = useTenantPlatformStore((s) => s.loaded);
  const vertical = useTenantPlatformStore((s) => s.settings?.vertical);
  const isAdmin = useIsAdmin();
  const allowed = platformLoaded && allowsPharmacyKnowledgeVertical(vertical) && isAdmin;
  const [rows, setRows] = useState<LocalRegimenSummary[]>([]);
  const [open, setOpen] = useState<LocalRegimenDetail | null>(null);
  const [creating, setCreating] = useState(false);
  const [productLabels, setProductLabels] = useState<Record<number, string>>({});
  const [overdueOnly, setOverdueOnly] = useState(false);
  const [reviewDueOn, setReviewDueOn] = useState('');
  const visibleRows = overdueOnly ? rows.filter((row) => row.reviewOverdue) : rows;
  const [form] = Form.useForm<LocalRegimenWrite>();
  const watchedItems = Form.useWatch('items', form);

  const load = useCallback(async () => {
    try {
      setRows(await listLocalRegimens());
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được kho đơn tham khảo'));
    }
  }, []);

  useEffect(() => {
    if (allowed) void load();
  }, [allowed, load]);

  function seedProductLabels(detail: LocalRegimenDetail) {
    const next: Record<number, string> = {};
    detail.items.forEach((item, index) => {
      if (!item.productId) return;
      next[index] = productOptionLabel('', item.catalogProductName || item.productLabel || item.productId);
    });
    setProductLabels(next);
  }

  function shiftProductLabels(index: number) {
    setProductLabels((current) => {
      const next: Record<number, string> = {};
      Object.entries(current).forEach(([key, label]) => {
        const line = Number(key);
        if (line < index) next[line] = label;
        if (line > index) next[line - 1] = label;
      });
      return next;
    });
  }

  function toWrite(values: LocalRegimenWrite): LocalRegimenItem[] {
    return (values.items ?? []).flatMap((item): LocalRegimenItem[] => {
      const productId = item.productId || null;
      const ingredient = item.ingredient?.trim() ?? '';
      const productLabel = item.productLabel?.trim() || null;
      if (productId) {
        return [{
          ...item,
          productId,
          productLabel: null,
          matchStatus: 'MATCHED',
          ingredient: ingredient || 'Theo sản phẩm đã gắn',
        }];
      }
      if (!ingredient && !productLabel) return [];
      return [{
        ...item,
        productId: null,
        productLabel,
        matchStatus: 'UNMATCHED',
        ingredient: ingredient || productLabel || 'Chưa gắn sản phẩm',
      }];
    });
  }

  async function save(values: LocalRegimenWrite) {
    const items = toWrite(values);
    if (items.length === 0) {
      message.error('Gõ tên thuốc và bấm đúng dòng trong danh sách bên dưới.');
      return;
    }
    try {
      const detail = creating
        ? await createLocalRegimen({ ...values, items })
        : await updateLocalRegimen(open!.id, { ...values, items });
      setOpen(detail);
      setCreating(false);
      message.success('Đã lưu bản nháp');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được mẫu'));
    }
  }

  async function act(action: 'submit' | 'return' | 'approve' | 'activate' | 'stop' | 'next-draft') {
    if (!open) return;
    const note = action === 'stop' || action === 'activate' ? window.prompt('Ghi chú') ?? undefined : undefined;
    try {
      const due = action === 'approve' || action === 'activate' ? reviewDueOn || null : null;
      const detail = await transitionLocalRegimen(open.id, action, open.versionId, open.updatedAt, note, due);
      setOpen(detail);
      setReviewDueOn('');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không thực hiện được'));
    }
  }

  const columns: ColumnsType<LocalRegimenSummary> = [
    { title: 'Tiêu đề', dataIndex: 'title' },
    { title: 'Nhóm vấn đề', dataIndex: 'conditionGroup' },
    { title: 'Phiên bản', dataIndex: 'version', width: 100 },
    {
      title: 'Trạng thái',
      dataIndex: 'lifecycleStatus',
      render: (value: string, row) => (
        <Space size={4} wrap>
          <Tag>{statusLabel(value)}</Tag>
          {row.reviewOverdue ? <Tag color="orange">{REVIEW_OVERDUE_LABEL}</Tag> : null}
        </Space>
      ),
    },
    { title: 'Áp dụng', dataIndex: 'activationStatus', render: (value?: string) => value ? statusLabel(value) : '—' },
    {
      title: '',
      width: 80,
      render: (_value, row) => (
        <Button type="link" onClick={() => { void getLocalRegimen(row.id).then((detail) => { setCreating(false); setOpen(detail); seedProductLabels(detail); form.setFieldsValue(detail); }); }}>
          Xem
        </Button>
      ),
    },
  ];

  if (!platformLoaded) return null;
  if (!allowed) {
    return <Result status="403" title="Chỉ quản trị nhà thuốc quản lý kho đơn tham khảo" />;
  }

  const draft = open?.lifecycleStatus === 'DRAFT';

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>Kho đơn tham khảo</Typography.Title>
        <Typography.Text type="secondary">
          Soạn đơn thuốc phổ biến của nhà thuốc và gắn sản phẩm đang bán. Nhân viên mới nhập triệu chứng ở quầy để xem đơn mẫu, rồi tự thêm đúng sản phẩm đã gắn vào đơn bán.
        </Typography.Text>
      </div>
      <Button
        type="primary"
        onClick={() => {
          setCreating(true);
          setOpen(null);
          setProductLabels({});
          form.resetFields();
          form.setFieldsValue({ items: [emptyItem()] });
        }}
      >
        Tạo mẫu
      </Button>
      <Checkbox checked={overdueOnly} onChange={(event) => setOverdueOnly(event.target.checked)}>
        Chỉ {REVIEW_OVERDUE_LABEL.toLowerCase()}
      </Checkbox>
      <Table rowKey="id" columns={columns} dataSource={visibleRows} pagination={false} />
      <Drawer
        width={640}
        open={creating || open !== null}
        title={creating ? 'Mẫu nháp mới' : open?.title}
        onClose={() => { setCreating(false); setOpen(null); }}
      >
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          message="Gõ tên thuốc đang bán, rồi bấm đúng dòng trong danh sách hiện ngay bên dưới. Dòng chưa gắn sản phẩm không dùng để bán. Quầy không tự thêm thuốc vào giỏ."
        />
        <Form form={form} layout="vertical" onFinish={(values) => void save(values)} disabled={!!open && !draft}>
          <Form.Item name="title" label="Tiêu đề" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="conditionGroup" label="Nhóm bệnh / vấn đề" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="audience" label="Đối tượng" rules={[{ required: true }]}><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="symptoms" label="Triệu chứng" rules={[{ required: true }]}><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="applicability" label="Điều kiện áp dụng" rules={[{ required: true }]}><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="limits" label="Giới hạn" rules={[{ required: true }]}><Input.TextArea rows={2} /></Form.Item>
          <Form.List name="items">
            {(fields, { add, remove }) => (
              <Space direction="vertical" style={{ width: '100%' }}>
                {fields.map((field) => {
                  const productId = watchedItems?.[field.name]?.productId;
                  return (
                    <div key={field.key} style={{ width: '100%', border: '1px solid #f0f0f0', borderRadius: 8, padding: 12 }}>
                      <Typography.Text strong>Thuốc {field.name + 1}</Typography.Text>
                      <div style={{ margin: '8px 0' }}>
                        <RegimenProductPicker
                          value={productId}
                          label={productLabels[field.name]}
                          disabled={!!open && !draft}
                          onPick={(product) => {
                            form.setFieldValue(['items', field.name, 'productId'], product.value);
                            form.setFieldValue(['items', field.name, 'matchStatus'], 'MATCHED');
                            form.setFieldValue(['items', field.name, 'productLabel'], undefined);
                            setProductLabels((current) => ({ ...current, [field.name]: product.label }));
                          }}
                          onClear={() => {
                            form.setFieldValue(['items', field.name, 'productId'], undefined);
                            form.setFieldValue(['items', field.name, 'matchStatus'], 'UNMATCHED');
                            setProductLabels((current) => {
                              const next = { ...current };
                              delete next[field.name];
                              return next;
                            });
                          }}
                        />
                      </div>
                      <Form.Item name={[field.name, 'productId']} hidden><Input /></Form.Item>
                      <Form.Item name={[field.name, 'matchStatus']} hidden initialValue="UNMATCHED"><Input /></Form.Item>
                      <Space wrap>
                        <Form.Item name={[field.name, 'ingredient']}><Input placeholder="Hoạt chất, nếu dược sĩ ghi" /></Form.Item>
                        <Form.Item name={[field.name, 'strength']}><Input placeholder="Hàm lượng" /></Form.Item>
                        <Form.Item name={[field.name, 'dosageForm']}><Input placeholder="Dạng bào chế" /></Form.Item>
                        {productId ? null : (
                          <Form.Item name={[field.name, 'productLabel']}><Input placeholder="Tên khi chưa có sản phẩm" /></Form.Item>
                        )}
                        <Button onClick={() => { remove(field.name); shiftProductLabels(field.name); }}>Xóa dòng</Button>
                      </Space>
                    </div>
                  );
                })}
                <Button onClick={() => add(emptyItem())}>Thêm dòng thuốc</Button>
              </Space>
            )}
          </Form.List>
          {(!open || draft) ? <Button type="primary" htmlType="submit">Lưu nháp</Button> : null}
        </Form>
        {open?.reviewOverdue ? (
          <Tag color="orange" style={{ marginTop: 16 }}>{REVIEW_OVERDUE_LABEL}{open.reviewDueOn ? ` · ${open.reviewDueOn.slice(0, 10)}` : ''}</Tag>
        ) : null}
        {open && (open.lifecycleStatus === 'IN_REVIEW' || (open.lifecycleStatus === 'APPROVED' && open.activationStatus !== 'ACTIVE')) ? (
          <div style={{ marginTop: 16 }}>
            <Typography.Text type="secondary">Hạn rà soát khi duyệt/áp dụng (để trống = 12 tháng tới). Người soạn hoặc gửi duyệt không tự duyệt được.</Typography.Text>
            <Input type="date" value={reviewDueOn} onChange={(event) => setReviewDueOn(event.target.value)} style={{ maxWidth: 200, display: 'block' }} />
          </div>
        ) : null}
        {open ? (
          <Space style={{ marginTop: 16 }} wrap>
            {draft ? <Button onClick={() => void act('submit')}>Gửi duyệt</Button> : null}
            {open.lifecycleStatus === 'IN_REVIEW' ? <Button onClick={() => void act('return')}>Trả lại</Button> : null}
            {open.lifecycleStatus === 'IN_REVIEW' ? <Button type="primary" onClick={() => void act('approve')}>Phê duyệt</Button> : null}
            {open.lifecycleStatus === 'APPROVED' && open.activationStatus !== 'ACTIVE' ? <Button type="primary" onClick={() => void act('activate')}>Áp dụng</Button> : null}
            {open.activationStatus === 'ACTIVE' ? <Button danger onClick={() => void act('stop')}>Ngừng áp dụng</Button> : null}
            {open.lifecycleStatus === 'APPROVED' ? <Button onClick={() => void act('next-draft')}>Tạo bản nháp mới</Button> : null}
          </Space>
        ) : null}
        {open ? (
          <div style={{ marginTop: 16 }}>
            <Typography.Text strong>Lịch sử</Typography.Text>
            {open.events.map((event) => (
              <div key={`${event.action}-${event.createdAt}`}>
                <Typography.Text type="secondary">
                  Phiên bản {event.version} · {event.action} · {new Date(event.createdAt).toLocaleString('vi-VN')}
                  {event.note ? ` · ${event.note}` : ''}
                </Typography.Text>
              </div>
            ))}
          </div>
        ) : null}
      </Drawer>
    </Space>
  );
}
