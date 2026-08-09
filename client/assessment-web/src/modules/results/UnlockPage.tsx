import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, Checkbox, Form, Input, Select, message } from 'antd';
import type { AxiosError } from 'axios';
import { captureLead } from '@/shared/api/assessment.api';

type FormValues = {
  respondentName: string;
  respondentPhone: string;
  respondentEmail?: string;
  respondentOrgName: string;
  orgScale: string;
  respondentNote?: string;
  consentMarketing: boolean;
};

const BENEFITS = [
  {
    title: 'Báo cáo đánh giá chi tiết',
    desc: 'Điểm mạnh, điểm yếu và mức độ trưởng thành của nhà thuốc',
    icon: 'report',
    tone: 'blue',
  },
  {
    title: 'Phân tích chuyên sâu',
    desc: 'So sánh với chuẩn ngành và phát hiện cơ hội cải thiện',
    icon: 'insight',
    tone: 'amber',
  },
  {
    title: 'Lộ trình cải thiện 30 – 60 – 90 ngày',
    desc: 'Hành động cụ thể, ưu tiên đúng việc cần làm trước',
    icon: 'roadmap',
    tone: 'teal',
  },
  {
    title: 'Khuyến nghị giải pháp',
    desc: 'Gợi ý công cụ và giải pháp giúp vận hành hiệu quả hơn',
    icon: 'chart',
    tone: 'violet',
  },
  {
    title: 'Tư vấn 1:1 cùng chuyên gia Novixa',
    desc: 'Được hỗ trợ trực tiếp, giải đáp thắc mắc và định hướng triển khai',
    icon: 'support',
    tone: 'rose',
  },
] as const;

const COMMITMENTS = [
  'Hoàn toàn miễn phí — Không ràng buộc',
  'Không spam — Không chia sẻ thông tin',
  'Bảo mật tuyệt đối thông tin của bạn',
  'Nhận báo cáo trong vòng 24h',
] as const;

const SCALE_CHIPS = [
  { value: 'small', label: 'Nhà thuốc độc lập (1 cơ sở)' },
  { value: 'medium', label: 'Chuỗi nhỏ (2 – 5 cơ sở)' },
  { value: 'chain', label: 'Chuỗi lớn (trên 5 cơ sở)' },
] as const;

