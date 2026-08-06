import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  App,
  Button,
  Card,
  ColorPicker,
  Form,
  Input,
  Space,
  Switch,
  Typography,
} from 'antd';
import { GlobalOutlined } from '@ant-design/icons';
import {
  fetchPharmacyStorefrontProfile,
  updatePharmacyStorefrontProfile,
} from '@/shared/api/storefront.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';

type FormValues = {
  slug: string;
  isPublished: boolean;
  brandName: string;
  shortName: string;
  logoText: string;
  primaryColor: string;
  accentColor: string;
  address: string;
  hours: string;
  phone: string;
  email: string;
  headline: string;
  subhead: string;
  heroImageUrl: string;
  ctaPrimaryLabel: string;
  ctaPrimaryHref: string;
  ctaSecondaryLabel: string;
  ctaSecondaryHref: string;
  appUrl: string;
  mission: string;
  whyUs: string;
  trustItems: string;
};

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function asString(value: unknown, fallback = ''): string {
  return typeof value === 'string' ? value : fallback;
}

function contentToForm(content: Record<string, unknown>, slug: string, isPublished: boolean): FormValues {
  const brand = asRecord(content.brand);
  const contact = asRecord(content.contact);
  const hero = asRecord(content.hero);
  const ctaPrimary = asRecord(hero.ctaPrimary);
  const ctaSecondary = asRecord(hero.ctaSecondary);
  const appPromo = asRecord(content.appPromo);
  const footer = asRecord(content.footer);
  const whyUs = Array.isArray(content.whyUs)
    ? content.whyUs.map((x) => String(x)).join('\n')
    : '';
  const trustItems = Array.isArray(hero.trustItems)
    ? hero.trustItems
        .map((item) => asString(asRecord(item).label))
        .filter(Boolean)
        .join('\n')
    : '';

  return {
    slug,
    isPublished,
    brandName: asString(brand.name),
    shortName: asString(brand.shortName),
    logoText: asString(brand.logoText, 'NT'),
    primaryColor: asString(brand.primaryColor, '#0d6b5c'),
    accentColor: asString(brand.accentColor, '#148f77'),
    address: asString(contact.address),
    hours: asString(contact.hours),
    phone: asString(contact.phone),
    email: asString(contact.email),
    headline: asString(hero.headline),
    subhead: asString(hero.subhead),
    heroImageUrl: asString(hero.imageUrl),
    ctaPrimaryLabel: asString(ctaPrimary.label, 'Đặt thuốc ngay'),
    ctaPrimaryHref: asString(ctaPrimary.href),
    ctaSecondaryLabel: asString(ctaSecondary.label, 'Tải App Novixa'),
    ctaSecondaryHref: asString(ctaSecondary.href),
    appUrl: asString(appPromo.appUrl),
    mission: asString(footer.mission),
    whyUs,
    trustItems,
  };
}

function formToContent(values: FormValues, previous: Record<string, unknown>): Record<string, unknown> {
  const whyUs = values.whyUs
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);
  const trustItems = values.trustItems
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .map((label) => ({ icon: 'check', label }));

  return {
    ...previous,
    brand: {
      ...asRecord(previous.brand),
      name: values.brandName.trim(),
      shortName: values.shortName.trim(),
      logoText: values.logoText.trim() || 'NT',
      primaryColor: values.primaryColor.trim() || '#0d6b5c',
      accentColor: values.accentColor.trim() || '#148f77',
    },
    contact: {
      ...asRecord(previous.contact),
      address: values.address.trim(),
      hours: values.hours.trim(),
      phone: values.phone.trim(),
      email: values.email.trim(),
    },
    hero: {
      ...asRecord(previous.hero),
      headline: values.headline.trim(),
      subhead: values.subhead.trim(),
      imageUrl: values.heroImageUrl.trim(),
      trustItems,
      ctaPrimary: {
        label: values.ctaPrimaryLabel.trim(),
        href: values.ctaPrimaryHref.trim() || values.appUrl.trim(),
      },
      ctaSecondary: {
        label: values.ctaSecondaryLabel.trim(),
        href: values.ctaSecondaryHref.trim() || values.appUrl.trim(),
      },
    },
    appPromo: {
      ...asRecord(previous.appPromo),
      appUrl: values.appUrl.trim(),
      appStoreUrl: asString(asRecord(previous.appPromo).appStoreUrl, values.appUrl.trim()),
      playStoreUrl: asString(asRecord(previous.appPromo).playStoreUrl, values.appUrl.trim()),
      title: asString(asRecord(previous.appPromo).title, 'Quản lý sức khỏe cả gia đình với App Novixa'),
    },
    whyUs,
    footer: {
      ...asRecord(previous.footer),
      mission: values.mission.trim(),
    },
  };
}

type HexColorPickerProps = {
  value?: string;
  onChange?: (value: string) => void;
  disabled?: boolean;
};

function HexColorPicker({ value, onChange, disabled }: HexColorPickerProps) {
  return (
    <ColorPicker
      disabled={disabled}
      value={value || '#0d6b5c'}
      showText
      format="hex"
      onChange={(color) => onChange?.(color.toHexString())}
    />
  );
}

