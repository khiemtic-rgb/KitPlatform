import { useCallback, useEffect, useState } from 'react';

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

      message.error(apiErrorMessage(error, 'Không tải được cài đặt ngưỡng tồn'));

    } finally {

      setLoading(false);

    }

  }, []);



  useEffect(() => {

    void load();

  }, [load]);



  const saveDefault = async () => {

    setSavingDefault(true);

    try {

      const saved = await updateLowStockDefault(defaultQty ?? undefined);

      setDefaultQty(saved ?? null);

      message.success('Đã lưu ngưỡng chung');

      await load();

      onChanged?.();

    } catch (error) {

      message.error(apiErrorMessage(error, 'Không lưu được ngưỡng chung'));

    } finally {

      setSavingDefault(false);

    }

  };



  const applyAll = async () => {

    setApplyingAll(true);

    try {

      const count = await applyLowStockToAll(onlyUnset, defaultQty ?? undefined);

      message.success(`Đã ghi ngưỡng lên ${count} sản phẩm`);

      onChanged?.();

    } catch (error) {

      message.error(apiErrorMessage(error, 'Không áp dụng được'));

    } finally {

      setApplyingAll(false);

    }

  };



  const saveCategory = async (row: CategoryLowStockSetting) => {

    const qty = categoryDrafts[row.id];

    try {

      await updateCategoryLowStockSetting(row.id, qty ?? undefined);

      message.success(`Đã lưu ngưỡng danh mục «${row.categoryName}»`);

      await load();

      onChanged?.();

    } catch (error) {

      message.error(apiErrorMessage(error, 'Không lưu được danh mục'));

    }

  };



  const applyCategory = async (row: CategoryLowStockSetting) => {

    try {

      const count = await applyLowStockToCategory(row.id, onlyUnset);

      message.success(`Đã ghi ngưỡng lên ${count} SP trong «${row.categoryName}»`);

      onChanged?.();

    } catch (error) {

      message.error(apiErrorMessage(error, 'Không áp dụng được'));

    }

  };



  const saveWarehouse = async (row: WarehouseLowStockSetting) => {

    const qty = warehouseDrafts[row.id];

    try {

      await updateWarehouseLowStockSetting(row.id, qty ?? undefined);

      message.success(`Đã lưu ngưỡng kho «${row.warehouseName}»`);

      await load();

      onChanged?.();

    } catch (error) {

      message.error(apiErrorMessage(error, 'Không lưu được kho'));

    }

  };



  const fallback = settings?.systemFallbackQty ?? 10;



  const categoryColumns: ColumnsType<CategoryLowStockSetting> = [

    { title: 'Mã', dataIndex: 'categoryCode', width: 90 },

    { title: 'Danh mục', dataIndex: 'categoryName' },

    {

      title: 'Ngưỡng',

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

      title: 'SP',

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

            Lưu

          </Button>

          <Button size="small" icon={<ThunderboltOutlined />} onClick={() => void applyCategory(row)}>

            Ghi SP

          </Button>

        </Space>

      ),

    },

  ];



  const warehouseColumns: ColumnsType<WarehouseLowStockSetting> = [

    { title: 'Mã kho', dataIndex: 'warehouseCode', width: 100 },

    {

      title: 'Kho / chi nhánh',

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

      title: 'Ngưỡng',

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

          Lưu

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

          label: 'Cài đặt ngưỡng tồn thấp (chung / kho / danh mục)',

          children: (

            <Space direction="vertical" size="middle" style={{ width: '100%' }}>

              <Alert

                type="info"

                showIcon

                message="Thứ tự ưu tiên khi cảnh báo"

                description={

                  <>

                    <strong>SP riêng</strong> → <strong>Kho</strong> → <strong>Danh mục</strong> →{' '}

                    <strong>Ngưỡng chung</strong> → mặc định {fallback}. Nút <strong>Ghi SP</strong> copy ngưỡng

                    xuống trường từng sản phẩm.

                  </>

                }

              />



              <Space wrap align="end">

                <div>

                  <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>

                    Ngưỡng chung (toàn nhà thuốc)

                  </Typography.Text>

                  <InputNumber

                    {...quantityInputNumberProps}

                    min={0}

                    placeholder={`Mặc định ${fallback}`}

                    value={defaultQty}

                    onChange={(v) => setDefaultQty(v)}

                    style={{ width: 160 }}

                  />

                </div>

                <Button type="primary" icon={<SaveOutlined />} loading={savingDefault} onClick={() => void saveDefault()}>

                  Lưu ngưỡng chung

                </Button>

                <Button icon={<ThunderboltOutlined />} loading={applyingAll} onClick={() => void applyAll()}>

                  Ghi ngưỡng chung lên SP

                </Button>

                <Checkbox checked={onlyUnset} onChange={(e) => setOnlyUnset(e.target.checked)}>

                  Chỉ SP chưa có ngưỡng riêng

                </Checkbox>

              </Space>



              <Typography.Text strong>Theo kho / chi nhánh</Typography.Text>

              <Table

                rowKey="id"

                size="small"

                loading={loading}

                pagination={false}

                dataSource={settings?.warehouses ?? []}

                columns={warehouseColumns}

                locale={{ emptyText: 'Chưa có kho — tạo tại Kho → Danh sách kho' }}

              />



              <Typography.Text strong>Theo danh mục SP</Typography.Text>

              <Table

                rowKey="id"

                size="small"

                loading={loading}

                pagination={false}

                dataSource={settings?.categories ?? []}

                columns={categoryColumns}

                locale={{ emptyText: 'Chưa có danh mục SP — tạo tại Danh mục → Danh mục SP' }}

              />

            </Space>

          ),

        },

      ]}

    />

  );

}


