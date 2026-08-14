import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  App,
  Alert,
  Button,
  Drawer,
  Form,
  Input,
  Space,
  Spin,
  Tag,
  Typography,
} from 'antd';
import {
  EditOutlined,
  CopyOutlined,
  DollarOutlined,
  MobileOutlined,
  PhoneOutlined,
  PlusOutlined,
  ReloadOutlined,
  ShoppingCartOutlined,
  UserAddOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import {
  createCustomer,
  fetchCustomerDetail,
  fetchCustomerPilotOtp,
  issueCounterPilotOtp,
  updateCustomerPhone,
} from '@/shared/api/customer.api';
import type { CustomerDetail, CustomerPilotOtpStatus } from '@/shared/api/customer.types';
import { searchCustomers } from '@/shared/api/sales.api';
import type { CustomerListItem } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatMoney } from '@/shared/utils/money';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';
import { CustomerCreditSheet } from '@/modules/customers/CustomerCreditSheet';
import { usePosSession } from '@/modules/pos/pos-session.store';
import { useCanSalesWrite } from '@/shared/auth/usePermission';

const OTP_POLL_MS = 3000;

function digitsOnly(value: string): string {
  return value.replace(/\D/g, '');
}

function hasUsablePhone(phone?: string | null): boolean {
  const d = digitsOnly(phone ?? '');
  return d.length >= 9 && d.length <= 12;
}

function displayPhone(phone?: string | null): string {
  if (!hasUsablePhone(phone)) return 'Chưa có SĐT';
  return (phone ?? '').trim();
}

function pharmacyRelationLabel(relation?: string): string | null {
  switch ((relation ?? '').toLowerCase()) {
    case 'member':
      return 'Thành viên NT';
    case 'prospect':
      return 'Khách tiềm năng';
    case 'revoked':
      return 'Đã thu hồi';
    default:
      return relation ? relation : null;
  }
}

function otpRemainingLabel(expiresAt?: string | null): string | null {
  if (!expiresAt) return null;
  const end = dayjs(expiresAt);
  if (!end.isValid()) return null;
  const sec = end.diff(dayjs(), 'second');
  if (sec <= 0) return 'Đã hết hạn';
  const m = Math.floor(sec / 60);
  const s = sec % 60;
  return m > 0 ? `Còn ${m}:${String(s).padStart(2, '0')}` : `Còn ${s}s`;
}

