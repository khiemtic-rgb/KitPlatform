import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Form, InputNumber, message, Space, Typography } from 'antd';
import {
  ArrowDownOutlined,
  ArrowLeftOutlined,
  ArrowUpOutlined,
  ClockCircleOutlined,
  ReloadOutlined,
  SaveOutlined,
  SafetyCertificateOutlined,
  StarFilled,
  TeamOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { Link } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchFamilies,
  fetchFamilyStarSettings,
  updateFamilyStarSettings,
  type FamilyStarSettings,
  type FamilySummary,
} from '@/shared/api/family-os.api';
import './family-os-routines.css';

type SettingsFormValues = {
  lateT1Minutes: number;
  lateT2Minutes: number;
  lateT3Minutes: number;
  lateHalfPct: number;
  latePenaltyHalfPct: number;
};

const DEFAULTS: SettingsFormValues = {
  lateT1Minutes: 30,
  lateT2Minutes: 60,
  lateT3Minutes: 90,
  lateHalfPct: 50,
  latePenaltyHalfPct: -50,
};

const EXAMPLE_STAR = 100;

function toFormValues(settings: FamilyStarSettings): SettingsFormValues {
  return {
    lateT1Minutes: settings.lateT1Minutes,
    lateT2Minutes: settings.lateT2Minutes,
    lateT3Minutes: settings.lateT3Minutes,
    lateHalfPct: settings.lateHalfPct,
    latePenaltyHalfPct: settings.latePenaltyHalfPct,
  };
}

function positiveFloor(star: number, pct: number): number {
  return Math.max(1, Math.floor((star * pct) / 100));
}

function penaltyFloor(star: number, pct: number): number {
  return -Math.max(1, Math.floor((star * Math.abs(pct)) / 100));
}

function familyLabel(displayName: string): string {
  const trimmed = displayName.trim();
  if (!trimmed) return 'Gia đình';
  return trimmed.toLowerCase().startsWith('gia đình') ? trimmed : `Gia đình ${trimmed}`;
}

