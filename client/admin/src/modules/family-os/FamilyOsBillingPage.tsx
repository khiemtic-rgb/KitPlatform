import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  InputNumber,
  Space,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import {
  CreditCardOutlined,
  FieldTimeOutlined,
  ReloadOutlined,
  SaveOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  extendFamilyTrial,
  fetchFamilies,
  fetchFamilySubscription,
  type FamilySubscription,
} from '@/shared/api/family-os.api';
import { listPaymentPlans, updatePaymentPlan, type PaymentPlan } from '@/shared/api/payment.api';
import './family-os-routines.css';

const PRODUCT_CODE = 'family_os';

interface FamilyBillingRow {
  familyId: string;
  familyName: string;
  memberCount: number;
  subscription: FamilySubscription | null;
  loadError?: string;
}

interface PlanDraft {
  amountVnd: number;
  trialDays: number;
}

const STATUS_META: Record<string, { color: string; label: string }> = {
  trial: { color: 'blue', label: 'Dùng thử' },
  active: { color: 'green', label: 'Đang hoạt động' },
  past_due: { color: 'orange', label: 'Chậm thanh toán' },
  expired: { color: 'red', label: 'Hết hạn' },
  canceled: { color: 'default', label: 'Đã hủy' },
  none: { color: 'default', label: 'Chưa có gói' },
};

function statusTag(status: string) {
  const meta = STATUS_META[status] ?? { color: 'default', label: status || '—' };
  return <Tag color={meta.color}>{meta.label}</Tag>;
}

