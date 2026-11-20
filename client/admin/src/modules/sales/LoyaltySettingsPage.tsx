import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  App,
  Button,
  Card,
  Form,
  Input,
  InputNumber,
  Space,
  Switch,
  Table,
  Typography,
} from 'antd';
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import { fetchLoyaltySettings, saveLoyaltySettings } from '@/shared/api/loyalty.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  DEFAULT_LOYALTY_PROGRAM,
  type LoyaltyProgramAdmin,
  type LoyaltyTierAdmin,
} from '@/shared/api/loyalty.types';
import { useHasPermission } from '@/shared/auth/usePermission';
import {
  formatDisplayMoney,
  moneyInputNumberPropsAllowZero,
  moneyInputNumberPropsAllowZeroSuffix,
  moneyInputNumberStyle,
  percentInputNumberProps,
} from '@/shared/utils/money';

type LoyaltyForm = {
  loyaltyEnabled: boolean;
  program: LoyaltyProgramAdmin;
};

export function LoyaltySettingsPage() {
  const { t } = useTranslation('sales', { keyPrefix: 'loyaltySettings' });
  const { message } = App.useApp();
  const canWrite = useHasPermission('sales.write');
  const [form] = Form.useForm<LoyaltyForm>();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const programId = Form.useWatch(['program', 'id'], form);
  const pointsPerAmount = Form.useWatch(['program', 'pointsPerAmount'], form) ?? 10000;
  const amountPerPoint = Form.useWatch(['program', 'amountPerPoint'], form) ?? 100;
  const maxRedeemPercent = Form.useWatch(['program', 'maxRedeemPercent'], form) ?? 5;

  useEffect(() => {
    void (async () => {
      setLoading(true);
      try {
        const settings = await fetchLoyaltySettings();
        form.setFieldsValue({
          loyaltyEnabled: settings.loyaltyEnabled,
          program: normalizeProgramForForm(settings.program ?? DEFAULT_LOYALTY_PROGRAM),
        });
      } catch (error) {
        message.error(apiErrorMessage(error, t('messages.loadFailed')));
      } finally {
        setLoading(false);
      }
    })();
  }, [form, message, t]);

  const onSave = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      const saved = await saveLoyaltySettings({
        loyaltyEnabled: values.loyaltyEnabled,
        program: {
          ...values.program,
          status: values.loyaltyEnabled ? 1 : 0,
          amountPerPoint: values.program.amountPerPoint,
          programCode: values.program.programCode.trim(),
          programName: values.program.programName.trim(),
          tiers: values.program.tiers.map((tier, index) => ({
            ...tier,
            tierCode: tier.tierCode.trim(),
            tierName: tier.tierName.trim(),
            sortOrder: tier.sortOrder || index + 1,
          })),
        },
      });
      form.setFieldsValue({
        loyaltyEnabled: saved.loyaltyEnabled,
        program: normalizeProgramForForm(saved.program ?? DEFAULT_LOYALTY_PROGRAM),
      });
      message.success(t('messages.saveSuccess'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  const redeemExampleAmount = formatDisplayMoney((amountPerPoint > 0 ? amountPerPoint : 100) * 100);
  const redeemExampleOrder = formatDisplayMoney(50_000);
  const redeemPercent = maxRedeemPercent >= 0 && maxRedeemPercent <= 100 ? maxRedeemPercent : 5;
  const redeemExampleMax = formatDisplayMoney(Math.round((50_000 * redeemPercent) / 100));

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Card title={t('title')} loading={loading}>
        <Form form={form} layout="vertical" disabled={!canWrite} style={{ maxWidth: 720 }}>
          <Form.Item name="loyaltyEnabled" label={t('loyaltyEnabled')} valuePropName="checked">
            <Switch checkedChildren={t('switchOn')} unCheckedChildren={t('switchOff')} />
          </Form.Item>

          <Typography.Title level={5} style={{ marginTop: 8 }}>
            {t('programSection')}
          </Typography.Title>

          <Form.Item name={['program', 'id']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={['program', 'status']} hidden>
            <InputNumber />
          </Form.Item>

          <Space wrap style={{ width: '100%' }}>
            <Form.Item
              name={['program', 'programCode']}
              label={t('programCode')}
              rules={[{ required: true, message: t('enterCode') }]}
              style={{ minWidth: 200 }}
            >
              <Input disabled={Boolean(programId)} />
            </Form.Item>
            <Form.Item
              name={['program', 'programName']}
              label={t('programName')}
              rules={[{ required: true, message: t('enterName') }]}
              style={{ flex: 1, minWidth: 240 }}
            >
              <Input />
            </Form.Item>
          </Space>

          <Space align="start" wrap style={{ width: '100%' }}>
            <Form.Item
              name={['program', 'pointsPerAmount']}
              label={t('pointsPerAmount')}
              rules={[{ required: true, type: 'number', min: 1 }]}
            >
              <InputNumber
                {...moneyInputNumberPropsAllowZeroSuffix}
                min={1}
                style={{ ...moneyInputNumberStyle, width: 200 }}
              />
            </Form.Item>
            <Typography.Text style={{ marginTop: 30, fontSize: 18 }}>→</Typography.Text>
            <Form.Item label={t('pointsEarned')}>
              <Input value={t('onePoint')} disabled style={{ width: 120, textAlign: 'center' }} />
            </Form.Item>
          </Space>

          <Form.Item
            name={['program', 'amountPerPoint']}
            label={t('amountPerPoint')}
            rules={[{ required: true, type: 'number', min: 1, message: t('enterPointValue') }]}
          >
            <InputNumber
              {...moneyInputNumberPropsAllowZeroSuffix}
              min={1}
              style={{ ...moneyInputNumberStyle, width: 200 }}
            />
          </Form.Item>

          <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
            {t('earnRuleHint', {
              amount: formatDisplayMoney(pointsPerAmount),
              pointValue: formatDisplayMoney(amountPerPoint > 0 ? amountPerPoint : 0),
            })}
          </Typography.Paragraph>

          <Form.Item
            name={['program', 'maxRedeemPercent']}
            label={t('maxRedeemPercent')}
            rules={[{ required: true, type: 'number', min: 0, max: 100 }]}
          >
            <InputNumber
              {...percentInputNumberProps}
              min={0}
              max={100}
              style={{ ...moneyInputNumberStyle, width: 160 }}
            />
          </Form.Item>
          <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
            {t('redeemExampleHint', {
              pointsValue: redeemExampleAmount,
              orderAmount: redeemExampleOrder,
              percent: redeemPercent,
              maxDiscount: redeemExampleMax,
            })}
          </Typography.Paragraph>

          <Typography.Title level={5}>{t('tiersSection')}</Typography.Title>

          <Form.List name={['program', 'tiers']}>
            {(fields, { add, remove }) => (
              <>
                <Table
                  size="small"
                  pagination={false}
                  rowKey="key"
                  dataSource={fields}
                  columns={[
                    {
                      title: t('tierColumns.code'),
                      width: 110,
                      render: (_, field) => (
                        <>
                          <Form.Item name={[field.name, 'id']} hidden>
                            <Input />
                          </Form.Item>
                          <Form.Item
                            name={[field.name, 'tierCode']}
                            rules={[{ required: true, message: t('enterCode') }]}
                            style={{ marginBottom: 0 }}
                          >
                            <Input placeholder={t('tierCodePlaceholder')} />
                          </Form.Item>
                        </>
                      ),
                    },
                    {
                      title: t('tierColumns.name'),
                      render: (_, field) => (
                        <Form.Item
                          name={[field.name, 'tierName']}
                          rules={[{ required: true, message: t('enterName') }]}
                          style={{ marginBottom: 0 }}
                        >
                          <Input placeholder={t('tierNamePlaceholder')} />
                        </Form.Item>
                      ),
                    },
                    {
                      title: t('tierColumns.minPoints'),
                      width: 110,
                      render: (_, field) => (
                        <Form.Item
                          name={[field.name, 'minPoints']}
                          rules={[{ required: true, type: 'number', min: 0 }]}
                          style={{ marginBottom: 0 }}
                        >
                          <InputNumber
                            {...moneyInputNumberPropsAllowZero}
                            style={{ ...moneyInputNumberStyle, width: '100%' }}
                          />
                        </Form.Item>
                      ),
                    },
                    {
                      title: t('tierColumns.discountPercent'),
                      width: 100,
                      render: (_, field) => (
                        <Form.Item
                          name={[field.name, 'discountPercent']}
                          rules={[{ required: true, type: 'number', min: 0, max: 100 }]}
                          style={{ marginBottom: 0 }}
                        >
                          <InputNumber
                            {...percentInputNumberProps}
                            style={{ ...moneyInputNumberStyle, width: '100%' }}
                          />
                        </Form.Item>
                      ),
                    },
                    {
                      title: '',
                      width: 48,
                      render: (_, field) =>
                        fields.length > 1 ? (
                          <Button
                            type="text"
                            danger
                            icon={<MinusCircleOutlined />}
                            onClick={() => remove(field.name)}
                          />
                        ) : null,
                    },
                  ]}
                />
                <Button
                  type="dashed"
                  onClick={() =>
                    add({
                      tierCode: '',
                      tierName: '',
                      minPoints: 0,
                      discountPercent: 0,
                      sortOrder: fields.length + 1,
                    } satisfies LoyaltyTierAdmin)
                  }
                  icon={<PlusOutlined />}
                  style={{ marginTop: 12 }}
                >
                  {t('addTier')}
                </Button>
              </>
            )}
          </Form.List>

          {canWrite ? (
            <Button type="primary" loading={saving} onClick={() => void onSave()} style={{ marginTop: 16 }}>
              {t('save')}
            </Button>
          ) : null}
        </Form>
      </Card>
    </Space>
  );
}

function normalizeProgramForForm(program: LoyaltyProgramAdmin): LoyaltyProgramAdmin {
  const pointsPerAmount =
    program.pointsPerAmount > 0 ? program.pointsPerAmount : DEFAULT_LOYALTY_PROGRAM.pointsPerAmount;
  const amountPerPoint =
    program.amountPerPoint > 0 ? program.amountPerPoint : DEFAULT_LOYALTY_PROGRAM.amountPerPoint;
  return {
    ...program,
    pointsPerAmount,
    amountPerPoint,
    maxRedeemPercent:
      program.maxRedeemPercent >= 0 ? program.maxRedeemPercent : DEFAULT_LOYALTY_PROGRAM.maxRedeemPercent,
  };
}
