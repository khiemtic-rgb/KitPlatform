import { Alert, Button } from 'antd';
import { useTranslation } from 'react-i18next';
import { useApiHealth } from '@/shared/api/useApiHealth';

export function ApiHealthBanner() {
  const { t } = useTranslation('common', { keyPrefix: 'apiHealth' });
  const { online, checking, recheck } = useApiHealth();
  if (online) return null;

  return (
    <Alert
      type="error"
      showIcon
      banner
      message={t('message')}
      description={t('description')}
      action={
        <Button size="small" loading={checking} onClick={() => void recheck()}>
          {t('recheck')}
        </Button>
      }
    />
  );
}