function PilotOtpBlock({
  customerId,
  phone,
  fullName,
  hasAppAccount,
  onIssued,
}: {
  customerId: string;
  phone: string;
  fullName: string;
  hasAppAccount?: boolean;
  onIssued?: () => void;
}) {
  const { message } = App.useApp();
  const [status, setStatus] = useState<CustomerPilotOtpStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [issuing, setIssuing] = useState(false);
  const [nowTick, setNowTick] = useState(0);

  const load = useCallback(async () => {
    try {
      setStatus(await fetchCustomerPilotOtp(customerId));
    } catch {
      setStatus({ enabled: false, code: null, expiresAt: null, createdAt: null });
    } finally {
      setLoading(false);
    }
  }, [customerId]);

  useEffect(() => {
    setLoading(true);
    void load();
    const timer = window.setInterval(() => void load(), OTP_POLL_MS);
    return () => window.clearInterval(timer);
  }, [load]);

  useEffect(() => {
    if (!status?.code || !status.expiresAt) return;
    const timer = window.setInterval(() => setNowTick((n) => n + 1), 1000);
    return () => window.clearInterval(timer);
  }, [status?.code, status?.expiresAt]);

  const remaining = useMemo(
    () => otpRemainingLabel(status?.expiresAt),
    [status?.expiresAt, nowTick],
  );

  const copyCode = async () => {
    if (!status?.code) return;
    try {
      await navigator.clipboard.writeText(status.code);
      message.success('Đã copy mã OTP');
    } catch {
      message.warning('Không copy được — đọc số trên màn hình');
    }
  };

  const issueAtCounter = async () => {
    if (!hasUsablePhone(phone)) {
      message.warning('Khách chưa có SĐT hợp lệ — cập nhật SĐT trước khi tạo OTP');
      return;
    }
    setIssuing(true);
    try {
      const result = await issueCounterPilotOtp({ phone, fullName });
      message.success(result.message || 'Đã tạo OTP tại quầy');
      await load();
      onIssued?.();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tạo được OTP tại quầy'));
    } finally {
      setIssuing(false);
    }
  };

  if (loading && !status) {
    return <Spin />;
  }

  if (!status?.enabled) {
    return (
      <Alert
        type="info"
        showIcon
        message="OTP pilot đang tắt trên máy chủ"
        description="Vẫn có thể tạo mã tại quầy nếu quyền POS được bật. Liên hệ admin nếu nút tạo OTP báo lỗi."
        action={
          <Button size="small" loading={issuing} onClick={() => void issueAtCounter()}>
            Thử tạo OTP
          </Button>
        }
      />
    );
  }

  if (status.code) {
    const expired = remaining === 'Đã hết hạn';
    return (
      <div className="customer-otp-card">
        <div className="customer-otp-card__head">
          <Typography.Text strong>{expired ? 'OTP đã hết hạn' : 'Mã OTP đang hiệu lực'}</Typography.Text>
          {remaining ? (
            <Typography.Text type={expired ? 'danger' : 'secondary'} style={{ fontSize: 12 }}>
              {remaining}
              {!expired && status.expiresAt ? ` · đến ${dayjs(status.expiresAt).format('HH:mm:ss')}` : ''}
            </Typography.Text>
          ) : null}
        </div>
        <div className="customer-otp-code">{status.code}</div>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          Đọc mã cho khách nhập trên app — không gửi Zalo/SMS ngoài hệ thống.
        </Typography.Text>
        <Space wrap style={{ marginTop: 10 }}>
          <Button icon={<CopyOutlined />} onClick={() => void copyCode()} disabled={expired}>
            Copy mã
          </Button>
          <Button icon={<ReloadOutlined />} loading={issuing} onClick={() => void issueAtCounter()}>
            Tạo mã mới
          </Button>
        </Space>
      </div>
    );
  }

  return (
    <div className="customer-otp-card customer-otp-card--wait">
      {hasAppAccount ? (
        <Alert
          type="success"
          showIcon
          message="Khách đã có tài khoản app"
          description="Chỉ cần OTP khi khách vừa bấm Gửi mã trên app và chưa kịp nhập."
          style={{ marginBottom: 10 }}
        />
      ) : (
        <Alert
          type="warning"
          showIcon
          icon={<MobileOutlined />}
          message="Chưa có OTP đang chờ"
          description="Khách mở app → nhập SĐT → Gửi mã, hoặc tạo mã ngay tại quầy để đọc cho khách."
          style={{ marginBottom: 10 }}
        />
      )}
      <Button
        type="primary"
        block
        size="large"
        icon={<MobileOutlined />}
        loading={issuing}
        disabled={!hasUsablePhone(phone)}
        onClick={() => void issueAtCounter()}
      >
        Tạo OTP tại quầy
      </Button>
      {!hasUsablePhone(phone) ? (
        <Typography.Text type="danger" style={{ display: 'block', marginTop: 8, fontSize: 12 }}>
          Thiếu SĐT hợp lệ — không tạo OTP được.
        </Typography.Text>
      ) : null}
    </div>
  );
}

