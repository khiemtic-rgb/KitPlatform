import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  App,
  Button,
  Card,
  Col,
  Divider,
  Form,
  Input,
  InputNumber,
  Row,
  Select,
  Space,
  Switch,
  Typography,
} from 'antd';
import {
  KeyOutlined,
  LoginOutlined,
  PlusOutlined,
  ShopOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons';
import { Link } from 'react-router-dom';
import { createPlatformTenant, fetchPlatformSetupStatus } from '@/shared/api/platform.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { CreatePlatformBranchRequest } from '@/shared/api/platform.types';
import {
  APP_BRAND,
  loadStoredPlatformKey,
  saveStoredPlatformKey,
  saveStoredTenantCode,
} from '@/shared/config/app-brand';

type AdditionalBranchFormValues = {
  branchCode: string;
  branchName: string;
  branchAddress?: string;
  branchPhone?: string;
  warehouseCode: string;
  warehouseName: string;
};

type SetupFormValues = {
  platformKey?: string;
  vertical: 'pharmacy' | 'clinic' | 'family';
  tenantCode: string;
  tenantName: string;
  branchCode: string;
  branchName: string;
  branchAddress?: string;
  branchPhone?: string;
  warehouseCode: string;
  warehouseName: string;
  additionalBranches?: AdditionalBranchFormValues[];
  adminUsername: string;
  adminEmail: string;
  adminFullName: string;
  adminPassword: string;
  adminPasswordConfirm: string;
  loyaltyEnabled: boolean;
};

function padBranchCode(index: number): string {
  return `CN${String(index).padStart(2, '0')}`;
}

function buildAdditionalBranchRows(
  count: number,
  counterName: (n: number) => string,
  warehouseBranchName: (code: string) => string,
): AdditionalBranchFormValues[] {
  return Array.from({ length: count }, (_, i) => {
    const n = i + 2;
    const code = padBranchCode(n);
    return {
      branchCode: code,
      branchName: counterName(n),
      warehouseCode: `WH_${code}`,
      warehouseName: warehouseBranchName(code),
    };
  });
}

export function SetupPage() {
  const { t } = useTranslation('auth', { keyPrefix: 'setup' });
  const { t: tc } = useTranslation('common');
  const { message } = App.useApp();
  const [form] = Form.useForm<SetupFormValues>();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [tenantsCount, setTenantsCount] = useState(0);
  const [provisioningKeyRequired, setProvisioningKeyRequired] = useState(false);
  const [extraBranchCount, setExtraBranchCount] = useState(0);
  const vertical = Form.useWatch('vertical', form) as SetupFormValues['vertical'] | undefined;
  const isFamily = vertical === 'family';
  const isClinic = vertical === 'clinic';

  const applyVerticalDefaults = (next: SetupFormValues['vertical']) => {
    if (next === 'family') {
      form.setFieldsValue({
        branchCode: 'HOME',
        branchName: t('defaults.familyHomeName'),
        warehouseCode: 'WH_HOME',
        warehouseName: t('defaults.familyWarehouseName'),
        loyaltyEnabled: false,
        additionalBranches: [],
        adminFullName: t('defaults.familyAdminFullName'),
      });
      setExtraBranchCount(0);
      return;
    }
    if (next === 'clinic') {
      form.setFieldsValue({
        branchCode: 'CN01',
        branchName: t('defaults.clinicBranchName'),
        warehouseCode: 'WH_MAIN',
        warehouseName: t('defaults.warehouseName'),
        loyaltyEnabled: false,
        adminFullName: t('defaults.adminFullName'),
      });
      return;
    }
    form.setFieldsValue({
      branchCode: 'CN01',
      branchName: t('defaults.branchName'),
      warehouseCode: 'WH_MAIN',
      warehouseName: t('defaults.warehouseName'),
      loyaltyEnabled: true,
      adminFullName: t('defaults.adminFullName'),
    });
  };

  const reload = useCallback(async () => {
    setLoading(true);
    try {
      const status = await fetchPlatformSetupStatus();
      setTenantsCount(status.tenantsCount);
      setProvisioningKeyRequired(status.provisioningKeyRequired);

      const storedKey =
        loadStoredPlatformKey() ||
        (import.meta.env.DEV ? 'dev-platform-key-for-local-setup-only' : '');
      if (storedKey) {
        form.setFieldValue('platformKey', storedKey);
        saveStoredPlatformKey(storedKey);
      }
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [form, message, t]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const onFinish = async (values: SetupFormValues) => {
    if (values.adminPassword !== values.adminPasswordConfirm) {
      message.error(t('messages.passwordMismatch'));
      return;
    }

    setSaving(true);
    try {
      const platformKey = values.platformKey?.trim();
      if (platformKey) {
        saveStoredPlatformKey(platformKey);
      }

      const additionalBranches = (values.additionalBranches ?? [])
        .filter((b) => b.branchCode?.trim() && b.branchName?.trim())
        .map(
          (b): CreatePlatformBranchRequest => ({
            branchCode: b.branchCode.trim(),
            branchName: b.branchName.trim(),
            branchAddress: b.branchAddress?.trim(),
            branchPhone: b.branchPhone?.trim(),
            warehouseCode: b.warehouseCode.trim(),
            warehouseName: b.warehouseName.trim(),
          }),
        );

      const created = await createPlatformTenant(
        {
          vertical: values.vertical,
          tenantCode: values.tenantCode.trim(),
          tenantName: values.tenantName.trim(),
          branchCode: values.branchCode.trim(),
          branchName: values.branchName.trim(),
          branchAddress: values.branchAddress?.trim(),
          branchPhone: values.branchPhone?.trim(),
          warehouseCode: values.warehouseCode.trim(),
          warehouseName: values.warehouseName.trim(),
          adminUsername: values.adminUsername.trim(),
          adminEmail: values.adminEmail.trim(),
          adminFullName: values.adminFullName.trim(),
          adminPassword: values.adminPassword,
          loyaltyEnabled: values.vertical === 'family' ? false : values.loyaltyEnabled,
          additionalBranches:
            values.vertical === 'family'
              ? undefined
              : additionalBranches.length > 0
                ? additionalBranches
                : undefined,
        },
        platformKey,
      );

      saveStoredTenantCode(created.tenantCode);
      message.success(
        t('messages.created', {
          code: created.tenantCode,
          branches:
            created.branchCount > 1
              ? t('messages.createdBranches', { count: created.branchCount })
              : '',
        }),
      );
      form.setFieldsValue({
        adminPassword: '',
        adminPasswordConfirm: '',
        additionalBranches: [],
      });
      setExtraBranchCount(0);
      await reload();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.createFailed')));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      style={{
        minHeight: '100vh',
        background: 'linear-gradient(135deg, #0f766e 0%, #134e4a 50%, #115e59 100%)',
        padding: 24,
      }}
    >
      <div style={{ maxWidth: 960, margin: '0 auto' }}>
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <div style={{ textAlign: 'center', color: '#ecfdf5' }}>
            <Typography.Title level={2} style={{ color: '#ecfdf5', marginBottom: 4 }}>
              {APP_BRAND}
            </Typography.Title>
            <Typography.Text style={{ color: '#99f6e4' }}>
              {t('platformSubtitle')}
            </Typography.Text>
          </div>

          <Card loading={loading}>
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
              {tenantsCount === 0 ? (
                <Alert
                  type="info"
                  showIcon
                  message={t('firstDeploy.title')}
                  description={t('firstDeploy.description')}
                />
              ) : (
                <Alert
                  type="warning"
                  showIcon
                  message={t('existingTenants.title', { count: tenantsCount })}
                  description={t('existingTenants.description')}
                  action={
                    <Link to="/setup/organizations">
                      <Button size="small" icon={<UnorderedListOutlined />}>
                        {t('organizations.openList')}
                      </Button>
                    </Link>
                  }
                />
              )}

              {tenantsCount > 0 ? (
                <Card size="small" type="inner" title={t('existingTenantsTitle')}>
                  <Space wrap>
                    <Typography.Text type="secondary">
                      {t('organizations.listHint', { count: tenantsCount })}
                    </Typography.Text>
                    <Link to="/setup/organizations">
                      <Button type="primary" icon={<UnorderedListOutlined />}>
                        {t('organizations.openList')}
                      </Button>
                    </Link>
                  </Space>
                </Card>
              ) : null}

              <Form
                form={form}
                layout="vertical"
                onFinish={onFinish}
                initialValues={{
                  vertical: 'pharmacy',
                  branchCode: 'CN01',
                  branchName: t('defaults.branchName'),
                  warehouseCode: 'WH_MAIN',
                  warehouseName: t('defaults.warehouseName'),
                  adminUsername: 'admin',
                  adminFullName: t('defaults.adminFullName'),
                  loyaltyEnabled: true,
                  platformKey: loadStoredPlatformKey(),
                }}
              >
                {provisioningKeyRequired ? (
                  <Form.Item
                    name="platformKey"
                    label={t('platformKey')}
                    tooltip={t('platformKeyTooltip')}
                    rules={[{ required: true, message: t('platformKeyRequired') }]}
                  >
                    <Input.Password
                      prefix={<KeyOutlined />}
                      placeholder={t('platformKeyPlaceholder')}
                      onBlur={() => {
                        const key = form.getFieldValue('platformKey')?.trim();
                        if (key) saveStoredPlatformKey(key);
                      }}
                    />
                  </Form.Item>
                ) : null}

                <Typography.Title level={5}>
                  <ShopOutlined /> {t('createNewTitle')}
                </Typography.Title>

                <Form.Item
                  name="vertical"
                  label={t('vertical')}
                  rules={[{ required: true, message: t('verticalRequired') }]}
                  tooltip={t('verticalTooltip')}
                >
                  <Select
                    options={[
                      { value: 'pharmacy', label: t('verticals.pharmacy') },
                      { value: 'clinic', label: t('verticals.clinic') },
                      { value: 'family', label: t('verticals.family') },
                    ]}
                    onChange={(v) => applyVerticalDefaults(v as SetupFormValues['vertical'])}
                  />
                </Form.Item>

                <Alert
                  type="info"
                  showIcon
                  style={{ marginBottom: 16 }}
                  message={
                    isFamily
                      ? t('verticalHint.family')
                      : isClinic
                        ? t('verticalHint.clinic')
                        : t('verticalHint.pharmacy')
                  }
                />

                <Row gutter={16}>
                  <Col xs={24} md={8}>
                    <Form.Item
                      name="tenantCode"
                      label={
                        isFamily
                          ? t('tenantCodeFamily')
                          : isClinic
                            ? t('tenantCodeClinic')
                            : t('tenantCode')
                      }
                      rules={[{ required: true, message: t('tenantCodeRequired') }]}
                      tooltip={t('tenantCodeTooltip')}
                    >
                      <Input
                        placeholder={isFamily ? 'FM_A' : isClinic ? 'PK_A' : 'NT_A'}
                        style={{ textTransform: 'uppercase' }}
                      />
                    </Form.Item>
                  </Col>
                  <Col xs={24} md={16}>
                    <Form.Item
                      name="tenantName"
                      label={
                        isFamily
                          ? t('tenantNameFamily')
                          : isClinic
                            ? t('tenantNameClinic')
                            : t('tenantName')
                      }
                      rules={[{ required: true, message: t('tenantNameRequired') }]}
                    >
                      <Input
                        placeholder={
                          isFamily
                            ? t('tenantNamePlaceholderFamily')
                            : isClinic
                              ? t('tenantNamePlaceholderClinic')
                              : t('tenantNamePlaceholder')
                        }
                      />
                    </Form.Item>
                  </Col>
                </Row>

                <Row gutter={16}>
                  <Col xs={24} md={8}>
                    <Form.Item
                      name="branchCode"
                      label={isFamily ? t('branchCodeFamily') : t('branchCode')}
                      rules={[{ required: true }]}
                    >
                      <Input />
                    </Form.Item>
                  </Col>
                  <Col xs={24} md={16}>
                    <Form.Item
                      name="branchName"
                      label={isFamily ? t('branchNameFamily') : t('branchName')}
                      rules={[{ required: true }]}
                    >
                      <Input />
                    </Form.Item>
                  </Col>
                </Row>

                <Row gutter={16}>
                  <Col xs={24} md={12}>
                    <Form.Item name="branchAddress" label={t('branchAddress')}>
                      <Input />
                    </Form.Item>
                  </Col>
                  <Col xs={24} md={12}>
                    <Form.Item name="branchPhone" label={t('branchPhone')}>
                      <Input />
                    </Form.Item>
                  </Col>
                </Row>

                {!isFamily ? (
                  <Row gutter={16}>
                    <Col xs={24} md={8}>
                      <Form.Item
                        name="warehouseCode"
                        label={t('warehouseCode')}
                        rules={[{ required: true }]}
                      >
                        <Input />
                      </Form.Item>
                    </Col>
                    <Col xs={24} md={16}>
                      <Form.Item
                        name="warehouseName"
                        label={t('warehouseName')}
                        rules={[{ required: true }]}
                      >
                        <Input />
                      </Form.Item>
                    </Col>
                  </Row>
                ) : (
                  <>
                    <Form.Item name="warehouseCode" hidden>
                      <Input />
                    </Form.Item>
                    <Form.Item name="warehouseName" hidden>
                      <Input />
                    </Form.Item>
                  </>
                )}

                {!isFamily ? (
                  <>
                <Divider orientation="left">{t('chainSection')}</Divider>
                <Alert
                  type="info"
                  showIcon
                  style={{ marginBottom: 12 }}
                  message={t('chainInfo.title')}
                  description={t('chainInfo.description')}
                />
                <Space wrap align="center" style={{ marginBottom: 16 }}>
                  <Typography.Text>{t('addBranches')}</Typography.Text>
                  <InputNumber
                    min={0}
                    max={20}
                    value={extraBranchCount}
                    onChange={(v) => setExtraBranchCount(v ?? 0)}
                  />
                  <Typography.Text>{t('branchCount')}</Typography.Text>
                  <Button
                    icon={<PlusOutlined />}
                    onClick={() => {
                      if (extraBranchCount <= 0) {
                        message.info(t('selectBranchCount'));
                        return;
                      }
                      form.setFieldValue(
                        'additionalBranches',
                        buildAdditionalBranchRows(
                          extraBranchCount,
                          (n) => t('defaults.counterName', { n }),
                          (code) => t('defaults.warehouseBranchName', { code }),
                        ),
                      );
                    }}
                  >
                    {t('addToForm')}
                  </Button>
                </Space>

                <Form.List name="additionalBranches">
                  {(fields, { remove }) => (
                    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                      {fields.map((field, index) => (
                        <Card
                          key={field.key}
                          size="small"
                          title={t('additionalBranch', { index: index + 1 })}
                          extra={
                            <Button type="link" danger onClick={() => remove(field.name)}>
                              {tc('actions.delete')}
                            </Button>
                          }
                        >
                          <Row gutter={16}>
                            <Col xs={24} md={8}>
                              <Form.Item
                                {...field}
                                name={[field.name, 'branchCode']}
                                label={t('branchCode')}
                                rules={[{ required: true }]}
                              >
                                <Input style={{ textTransform: 'uppercase' }} />
                              </Form.Item>
                            </Col>
                            <Col xs={24} md={16}>
                              <Form.Item
                                {...field}
                                name={[field.name, 'branchName']}
                                label={t('branchName')}
                                rules={[{ required: true }]}
                              >
                                <Input />
                              </Form.Item>
                            </Col>
                          </Row>
                          <Row gutter={16}>
                            <Col xs={24} md={12}>
                              <Form.Item {...field} name={[field.name, 'branchAddress']} label={t('branchAddress')}>
                                <Input />
                              </Form.Item>
                            </Col>
                            <Col xs={24} md={12}>
                              <Form.Item {...field} name={[field.name, 'branchPhone']} label={t('branchPhone')}>
                                <Input />
                              </Form.Item>
                            </Col>
                          </Row>
                          <Row gutter={16}>
                            <Col xs={24} md={8}>
                              <Form.Item
                                {...field}
                                name={[field.name, 'warehouseCode']}
                                label={t('warehouseCode')}
                                rules={[{ required: true }]}
                              >
                                <Input style={{ textTransform: 'uppercase' }} />
                              </Form.Item>
                            </Col>
                            <Col xs={24} md={16}>
                              <Form.Item
                                {...field}
                                name={[field.name, 'warehouseName']}
                                label={t('warehouseName')}
                                rules={[{ required: true }]}
                              >
                                <Input />
                              </Form.Item>
                            </Col>
                          </Row>
                        </Card>
                      ))}
                    </Space>
                  )}
                </Form.List>
                  </>
                ) : null}

                <Divider orientation="left">{t('adminSection')}</Divider>

                <Row gutter={16}>
                  <Col xs={24} md={8}>
                    <Form.Item name="adminUsername" label={t('adminUsername')} rules={[{ required: true }]}>
                      <Input />
                    </Form.Item>
                  </Col>
                  <Col xs={24} md={16}>
                    <Form.Item
                      name="adminEmail"
                      label={t('adminEmail')}
                      rules={[{ required: true, type: 'email' }]}
                    >
                      <Input />
                    </Form.Item>
                  </Col>
                </Row>

                <Form.Item name="adminFullName" label={t('adminFullName')}>
                  <Input />
                </Form.Item>

                <Row gutter={16}>
                  <Col xs={24} md={12}>
                    <Form.Item
                      name="adminPassword"
                      label={t('adminPassword')}
                      rules={[{ required: true, min: 8, message: t('adminPasswordMin') }]}
                    >
                      <Input.Password />
                    </Form.Item>
                  </Col>
                  <Col xs={24} md={12}>
                    <Form.Item
                      name="adminPasswordConfirm"
                      label={t('adminPasswordConfirm')}
                      rules={[{ required: true }]}
                    >
                      <Input.Password />
                    </Form.Item>
                  </Col>
                </Row>

                {!isFamily ? (
                  <Form.Item name="loyaltyEnabled" label={t('loyaltyEnabled')} valuePropName="checked">
                    <Switch />
                  </Form.Item>
                ) : null}

                <Space wrap>
                  <Button type="primary" htmlType="submit" loading={saving} icon={<ShopOutlined />}>
                    {isFamily
                      ? t('createTenantFamily')
                      : isClinic
                        ? t('createTenantClinic')
                        : t('createTenant')}
                  </Button>
                  <Link to="/login">
                    <Button icon={<LoginOutlined />}>{t('goToLogin')}</Button>
                  </Link>
                </Space>
              </Form>
            </Space>
          </Card>
        </Space>
      </div>
    </div>
  );
}
