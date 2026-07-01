import { GlobalOutlined } from '@ant-design/icons';
import { Select } from 'antd';
import { useTranslation } from 'react-i18next';
import { ADMIN_LOCALE_STORAGE_KEY, type AdminLocale } from '@/shared/i18n';

const SUPPORTED_LOCALES: AdminLocale[] = ['vi-VN', 'en-US'];

export function AdminLanguageSelect() {
  const { i18n, t } = useTranslation('common', { keyPrefix: 'language' });

  const locale = (SUPPORTED_LOCALES.includes(i18n.language as AdminLocale)
    ? i18n.language
    : 'vi-VN') as AdminLocale;

  return (
    <Select<AdminLocale>
      size="small"
      value={locale}
      aria-label={t('ariaLabel')}
      suffixIcon={<GlobalOutlined />}
      style={{ width: 128 }}
      popupMatchSelectWidth={false}
      options={SUPPORTED_LOCALES.map((code) => ({
        value: code,
        label: t(`locale.${code}`),
      }))}
      onChange={(next) => {
        void i18n.changeLanguage(next);
        localStorage.setItem(ADMIN_LOCALE_STORAGE_KEY, next);
      }}
    />
  );
}
