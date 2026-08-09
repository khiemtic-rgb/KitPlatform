import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, Checkbox, Form, Input, Select, Typography, message } from 'antd';
import type { AxiosError } from 'axios';
import { captureLead } from '@/shared/api/assessment.api';

const { Title, Paragraph } = Typography;

type FormValues = {
  respondentName: string;
  respondentPhone: string;
  respondentEmail?: string;
  respondentOrgName?: string;
  orgScale?: string;
  respondentNote?: string;
  consentMarketing: boolean;
};

const ORG_SCALE_OPTIONS = [
  { value: 'micro', label: 'Cá nhân / quầy nhỏ (1–2 nhân viên)' },
  { value: 'small', label: 'Nhà thuốc nhỏ (3–5 nhân viên)' },
  { value: 'medium', label: 'Nhà thuốc vừa (6–15 nhân viên)' },
  { value: 'large', label: 'Lớn (16+ nhân viên, 1 cơ sở)' },
  { value: 'chain', label: 'Chuỗi (2+ chi nhánh)' },
];

export function UnlockPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);

  async function onFinish(values: FormValues) {
    setLoading(true);
    try {
      await captureLead(id, {
        respondentName: values.respondentName,
        respondentPhone: values.respondentPhone,
        respondentEmail: values.respondentEmail?.trim() || undefined,
        respondentOrgName: values.respondentOrgName?.trim() || undefined,
        orgScale: values.orgScale || undefined,
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
    message.warning('Vui lòng điền họ tên và số điện thoại.');
  }

  return (
    <div className="page-shell">
      <Title level={3}>Nhận báo cáo chi tiết</Title>
      <Paragraph type="secondary">
        Chỉ cần họ tên và số điện thoại — phần còn lại tuỳ chọn.
      </Paragraph>

      <Form
        layout="vertical"
        onFinish={onFinish}
        onFinishFailed={onFinishFailed}
        scrollToFirstError={{ behavior: 'smooth', block: 'center' }}
        requiredMark
      >
        <Form.Item
          label="Họ tên"
          name="respondentName"
          rules={[{ required: true, min: 2, message: 'Nhập họ tên' }]}
        >
          <Input placeholder="Nguyễn Văn A" />
        </Form.Item>
        <Form.Item
          label="Số điện thoại"
          name="respondentPhone"
          rules={[{ required: true, pattern: /^0[0-9]{9}$/, message: 'SĐT 10 số, bắt đầu 0' }]}
        >
          <Input placeholder="0909123456" />
        </Form.Item>
        <Form.Item
          label="Email (tuỳ chọn)"
          name="respondentEmail"
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
          <Input placeholder="owner@example.com" />
        </Form.Item>
        <Form.Item
          label="Tên nhà thuốc / cơ sở (tuỳ chọn)"
          name="respondentOrgName"
          rules={[{ min: 2, message: 'Tối thiểu 2 ký tự nếu điền' }]}
        >
          <Input placeholder="Nhà thuốc ABC" />
        </Form.Item>
        <Form.Item label="Quy mô cơ sở (tuỳ chọn)" name="orgScale">
          <Select allowClear placeholder="Chọn nếu muốn" options={ORG_SCALE_OPTIONS} />
        </Form.Item>
        <Form.Item label="Ghi chú (tuỳ chọn)" name="respondentNote">
          <Input.TextArea rows={3} placeholder="Muốn tư vấn phần mềm quản lý..." />
        </Form.Item>
        <Form.Item name="consentMarketing" valuePropName="checked" initialValue={true}>
          <Checkbox>Tôi đồng ý nhận thông tin tư vấn từ Novixa</Checkbox>
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={loading} block size="large">
          Gửi &amp; xem báo cáo
        </Button>
      </Form>
    </div>
  );
}