export function FamilyOsStarSettingsPage() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [family, setFamily] = useState<FamilySummary | null>(null);
  const [settings, setSettings] = useState<FamilyStarSettings | null>(null);
  const [form] = Form.useForm<SettingsFormValues>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const families = await fetchFamilies();
      const first = families[0] ?? null;
      setFamily(first);
      if (!first) {
        setSettings(null);
        return;
      }
      const row = await fetchFamilyStarSettings(first.id);
      setSettings(row);
      form.setFieldsValue(toFormValues(row));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được cài đặt sao'));
    } finally {
      setLoading(false);
    }
  }, [form]);

  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    if (!family) return;
    const values = await form.validateFields();
    setSaving(true);
    try {
      const updated = await updateFamilyStarSettings(family.id, {
        lateT1Minutes: values.lateT1Minutes,
        lateT2Minutes: values.lateT2Minutes,
        lateT3Minutes: values.lateT3Minutes,
        lateHalfPct: values.lateHalfPct,
        lateZeroPct: 0,
        latePenaltyHalfPct: values.latePenaltyHalfPct,
        latePenaltyFullPct: -100,
      });
      setSettings(updated);
      message.success('Đã lưu luật sao muộn');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được cài đặt'));
    } finally {
      setSaving(false);
    }
  };

  const resetDefaults = () => {
    form.setFieldsValue(DEFAULTS);
  };

  const watchedT1 = Form.useWatch('lateT1Minutes', form) ?? settings?.lateT1Minutes ?? 30;
  const watchedT2 = Form.useWatch('lateT2Minutes', form) ?? settings?.lateT2Minutes ?? 60;
  const watchedT3 = Form.useWatch('lateT3Minutes', form) ?? settings?.lateT3Minutes ?? 90;
  const watchedHalf = Form.useWatch('lateHalfPct', form) ?? settings?.lateHalfPct ?? 50;
  const watchedPenaltyHalf =
    Form.useWatch('latePenaltyHalfPct', form) ?? settings?.latePenaltyHalfPct ?? -50;

  const familyTitle = family ? familyLabel(family.displayName) : 'Gia đình';

  const referenceRows = useMemo(
    () => [
      {
        key: 'on-time',
        tier: 'Đúng giờ',
        icon: '✅',
        condition: 'late ≤ 0',
        delta: '+star_reward',
        example: `${EXAMPLE_STAR} ⭐ → +100% = ${EXAMPLE_STAR} ⭐`,
      },
      {
        key: 'light-late',
        tier: 'Muộn nhẹ',
        icon: '⭐',
        condition: `0 < late ≤ T1 (${watchedT1})`,
        delta: `+floor(star × ${watchedHalf}%)`,
        example: `${EXAMPLE_STAR} ⭐ → +${watchedHalf}% = ${positiveFloor(EXAMPLE_STAR, watchedHalf)} ⭐ (min 1)`,
      },
      {
        key: 'zero',
        tier: 'Không thưởng',
        icon: '😐',
        condition: `T1 < late ≤ T2 (${watchedT2})`,
        delta: '0',
        example: `${EXAMPLE_STAR} ⭐ → +0% = 0 ⭐`,
      },
      {
        key: 'half-penalty',
        tier: 'Phạt nửa',
        icon: '😟',
        condition: `T2 < late ≤ T3 (${watchedT3})`,
        delta: `-floor(star × ${Math.abs(watchedPenaltyHalf)}%)`,
        example: `${EXAMPLE_STAR} ⭐ → ${watchedPenaltyHalf}% = ${penaltyFloor(EXAMPLE_STAR, watchedPenaltyHalf)} ⭐ (min -1)`,
      },
      {
        key: 'full-penalty',
        tier: 'Phạt full',
        icon: '😫',
        condition: 'late > T3',
        delta: '-star_reward',
        example: `${EXAMPLE_STAR} ⭐ → -100% = -${EXAMPLE_STAR} ⭐`,
      },
    ],
    [watchedT1, watchedT2, watchedT3, watchedHalf, watchedPenaltyHalf],
  );

  return (
    <div className={`fr-page fr-star-settings${loading ? ' is-loading' : ''}`}>
      <header className="fr-header">
        <div className="fr-header-brand">
          <Link to="/family-os/overview" className="fr-back-link" aria-label="Quay lại Tổng quan">
            <ArrowLeftOutlined />
          </Link>
          <span className="fr-header-ico is-gold" aria-hidden>
            <StarFilled />
          </span>
          <div>
            <h1>Cài đặt sao</h1>
            <p>
              {family
                ? `${familyTitle} — Ngưỡng muộn & hệ số thưởng/phạt cho cả nhà`
                : 'Chưa có gia đình'}
            </p>
          </div>
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Làm mới
          </Button>
          <Button
            type="primary"
            icon={<SaveOutlined />}
            loading={saving}
            disabled={!family}
            onClick={() => void save()}
          >
            Lưu
          </Button>
        </Space>
      </header>

      <section className="fr-banner">
        <div className="fr-banner-copy">
          <span className="fr-banner-i" aria-hidden>
            i
          </span>
          <div>
            <strong>Luật sao áp dụng khi bé hoàn thành sau khung giờ (window_end).</strong>
            <p>
              Mỗi cam kết có <em>star_reward</em> riêng ở tab Nhịp sống. Bảng dưới chỉ điều chỉnh
              ngưỡng phút muộn và hệ số % so với star_reward của từng việc.
            </p>
            <p className="fr-banner-shield">
              <SafetyCertificateOutlined aria-hidden />
              <span>App con vẫn nhận delta từ API — không hardcode trên FE.</span>
            </p>
          </div>
        </div>
        <span className="fr-banner-art" aria-hidden>
          ⭐📅
        </span>
      </section>

      {!family ? (
        <section className="fr-card">
          <Typography.Text type="secondary">
            Chưa có gia đình — thêm thành viên hoặc seed trước.
          </Typography.Text>
        </section>
      ) : (
        <Form form={form} layout="vertical" disabled={loading}>
          <section className="fr-card">
            <div className="fr-section-head">
              <span className="fr-section-num" aria-hidden>
                1
              </span>
              <h2>Ngưỡng muộn (phút sau window_end)</h2>
            </div>
            <div className="fr-star-fields is-3">
              <div className="fr-star-field">
                <label className="fr-star-label">
                  T1 — muộn nhẹ (≤ T1) <span className="fr-req">*</span>
                </label>
                <div className="fr-star-field-row">
                  <span className="fr-field-ico is-purple" aria-hidden>
                    <ClockCircleOutlined />
                  </span>
                  <Form.Item
                    name="lateT1Minutes"
                    rules={[{ required: true, type: 'number', min: 1 }]}
                    noStyle
                  >
                    <InputNumber min={1} max={240} addonAfter="phút" style={{ width: '100%' }} />
                  </Form.Item>
                </div>
                <span className="fr-field-help">
                  VD: 15 → muộn 20′ rồi vào tầng 0⭐ (giữa T1–T2)
                </span>
              </div>

              <div className="fr-star-field">
                <label className="fr-star-label">
                  T2 — không thưởng (≤ T2) <span className="fr-req">*</span>
                </label>
                <div className="fr-star-field-row">
                  <span className="fr-field-ico is-gold" aria-hidden>
                    😐
                  </span>
                  <Form.Item
                    name="lateT2Minutes"
                    rules={[{ required: true, type: 'number', min: 2 }]}
                    noStyle
                  >
                    <InputNumber min={2} max={360} addonAfter="phút" style={{ width: '100%' }} />
                  </Form.Item>
                </div>
              </div>

              <div className="fr-star-field">
                <label className="fr-star-label">
                  T3 — phạt nửa (≤ T3) <span className="fr-req">*</span>
                </label>
                <div className="fr-star-field-row">
                  <span className="fr-field-ico is-danger" aria-hidden>
                    <WarningOutlined />
                  </span>
                  <Form.Item
                    name="lateT3Minutes"
                    rules={[{ required: true, type: 'number', min: 3 }]}
                    noStyle
                  >
                    <InputNumber min={3} max={480} addonAfter="phút" style={{ width: '100%' }} />
                  </Form.Item>
                </div>
              </div>
            </div>
          </section>

          <section className="fr-card">
            <div className="fr-section-head">
              <span className="fr-section-num" aria-hidden>
                2
              </span>
              <h2>Hệ số thưởng / phạt (% star_reward)</h2>
            </div>
            <div className="fr-star-fields is-4">
              <div className="fr-star-field">
                <label className="fr-star-label">Muộn ≤ T1</label>
                <div className="fr-star-field-row">
                  <span className="fr-field-ico is-green" aria-hidden>
                    <ArrowUpOutlined />
                  </span>
                  <Form.Item
                    name="lateHalfPct"
                    rules={[{ required: true, type: 'number', min: 0, max: 100 }]}
                    noStyle
                  >
                    <InputNumber min={0} max={100} addonAfter="%" style={{ width: '100%' }} />
                  </Form.Item>
                </div>
                <span className="fr-field-help">Mặc định 50% = nửa sao (floor)</span>
              </div>

              <div className="fr-star-field">
                <label className="fr-star-label">Muộn ≤ T2</label>
                <div className="fr-star-field-row">
                  <span className="fr-field-ico is-gold" aria-hidden>
                    😐
                  </span>
                  <InputNumber value={0} disabled addonAfter="%" style={{ width: '100%' }} />
                </div>
                <span className="fr-field-help">Cố định 0%</span>
              </div>

              <div className="fr-star-field">
                <label className="fr-star-label">Muộn ≤ T3</label>
                <div className="fr-star-field-row">
                  <span className="fr-field-ico is-danger" aria-hidden>
                    <ArrowDownOutlined />
                  </span>
                  <Form.Item
                    name="latePenaltyHalfPct"
                    rules={[{ required: true, type: 'number', min: -100, max: -1 }]}
                    noStyle
                  >
                    <InputNumber min={-100} max={-1} addonAfter="%" style={{ width: '100%' }} />
                  </Form.Item>
                </div>
                <span className="fr-field-help">Mặc định -50% = trừ nửa sao</span>
              </div>

              <div className="fr-star-field">
                <label className="fr-star-label">Muộn &gt; T3</label>
                <div className="fr-star-field-row">
                  <span className="fr-field-ico is-purple" aria-hidden>
                    <ArrowDownOutlined />
                  </span>
                  <InputNumber value={-100} disabled addonAfter="%" style={{ width: '100%' }} />
                </div>
                <span className="fr-field-help">Cố định -100%</span>
              </div>
            </div>
          </section>

          <section className="fr-card">
            <div className="fr-section-head">
              <span className="fr-section-num" aria-hidden>
                3
              </span>
              <h2>Bảng tham chiếu (mặc định hệ thống)</h2>
            </div>
            <p className="fr-ref-sub">
              Đúng giờ: +100% star_reward · Không có window_end: +100%
            </p>
            <div className="fr-ref-table-wrap">
              <table className="fr-ref-table">
                <thead>
                  <tr>
                    <th>Tầng</th>
                    <th>Điều kiện</th>
                    <th>Delta (áp dụng vào star_reward)</th>
                    <th>Ví dụ minh hoạ</th>
                  </tr>
                </thead>
                <tbody>
                  {referenceRows.map((row) => (
                    <tr key={row.key}>
                      <td>
                        <span className="fr-ref-tier">
                          <span aria-hidden>{row.icon}</span>
                          {row.tier}
                        </span>
                      </td>
                      <td>
                        <code className="fr-ref-code">{row.condition}</code>
                      </td>
                      <td>
                        <code className="fr-ref-code">{row.delta}</code>
                      </td>
                      <td>{row.example}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="fr-card-foot">
              <Button icon={<ReloadOutlined />} onClick={resetDefaults}>
                Khôi phục mặc định
              </Button>
              <Button
                type="primary"
                icon={<TeamOutlined />}
                loading={saving}
                onClick={() => void save()}
              >
                Lưu cho {familyTitle}
              </Button>
            </div>
          </section>
        </Form>
      )}
    </div>
  );
}