function formatDate(value?: string): string {
  if (!value) return '—';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

function formatVnd(amount: number): string {
  return `${amount.toLocaleString('vi-VN')} đ`;
}

export function FamilyOsBillingPage() {
  const [loading, setLoading] = useState(true);
  const [plans, setPlans] = useState<PaymentPlan[]>([]);
  const [planDrafts, setPlanDrafts] = useState<Record<string, PlanDraft>>({});
  const [savingPlan, setSavingPlan] = useState<string | null>(null);
  const [rows, setRows] = useState<FamilyBillingRow[]>([]);
  const [extending, setExtending] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [planList, families] = await Promise.all([
        listPaymentPlans(PRODUCT_CODE).catch(() => [] as PaymentPlan[]),
        fetchFamilies(),
      ]);
      setPlans(planList);
      setPlanDrafts(
        Object.fromEntries(
          planList.map((p) => [p.planCode, { amountVnd: p.amountVnd, trialDays: p.trialDays }]),
        ),
      );

      const billingRows = await Promise.all(
        families.map(async (f): Promise<FamilyBillingRow> => {
          try {
            const subscription = await fetchFamilySubscription(f.id);
            return {
              familyId: f.id,
              familyName: f.displayName,
              memberCount: f.members.length,
              subscription,
            };
          } catch (error) {
            return {
              familyId: f.id,
              familyName: f.displayName,
              memberCount: f.members.length,
              subscription: null,
              loadError: apiErrorMessage(error, 'Không tải được gói'),
            };
          }
        }),
      );
      setRows(billingRows);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được dữ liệu billing'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const savePlan = async (plan: PaymentPlan) => {
    const draft = planDrafts[plan.planCode];
    if (!draft) return;
    setSavingPlan(plan.planCode);
    try {
      const updated = await updatePaymentPlan(PRODUCT_CODE, plan.planCode, {
        amountVnd: draft.amountVnd,
        trialDays: draft.trialDays,
      });
      setPlans((prev) => prev.map((p) => (p.planCode === updated.planCode ? updated : p)));
      message.success(
        `Đã lưu gói ${updated.displayName}: ${formatVnd(updated.amountVnd)} · dùng thử ${updated.trialDays} ngày`,
      );
    } catch (error) {
      message.error(
        apiErrorMessage(error, 'Không lưu được — cần quyền payment.ops.activate (PLATFORM_OPS)'),
      );
    } finally {
      setSavingPlan(null);
    }
  };

  const extendTrial = async (row: FamilyBillingRow, extraDays: number) => {
    setExtending(row.familyId);
    try {
      const subscription = await extendFamilyTrial(row.familyId, extraDays);
      setRows((prev) =>
        prev.map((r) =>
          r.familyId === row.familyId ? { ...r, subscription, loadError: undefined } : r,
        ),
      );
      message.success(
        `Đã cộng ${extraDays} ngày dùng thử cho ${row.familyName} — hết hạn ${formatDate(subscription.trialEndsAt)}`,
      );
    } catch (error) {
      message.error(
        apiErrorMessage(error, 'Không gia hạn được — cần quyền payment.ops.activate (PLATFORM_OPS)'),
      );
    } finally {
      setExtending(null);
    }
  };

  const planColumns = useMemo(
    () => [
      {
        title: 'Gói',
        dataIndex: 'displayName',
        key: 'displayName',
        render: (_: unknown, plan: PaymentPlan) => (
          <Space direction="vertical" size={0}>
            <Typography.Text strong>{plan.displayName}</Typography.Text>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {plan.productCode}/{plan.planCode} · chu kỳ {plan.intervalDays} ngày
            </Typography.Text>
          </Space>
        ),
      },
      {
        title: 'Giá (VND)',
        key: 'amountVnd',
        width: 170,
        render: (_: unknown, plan: PaymentPlan) => (
          <InputNumber
            min={1000}
            step={1000}
            style={{ width: 150 }}
            value={planDrafts[plan.planCode]?.amountVnd ?? plan.amountVnd}
            formatter={(v) => `${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.')}
            parser={(v) => Number((v ?? '').replace(/\./g, ''))}
            onChange={(value) =>
              setPlanDrafts((prev) => ({
                ...prev,
                [plan.planCode]: {
                  amountVnd: Number(value ?? plan.amountVnd),
                  trialDays: prev[plan.planCode]?.trialDays ?? plan.trialDays,
                },
              }))
            }
          />
        ),
      },
      {
        title: 'Ngày dùng thử',
        key: 'trialDays',
        width: 140,
        render: (_: unknown, plan: PaymentPlan) => (
          <InputNumber
            min={0}
            max={365}
            style={{ width: 110 }}
            value={planDrafts[plan.planCode]?.trialDays ?? plan.trialDays}
            addonAfter="ngày"
            onChange={(value) =>
              setPlanDrafts((prev) => ({
                ...prev,
                [plan.planCode]: {
                  amountVnd: prev[plan.planCode]?.amountVnd ?? plan.amountVnd,
                  trialDays: Number(value ?? plan.trialDays),
                },
              }))
            }
          />
        ),
      },
      {
        title: '',
        key: 'actions',
        width: 110,
        render: (_: unknown, plan: PaymentPlan) => {
          const draft = planDrafts[plan.planCode];
          const dirty =
            draft != null && (draft.amountVnd !== plan.amountVnd || draft.trialDays !== plan.trialDays);
          return (
            <Button
              type="primary"
              icon={<SaveOutlined />}
              disabled={!dirty}
              loading={savingPlan === plan.planCode}
              onClick={() => void savePlan(plan)}
            >
              Lưu
            </Button>
          );
        },
      },
    ],
    [planDrafts, savingPlan], // eslint-disable-line react-hooks/exhaustive-deps
  );

  const familyColumns = useMemo(
    () => [
      {
        title: 'Gia đình',
        key: 'familyName',
        render: (_: unknown, row: FamilyBillingRow) => (
          <Space direction="vertical" size={0}>
            <Typography.Text strong>{row.familyName}</Typography.Text>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {row.memberCount} thành viên
            </Typography.Text>
          </Space>
        ),
      },
      {
        title: 'Gói',
        key: 'planCode',
        width: 130,
        render: (_: unknown, row: FamilyBillingRow) => row.subscription?.planCode ?? '—',
      },
      {
        title: 'Trạng thái',
        key: 'status',
        width: 140,
        render: (_: unknown, row: FamilyBillingRow) =>
          row.loadError ? <Tag color="red">Lỗi tải</Tag> : statusTag(row.subscription?.status ?? 'none'),
      },
      {
        title: 'Dùng thử',
        key: 'trial',
        width: 170,
        render: (_: unknown, row: FamilyBillingRow) => {
          const sub = row.subscription;
          if (!sub?.trialEndsAt) return '—';
          const remaining = sub.trialDaysRemaining;
          return (
            <Space direction="vertical" size={0}>
              <span>đến {formatDate(sub.trialEndsAt)}</span>
              {remaining != null && sub.status === 'trial' ? (
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  còn {remaining}
                  {sub.trialDaysTotal != null ? `/${sub.trialDaysTotal}` : ''} ngày
                </Typography.Text>
              ) : null}
            </Space>
          );
        },
      },
      {
        title: 'Hết hạn gói',
        key: 'periodEnd',
        width: 120,
        render: (_: unknown, row: FamilyBillingRow) => formatDate(row.subscription?.currentPeriodEnd),
      },
      {
        title: 'Gia hạn dùng thử',
        key: 'actions',
        width: 210,
        render: (_: unknown, row: FamilyBillingRow) => {
          const status = row.subscription?.status ?? 'none';
          const canExtend = status === 'trial' || status === 'expired' || status === 'none' || status === 'canceled';
          return (
            <Space>
              {[7, 30].map((days) => (
                <Button
                  key={days}
                  size="small"
                  icon={<FieldTimeOutlined />}
                  disabled={!canExtend}
                  loading={extending === row.familyId}
                  onClick={() => void extendTrial(row, days)}
                >
                  +{days} ngày
                </Button>
              ))}
            </Space>
          );
        },
      },
    ],
    [extending], // eslint-disable-line react-hooks/exhaustive-deps
  );

  return (
    <div className="fr-page">
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Space align="center" style={{ justifyContent: 'space-between', width: '100%' }}>
          <Space align="center">
            <CreditCardOutlined style={{ fontSize: 20 }} />
            <Typography.Title level={4} style={{ margin: 0 }}>
              Billing — gói & dùng thử
            </Typography.Title>
          </Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Tải lại
          </Button>
        </Space>

        <Alert
          type="info"
          showIcon
          message="Số ngày dùng thử áp dụng cho gia đình đăng ký MỚI"
          description="Chỉnh 'Ngày dùng thử' bên dưới sẽ có hiệu lực ngay, không cần restart server. Gia đình hiện có không đổi — dùng nút gia hạn ở bảng dưới. Thao tác lưu gói / gia hạn cần quyền payment.ops.activate (PLATFORM_OPS)."
        />

        <Card title="Gói bán (Kit Payment · family_os)" size="small">
          <Table
            rowKey="planCode"
            columns={planColumns}
            dataSource={plans}
            loading={loading}
            pagination={false}
            locale={{ emptyText: 'Chưa có gói nào — chạy migration 224/225 để seed gói starter_month.' }}
          />
        </Card>

        <Card title="Gói của từng gia đình" size="small">
          <Table
            rowKey="familyId"
            columns={familyColumns}
            dataSource={rows}
            loading={loading}
            pagination={false}
            locale={{ emptyText: 'Chưa có gia đình nào trong tenant này.' }}
          />
        </Card>
      </Space>
    </div>
  );
}
