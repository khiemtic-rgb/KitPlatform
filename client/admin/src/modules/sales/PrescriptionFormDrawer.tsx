import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  App,
  AutoComplete,
  Button,
  DatePicker,
  Drawer,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Table,
  Tag,
  Upload,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined, PlusOutlined, UploadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import { lookupPosProduct, searchCustomers, searchPosProducts } from '@/shared/api/sales.api';
import type { CustomerListItem } from '@/shared/api/sales.types';
import {
  addPrescriptionAttachment,
  createPrescription,
  fetchPrescribers,
  fetchPrescription,
  submitPrescription,
  type RxPrescriber,
  type RxPrescriptionDetail,
  type UpsertPrescriptionInput,
  updatePrescription,
  uploadPrescriptionFile,
  verifyPrescription,
} from '@/shared/api/rx.api';
import { useHasPermission } from '@/shared/auth/usePermission';

type PrescriptionLineForm = {
  key: string;
  productId: string;
  productUnitId?: string;
  productCode: string;
  productName: string;
  unitName?: string;
  qtyPrescribed: number;
  dosageInstruction?: string;
  lineDispensingClass?: string;
};

type FormValues = {
  linkedPrescriberId: string;
  patientName?: string;
  patientPhone?: string;
  notes?: string;
};

type Props = {
  open: boolean;
  prescriptionId?: string;
  readOnly?: boolean;
  onClose: () => void;
  onSaved: () => void;
};

const STATUS_LABELS: Record<string, string> = {
  draft: 'Nháp',
  pending_verification: 'Chờ xác minh',
  verified: 'Đã xác minh',
  signed: 'Đã ký',
  partially_dispensed: 'Đã bán một phần',
  dispensed: 'Đã bán hết',
  expired: 'Hết hạn',
  cancelled: 'Đã hủy',
};