export function CustomersPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const canWrite = useCanSalesWrite();
  const setCustomer = usePosSession((s) => s.setCustomer);

  const [query, setQuery] = useState('');
  const [hits, setHits] = useState<CustomerListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [selected, setSelected] = useState<CustomerListItem | null>(null);
  const [detail, setDetail] = useState<CustomerDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [creditOpen, setCreditOpen] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [creating, setCreating] = useState(false);
  const [phoneDraft, setPhoneDraft] = useState('');
  const [editingPhone, setEditingPhone] = useState(false);
  const [savingPhone, setSavingPhone] = useState(false);
  const [createForm] = Form.useForm<{ fullName: string; phone: string }>();

  const pageSize = 30;

  const canSearch = useMemo(() => {
    const q = query.trim();
    if (q.length === 0) return true;
    const digits = digitsOnly(q);
    const phoneish = digits.length >= 3 && digits.length >= Math.ceil(q.length * 0.6);
    return q.length >= 2 || phoneish;
  }, [query]);

  const loadList = useCallback(
    async (search: string, nextPage: number, append: boolean) => {
      if (append) setLoadingMore(true);
      else setLoading(true);
      try {
        const result = await searchCustomers(search || undefined, { page: nextPage, pageSize });
        setHits((prev) => (append ? [...prev, ...result.items] : result.items));
        setTotal(result.total);
        setPage(result.page);
        setHasMore(result.hasMore);
      } catch (error) {
        if (!append) setHits([]);
        setHasMore(false);
        message.error(apiErrorMessage(error, 'Không tải được danh sách khách'));
      } finally {
        setLoading(false);
        setLoadingMore(false);
      }
    },
    [message],
  );

  useEffect(() => {
    if (!canSearch) return;
    const q = query.trim();
    const timer = window.setTimeout(() => {
      void loadList(q, 1, false);
    }, q.length === 0 ? 0 : 280);
    return () => window.clearTimeout(timer);
  }, [query, canSearch, loadList]);

  const loadMore = () => {
    if (!hasMore || loadingMore || loading) return;
    void loadList(query.trim(), page + 1, true);
  };

  useEffect(() => {
    if (!selected) {
      setDetail(null);
      return;
    }
    setDetailLoading(true);
    void fetchCustomerDetail(selected.id)
      .then((row) => {
        setDetail({
          ...row,
          // Outstanding / credit from sales search is more accurate for counter work
          allowCredit: selected.allowCredit ?? row.allowCredit,
          creditLimit: selected.creditLimit ?? row.creditLimit,
          currentOutstanding: selected.currentOutstanding ?? row.currentOutstanding ?? 0,
          customerGroupName: selected.customerGroupName ?? row.customerGroupName,
          groupDiscountPercent: selected.groupDiscountPercent ?? row.groupDiscountPercent,
          pharmacyRelation: selected.pharmacyRelation ?? row.pharmacyRelation,
        });
      })
      .catch(() => {
        setDetail({
          ...selected,
          currentOutstanding: selected.currentOutstanding ?? 0,
        });
      })
      .finally(() => setDetailLoading(false));
  }, [selected]);

  const openCustomer = (row: CustomerListItem) => {
    setSelected(row);
    const missing = !hasUsablePhone(row.phone);
    setEditingPhone(missing);
    setPhoneDraft(missing ? '' : row.phone);
  };

  const applyCustomerPhoneUpdate = (updated: CustomerDetail) => {
    const next: CustomerListItem = {
      id: updated.id,
      customerCode: updated.customerCode,
      fullName: updated.fullName,
      phone: updated.phone,
      allowCredit: updated.allowCredit,
      creditLimit: updated.creditLimit,
      currentOutstanding: selected?.currentOutstanding ?? updated.currentOutstanding ?? 0,
      customerGroupName: selected?.customerGroupName,
      groupDiscountPercent: selected?.groupDiscountPercent,
      pharmacyRelation: selected?.pharmacyRelation,
    };
    setSelected(next);
    setDetail((prev) =>
      prev
        ? { ...prev, ...updated, currentOutstanding: next.currentOutstanding }
        : { ...updated, currentOutstanding: next.currentOutstanding },
    );
    setHits((prev) => prev.map((row) => (row.id === next.id ? { ...row, ...next } : row)));
    setEditingPhone(false);
    setPhoneDraft(updated.phone);
  };

  const savePhone = async () => {
    if (!selected) return;
    const nextPhone = phoneDraft.trim();
    if (!hasUsablePhone(nextPhone)) {
      message.warning('Nhập SĐT hợp lệ (9–12 số)');
      return;
    }
    if (!canWrite) {
      message.warning('Cần quyền bán hàng ghi để sửa SĐT');
      return;
    }
    setSavingPhone(true);
    try {
      const updated = await updateCustomerPhone(selected.id, nextPhone);
      applyCustomerPhoneUpdate(updated);
      message.success('Đã cập nhật SĐT');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được SĐT'));
    } finally {
      setSavingPhone(false);
    }
  };

  const copyText = async (value: string, label: string) => {
    try {
      await navigator.clipboard.writeText(value);
      message.success(`Đã copy ${label}`);
    } catch {
      message.warning(`Không copy được ${label}`);
    }
  };

  const sendToPos = () => {
    if (!selected) return;
    setCustomer({
      id: selected.id,
      customerCode: selected.customerCode,
      fullName: selected.fullName,
      phone: selected.phone,
      allowCredit: selected.allowCredit,
    });
    message.success(`Đã chọn ${selected.fullName} trên POS`);
    navigate('/pos');
  };

  const createNew = async (values: { fullName: string; phone: string }) => {
    setCreating(true);
    try {
      const created = await createCustomer({
        fullName: values.fullName.trim(),
        phone: values.phone.trim(),
      });
      message.success('Đã thêm khách');
      setCreateOpen(false);
      createForm.resetFields();
      setQuery(created.phone || created.fullName);
      setSelected({
        id: created.id,
        customerCode: created.customerCode,
        fullName: created.fullName,
        phone: created.phone,
        allowCredit: created.allowCredit,
        creditLimit: created.creditLimit,
        currentOutstanding: 0,
      });
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không thêm được khách'));
    } finally {
      setCreating(false);
    }
  };

  const sheetCustomer = detail ?? selected;

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Khách + OTP"
        subtitle="Tìm · OTP · nợ · đưa POS"
        backTo="/"
        right={
          canWrite ? (
            <Button
              type="primary"
              className="customer-header-add"
              icon={<PlusOutlined />}
              onClick={() => setCreateOpen(true)}
            >
              Thêm
            </Button>
          ) : null
        }
      />
      <main className="staff-body">
        <Input
          size="large"
          allowClear
          prefix={<PhoneOutlined style={{ color: '#94a3b8' }} />}
          placeholder="SĐT, tên hoặc mã khách…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          inputMode="search"
        />
        <Typography.Text type="secondary" style={{ display: 'block', margin: '8px 0 12px', fontSize: 12 }}>
          {query.trim().length === 0
            ? `Danh sách A–Z · ${total > 0 ? `${hits.length}/${total}` : 'đang tải'} · chọn khách để xem OTP, nợ, đưa vào POS.`
            : canSearch
              ? total > 0
                ? `Tìm thấy ${total} khách · đang hiện ${hits.length}.`
                : 'Chạm khách để xem chi tiết OTP / ghi nợ.'
              : 'Gõ thêm ký tự (hoặc ≥3 số SĐT) để tìm.'}
        </Typography.Text>

        {loading ? (
          <div style={{ textAlign: 'center', padding: 16 }}>
            <Spin />
          </div>
        ) : null}

        {!loading && hits.length === 0 ? (
          <div className="customer-empty">
            <Typography.Text type="secondary">
              {query.trim() ? 'Không tìm thấy khách phù hợp.' : 'Chưa có khách trong hệ thống.'}
            </Typography.Text>
            {canWrite ? (
              <Button
                type="primary"
                icon={<UserAddOutlined />}
                style={{ marginTop: 12 }}
                onClick={() => {
                  createForm.setFieldsValue({
                    phone: digitsOnly(query).length >= 9 ? digitsOnly(query) : query.trim(),
                    fullName: digitsOnly(query).length >= 9 ? '' : query.trim(),
                  });
                  setCreateOpen(true);
                }}
              >
                Thêm khách mới
              </Button>
            ) : null}
          </div>
        ) : null}

        {!loading
          ? hits.map((c) => {
              const outstanding = c.currentOutstanding ?? 0;
              const relation = pharmacyRelationLabel(c.pharmacyRelation);
              return (
                <button
                  key={c.id}
                  type="button"
                  className="customer-hit"
                  onClick={() => openCustomer(c)}
                >
                  <div className="customer-hit__top">
                    <Typography.Text strong className="customer-hit__name">
                      {c.fullName}
                    </Typography.Text>
                    {outstanding > 0 ? (
                      <span className="customer-hit__debt">Nợ {formatMoney(outstanding)}</span>
                    ) : null}
                  </div>
                  <div className="customer-hit__meta">
                    <span>{displayPhone(c.phone)}</span>
                    <span>· {c.customerCode || '—'}</span>
                  </div>
                  <div className="customer-hit__tags">
                    {c.allowCredit ? <Tag color="gold">Được nợ</Tag> : null}
                    {(c.groupDiscountPercent ?? 0) > 0 ? (
                      <Tag color="cyan">
                        {c.customerGroupName || 'Nhóm'} −{c.groupDiscountPercent}%
                      </Tag>
                    ) : c.customerGroupName ? (
                      <Tag>{c.customerGroupName}</Tag>
                    ) : null}
                    {relation ? <Tag>{relation}</Tag> : null}
                    {!hasUsablePhone(c.phone) ? <Tag color="red">Thiếu SĐT</Tag> : null}
                  </div>
                </button>
              );
            })
          : null}

        {!loading && hasMore ? (
          <Button
            block
            size="large"
            className="customer-load-more"
            loading={loadingMore}
            onClick={loadMore}
          >
            Xem thêm ({hits.length}/{total})
          </Button>
        ) : null}

        {!loading && !hasMore && hits.length > 0 && total > pageSize ? (
          <Typography.Text
            type="secondary"
            style={{ display: 'block', textAlign: 'center', marginTop: 8, fontSize: 12 }}
          >
            Đã hiện hết {total} khách
          </Typography.Text>
        ) : null}
      </main>

      <Drawer
        title={sheetCustomer?.fullName ?? 'Khách hàng'}
        open={Boolean(selected)}
        onClose={() => {
          setSelected(null);
          setEditingPhone(false);
          setPhoneDraft('');
        }}
        height="88%"
        placement="bottom"
        className="customer-detail-drawer"
        destroyOnClose
      >
        {selected ? (
          <div className="customer-detail">
            {detailLoading ? <Spin style={{ marginBottom: 12 }} /> : null}

            <div className="customer-detail__phone">
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                Số điện thoại
              </Typography.Text>
              {editingPhone ? (
                <div className="customer-detail__phone-edit">
                  <Input
                    size="large"
                    value={phoneDraft}
                    onChange={(e) => setPhoneDraft(e.target.value)}
                    placeholder="09xxxxxxxx"
                    inputMode="tel"
                    autoFocus
                    status={phoneDraft && !hasUsablePhone(phoneDraft) ? 'error' : undefined}
                    onPressEnter={() => void savePhone()}
                  />
                  <Space style={{ marginTop: 8 }} wrap>
                    <Button
                      type="primary"
                      loading={savingPhone}
                      disabled={!canWrite}
                      onClick={() => void savePhone()}
                    >
                      Lưu SĐT
                    </Button>
                    {hasUsablePhone(selected.phone) ? (
                      <Button
                        disabled={savingPhone}
                        onClick={() => {
                          setEditingPhone(false);
                          setPhoneDraft(selected.phone);
                        }}
                      >
                        Hủy
                      </Button>
                    ) : null}
                  </Space>
                  {!canWrite ? (
                    <Typography.Text type="secondary" style={{ display: 'block', marginTop: 6, fontSize: 12 }}>
                      Cần quyền ghi bán hàng để sửa SĐT.
                    </Typography.Text>
                  ) : (
                    <Typography.Text type="secondary" style={{ display: 'block', marginTop: 6, fontSize: 12 }}>
                      {hasUsablePhone(selected.phone)
                        ? 'Sửa SĐT nếu khách đổi số hoặc nhập sai.'
                        : 'Thiếu SĐT — nhập ngay để tạo OTP / liên hệ khách.'}
                    </Typography.Text>
                  )}
                </div>
              ) : (
                <div className="customer-detail__row" style={{ marginBottom: 0 }}>
                  <div className="customer-detail__value">{displayPhone(selected.phone)}</div>
                  <Space>
                    {hasUsablePhone(selected.phone) ? (
                      <>
                        <Button
                          icon={<PhoneOutlined />}
                          href={`tel:${digitsOnly(selected.phone)}`}
                          aria-label="Gọi"
                        />
                        <Button
                          icon={<CopyOutlined />}
                          onClick={() => void copyText(selected.phone, 'SĐT')}
                          aria-label="Copy SĐT"
                        />
                      </>
                    ) : null}
                    <Button
                      icon={<EditOutlined />}
                      onClick={() => {
                        setPhoneDraft(hasUsablePhone(selected.phone) ? selected.phone : '');
                        setEditingPhone(true);
                      }}
                      aria-label="Sửa SĐT"
                    >
                      Sửa
                    </Button>
                  </Space>
                </div>
              )}
            </div>

            <div className="customer-detail__row">
              <div>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  Mã khách
                </Typography.Text>
                <div className="customer-detail__value">{selected.customerCode || '—'}</div>
              </div>
              {selected.customerCode ? (
                <Button
                  icon={<CopyOutlined />}
                  onClick={() => void copyText(selected.customerCode, 'mã khách')}
                  aria-label="Copy mã"
                />
              ) : null}
            </div>

            <div className="customer-detail__tags">
              {detail?.hasAppAccount ? <Tag color="green">Có app</Tag> : <Tag>Chưa có app</Tag>}
              {selected.allowCredit ? <Tag color="gold">Được ghi nợ</Tag> : <Tag>Chưa cho nợ</Tag>}
              {(selected.currentOutstanding ?? 0) > 0 ? (
                <Tag color="red">Đang nợ {formatMoney(selected.currentOutstanding ?? 0)}</Tag>
              ) : (
                <Tag color="default">Không nợ</Tag>
              )}
              {(selected.groupDiscountPercent ?? 0) > 0 ? (
                <Tag color="cyan">
                  CK nhóm {selected.groupDiscountPercent}%
                  {selected.customerGroupName ? ` · ${selected.customerGroupName}` : ''}
                </Tag>
              ) : null}
              {pharmacyRelationLabel(selected.pharmacyRelation) ? (
                <Tag>{pharmacyRelationLabel(selected.pharmacyRelation)}</Tag>
              ) : null}
            </div>

            {selected.allowCredit && selected.creditLimit != null ? (
              <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 10, fontSize: 12 }}>
                Hạn mức nợ: {formatMoney(selected.creditLimit)}
              </Typography.Text>
            ) : null}

            {detail?.appLastLoginAt ? (
              <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 10, fontSize: 12 }}>
                App đăng nhập gần nhất: {dayjs(detail.appLastLoginAt).format('DD/MM/YYYY HH:mm')}
              </Typography.Text>
            ) : null}

            <Space direction="vertical" size={10} style={{ width: '100%', marginBottom: 14 }}>
              <Button type="primary" block size="large" icon={<ShoppingCartOutlined />} onClick={sendToPos}>
                Đưa vào POS
              </Button>
              <div className="customer-detail__actions">
                <Button
                  size="large"
                  icon={<DollarOutlined />}
                  onClick={() => setCreditOpen(true)}
                >
                  Ghi nợ
                </Button>
                <Button
                  size="large"
                  disabled={(selected.currentOutstanding ?? 0) <= 0}
                  onClick={() => navigate('/collect')}
                >
                  Thu nợ
                </Button>
              </div>
            </Space>

            <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
              OTP app khách
            </Typography.Text>
            <PilotOtpBlock
              customerId={selected.id}
              phone={selected.phone}
              fullName={selected.fullName}
              hasAppAccount={detail?.hasAppAccount}
              onIssued={() => void loadList(query.trim(), 1, false)}
            />
          </div>
        ) : null}
      </Drawer>

      <Drawer
        title="Thêm khách mới"
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        height="auto"
        placement="bottom"
        destroyOnClose
      >
        <Form
          form={createForm}
          layout="vertical"
          onFinish={(values) => void createNew(values)}
          initialValues={{ fullName: '', phone: '' }}
        >
          <Form.Item
            name="fullName"
            label="Họ tên"
            rules={[{ required: true, message: 'Nhập họ tên' }]}
          >
            <Input size="large" placeholder="Nguyễn Văn A" autoComplete="name" />
          </Form.Item>
          <Form.Item
            name="phone"
            label="Số điện thoại"
            rules={[
              { required: true, message: 'Nhập SĐT' },
              {
                validator: async (_, value) => {
                  if (!hasUsablePhone(String(value ?? ''))) {
                    throw new Error('SĐT không hợp lệ (9–12 số)');
                  }
                },
              },
            ]}
          >
            <Input size="large" placeholder="09xxxxxxxx" inputMode="tel" autoComplete="tel" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block size="large" loading={creating} icon={<UserAddOutlined />}>
            Lưu khách
          </Button>
        </Form>
      </Drawer>

      <CustomerCreditSheet
        customer={
          sheetCustomer
            ? {
                id: sheetCustomer.id,
                customerCode: sheetCustomer.customerCode,
                fullName: sheetCustomer.fullName,
                phone: sheetCustomer.phone,
                allowCredit: sheetCustomer.allowCredit,
                creditLimit: sheetCustomer.creditLimit,
                currentOutstanding: sheetCustomer.currentOutstanding,
                hasAppAccount: detail?.hasAppAccount,
              }
            : null
        }
        open={creditOpen}
        onClose={() => setCreditOpen(false)}
        onUpdated={(updated) => {
          const next = {
            id: updated.id,
            customerCode: updated.customerCode,
            fullName: updated.fullName,
            phone: updated.phone,
            allowCredit: updated.allowCredit,
            creditLimit: updated.creditLimit,
            currentOutstanding: selected?.currentOutstanding ?? updated.currentOutstanding ?? 0,
            customerGroupName: selected?.customerGroupName,
            groupDiscountPercent: selected?.groupDiscountPercent,
            pharmacyRelation: selected?.pharmacyRelation,
          };
          setSelected(next);
          setHits((prev) => prev.map((row) => (row.id === next.id ? { ...row, ...next } : row)));
          setDetail((prev) => (prev ? { ...prev, ...updated, currentOutstanding: next.currentOutstanding } : prev));
        }}
      />
    </div>
  );
}