export function StorefrontSettingsPage() {
  const { t } = useTranslation('system', { keyPrefix: 'storefrontSettings' });
  const { message } = App.useApp();
  const canWrite = useHasPermission('sales.write');
  const [form] = Form.useForm<FormValues>();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [hostHint, setHostHint] = useState('xuanhoa.novixa.vn');
  const contentRef = useRef<Record<string, unknown>>({});
  const loadedRef = useRef(false);

  useEffect(() => {
    if (loadedRef.current) return;
    loadedRef.current = true;

    void (async () => {
      setLoading(true);
      try {
        const profile = await fetchPharmacyStorefrontProfile();
        contentRef.current = profile.content ?? {};
        setHostHint(profile.publicHostHint || `${profile.slug}.novixa.vn`);
        form.setFieldsValue(contentToForm(profile.content ?? {}, profile.slug, profile.isPublished));
      } catch (error) {
        message.error(apiErrorMessage(error, t('loadFailed')));
      } finally {
        setLoading(false);
      }
    })();
  }, [form, message, t]);

  const onSave = async (values: FormValues) => {
    setSaving(true);
    try {
      const content = formToContent(values, contentRef.current);
      const saved = await updatePharmacyStorefrontProfile({
        slug: values.slug.trim().toLowerCase(),
        isPublished: values.isPublished,
        content,
      });
      contentRef.current = saved.content ?? {};
      setHostHint(saved.publicHostHint || `${saved.slug}.novixa.vn`);
      form.setFieldsValue(contentToForm(saved.content ?? {}, saved.slug, saved.isPublished));
      message.success(t('saveSuccess'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%', maxWidth: 820 }}>
      <Alert
        type="info"
        showIcon
        icon={<GlobalOutlined />}
        message={t('introTitle')}
        description={
          <span>
            {t('introBody')}{' '}
            <Typography.Link href={`https://${hostHint}`} target="_blank" rel="noreferrer">
              https://{hostHint}
            </Typography.Link>
          </span>
        }
      />

      <Card title={t('title')} loading={loading}>
        <Form form={form} layout="vertical" disabled={!canWrite} onFinish={(v) => void onSave(v)}>
          <Form.Item name="slug" label={t('slug')} rules={[{ required: true }]}>
            <Input placeholder="xuanhoa" addonBefore="https://" addonAfter=".novixa.vn" />
          </Form.Item>
          <Form.Item name="isPublished" label={t('published')} valuePropName="checked">
            <Switch checkedChildren={t('publishedOn')} unCheckedChildren={t('publishedOff')} />
          </Form.Item>

          <Typography.Title level={5}>{t('sectionBrand')}</Typography.Title>
          <Form.Item name="brandName" label={t('brandName')} rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="shortName" label={t('shortName')}>
            <Input />
          </Form.Item>
          <Form.Item name="logoText" label={t('logoText')}>
            <Input maxLength={4} style={{ maxWidth: 120 }} />
          </Form.Item>
          <Space size="large" wrap>
            <Form.Item name="primaryColor" label={t('primaryColor')}>
              <HexColorPicker />
            </Form.Item>
            <Form.Item name="accentColor" label={t('accentColor')}>
              <HexColorPicker />
            </Form.Item>
          </Space>

          <Typography.Title level={5}>{t('sectionContact')}</Typography.Title>
          <Form.Item name="address" label={t('address')}>
            <Input />
          </Form.Item>
          <Form.Item name="hours" label={t('hours')}>
            <Input />
          </Form.Item>
          <Form.Item name="phone" label={t('phone')}>
            <Input />
          </Form.Item>
          <Form.Item name="email" label={t('email')}>
            <Input type="email" />
          </Form.Item>

          <Typography.Title level={5}>{t('sectionHero')}</Typography.Title>
          <Form.Item name="headline" label={t('headline')}>
            <Input />
          </Form.Item>
          <Form.Item name="subhead" label={t('subhead')}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="heroImageUrl" label={t('heroImageUrl')}>
            <Input placeholder="https://..." />
          </Form.Item>
          <Form.Item name="trustItems" label={t('trustItems')} tooltip={t('trustItemsHint')}>
            <Input.TextArea rows={4} />
          </Form.Item>
          <Form.Item name="ctaPrimaryLabel" label={t('ctaPrimaryLabel')}>
            <Input />
          </Form.Item>
          <Form.Item name="ctaPrimaryHref" label={t('ctaPrimaryHref')}>
            <Input />
          </Form.Item>
          <Form.Item name="ctaSecondaryLabel" label={t('ctaSecondaryLabel')}>
            <Input />
          </Form.Item>
          <Form.Item name="ctaSecondaryHref" label={t('ctaSecondaryHref')}>
            <Input />
          </Form.Item>
          <Form.Item name="appUrl" label={t('appUrl')}>
            <Input placeholder="https://app.novixa.vn/?tenantCode=..." />
          </Form.Item>

          <Typography.Title level={5}>{t('sectionWhy')}</Typography.Title>
          <Form.Item name="whyUs" label={t('whyUs')} tooltip={t('whyUsHint')}>
            <Input.TextArea rows={5} />
          </Form.Item>
          <Form.Item name="mission" label={t('mission')}>
            <Input.TextArea rows={3} />
          </Form.Item>

          {canWrite ? (
            <Button type="primary" htmlType="submit" loading={saving}>
              {t('save')}
            </Button>
          ) : null}
        </Form>
      </Card>
    </Space>
  );
}
