import { useState } from 'react';
import { Button, Modal } from 'antd';
import { useTranslation } from 'react-i18next';
import { usePwaInstallContext } from '@/shared/pwa/PwaInstallProvider';

/** Gợi ý nhẹ: cài app ra màn hình chính (sau vài lần mở, có thể tắt). */
export function PwaInstallBanner() {
  const { t } = useTranslation();
  const { softBannerEligible, canNativeInstall, showIosGuide, install, dismiss } =
    usePwaInstallContext();
  const [iosOpen, setIosOpen] = useState(false);

  if (!softBannerEligible) return null;

  const onPrimary = () => {
    if (canNativeInstall) {
      void install();
      return;
    }
    if (showIosGuide) setIosOpen(true);
  };

  return (
    <>
      <div
        role="status"
        style={{
          position: 'fixed',
          left: 12,
          right: 12,
          bottom: 'calc(64px + env(safe-area-inset-bottom, 0px))',
          zIndex: 1090,
          display: 'flex',
          gap: 10,
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '10px 12px',
          borderRadius: 12,
          background: '#0f172a',
          color: '#fff',
          boxShadow: '0 8px 24px rgba(15, 23, 42, 0.18)',
        }}
      >
        <span style={{ fontSize: 13, lineHeight: 1.35 }}>{t('pwa.installBanner')}</span>
        <div style={{ display: 'flex', gap: 6, flexShrink: 0 }}>
          <Button size="small" type="text" onClick={dismiss} style={{ color: '#cbd5e1' }}>
            {t('pwa.later')}
          </Button>
          <Button size="small" type="primary" onClick={onPrimary}>
            {t('pwa.install')}
          </Button>
        </div>
      </div>

      <Modal
        open={iosOpen}
        title={t('pwa.iosTitle')}
        onCancel={() => setIosOpen(false)}
        footer={
          <Button type="primary" onClick={() => setIosOpen(false)}>
            {t('common.close')}
          </Button>
        }
        centered
      >
        <ol style={{ margin: 0, paddingLeft: 18, lineHeight: 1.6 }}>
          <li>{t('pwa.iosStep1')}</li>
          <li>{t('pwa.iosStep2')}</li>
          <li>{t('pwa.iosStep3')}</li>
        </ol>
      </Modal>
    </>
  );
}
