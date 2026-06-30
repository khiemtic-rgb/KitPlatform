import { Space, Tag, Typography } from 'antd';
import { CheckCircleOutlined, SendOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import {
  CUSTOMER_DRAFT_ORDER_STATUS,
  type CustomerDraftOrder,
} from '@/shared/api/customer-draft-orders.api';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';

const STATUS_COLOR: Record<number, string> = {
  [CUSTOMER_DRAFT_ORDER_STATUS.Draft]: 'default',
  [CUSTOMER_DRAFT_ORDER_STATUS.Sent]: 'processing',
  [CUSTOMER_DRAFT_ORDER_STATUS.Confirmed]: 'success',
  [CUSTOMER_DRAFT_ORDER_STATUS.Completed]: 'green',
  [CUSTOMER_DRAFT_ORDER_STATUS.Cancelled]: 'error',
  [CUSTOMER_DRAFT_ORDER_STATUS.Expired]: 'warning',
};

interface CustomerDraftOrderStatusBarProps {
  draft: Pick<
    CustomerDraftOrder,
    'draftNumber' | 'status' | 'confirmedAt' | 'sentAt' | 'totalAmount'
  > | null;
}

export function CustomerDraftOrderStatusBar({ draft }: CustomerDraftOrderStatusBarProps) {
  const { t } = useTranslation('sales');
  const { customerDraftStatusLabel } = useSalesEnums();

  if (!draft) return null;

  const isConfirmed = draft.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed;

  return (
    <div
      style={{
        marginBottom: 12,
        padding: '10px 12px',
        borderRadius: 8,
        background: isConfirmed ? '#ecfdf5' : '#f0f9ff',
        border: `1px solid ${isConfirmed ? '#99f6e4' : '#bae6fd'}`,
      }}
    >
      <Space wrap size={[8, 4]}>
        <Typography.Text strong style={{ fontSize: 13 }}>
          {t('pos.customerDraft.barTitle')}
        </Typography.Text>
        <Tag>{draft.draftNumber}</Tag>
        <Tag color={STATUS_COLOR[draft.status] ?? 'default'} icon={isConfirmed ? <CheckCircleOutlined /> : <SendOutlined />}>
          {customerDraftStatusLabel(draft.status)}
        </Tag>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {draft.totalAmount.toLocaleString()}đ
        </Typography.Text>
        {draft.confirmedAt ? (
          <Typography.Text type="success" style={{ fontSize: 12 }}>
            {t('pos.customerDraft.confirmedAt', { time: dayjs(draft.confirmedAt).format('DD/MM HH:mm') })}
          </Typography.Text>
        ) : draft.sentAt ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('pos.customerDraft.sentAt', { time: dayjs(draft.sentAt).format('DD/MM HH:mm') })}
          </Typography.Text>
        ) : null}
      </Space>
    </div>
  );
}