function Icon({ d, size = 16 }: { d: string; size?: number }) {
  return (
    <svg viewBox="0 0 24 24" width={size} height={size} fill="none" aria-hidden>
      <path d={d} stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function BenefitIcon({ kind }: { kind: (typeof BENEFITS)[number]['icon'] }) {
  const map: Record<(typeof BENEFITS)[number]['icon'], string> = {
    report: 'M7 3h8l4 4v14a1 1 0 01-1 1H7a1 1 0 01-1-1V4a1 1 0 011-1zM14 3v5h5M9 13h6M9 17h4',
    insight: 'M9 18h6M10 21h4M12 3a6 6 0 00-3 11.2V16h6v-1.8A6 6 0 0012 3z',
    roadmap: 'M12 4a8 8 0 100 16 8 8 0 000-16zm0 4a4 4 0 100 8 4 4 0 000-8z',
    chart: 'M4 19V5M4 19h16M8 15V10M12 15V7M16 15v-3',
    support:
      'M4 14v-1a4 4 0 014-4h1M19 14v-1a4 4 0 00-4-4h-1M8 14h8v2a3 3 0 01-3 3h-2a3 3 0 01-3-3v-2z',
  };
  return <Icon d={map[kind]} size={18} />;
}

function PharmacyArt() {
  return (
    <svg className="unlock-art" viewBox="0 0 320 180" role="img" aria-label="Minh họa nhà thuốc">
      <defs>
        <linearGradient id="ua-sky" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#d1fae5" />
          <stop offset="100%" stopColor="#ecfeff" />
        </linearGradient>
        <linearGradient id="ua-build" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="#ffffff" />
          <stop offset="100%" stopColor="#f0fdfa" />
        </linearGradient>
      </defs>
      <rect width="320" height="180" rx="18" fill="url(#ua-sky)" />
      <ellipse cx="160" cy="158" rx="110" ry="10" fill="#99f6e4" opacity="0.55" />
      <rect x="88" y="52" width="144" height="98" rx="10" fill="url(#ua-build)" stroke="#0f766e" strokeWidth="2" />
      <rect x="72" y="70" width="28" height="80" rx="6" fill="#ccfbf1" stroke="#14b8a6" strokeWidth="1.5" />
      <rect x="220" y="70" width="28" height="80" rx="6" fill="#ccfbf1" stroke="#14b8a6" strokeWidth="1.5" />
      <rect x="108" y="72" width="104" height="42" rx="8" fill="#ecfeff" stroke="#0d9488" strokeWidth="2" />
      <path d="M148 84h24M160 74v28" stroke="#0f766e" strokeWidth="3.5" strokeLinecap="round" />
      <rect x="134" y="124" width="52" height="26" rx="4" fill="#0f766e" />
      <rect x="98" y="58" width="16" height="10" rx="2" fill="#5eead4" />
      <rect x="206" y="58" width="16" height="10" rx="2" fill="#5eead4" />
      <text
        x="160"
        y="42"
        textAnchor="middle"
        fill="#0f766e"
        fontSize="13"
        fontWeight="700"
        fontFamily="system-ui,Segoe UI,sans-serif"
      >
        PHARMACY
      </text>
    </svg>
  );
}

function ScaleField({
  value,
  onChange,
}: {
  value?: string;
  onChange?: (value: string) => void;
}) {
  return (
    <Select
      size="middle"
      value={value}
      onChange={onChange}
      placeholder="Chọn quy mô phù hợp"
      options={SCALE_CHIPS.map((c) => ({ value: c.value, label: c.label }))}
      className="unlock-scale-select"
    />
  );
}

export function UnlockPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm<FormValues>();

  async function onFinish(values: FormValues) {
    setLoading(true);
    try {
      await captureLead(id, {
        respondentName: values.respondentName,
        respondentPhone: values.respondentPhone,
        respondentEmail: values.respondentEmail?.trim() || undefined,
        respondentOrgName: values.respondentOrgName.trim(),
        orgScale: values.orgScale,
        respondentNote: values.respondentNote,
        consentMarketing: values.consentMarketing,
      });
      message.success('Cảm ơn! Báo cáo đã sẵn sàng.');
      navigate(`/report/${id}`);
    } catch (err: unknown) {
      const axiosErr = err as AxiosError<{ message?: string }>;
      const msg = axiosErr.response?.data?.message;
      message.error(msg ?? 'Gửi thông tin thất bại. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  }

  function onFinishFailed() {
    message.warning('Vui lòng điền các trường bắt buộc (đánh dấu *).');
  }

  return (
    <div className="unlock-page">
      <header className="unlock-topbar">
        <a className="unlock-brand" href="https://novixa.vn/vi/" aria-label="Novixa">
          <img className="unlock-brand__logo" src="/logo.png" alt="Novixa" width="140" height="40" />
        </a>
        <span className="unlock-privacy">
          <Icon d="M7 11V8a5 5 0 0110 0v3M6 11h12v10H6V11z" />
          Thông tin của bạn được bảo mật tuyệt đối
        </span>
      </header>

      <section className="unlock-hero">
        <span className="unlock-hero__badge">Báo cáo chi tiết &amp; tư vấn miễn phí</span>
        <h1 className="unlock-hero__title">
          Nhận báo cáo chi tiết và tư vấn <em>giải pháp phù hợp</em> cho nhà thuốc của bạn
        </h1>
        <p className="unlock-hero__sub">
          Chỉ mất 7 phút hoàn thành — Nhận ngay báo cáo đánh giá và khuyến nghị cải thiện
        </p>
      </section>

      <div className="unlock-grid">
        <aside className="unlock-panel">
          <h2 className="unlock-panel__title">Bạn sẽ nhận được gì?</h2>
          <ul className="unlock-benefits">
            {BENEFITS.map((item) => (
              <li key={item.title}>
                <span className="unlock-benefits__icon" data-tone={item.tone}>
                  <BenefitIcon kind={item.icon} />
                </span>
                <div>
                  <strong>{item.title}</strong>
                  <p>{item.desc}</p>
                </div>
              </li>
            ))}
          </ul>

          <div className="unlock-commit">
            <h3>
              <span className="unlock-commit__icon" aria-hidden>
                <Icon d="M12 3l7 3v5c0 4.5-3 7.5-7 9-4-1.5-7-4.5-7-9V6l7-3zM9.5 12l1.8 1.8L15 10" size={16} />
              </span>
              Cam kết từ Novixa
            </h3>
            <ul>
              {COMMITMENTS.map((item) => (
                <li key={item}>
                  <span aria-hidden>✓</span>
                  {item}
                </li>
              ))}
            </ul>
          </div>

          <PharmacyArt />
        </aside>

        <section className="unlock-card">
          <Form
            form={form}
            layout="vertical"
            onFinish={onFinish}
            onFinishFailed={onFinishFailed}
            scrollToFirstError={{ behavior: 'smooth', block: 'center' }}
            requiredMark
            initialValues={{ orgScale: 'small', consentMarketing: true }}
          >
            <div className="unlock-section">
              <h3 className="unlock-section__title">
                <Icon d="M4 10l8-6 8 6v9a1 1 0 01-1 1h-5v-6H10v6H5a1 1 0 01-1-1v-9z" />
                Thông tin liên hệ
              </h3>
              <Form.Item
                label="Họ và tên"
                name="respondentName"
                rules={[{ required: true, min: 2, message: 'Nhập họ và tên' }]}
              >
                <Input
                  size="middle"
                  placeholder="Nhập họ và tên của bạn"
                  autoComplete="name"
                  prefix={<Icon d="M12 12a4 4 0 100-8 4 4 0 000 8zM5 20c1.5-3.2 4-5 7-5s5.5 1.8 7 5" />}
                />
              </Form.Item>
              <Form.Item
                label="Số điện thoại"
                name="respondentPhone"
                rules={[{ required: true, pattern: /^0[0-9]{9}$/, message: 'SĐT 10 số, bắt đầu 0' }]}
                extra="Chuyên gia sẽ liên hệ tư vấn cho bạn"
              >
                <Input
                  size="middle"
                  placeholder="Nhập số điện thoại"
                  inputMode="tel"
                  autoComplete="tel"
                  prefix={<Icon d="M7 4h4l1.5 4-2 1.5a10 10 0 004.5 4.5L16.5 12l4 1.5V18a2 2 0 01-2 2A14 14 0 015 6a2 2 0 012-2z" />}
                />
              </Form.Item>
              <Form.Item
                label="Email (tuỳ chọn)"
                name="respondentEmail"
                extra="Dùng để nhận báo cáo chi tiết"
                rules={[
                  {
                    validator: async (_, value?: string) => {
                      const v = value?.trim() ?? '';
                      if (!v) return;
                      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v)) {
                        throw new Error('Email không hợp lệ');
                      }
                    },
                  },
                ]}
              >
                <Input
                  size="middle"
                  placeholder="Nhập email"
                  autoComplete="email"
                  prefix={<Icon d="M4 6h16v12H4V6zm0 0l8 7 8-7" />}
                />
              </Form.Item>
            </div>

            <div className="unlock-section">
              <h3 className="unlock-section__title">
                <Icon d="M4 10l8-6 8 6v9a1 1 0 01-1 1H5a1 1 0 01-1-1v-9zM9 20v-6h6v6" />
                Thông tin nhà thuốc / cơ sở
              </h3>
              <Form.Item
                label="Tên nhà thuốc / cơ sở"
                name="respondentOrgName"
                rules={[{ required: true, min: 2, message: 'Nhập tên nhà thuốc hoặc cơ sở' }]}
              >
                <Input
                  size="middle"
                  placeholder="Nhập tên nhà thuốc hoặc cơ sở"
                  prefix={<Icon d="M4 20V9l8-5 8 5v11H4zm5-4h6" />}
                />
              </Form.Item>

              <Form.Item
                label="Quy mô cơ sở"
                name="orgScale"
                rules={[{ required: true, message: 'Chọn quy mô nhà thuốc' }]}
              >
                <ScaleField />
              </Form.Item>

              <Form.Item
                label="Ghi chú thêm (tuỳ chọn)"
                name="respondentNote"
                extra="Ví dụ: muốn tối ưu tồn kho, tăng doanh thu, quản lý nhân sự..."
              >
                <Input.TextArea
                  rows={2}
                  placeholder="Mô tả ngắn về nhà thuốc hoặc vấn đề bạn đang quan tâm..."
                />
              </Form.Item>
            </div>

            <Form.Item name="consentMarketing" valuePropName="checked" className="unlock-consent">
              <Checkbox>
                Tôi đồng ý nhận thông tin tư vấn từ Novixa.
                <span className="unlock-consent__note">
                  {' '}
                  Chúng tôi cam kết bảo mật thông tin và không chia sẻ cho bên thứ ba.
                </span>
              </Checkbox>
            </Form.Item>

            <Button
              type="primary"
              htmlType="submit"
              loading={loading}
              block
              size="large"
              className="unlock-submit"
              icon={<Icon d="M4 12l16-7-7 16-2-6-7-3z" size={18} />}
            >
              Nhận &amp; xem báo cáo
            </Button>

            <div className="unlock-eta">
              <Icon d="M12 4a8 8 0 100 16 8 8 0 000-16zm0 4v4l3 2" />
              <span>
                <strong>Hoàn thành trong 2 phút.</strong> Báo cáo sẽ được gửi qua email và chuyên gia liên
                hệ trong 24h.
              </span>
            </div>
          </Form>
        </section>
      </div>

      <footer className="unlock-footer">
        <div className="unlock-footer__item">
          <span className="unlock-footer__icon" data-tone="teal">
            <Icon d="M12 3l7 3v5c0 4.5-3 7.5-7 9-4-1.5-7-4.5-7-9V6l7-3z" size={18} />
          </span>
          <div>
            <strong>Bảo mật tuyệt đối</strong>
            <p>Thông tin của bạn được mã hóa và bảo vệ an toàn</p>
          </div>
        </div>
        <div className="unlock-footer__item">
          <span className="unlock-footer__icon" data-tone="amber">
            <Icon d="M13 3L5 14h6l-1 7 8-11h-6l1-7z" size={18} />
          </span>
          <div>
            <strong>Nhanh chóng</strong>
            <p>Chỉ 7 phút — Nhận báo cáo trong 24h</p>
          </div>
        </div>
        <div className="unlock-footer__item">
          <span className="unlock-footer__icon" data-tone="blue">
            <Icon
              d="M4 14v-1a4 4 0 014-4h1M19 14v-1a4 4 0 00-4-4h-1M8 14h8v2a3 3 0 01-3 3h-2a3 3 0 01-3-3v-2z"
              size={18}
            />
          </span>
          <div>
            <strong>Hỗ trợ chuyên nghiệp</strong>
            <p>Đội ngũ chuyên gia đồng hành cùng nhà thuốc</p>
          </div>
        </div>
      </footer>
    </div>
  );
}
