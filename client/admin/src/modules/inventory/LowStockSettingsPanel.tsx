import { useCallback, useEffect, useState } from 'react';
import { useTranslation, Trans } from 'react-i18next';
import {
  Alert,
  Button,
  Checkbox,
  Collapse,
  InputNumber,
  Space,
  Table,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { SaveOutlined, ThunderboltOutlined } from '@ant-design/icons';
import {
  applyLowStockToAll,
  applyLowStockToCategory,
  fetchLowStockSettings,
  updateCategoryLowStockSetting,
  updateLowStockDefault,
  updateWarehouseLowStockSetting,
} from '@/shared/api/inventory.api';
import type {
  CategoryLowStockSetting,
  LowStockSettings,
  WarehouseLowStockSetting,
} from '@/shared/api/inventory.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { quantityInputNumberProps } from '@/shared/utils/money';

interface LowStockSettingsPanelProps {
  onChanged?: () => void;
}

export function LowStockSettingsPanel({ onChanged }: LowStockSettingsPanelProps) {
  const { t } = useTranslation('inventory', { keyPrefix: 'lowStockSettings' });
  const { t: tc } = useTranslation('common');
  const [settings, setSettings] = useState<LowStockSettings | null>(null);
  const [loading, setLoading] = useState(false);
  const [defaultQty, setDefaultQty] = useState<number | null>(null);
  const [onlyUnset, setOnlyUnset] = useState(true);
  const [categoryDrafts, setCategoryDrafts] = useState<Record<string, number | null>>({});
  const [warehouseDrafts, setWarehouseDrafts] = useState<Record<string, number | null>>({});
  const [savingDefault, setSavingDefault] = useState(false);
  const [applyingAll, setApplyingAll] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await fetchLowStockSettings();
      setSettings(data);
      setDefaultQty(data.defaultMinStockQty ?? null);
      setCategoryDrafts(
        Object.fromEntries(data.categories.map((c) => [c.id, c.minStockQty ?? null])),
      );
      setWarehouseDrafts(
        Object.fromEntries(data.warehouses.map((w) => [w.id, w.minStockQty ?? null])),
      );
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const saveDefault = async () => {
    setSavingDefault(true);
    try {
      const saved = await updateLowStockDefault(defaultQty ?? undefined);
      setDefaultQty(saved ?? null);
      message.success(t('messages.defaultSaveSuccess'));
      await load();
      onChanged?.();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.defaultSaveFailed')));
    } finally {
      setSavingDefault(false);
    }
  };

  const applyAll = async () => {
    setApplyingAll(true);
    try {
      const count = await applyLowStockToAll(onlyUnset, defaultQty ?? undefined);
      message.success(t('messages.applyAllSuccess', { count }));
      onChanged?.();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.applyFailed')));
    } finally {
      setApplyingAll(false);
    }
  };

  const saveCategory = async (row: CategoryLowStockSetting) => {
    const qty = categoryDrafts[row.id];
    try {
      await updateCategoryLowStockSetting(row.id, qty ?? undefined);
      message.success(t('messages.categorySaveSuccess', { name: row.categoryName }));
      await load();
      onChanged?.();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.categorySaveFailed')));
    }
  };

  const applyCategory = async (row: CategoryLowStockSetting) => {
    try {
      const count = await applyLowStockToCategory(row.id, onlyUnset);
      message.success(t('messages.categoryApplySuccess', { count, name: row.categoryName }));
      onChanged?.();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.applyFailed')));
    }
  };

  const saveWarehouse = async (row: WarehouseLowStockSetting) => {
    const qty = warehouseDrafts[row.id];
    try {
      await updateWarehouseLowStockSetting(row.id, qty ?? undefined);
      message.success(t('messages.warehouseSaveSuccess', { name: row.warehouseName }));
      await load();
      onChanged?.();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.warehouseSaveFailed')));
    }
  };

  const fallback = settings?.systemFallbackQty ?? 10;

  const categoryColumns: ColumnsType<CategoryLowStockSetting> = [
    { title: t('columns.code'), dataIndex: 'categoryCode', width: 90 },
    { title: t('columns.category'), dataIndex: 'categoryName' },
    {
      title: t('columns.threshold'),
      key: 'minStockQty',
      width: 130,
      render: (_, row) => (
        <InputNumber
          {...quantityInputNumberProps}
          min={0}
          placeholder={`${fallback}`}
          value={categoryDrafts[row.id]}
          onChange={(v) => setCategoryDrafts((prev) => ({ ...prev, [row.id]: v }))}
        />
      ),
    },
    {
      title: t('columns.products'),
      dataIndex: 'productCount',
      width: 60,
      align: 'right',
    },
    {
      title: '',
      key: 'actions',
      width: 180,
      render: (_, row) => (
        <Space size="small">
          <Button size="small" icon={<SaveOutlined />} onClick={() => void saveCategory(row)}>
            {tc('actions.save')}
          </Button>
          <Button size="small" icon={<ThunderboltOutlined />} onClick={() => void applyCategory(row)}>
            {t('columns.applyToProducts')}
          </Button>
        </Space>
      ),
    },
  ];

  const warehouseColumns: ColumnsType<WarehouseLowStockSetting> = [
    { title: t('columns.warehouseCode'), dataIndex: 'warehouseCode', width: 100 },
    {
      title: t('columns.warehouseBranch'),
      key: 'name',
      render: (_, row) => (
        <Space direction="vertical" size={0}>
          <span>{row.warehouseName}</span>
          {row.branchName ? (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {row.branchName}
            </Typography.Text>
          ) : null}
        </Space>
      ),
    },
    {
      title: t('columns.threshold'),
      key: 'minStockQty',
      width: 130,
      render: (_, row) => (
        <InputNumber
          {...quantityInputNumberProps}
          min={0}
          placeholder={`${fallback}`}
          value={warehouseDrafts[row.id]}
          onChange={(v) => setWarehouseDrafts((prev) => ({ ...prev, [row.id]: v }))}
        />
      ),
    },
    {
      title: '',
      key: 'actions',
      width: 90,
      render: (_, row) => (
        <Button size="small" icon={<SaveOutlined />} onClick={() => void saveWarehouse(row)}>
          {tc('actions.save')}
        </Button>
      ),
    },
  ];

  return (
    <Collapse
      size="small"
      items={[
        {
          key: 'settings',
          label: t('title'),
          children: (
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
              <Alert
                type="info"
                showIcon
                message={t('priorityAlertTitle')}
                description={
                  <Trans
                    i18nKey="priorityAlertDescription"
                    ns="inventory"
                    t={t}
                    values={{ fallback }}
                  />
                }
              />

              <Space wrap align="end">
                <div>
                  <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                    {t('defaultThreshold')}
                  </Typography.Text>
                  <InputNumber
                    {...quantityInputNumberProps}
                    min={0}
                    placeholder={t('defaultPlaceholder', { fallback })}
                    value={defaultQty}
                    onChange={(v) => setDefaultQty(v)}
                    style={{ width: 160 }}
                  />
                </div>
                <Button type="primary" icon={<SaveOutlined />} loading={savingDefault} onClick={() => void saveDefault()}>
                  {t('saveDefault')}
                </Button>
                <Button icon={<ThunderboltOutlined />} loading={applyingAll} onClick={() => void applyAll()}>
                  {t('applyDefaultToProducts')}
                </Button>
                <Checkbox checked={onlyUnset} onChange={(e) => setOnlyUnset(e.target.checked)}>
                  {t('onlyUnsetProducts')}
                </Checkbox>
              </Space>

              <Typography.Text strong>{t('byWarehouse')}</Typography.Text>
              <Table
                rowKey="id"
                size="small"
                loading={loading}
                pagination={false}
                dataSource={settings?.warehouses ?? []}
                columns={warehouseColumns}
                locale={{ emptyText: t('empty.noWarehouses') }}
              />

              <Typography.Text strong>{t('byCategory')}</Typography.Text>
              <Table
                rowKey="id"
                size="small"
                loading={loading}
                pagination={false}
                dataSource={settings?.categories ?? []}
                columns={categoryColumns}
                locale={{ emptyText: t('empty.noCategories') }}
              />
            </Space>
          ),
        },
      ]}
    />
  );
}
