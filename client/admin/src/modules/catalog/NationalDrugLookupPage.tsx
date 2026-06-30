import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  App,
  Button,
  Card,
  Col,
  Descriptions,
  Input,
  Row,
  Space,
  Spin,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { CloudSyncOutlined, PlusOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import {
  fetchNationalDrugConnectionStatus,
  fetchNationalDrugDetail,
  fetchNationalDrugFieldMap,
  fetchNationalDrugPrefill,
  searchNationalDrugs,
} from '@/shared/api/national-drug.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type {
  NationalDrugConnectionStatus,
  NationalDrugDetail,
  NationalDrugFieldMap,
  NationalDrugListItem,
  ProductFormNationalPrefill,
} from '@/shared/api/national-drug.types';
import { mapNationalPrefillToProductForm } from '@/shared/api/national-drug.types';
import type { ProductDetail } from '@/shared/api/catalog.types';
import { ProductFormDrawer } from '@/modules/catalog/ProductFormDrawer';
import { formatDisplayDate } from '@/shared/utils/date';

export function NationalDrugLookupPage() {
  const { t } = useTranslation('catalog', { keyPrefix: 'nationalDrugs' });
  const { message } = App.useApp();
  const [connection, setConnection] = useState<NationalDrugConnectionStatus | null>(null);
  const [fieldMap, setFieldMap] = useState<NationalDrugFieldMap[]>([]);
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<NationalDrugListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [selectedDrugId, setSelectedDrugId] = useState<string | null>(null);
  const [detail, setDetail] = useState<NationalDrugDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [prefillLoading, setPrefillLoading] = useState(false);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [productPrefill, setProductPrefill] = useState<ProductFormNationalPrefill | null>(null);
  const [createdProduct, setCreatedProduct] = useState<ProductDetail | null>(null);

  const loadMeta = useCallback(async () => {
    try {
      const [status, map] = await Promise.all([
        fetchNationalDrugConnectionStatus(),
        fetchNationalDrugFieldMap(),
      ]);
      setConnection(status);
      setFieldMap(map);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.configLoadFailed')));
    }
  }, [message, t]);

  const loadResults = useCallback(async () => {
    setLoading(true);
    try {
      const result = await searchNationalDrugs({ search: search || undefined, page, pageSize });
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.searchFailed')));
    } finally {
      setLoading(false);
    }
  }, [search, page, pageSize, message, t]);

  useEffect(() => {
    void loadMeta();
  }, [loadMeta]);

  useEffect(() => {
    void loadResults();
  }, [loadResults]);

  useEffect(() => {
    if (!selectedDrugId) {
      setDetail(null);
      return;
    }
    setDetailLoading(true);
    void fetchNationalDrugDetail(selectedDrugId)
      .then(setDetail)
      .catch((error) => {
        message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
        setDetail(null);
      })
      .finally(() => setDetailLoading(false));
  }, [selectedDrugId, message, t]);

  const columns: ColumnsType<NationalDrugListItem> = useMemo(
    () => [
      { title: t('columns.nationalId'), dataIndex: 'drugId', width: 130 },
      { title: t('columns.registrationNumber'), dataIndex: 'registrationNumber', width: 110 },
      { title: t('columns.productName'), dataIndex: 'productName' },
      {
        title: t('columns.ingredient'),
        key: 'ingredient',
        render: (_, row) =>
          [row.activeIngredient, row.strength].filter(Boolean).join(' · ') || '—',
      },
      { title: t('columns.unit'), dataIndex: 'unitName', width: 70 },
      { title: t('columns.category'), dataIndex: 'drugCategoryLabel', width: 90 },
    ],
    [t],
  );

  const fieldMapColumns: ColumnsType<NationalDrugFieldMap> = useMemo(
    () => [
      { title: t('fieldMapColumns.nationalLabel'), dataIndex: 'nationalLabel', width: 180 },
      {
        title: t('fieldMapColumns.nationalField'),
        dataIndex: 'nationalField',
        width: 120,
        render: (v) => <Typography.Text code>{v}</Typography.Text>,
      },
      { title: t('fieldMapColumns.productLabel'), dataIndex: 'productLabel', width: 160 },
      {
        title: t('fieldMapColumns.productField'),
        dataIndex: 'productField',
        width: 130,
        render: (v) => <Typography.Text code>{v}</Typography.Text>,
      },
      { title: t('fieldMapColumns.notes'), dataIndex: 'notes', render: (v) => v ?? '—' },
    ],
    [t],
  );

  const handleCreateProduct = async () => {
    if (!selectedDrugId) return;
    setPrefillLoading(true);
    try {
      const prefill = await fetchNationalDrugPrefill(selectedDrugId);
      setProductPrefill(mapNationalPrefillToProductForm(prefill));
      setCreatedProduct(null);
      setDrawerOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.prefillFailed')));
    } finally {
      setPrefillLoading(false);
    }
  };

  return (
    <>
      <Alert
        type="info"
        showIcon
        icon={<CloudSyncOutlined />}
        style={{ marginBottom: 16 }}
        message={
          <Space wrap>
            <span>{t('alertTitle')}</span>
            {connection && (
              <Tag color={connection.isLive ? 'green' : 'gold'}>{connection.modeLabel}</Tag>
            )}
          </Space>
        }
        description={connection?.message ?? t('alertDescriptionDefault')}
      />

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={14}>
          <Card
            title={t('listTitle')}
            extra={
              <Space>
                <Input
                  allowClear
                  placeholder={t('searchPlaceholder')}
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  onPressEnter={() => {
                    setPage(1);
                    setSearch(searchInput.trim());
                  }}
                  style={{ width: 280 }}
                  prefix={<SearchOutlined />}
                />
                <Button
                  type="primary"
                  onClick={() => {
                    setPage(1);
                    setSearch(searchInput.trim());
                  }}
                >
                  {t('search')}
                </Button>
                <Button icon={<ReloadOutlined />} onClick={() => void loadResults()} loading={loading} />
              </Space>
            }
          >
            <Table
              rowKey="drugId"
              size="small"
              loading={loading}
              columns={columns}
              dataSource={items}
              pagination={{
                current: page,
                pageSize,
                total,
                showSizeChanger: true,
                onChange: (nextPage, nextSize) => {
                  setPage(nextPage);
                  setPageSize(nextSize);
                },
              }}
              rowSelection={{
                type: 'radio',
                selectedRowKeys: selectedDrugId ? [selectedDrugId] : [],
                onChange: (keys) => setSelectedDrugId(String(keys[0] ?? '') || null),
              }}
              onRow={(row) => ({
                onClick: () => setSelectedDrugId(row.drugId),
                style: { cursor: 'pointer' },
              })}
            />
          </Card>
        </Col>

        <Col xs={24} xl={10}>
          <Card
            title={t('detailTitle')}
            extra={
              <Button
                type="primary"
                icon={<PlusOutlined />}
                disabled={!selectedDrugId}
                loading={prefillLoading}
                onClick={() => void handleCreateProduct()}
              >
                {t('createProduct')}
              </Button>
            }
          >
            {!detail ? (
              <Typography.Text type="secondary">{t('selectRowHint')}</Typography.Text>
            ) : (
              <Spin spinning={detailLoading}>
                <Descriptions
                  size="small"
                  column={1}
                  bordered
                  styles={{ label: { width: 130 } }}
                >
                  <Descriptions.Item label={t('detail.nationalId')}>{detail.drugId}</Descriptions.Item>
                  <Descriptions.Item label={t('detail.registrationNumber')}>
                    {detail.registrationNumber}
                  </Descriptions.Item>
                  <Descriptions.Item label={t('detail.productName')}>{detail.productName}</Descriptions.Item>
                  <Descriptions.Item label={t('detail.ingredient')}>
                    {[detail.activeIngredient, detail.strength].filter(Boolean).join(' · ') || '—'}
                  </Descriptions.Item>
                  <Descriptions.Item label={t('detail.dosageForm')}>{detail.dosageForm ?? '—'}</Descriptions.Item>
                  <Descriptions.Item label={t('detail.packaging')}>{detail.packaging ?? '—'}</Descriptions.Item>
                  <Descriptions.Item label={t('detail.unit')}>{detail.unitName ?? '—'}</Descriptions.Item>
                  <Descriptions.Item label={t('detail.manufacturer')}>{detail.manufacturer ?? '—'}</Descriptions.Item>
                  <Descriptions.Item label={t('detail.countryOfOrigin')}>
                    {detail.countryOfOrigin ?? '—'}
                  </Descriptions.Item>
                  <Descriptions.Item label={t('detail.category')}>{detail.drugCategoryLabel}</Descriptions.Item>
                  <Descriptions.Item label={t('detail.barcode')}>{detail.barcode ?? '—'}</Descriptions.Item>
                  <Descriptions.Item label={t('detail.atc')}>{detail.atcCode ?? '—'}</Descriptions.Item>
                  <Descriptions.Item label={t('detail.registrationExpiry')}>
                    {detail.registrationExpiryDate ? formatDisplayDate(detail.registrationExpiryDate) : '—'}
                  </Descriptions.Item>
                </Descriptions>
              </Spin>
            )}
          </Card>
        </Col>
      </Row>

      <Card title={t('fieldMapTitle')} style={{ marginTop: 16 }}>
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          {t('fieldMapIntro')}
        </Typography.Paragraph>
        <Table
          rowKey="nationalField"
          size="small"
          pagination={false}
          columns={fieldMapColumns}
          dataSource={fieldMap}
        />
      </Card>

      <ProductFormDrawer
        open={drawerOpen}
        editing={createdProduct}
        nationalPrefill={drawerOpen && !createdProduct ? productPrefill : null}
        onClose={() => {
          setDrawerOpen(false);
          setProductPrefill(null);
        }}
        onCreated={(product) => {
          setCreatedProduct(product);
          message.success(
            t('messages.createSuccess', {
              code: product.productCode,
              nationalId: product.nationalDrugId ?? '',
            }),
          );
        }}
        onUpdated={() => {}}
      />
    </>
  );
}
