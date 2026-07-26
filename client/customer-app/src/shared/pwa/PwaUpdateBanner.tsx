import { useEffect, useState } from 'react';
import { Button } from 'antd';
import { useTranslation } from 'react-i18next';
import { applyPwaUpdate, subscribePwaNeedRefresh } from '@/shared/push/sw-registration';

/** Soft prompt khi có bản PWA mới — tránh reload cứng giữa phiên. */
export function PwaUpdateBanner() {
  const { t } = useTranslation();
  const [visible, setVisible] = useState(false);

  useEffect(() => subscribePwaNeedRefresh(() => setVisible(true)), []);

  if (!visible) return null;

  return (
    <div
      role="status"
      style={{
        position: 'fixed',
        left: 12,
        right: 12,
        bottom: 'calc(12px + env(safe-area-inset-bottom, 0px))',
        zIndex: 1100,
        display: 'flex',
        gap: 10,
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '10px 12px',
        borderRadius: 12,
        background: '#0f766e',
        color: '#fff',
        boxShadow: '0 8px 24px rgba(15, 23, 42, 0.18)',
      }}
    >
      <span style={{ fontSize: 13, lineHeight: 1.35 }}>{t('pwa.updateAvailable')}</span>
      <Button
        size="small"
        type="default"
        onClick={() => applyPwaUpdate()}
        style={{ flexShrink: 0, fontWeight: 600 }}
      >
        {t('pwa.reload')}
      </Button>
    </div>
  );
}
