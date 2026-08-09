import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, Checkbox, Form, Input, message } from 'antd';
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
  },
  {
    title: 'Phân tích chuyên sâu',
    desc: 'So sánh với chuẩn ngành và phát hiện cơ hội cải thiện',
    icon: 'insight',
  },
  {
    title: 'Lộ trình cải thiện 30 – 60 – 90 ngày',
    desc: 'Hành động cụ thể, ưu tiên đúng việc cần làm trước',
    icon: 'roadmap',
  },
  {
    title: 'Khuyến nghị giải pháp',
    desc: 'Gợi ý công cụ và giải pháp giúp vận hành hiệu quả hơn',
    icon: 'chart',
  },
  {
    title: 'Tư vấn 1:1 cùng chuyên gia Novixa',
    desc: 'Được hỗ trợ trực tiếp, giải đáp thắc mắc và định hướng triển khai',
    icon: 'support',
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

function IconLock() {
  return (
    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" aria-hidden>
      <rect x="5" y="11" width="14" height="10" rx="2" stroke="currentColor" strokeWidth="1.75" />
      <path d="M8 11V8a4 4 0 018 0v3" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}

function IconSend() {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" aria-hidden>
      <path d="M4 12l16-7-7 16-2-6-7-3z" stroke="currentColor" strokeWidth="1.75" strokeLinejoin="round" />
    </svg>
  );
}

function IconClock() {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" aria-hidden>
      <circle cx="12" cy="12" r="8" stroke="currentColor" strokeWidth="1.75" />
      <path d="M12 8v4l2.5 1.5" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}

function BenefitIcon({ kind }: { kind: (typeof BENEFITS)[number]['icon'] }) {
  const props = { viewBox: '0 0 24 24', width: 18, height: 18, fill: 'none', 'aria-hidden': true as const };
  switch (kind) {
    case 'report':
      return (
        <svg {...props}>
          <rect x="5" y="3" width="14" height="18" rx="2" stroke="currentColor" strokeWidth="1.75" />
          <path d="M8 8h8M8 12h8M8 16h5" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
        </svg>
      );
    case 'insight':
      return (
        <svg {...props}>
          <path
            d="M9 18h6M10 21h4M12 3a6 6 0 00-3 11.2V16h6v-1.8A6 6 0 0012 3z"
            stroke="currentColor"
            strokeWidth="1.75"
            strokeLinejoin="round"
          />
        </svg>
      );
    case 'roadmap':
      return (
        <svg {...props}>
          <circle cx="12" cy="12" r="8" stroke="currentColor" strokeWidth="1.75" />
          <circle cx="12" cy="12" r="4" stroke="currentColor" strokeWidth="1.75" />
          <circle cx="12" cy="12" r="1.5" fill="currentColor" />
        </svg>
      );
    case 'chart':
      return (
        <svg {...props}>
          <path d="M4 19V5M4 19h16" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
          <path d="M8 15V10M12 15V7M16 15v-3" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
        </svg>
      );
    default:
      return (
        <svg {...props}>
          <path
            d="M4 18v-1a4 4 0 014-4h2a4 4 0 014 4v1M12 13a3.5 3.5 0 100-7 3.5 3.5 0 000 7zM17 9h4M19 7v4"
            stroke="currentColor"
            strokeWidth="1.75"
            strokeLinecap="round"
          />
        </svg>
      );
  }
}

function PharmacyArt() {
  return (
    <svg className="unlock-art" viewBox="0 0 280 160" role="img" aria-label="Minh họa nhà thuốc">
      <defs>
        <linearGradient id="unlock-art-bg" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="#ccfbf1" />
          <stop offset="100%" stopColor="#99f6e4" />
        </linearGradient>
      </defs>
      <rect width="280" height="160" rx="16" fill="url(#unlock-art-bg)" />
      <rect x="78" y="48" width="124" height="90" rx="8" fill="#fff" stroke="#0f766e" strokeWidth="2" />
      <rect x="98" y="68" width="84" height="36" rx="6" fill="#ecfeff" stroke="#14b8a6" strokeWidth="2" />
      <path d="M130 78h20M140 70v20" stroke="#0f766e" strokeWidth="3" strokeLinecap="round" />
      <rect x="118" y="112" width="44" height="26" rx="3" fill="#0f766e" />
      <text x="140" y="42" textAnchor="middle" fill="#0f766e" fontSize="12" fontWeight="700">
        PHARMACY
      </text>
      <circle cx="48" cy="118" r="10" fill="#5eead4" opacity="0.8" />
      <circle cx="232" cy="56" r="14" fill="#2dd4bf" opacity="0.55" />
      <circle cx="246" cy="120" r="8" fill="#99f6e4" />
    </svg>
  );
}

function ScaleChips({
  value,
  onChange,
}: {
  value?: string;
  onChange?: (value: string) => void;
}) {
  return (
    <div className="unlock-scale" role="radiogroup" aria-label="Quy mô cơ sở">
      {SCALE_CHIPS.map((chip) => {
        const selected = value === chip.value;
        return (
          <button
            key={chip.value}
            type="button"
            role="radio"
            aria-checked={selected}
            className={['unlock-scale__chip', selected ? 'is-selected' : ''].filter(Boolean).join(' ')}
            onClick={() => onChange?.(chip.value)}
          >
            {chip.label}
          </button>
        );
      })}
    </div>
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
        <a className="unlock-brand" href="https://novixa.vn/vi/">
          <span className="unlock-brand__mark" aria-hidden>
            N
          </span>
          <span>Novixa</span>
        </a>
        <span className="unlock-privacy">
          <IconLock />
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
                <span className="unlock-benefits__icon" data-icon={item.icon}>
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
            <h3>Cam kết từ Novixa</h3>
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
              <h3 className="unlock-section__title">Thông tin liên hệ</h3>
              <Form.Item
                label="Họ và tên"
                name="respondentName"
                rules={[{ required: true, min: 2, message: 'Nhập họ và tên' }]}
              >
                <Input size="large" placeholder="Nhập họ và tên của bạn" autoComplete="name" />
              </Form.Item>
              <Form.Item
                label="Số điện thoại"
                name="respondentPhone"
                rules={[{ required: true, pattern: /^0[0-9]{9}$/, message: 'SĐT 10 số, bắt đầu 0' }]}
                extra="Chuyên gia sẽ liên hệ tư vấn cho bạn"
              >
                <Input size="large" placeholder="Nhập số điện thoại" inputMode="tel" autoComplete="tel" />
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
                <Input size="large" placeholder="Nhập email" autoComplete="email" />
              </Form.Item>
            </div>

            <div className="unlock-section">
              <h3 className="unlock-section__title">Thông tin nhà thuốc / cơ sở</h3>
              <Form.Item
                label="Tên nhà thuốc / cơ sở"
                name="respondentOrgName"
                rules={[{ required: true, min: 2, message: 'Nhập tên nhà thuốc hoặc cơ sở' }]}
              >
                <Input size="large" placeholder="Nhập tên nhà thuốc hoặc cơ sở" />
              </Form.Item>

              <Form.Item
                label="Quy mô cơ sở"
                name="orgScale"
                rules={[{ required: true, message: 'Chọn quy mô nhà thuốc' }]}
              >
                <ScaleChips />
              </Form.Item>

              <Form.Item
                label="Ghi chú thêm (tuỳ chọn)"
                name="respondentNote"
                extra="Ví dụ: muốn tối ưu tồn kho, tăng doanh thu, quản lý nhân sự..."
              >
                <Input.TextArea
                  rows={3}
                  placeholder="Mô tả ngắn về nhà thuốc hoặc vấn đề bạn đang quan tâm..."
                />
              </Form.Item>
            </div>

            <Form.Item name="consentMarketing" valuePropName="checked" initialValue={true}>
              <Checkbox>
                Tôi đồng ý nhận thông tin tư vấn từ Novixa. Chúng tôi cam kết bảo mật thông tin và không
                chia sẻ cho bên thứ ba.
              </Checkbox>
            </Form.Item>

            <Button
              type="primary"
              htmlType="submit"
              loading={loading}
              block
              size="large"
              className="unlock-submit"
              icon={<IconSend />}
            >
              Gửi &amp; xem báo cáo
            </Button>

            <div className="unlock-eta">
              <IconClock />
              <span>
                Hoàn thành trong 2 phút. Báo cáo sẽ được gửi qua email và chuyên gia liên hệ trong 24h.
              </span>
            </div>
          </Form>
        </section>
      </div>

      <footer className="unlock-footer">
        <div>
          <strong>Bảo mật tuyệt đối</strong>
          <p>Thông tin của bạn được mã hóa và bảo vệ an toàn</p>
        </div>
        <div>
          <strong>Nhanh chóng</strong>
          <p>Chỉ 7 phút — Nhận báo cáo trong 24h</p>
        </div>
        <div>
          <strong>Hỗ trợ chuyên nghiệp</strong>
          <p>Đội ngũ chuyên gia đồng hành cùng nhà thuốc</p>
        </div>
      </footer>
    </div>
  );
}
