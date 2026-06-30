import { useCallback, useEffect, useMemo, useState } from 'react';
import { formatDisplayMoney } from '@/shared/utils/money';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Button,
  Drawer,
  Input,
  InputNumber,
  List,
  Select,
  Space,
  Tag,
  Typography,
  message,
} from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import {
  cancelCustomerDraftOrder,
  createCustomerDraftOrder,
  fetchCustomerDraftOrders,
  sendCustomerDraftOrder,
  CUSTOMER_DRAFT_ORDER_STATUS,
  type CustomerDraftOrderLineInput,
  type CustomerDraftOrderListItem,
} from '@/shared/api/customer-draft-orders.api';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import { lookupPosProduct, searchPosProducts } from '@/shared/api/sales.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';

type DraftLineForm = CustomerDraftOrderLineInput & {
  key: string;
  productCode: string;
  productName: string;
  unitName: string;
  unitPrice: number;
};

interface CustomerDraftOrderDrawerProps {
  open: boolean;
  customerId?: string;
  customerName?: string;
  onClose: () => void;
}

export function CustomerDraftOrderDrawer({
  open,
  customerId,
  customerName,
  onClose,
}: CustomerDraftOrderDrawerProps) {
  const { t } = useTranslation('sales', { keyPrefix: 'customerDrafts.drawer' });
  const { t: tDrafts } = useTranslation('sales', { keyPrefix: 'customerDrafts' });
  const { customerDraftStatusLabel } = useSalesEnums();
  const [warehouseId, setWarehouseId] = useState<string>();
  const [orders, setOrders] = useState<CustomerDraftOrderListItem[]>([]);
  const [loadingOrders, setLoadingOrders] = useState(false);
  const [lines, setLines] = useState<DraftLineForm[]>([]);
  const [notes, setNotes] = useState('');
  const [productSearch, setProductSearch] = useState('');
  const [productOptions, setProductOptions] = useState<
    Array<{ value: string; label: string; productCode: string }>
  >([]);
  const [saving, setSaving] = useState(false);

  const loadOrders = useCallback(async () => {
    if (!customerId) return;
    setLoadingOrders(true);
    try {
      setOrders(await fetchCustomerDraftOrders(customerId));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoadingOrders(false);
    }
  }, [customerId, t]);

  useEffect(() => {
    if (!open) return;
    void fetchWarehouses().then((warehouses) => {
      const defaultWh = warehouses.find((w) => w.isDefault) ?? warehouses[0];
      if (defaultWh) setWarehouseId(defaultWh.id);
    });
    void loadOrders();
    setLines([]);
    setNotes('');
  }, [open, loadOrders]);

  useEffect(() => {
    if (!open || !warehouseId || productSearch.trim().length < 2) {
      setProductOptions([]);
      return;
    }
    const timer = window.setTimeout(() => {
      void searchPosProducts(productSearch.trim(), warehouseId)
        .then((items) =>
          setProductOptions(
            items.map((item) => ({
              value: item.productCode,
              label: `${item.productName} (${item.productCode}) — ${formatDisplayMoney(item.unitPrice)}`,
              productCode: item.productCode,
            })),
          ),
        )
        .catch(() => setProductOptions([]));
    }, 300);
    return () => window.clearTimeout(timer);
  }, [open, productSearch, warehouseId]);

  const buildPayload = () => {
    if (!customerId) throw new Error('missing-customer');
    return {
      customerId,
      warehouseId,
      priceType: 1,
      notes: notes.trim() || undefined,
      items: lines.map(({ productId, productUnitId, quantity, dosageNote }) => ({
        productId,
        productUnitId,
        quantity,
        dosageNote: dosageNote?.trim() || undefined,
      })),
    };
  };

  const onSaveDraft = async () => {
    if (!customerId || lines.length === 0) {
      message.warning(t('messages.addProductRequired'));
      return;
    }
    setSaving(true);
    try {
      await createCustomerDraftOrder(buildPayload());
      message.success(t('messages.saveSuccess'));
      setLines([]);
      setNotes('');
      await loadOrders();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  const onSendDraft = async (id: string) => {
    setSaving(true);
    try {
      await sendCustomerDraftOrder(id);
      message.success(t('messages.sendSuccess'));
      await loadOrders();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.sendFailed')));
    } finally {
      setSaving(false);
    }
  };

  const onCancelDraft = async (id: string) => {
    try {
      await cancelCustomerDraftOrder(id);
      message.success(tDrafts('messages.cancelSuccess'));
      await loadOrders();
    } catch (error) {
      message.error(apiErrorMessage(error, tDrafts('messages.cancelFailed')));
    }
  };

  const addProduct = async (productCode: string) => {
    if (!warehouseId) return;
    try {
      const item = await lookupPosProduct(productCode, warehouseId);
      setLines((prev) => {
        const existing = prev.find((line) => line.productUnitId === item.productUnitId);
        if (existing) {
          return prev.map((line) =>
            line.productUnitId === item.productUnitId
              ? { ...line, quantity: line.quantity + 1 }
              : line,
          );
        }
        return [
          ...prev,
          {
            key: item.productUnitId,
            productId: item.productId,
            productUnitId: item.productUnitId,
            productCode: item.productCode,
            productName: item.productName,
            unitName: item.unitName,
            unitPrice: item.unitPrice,
            quantity: 1,
          },
        ];
      });
      setProductSearch('');
      setProductOptions([]);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.addProductFailed')));
    }
  };

  const estimatedTotal = useMemo(
    () => lines.reduce((sum, line) => sum + line.quantity * line.unitPrice, 0),
    [lines],
  );

  return (
    <Drawer
      title={t('title', { customerName: customerName ?? t('defaultCustomer') })}
      open={open}
      onClose={onClose}
      width={420}
      destroyOnClose
    >
      <Alert type="info" showIcon style={{ marginBottom: 12 }} message={t('alert')} />

      <Typography.Text strong>{t('existingList')}</Typography.Text>
      <List
        loading={loadingOrders}
        locale={{ emptyText: t('emptyList') }}
        dataSource={orders}
        style={{ marginTop: 8, marginBottom: 16 }}
        renderItem={(item) => (
          <List.Item
            actions={[
              item.status === CUSTOMER_DRAFT_ORDER_STATUS.Draft ? (
                <Button type="link" size="small" onClick={() => void onSendDraft(item.id)}>
                  {t('sendToCustomer')}
                </Button>
              ) : null,
              item.status === CUSTOMER_DRAFT_ORDER_STATUS.Draft ||
              item.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ||
              item.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed ? (
                <Button type="link" size="small" danger onClick={() => void onCancelDraft(item.id)}>
                  {t('cancel')}
                </Button>
              ) : null,
            ].filter(Boolean)}
          >
            <List.Item.Meta
              title={
                <Space>
                  <span>{item.draftNumber}</span>
                  <Tag>{customerDraftStatusLabel(item.status)}</Tag>
                </Space>
              }
              description={t('lineSummary', {
                count: item.itemCount,
                amount: formatDisplayMoney(item.totalAmount),
              })}
            />
          </List.Item>
        )}
      />

      <Typography.Text strong>{t('createNew')}</Typography.Text>
      <Space direction="vertical" style={{ width: '100%', marginTop: 8 }} size="middle">
        <Select
          showSearch
          allowClear
          placeholder={t('productSearchPlaceholder')}
          filterOption={false}
          value={undefined}
          searchValue={productSearch}
          onSearch={setProductSearch}
          onSelect={(value) => void addProduct(String(value))}
          options={productOptions}
          notFoundContent={productSearch.length < 2 ? t('searchMinChars') : t('productNotFound')}
          style={{ width: '100%' }}
        />

        {lines.map((line) => (
          <div key={line.key} style={{ border: '1px solid #f0f0f0', borderRadius: 8, padding: 8 }}>
            <Space direction="vertical" style={{ width: '100%' }} size={4}>
              <Space style={{ justifyContent: 'space-between', width: '100%' }}>
                <Typography.Text>{line.productName}</Typography.Text>
                <Button
                  type="text"
                  size="small"
                  danger
                  icon={<DeleteOutlined />}
                  onClick={() => setLines((prev) => prev.filter((x) => x.key !== line.key))}
                />
              </Space>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {line.productCode} · {formatDisplayMoney(line.unitPrice)}/{line.unitName}
              </Typography.Text>
              <InputNumber
                min={0.01}
                step={1}
                value={line.quantity}
                onChange={(value) =>
                  setLines((prev) =>
                    prev.map((x) =>
                      x.key === line.key ? { ...x, quantity: Number(value) || 1 } : x,
                    ),
                  )
                }
                addonBefore={t('qtyLabel')}
                style={{ width: '100%' }}
              />
              <Input
                placeholder={t('dosagePlaceholder')}
                value={line.dosageNote ?? ''}
                onChange={(e) =>
                  setLines((prev) =>
                    prev.map((x) => (x.key === line.key ? { ...x, dosageNote: e.target.value } : x)),
                  )
                }
              />
            </Space>
          </div>
        ))}

        <Input.TextArea
          rows={2}
          placeholder={t('notesPlaceholder')}
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
        />

        <Typography.Text type="secondary">
          {t('estimatedTotal', { amount: formatDisplayMoney(estimatedTotal) })}
        </Typography.Text>

        <Button type="primary" icon={<PlusOutlined />} loading={saving} onClick={() => void onSaveDraft()}>
          {t('saveDraft')}
        </Button>
      </Space>
    </Drawer>
  );
}
