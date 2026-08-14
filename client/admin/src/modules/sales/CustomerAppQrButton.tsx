import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Modal, Tooltip } from 'antd';
import { QrcodeOutlined } from '@ant-design/icons';
import { CustomerAppLinkQrPanel } from '@/modules/sales/CustomerAppLinkQrPanel';

interface CustomerAppQrButtonProps {
  size?: 'small' | 'middle' | 'large';
  /** Primary label in toolbar vs icon-only with tooltip. */
  showLabel?: boolean;
  type?: 'default' | 'primary' | 'dashed' | 'link' | 'text';
}

/** Nút nhanh: mở QR Novixa Health + kịch bản giới thiệu tại quầy. */
export function CustomerAppQrButton({
  size = 'middle',
  showLabel = true,
  type = 'default',
}: CustomerAppQrButtonProps) {
  const { t } = useTranslation('sales', { keyPrefix: 'receiptSettings.customerAppLinkCard' });
  const [open, setOpen] = useState(false);

  const button = (
    <Button type={type} size={size} icon={<QrcodeOutlined />} onClick={() => setOpen(true)}>
      {showLabel ? t('openButton') : null}
    </Button>
  );

  return (
    <>
      {showLabel ? button : <Tooltip title={t('openButtonTooltip')}>{button}</Tooltip>}
      <Modal
        title={t('modalTitle')}
        open={open}
        onCancel={() => setOpen(false)}
        footer={
          <Button type="primary" onClick={() => setOpen(false)}>
            {t('close')}
          </Button>
        }
        width={600}
        destroyOnClose
        centered
      >
        <CustomerAppLinkQrPanel compact />
      </Modal>
    </>
  );
}