export function PrescriptionFormDrawer({
  open,
  prescriptionId,
  readOnly = false,
  onClose,
  onSaved,
}: Props) {
  const { message } = App.useApp();
  const canWrite = useHasPermission('rx.prescription.create') || useHasPermission('sales.write');
  const canVerify = useHasPermission('rx.prescription.verify');
  const [form] = Form.useForm<FormValues>();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [verifySaving, setVerifySaving] = useState(false);
  const [prescribers, setPrescribers] = useState<RxPrescriber[]>([]);
  const [lines, setLines] = useState<PrescriptionLineForm[]>([]);
  const [detail, setDetail] = useState<RxPrescriptionDetail | null>(null);
  const [warehouseId, setWarehouseId] = useState<string>();
  const [searchText, setSearchText] = useState('');
  const [searchOptions, setSearchOptions] = useState<{ value: string; label: string }[]>([]);
  const [verifyMethod, setVerifyMethod] = useState('manual_check');
  const [signedAt, setSignedAt] = useState<string | undefined>(undefined);
  const [customerId, setCustomerId] = useState<string>();
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [customerSearch, setCustomerSearch] = useState('');

  const effectiveReadOnly = readOnly || !canWrite;
  const allowSubmit = !effectiveReadOnly && detail?.status === 'draft';
  const allowVerify = canVerify && detail?.status === 'pending_verification';

  const loadDetail = useCallback(
    async (id: string) => {
      const item = await fetchPrescription(id);
      setDetail(item);
      setCustomerId(item.customerId);
      if (item.customerId && item.patientPhone) {
        void searchCustomers(item.patientPhone).then((rows) => setCustomers(rows.slice(0, 30)));
      }
      form.setFieldsValue({
        linkedPrescriberId: item.linkedPrescriberId,
        patientName: item.patientName,
        patientPhone: item.patientPhone,
        notes: item.notes,
      });
      setLines(
        item.lines.map((line) => ({
          key: line.id,
          productId: line.productId,
          productUnitId: line.productUnitId,
          productCode: line.productCode,
          productName: line.productName,
          unitName: line.unitName,
          qtyPrescribed: line.qtyPrescribed,
          dosageInstruction: line.dosageInstruction,
          lineDispensingClass: line.lineDispensingClass,
        })),
      );
      setVerifyMethod(item.verificationMethod || 'manual_check');
      setSignedAt(item.signedAt);
    },
    [form],
  );

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    void (async () => {
      try {
        const [prescriberRows, warehouses] = await Promise.all([
          fetchPrescribers(undefined, true),
          fetchWarehouses(),
        ]);
        if (cancelled) return;
        setPrescribers(prescriberRows.filter((row) => row.status === 1));
        setWarehouseId(warehouses[0]?.id);
        if (prescriptionId) {
          await loadDetail(prescriptionId);
        } else {
          setDetail(null);
          setLines([]);
          setCustomerId(undefined);
          setCustomers([]);
          setCustomerSearch('');
          form.resetFields();
          setSignedAt(undefined);
          setVerifyMethod('manual_check');
        }
      } catch (error) {
        if (!cancelled) {
          message.error(apiErrorMessage(error, 'Không tải được dữ liệu đơn thuốc'));
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [open, prescriptionId, loadDetail, form, message]);

  useEffect(() => {
    if (!open || !warehouseId) {
      setSearchOptions([]);
      return;
    }
    const q = searchText.trim();
    if (q.length < 2) {
      setSearchOptions([]);
      return;
    }
    let cancelled = false;
    const timer = window.setTimeout(() => {
      void searchPosProducts(q, warehouseId)
        .then((items) => {
          if (cancelled) return;
          setSearchOptions(
            items.slice(0, 20).map((item) => ({
              value: item.lookupCode,
              label: `${item.productCode} - ${item.productName} (tồn ${item.stockAvailable})`,
            })),
          );
        })
        .catch(() => {
          if (!cancelled) setSearchOptions([]);
        });
    }, 250);
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [open, searchText, warehouseId]);

  useEffect(() => {
    if (!open) return;
    const q = customerSearch.trim();
    let cancelled = false;
    const timer = window.setTimeout(() => {
      void searchCustomers(q || undefined)
        .then((items) => {
          if (!cancelled) setCustomers(items.slice(0, 30));
        })
        .catch(() => {
          if (!cancelled) setCustomers([]);
        });
    }, q ? 250 : 0);
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [open, customerSearch]);

  const handleCustomerChange = (nextCustomerId?: string) => {
    setCustomerId(nextCustomerId);
    if (!nextCustomerId) return;
    const customer = customers.find((row) => row.id === nextCustomerId);
    if (!customer) return;
    form.setFieldsValue({
      patientName: customer.fullName,
      patientPhone: customer.phone,
    });
  };

  const addProduct = async (lookupCode: string) => {
    if (!warehouseId) {
      message.warning('Chưa có kho để tra cứu sản phẩm');
      return;
    }
    try {
      const lookup = await lookupPosProduct(lookupCode, warehouseId);
      setLines((prev) => {
        const existing = prev.find((line) => line.productUnitId === lookup.productUnitId);
        if (existing) {
          return prev.map((line) =>
            line.productUnitId === lookup.productUnitId
              ? { ...line, qtyPrescribed: line.qtyPrescribed + 1 }
              : line,
          );
        }
        return [
          ...prev,
          {
            key: `${lookup.productUnitId}-${Date.now()}`,
            productId: lookup.productId,
            productUnitId: lookup.productUnitId,
            productCode: lookup.productCode,
            productName: lookup.productName,
            unitName: lookup.unitName,
            qtyPrescribed: 1,
            lineDispensingClass: lookup.dispensingClass ?? 'otc',
          },
        ];
      });
      setSearchText('');
      setSearchOptions([]);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không thêm được sản phẩm vào đơn'));
    }
  };

  const toPayload = (values: FormValues): UpsertPrescriptionInput => ({
    linkedPrescriberId: values.linkedPrescriberId,
    customerId: customerId || undefined,
    patientName: values.patientName,
    patientPhone: values.patientPhone,
    notes: values.notes,
    source: 'staff_entry',
    lines: lines.map((line, index) => ({
      productId: line.productId,
      productUnitId: line.productUnitId,
      qtyPrescribed: line.qtyPrescribed,
      dosageInstruction: line.dosageInstruction,
      sortOrder: index,
    })),
  });

  const save = async () => {
    const values = await form.validateFields();
    if (lines.length === 0) {
      message.warning('Thêm ít nhất 1 dòng thuốc');
      return;
    }
    setSaving(true);
    try {
      if (prescriptionId) {
        await updatePrescription(prescriptionId, toPayload(values));
        message.success('Đã cập nhật đơn thuốc');
        await loadDetail(prescriptionId);
      } else {
        await createPrescription(toPayload(values));
        message.success('Đã tạo đơn thuốc');
        onSaved();
      }
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được đơn thuốc'));
    } finally {
      setSaving(false);
    }
  };

  const submitForVerification = async () => {
    if (!prescriptionId) return;
    setSubmitting(true);
    try {
      await submitPrescription(prescriptionId);
      message.success('Đã gửi đơn chờ xác minh');
      await loadDetail(prescriptionId);
      onSaved();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không gửi được đơn xác minh'));
    } finally {
      setSubmitting(false);
    }
  };

  const doVerify = async () => {
    if (!prescriptionId) return;
    setVerifySaving(true);
    try {
      await verifyPrescription(prescriptionId, {
        verificationMethod: verifyMethod,
        signedAt: signedAt || undefined,
      });
      message.success('Đã xác minh đơn thuốc');
      await loadDetail(prescriptionId);
      onSaved();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không xác minh được đơn'));
    } finally {
      setVerifySaving(false);
    }
  };

  const lineColumns: ColumnsType<PrescriptionLineForm> = useMemo(
    () => [
      {
        title: 'Sản phẩm',
        key: 'product',
        render: (_, row) => (
          <div>
            <div>{row.productName}</div>
            <small>
              {row.productCode}
              {row.lineDispensingClass ? ` · ${row.lineDispensingClass}` : ''}
            </small>
          </div>
        ),
      },
      {
        title: 'ĐVT',
        dataIndex: 'unitName',
        width: 80,
        render: (value?: string) => value || '—',
      },
      {
        title: 'SL kê',
        width: 100,
        render: (_, row) => (
          <InputNumber
            min={0.01}
            value={row.qtyPrescribed}
            disabled={effectiveReadOnly}
            onChange={(value) =>
              setLines((prev) =>
                prev.map((line) =>
                  line.key === row.key ? { ...line, qtyPrescribed: Number(value ?? 0) } : line,
                ),
              )
            }
          />
        ),
      },
      {
        title: 'Liều dùng',
        width: 220,
        render: (_, row) => (
          <Input
            value={row.dosageInstruction}
            disabled={effectiveReadOnly}
            onChange={(event) =>
              setLines((prev) =>
                prev.map((line) =>
                  line.key === row.key ? { ...line, dosageInstruction: event.target.value } : line,
                ),
              )
            }
          />
        ),
      },
      {
        title: '',
        width: 44,
        render: (_, row) =>
          effectiveReadOnly ? null : (
            <Button
              type="text"
              danger
              icon={<DeleteOutlined />}
              onClick={() => setLines((prev) => prev.filter((line) => line.key !== row.key))}
            />
          ),
      },
    ],
    [effectiveReadOnly],
  );

  return (
    <Drawer
      width={980}
      open={open}
      onClose={onClose}
      title={detail ? `Đơn thuốc ${detail.prescriptionCode}` : 'Tạo đơn thuốc'}
      extra={
        <Space>
          {!effectiveReadOnly ? (
            <Button loading={saving} type="primary" onClick={() => void save()}>
              {prescriptionId ? 'Lưu cập nhật' : 'Tạo đơn'}
            </Button>
          ) : null}
          {allowSubmit ? (
            <Button loading={submitting} onClick={() => void submitForVerification()}>
              Gửi xác minh
            </Button>
          ) : null}
        </Space>
      }
    >
      <Form form={form} layout="vertical" disabled={loading || effectiveReadOnly}>
        {detail ? (
          <Space style={{ marginBottom: 12 }} wrap>
            <Tag color="blue">{STATUS_LABELS[detail.status] || detail.status}</Tag>
            <span>Bác sĩ: {detail.prescriberName}</span>
            <span>Bệnh nhân: {detail.patientName || '—'}</span>
          </Space>
        ) : null}

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <Form.Item
            label="Bác sĩ kê đơn"
            name="linkedPrescriberId"
            rules={[{ required: true, message: 'Chọn bác sĩ kê đơn' }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              options={prescribers.map((row) => ({
                value: row.id,
                label: `${row.fullName}${row.licenseNumber ? ` - ${row.licenseNumber}` : ''}`,
              }))}
            />
          </Form.Item>
          <Form.Item label="Khách hàng (bệnh nhân)" tooltip="Chọn từ CRM để tránh trùng hồ sơ. Có thể sửa tên/SĐT bên dưới nếu cần.">
            <Select
              allowClear
              showSearch
              filterOption={false}
              placeholder="Tìm theo mã, tên hoặc SĐT"
              value={customerId}
              onSearch={setCustomerSearch}
              onChange={handleCustomerChange}
              options={customers.map((row) => ({
                value: row.id,
                label: `${row.customerCode} — ${row.fullName}${row.phone ? ` (${row.phone})` : ''}`,
              }))}
            />
          </Form.Item>
          <Form.Item label="SĐT bệnh nhân" name="patientPhone">
            <Input placeholder="09xxxxxxxx" />
          </Form.Item>
          <Form.Item label="Tên bệnh nhân" name="patientName">
            <Input placeholder="Nguyễn Văn B" />
          </Form.Item>
          <Form.Item label="Ghi chú" name="notes" style={{ gridColumn: '1 / -1' }}>
            <Input placeholder="Ghi chú kê đơn" />
          </Form.Item>
        </div>
      </Form>

      {!effectiveReadOnly ? (
        <Space.Compact style={{ width: '100%', marginBottom: 12 }}>
          <AutoComplete
            style={{ width: '100%' }}
            value={searchText}
            options={searchOptions}
            placeholder="Tìm sản phẩm để thêm dòng đơn"
            onChange={setSearchText}
            onSelect={(value) => void addProduct(String(value))}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                void addProduct(searchText);
              }
            }}
          />
          <Button icon={<PlusOutlined />} onClick={() => void addProduct(searchText)}>
            Thêm
          </Button>
        </Space.Compact>
      ) : null}

      <Table
        rowKey="key"
        size="small"
        pagination={false}
        columns={lineColumns}
        dataSource={lines}
        locale={{ emptyText: 'Chưa có dòng thuốc' }}
      />

      {detail ? (
        <>
          <div style={{ marginTop: 18, marginBottom: 8, fontWeight: 600 }}>Tài liệu đính kèm</div>
          <Space direction="vertical" style={{ width: '100%' }} size={8}>
            {detail.attachments.map((item) => (
              <a key={item.id} href={item.fileUrl} target="_blank" rel="noreferrer">
                {item.fileName || item.fileUrl}
              </a>
            ))}
            {!effectiveReadOnly ? (
              <Upload
                showUploadList={false}
                customRequest={({ file, onSuccess, onError }) => {
                  void (async () => {
                    try {
                      const f = file as File;
                      const url = await uploadPrescriptionFile(f);
                      await addPrescriptionAttachment(detail.id, {
                        fileUrl: url,
                        fileName: f.name,
                      });
                      await loadDetail(detail.id);
                      onSuccess?.({});
                      message.success('Đã tải tệp đính kèm');
                    } catch (error) {
                      onError?.(error as Error);
                      message.error(apiErrorMessage(error, 'Không tải được tệp đính kèm'));
                    }
                  })();
                }}
              >
                <Button icon={<UploadOutlined />}>Tải ảnh/PDF đơn thuốc</Button>
              </Upload>
            ) : null}
          </Space>
        </>
      ) : null}

      {allowVerify ? (
        <div style={{ marginTop: 18 }}>
          <div style={{ marginBottom: 8, fontWeight: 600 }}>Xác minh đơn</div>
          <Space wrap align="end">
            <div>
              <div style={{ marginBottom: 4 }}>Phương thức</div>
              <Select
                value={verifyMethod}
                style={{ width: 220 }}
                options={[
                  { value: 'manual_check', label: 'manual_check' },
                  { value: 'doctor_call', label: 'doctor_call' },
                  { value: 'e_rx', label: 'e_rx' },
                ]}
                onChange={setVerifyMethod}
              />
            </div>
            <div>
              <div style={{ marginBottom: 4 }}>Ngày ký (tuỳ chọn)</div>
              <DatePicker
                showTime
                format="DD/MM/YYYY HH:mm"
                value={signedAt ? dayjs(signedAt) : null}
                onChange={(value) => setSignedAt(value ? value.toISOString() : undefined)}
              />
            </div>
            <Button type="primary" loading={verifySaving} onClick={() => void doVerify()}>
              Xác minh
            </Button>
          </Space>
        </div>
      ) : null}
    </Drawer>
  );
}
