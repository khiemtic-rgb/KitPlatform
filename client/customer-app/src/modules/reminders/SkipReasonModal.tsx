import { Modal, Radio, Space, Typography } from 'antd';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MED_SKIP_REASON_OPTIONS, type MedSkipReasonCode } from '@/shared/care/med-skip-reasons';

type Props = {
  open: boolean;
  productName?: string;
  confirmLoading?: boolean;
  onCancel: () => void;
  onConfirm: (reason: MedSkipReasonCode) => void;
};

/** Modal chọn lý do khi bỏ liều — không bắt buộc ở BE nhưng UI khuyến khích chọn. */
export function SkipReasonModal({ open, productName, confirmLoading, onCancel, onConfirm }: Props) {
  const { t } = useTranslation();
  const [reason, setReason] = useState<MedSkipReasonCode>('forgot');

  return (
    <Modal
      open={open}
      title={t('reminders.skipReasonTitle')}
      okText={t('reminders.skipReasonConfirm')}
      cancelText={t('common.cancel')}
      confirmLoading={confirmLoading}
      onCancel={onCancel}
      onOk={() => onConfirm(reason)}
      destroyOnClose
      afterOpenChange={(visible) => {
        if (visible) setReason('forgot');
      }}
    >
      {productName ? (
        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12 }}>
          {productName}
        </Typography.Text>
      ) : null}
      <Typography.Paragraph style={{ marginBottom: 12 }}>
        {t('reminders.skipReasonHint')}
      </Typography.Paragraph>
      <Radio.Group
        value={reason}
        onChange={(e) => setReason(e.target.value as MedSkipReasonCode)}
        style={{ width: '100%' }}
      >
        <Space direction="vertical" style={{ width: '100%' }}>
          {MED_SKIP_REASON_OPTIONS.map((opt) => (
            <Radio key={opt.value} value={opt.value}>
              {t(opt.labelKey)}
            </Radio>
          ))}
        </Space>
      </Radio.Group>
    </Modal>
  );
}
