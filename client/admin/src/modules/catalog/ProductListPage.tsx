import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import {
  Alert,
  App,
  AutoComplete,
  Avatar,
  Button,
  Card,
  Col,
  Collapse,
  Empty,
  Input,
  InputNumber,
  Popconfirm,
  Row,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { FilterOutlined, PlusOutlined, ReloadOutlined, SearchOutlined, EditOutlined, DeleteOutlined, CloudSyncOutlined, ImportOutlined } from '@ant-design/icons';
import {
  bulkDeleteProducts,
  deleteProduct,
  fetchBrandLookups,
  fetchCategoryLookups,
  fetchProduct,
  fetchProducts,
} from '@/shared/api/catalog.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { LookupItem, ProductDetail, ProductListFilter, ProductListItem } from '@/shared/api/catalog.types';
import { useCatalogEnums } from '@/shared/i18n/use-catalog-enums';
import { formatDisplayMoney } from '@/shared/utils/money';
import { isProductFeatureEnabled } from '@/shared/product/product-phases';
import { ProductFormDrawer } from '@/modules/catalog/ProductFormDrawer';

const emptyAdvancedFilters: Omit<ProductListFilter, 'search' | 'page' | 'pageSize'> = {
  drugTypes: undefined,
  categoryIds: undefined,
  brandIds: undefined,
  status: undefined,
  priceMin: undefined,
  priceMax: undefined,
  hasBarcode: undefined,
  hasPrice: undefined,
};

export function ProductListPage() {
  const { t } = useTranslation('catalog', { keyPrefix: 'products' });
  const { t: ts } = useTranslation('catalog', { keyPrefix: 'shared' });
  const { drugTypeOptions, productStatusLabel, productStatusOptions } = useCatalogEnums();
  const { message: msg } = App.useApp();
  const navigate = useNavigate();
  const showNationalDrugLookup = isProductFeatureEnabled('catalog.nationalDrug');
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [items, setItems] = useState<ProductListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [search, setSearch] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [advancedFilters, setAdvancedFilters] = useState(emptyAdvancedFilters);
  const [filterDraft, setFilterDraft] = useState(emptyAdvancedFilters);
  const [filterLookups, setFilterLookups] = useState<{ categories: LookupItem[]; brands: LookupItem[] }>({
    categories: [],
    brands: [],
  });

  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<ProductDetail | null>(null);
  const [selectedRowKeys, setSelectedRowKeys] = useState<string[]>([]);
  const [bulkDeleting, setBulkDeleting] = useState(false);
  const [suggestionProducts, setSuggestionProducts] = useState<ProductListItem[]>([]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await fetchProducts({
        search: search || undefined,
        page,
        pageSize,
        drugTypes: advancedFilters.drugTypes?.length ? advancedFilters.drugTypes : undefined,
        categoryIds: advancedFilters.categoryIds?.length ? advancedFilters.categoryIds : undefined,
        brandIds: advancedFilters.brandIds?.length ? advancedFilters.brandIds : undefined,
        status: advancedFilters.status,
        priceMin: advancedFilters.priceMin,
        priceMax: advancedFilters.priceMax,
        hasBarcode: advancedFilters.hasBarcode,
        hasPrice: advancedFilters.hasPrice,
      });
      setItems(data.items ?? []);
      setTotal(data.total ?? 0);
      setLoadError(null);
    } catch (error) {
      const text = apiErrorMessage(error, t('messages.loadFailed'));
      setLoadError(text);
      msg.error(text);
    } finally {
      setLoading(false);
    }
  }, [search, page, pageSize, advancedFilters, msg, t]);

  const hasActiveFilters =
    Boolean(advancedFilters.drugTypes?.length) ||
    Boolean(advancedFilters.categoryIds?.length) ||
    Boolean(advancedFilters.brandIds?.length) ||
    advancedFilters.status != null ||
    advancedFilters.priceMin != null ||
    advancedFilters.priceMax != null ||
    advancedFilters.hasBarcode != null ||
    advancedFilters.hasPrice != null;

  const applyFilters = () => {
    setPage(1);
    setAdvancedFilters({
      drugTypes: filterDraft.drugTypes?.length ? [...filterDraft.drugTypes] : undefined,
      categoryIds: filterDraft.categoryIds?.length ? [...filterDraft.categoryIds] : undefined,
      brandIds: filterDraft.brandIds?.length ? [...filterDraft.brandIds] : undefined,
      status: filterDraft.status,
      priceMin: filterDraft.priceMin,
      priceMax: filterDraft.priceMax,
      hasBarcode: filterDraft.hasBarcode,
      hasPrice: filterDraft.hasPrice,
    });
  };

  const clearAllFilters = () => {
    setFilterDraft(emptyAdvancedFilters);
    setAdvancedFilters(emptyAdvancedFilters);
    setSearch('');
    setSearchInput('');
    setPage(1);
  };

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    Promise.all([fetchCategoryLookups(), fetchBrandLookups()])
      .then(([categories, brands]) => setFilterLookups({ categories, brands }))
      .catch(() => {});
    fetchProducts({ page: 1, pageSize: 200 })
      .then((data) => setSuggestionProducts(data.items ?? []))
      .catch(() => {});
  }, []);

  const productSuggestions = useMemo(() => {
    const q = searchInput.trim().toLowerCase();
    const filtered = suggestionProducts.filter((p) => {
      if (!q) return true;
      return (
        p.productCode.toLowerCase().includes(q) ||
        p.productName.toLowerCase().includes(q) ||
        (p.primaryBarcode?.toLowerCase().includes(q) ?? false) ||
        (p.genericName?.toLowerCase().includes(q) ?? false)
      );
    });
    return filtered.slice(0, 20).map((p) => ({
      value: p.id,
      label: `${p.productCode} — ${p.productName}${p.primaryBarcode ? ` · ${p.primaryBarcode}` : ''}`,
    }));
  }, [suggestionProducts, searchInput]);

  const selectSuggestedProduct = (productId: string) => {
    const product = suggestionProducts.find((p) => p.id === productId);
    if (!product) return;
    const text = product.primaryBarcode || product.productCode || product.productName;
    setSearchInput(text);
    setPage(1);
    setSearch(text);
  };

  const applySearch = () => {
    setPage(1);
    setSearch(searchInput.trim());
  };

  const openCreate = () => {
    setEditing(null);
    setDrawerOpen(true);
  };

  const openEdit = async (id: string) => {
    try {
      const product = await fetchProduct(id);
      setEditing(product);
      setDrawerOpen(true);
    } catch {
      msg.error(t('messages.detailLoadFailed'));
    }
  };

  const handleProductCreated = (product: ProductDetail) => {
    setEditing(product);
    load();
  };

  const handleProductUpdated = async (product?: ProductDetail) => {
    load();
    if (product) {
      setEditing(product);
      return;
    }
    if (editing) {
      try {
        const refreshed = await fetchProduct(editing.id);
        setEditing(refreshed);
      } catch {
        msg.error(t('messages.detailReloadFailed'));
      }
    }
  };

  const handleDrawerClose = () => {
    setDrawerOpen(false);
    setEditing(null);
  };

  const handleDeleteOne = async (id: string) => {
    try {
      await deleteProduct(id);
      msg.success(t('messages.deleteSuccess'));
      setSelectedRowKeys((keys) => keys.filter((k) => k !== id));
      load();
    } catch (error) {
      msg.error(apiErrorMessage(error, t('messages.deleteFailed')));
    }
  };

  const handleBulkDelete = async () => {
    if (selectedRowKeys.length === 0) return;
    setBulkDeleting(true);
    try {
      const count = await bulkDeleteProducts(selectedRowKeys);
      msg.success(t('messages.bulkDeleteSuccess', { count }));
      setSelectedRowKeys([]);
      load();
    } catch (error) {
      msg.error(apiErrorMessage(error, t('messages.bulkDeleteFailed')));
    } finally {
      setBulkDeleting(false);
    }
  };

  const columns: ColumnsType<ProductListItem> = useMemo(
    () => [
      {
        title: t('columns.image'),
        dataIndex: 'primaryImageUrl',
        width: 56,
        render: (url?: string) =>
          url ? (
            <Avatar shape="square" size={40} src={url} alt="" />
          ) : (
            <Avatar shape="square" size={40}>
              —
            </Avatar>
          ),
      },
      {
        title: t('columns.barcode'),
        dataIndex: 'primaryBarcode',
        width: 130,
        render: (v?: string) => v ?? '—',
      },
      { title: t('columns.productName'), dataIndex: 'productName', ellipsis: true },
      {
        title: t('columns.genericName'),
        dataIndex: 'genericName',
        ellipsis: true,
        render: (v?: string) => v ?? '—',
      },
      {
        title: t('columns.saleUnit'),
        dataIndex: 'saleUnitName',
        width: 100,
        render: (v?: string) => v ?? '—',
      },
      {
        title: t('columns.retailPrice'),
        dataIndex: 'retailPrice',
        width: 110,
        align: 'right',
        render: (v?: number) => (
          <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
        ),
      },
      {
        title: t('columns.status'),
        dataIndex: 'status',
        width: 100,
        render: (v: number) => (
          <Tag color={v === 1 ? 'green' : 'default'}>{productStatusLabel(v)}</Tag>
        ),
      },
      {
        title: t('columns.actions'),
        key: 'actions',
        width: 140,
        fixed: 'right',
        render: (_, row) => (
          <Space size={0}>
            <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row.id)}>
              {ts('edit')}
            </Button>
            <Popconfirm title={t('deleteConfirm')} onConfirm={() => handleDeleteOne(row.id)}>
              <Button type="link" size="small" danger icon={<DeleteOutlined />}>
                {ts('delete')}
              </Button>
            </Popconfirm>
          </Space>
        ),
      },
    ],
    [t, ts, productStatusLabel],
  );

  return (
    <Card
      title={t('title')}
      extra={
        <Space wrap>
          {selectedRowKeys.length > 0 && (
            <Popconfirm
              title={t('bulkDeleteConfirm', { count: selectedRowKeys.length })}
              onConfirm={handleBulkDelete}
            >
              <Button danger loading={bulkDeleting}>
                {t('bulkDeleteButton', { count: selectedRowKeys.length })}
              </Button>
            </Popconfirm>
          )}
          <Space.Compact>
            <AutoComplete
              style={{ width: 280 }}
              options={productSuggestions}
              value={searchInput}
              onSelect={(id) => selectSuggestedProduct(String(id))}
              onChange={(value) => {
                setSearchInput(value);
                if (!value) {
                  setSearch('');
                  setPage(1);
                }
              }}
            >
              <Input
                placeholder={t('searchPlaceholder')}
                prefix={<SearchOutlined />}
                onPressEnter={applySearch}
                allowClear
              />
            </AutoComplete>
            <Button type="primary" icon={<SearchOutlined />} onClick={applySearch}>
              {t('search')}
            </Button>
          </Space.Compact>
          <Button icon={<ReloadOutlined />} onClick={load} />
          <Button icon={<ImportOutlined />} onClick={() => navigate('/catalog/import')}>
            {t('importExcel')}
          </Button>
          {showNationalDrugLookup && (
            <Button icon={<CloudSyncOutlined />} onClick={() => navigate('/catalog/national-drugs')}>
              {t('nationalLookup')}
            </Button>
          )}
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('addProduct')}
          </Button>
        </Space>
      }
    >
      {loadError && (
        <Alert
          type="error"
          showIcon
          message={loadError}
          action={
            <Button size="small" onClick={load}>
              {ts('retry')}
            </Button>
          }
          style={{ marginBottom: 16 }}
        />
      )}
      {hasActiveFilters && !loadError && (
        <Alert
          type="info"
          showIcon
          message={t('filterActiveAlert')}
          action={
            <Button size="small" onClick={clearAllFilters}>
              {t('clearFilters')}
            </Button>
          }
          style={{ marginBottom: 16 }}
        />
      )}
      <Collapse
        ghost
        style={{ marginBottom: 16 }}
        items={[
          {
            key: 'filters',
            label: (
              <Space>
                <FilterOutlined />
                {t('advancedFilters')}
              </Space>
            ),
            children: (
              <>
                <Row gutter={[16, 8]}>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">{t('filters.drugType')}</Typography.Text>
                    <Select
                      mode="multiple"
                      allowClear
                      style={{ width: '100%' }}
                      placeholder={ts('all')}
                      value={filterDraft.drugTypes}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, drugTypes: v }))}
                      options={drugTypeOptions}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">{t('filters.category')}</Typography.Text>
                    <Select
                      mode="multiple"
                      allowClear
                      style={{ width: '100%' }}
                      placeholder={ts('all')}
                      value={filterDraft.categoryIds}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, categoryIds: v }))}
                      options={filterLookups.categories.map((c) => ({ value: c.id, label: c.name }))}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">{t('filters.brand')}</Typography.Text>
                    <Select
                      mode="multiple"
                      allowClear
                      style={{ width: '100%' }}
                      placeholder={ts('all')}
                      value={filterDraft.brandIds}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, brandIds: v }))}
                      options={filterLookups.brands.map((b) => ({ value: b.id, label: b.name }))}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">{t('filters.status')}</Typography.Text>
                    <Select
                      allowClear
                      style={{ width: '100%' }}
                      placeholder={ts('all')}
                      value={filterDraft.status}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, status: v }))}
                      options={productStatusOptions}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">{t('filters.retailPriceFrom')}</Typography.Text>
                    <InputNumber
                      min={0}
                      style={{ width: '100%' }}
                      value={filterDraft.priceMin}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, priceMin: v ?? undefined }))}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">{t('filters.retailPriceTo')}</Typography.Text>
                    <InputNumber
                      min={0}
                      style={{ width: '100%' }}
                      value={filterDraft.priceMax}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, priceMax: v ?? undefined }))}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">{t('filters.barcode')}</Typography.Text>
                    <Select
                      allowClear
                      style={{ width: '100%' }}
                      placeholder={ts('all')}
                      value={filterDraft.hasBarcode}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, hasBarcode: v }))}
                      options={[
                        { value: true, label: t('filters.hasBarcode') },
                        { value: false, label: t('filters.noBarcode') },
                      ]}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">{t('filters.price')}</Typography.Text>
                    <Select
                      allowClear
                      style={{ width: '100%' }}
                      placeholder={ts('all')}
                      value={filterDraft.hasPrice}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, hasPrice: v }))}
                      options={[
                        { value: true, label: t('filters.hasPrice') },
                        { value: false, label: t('filters.noPrice') },
                      ]}
                    />
                  </Col>
                </Row>
                <Space style={{ marginTop: 12 }}>
                  <Button type="primary" onClick={applyFilters}>
                    {t('applyFilters')}
                  </Button>
                  <Button
                    onClick={() => {
                      setFilterDraft(emptyAdvancedFilters);
                      setAdvancedFilters(emptyAdvancedFilters);
                      setPage(1);
                    }}
                  >
                    {t('clearFilters')}
                  </Button>
                </Space>
              </>
            ),
          },
        ]}
      />
      <Table
        rowKey="id"
        loading={loading}
        columns={columns}
        dataSource={items}
        rowSelection={{
          selectedRowKeys,
          onChange: (keys) => setSelectedRowKeys(keys as string[]),
        }}
        scroll={{ x: 820 }}
        locale={{
          emptyText: (
            <Empty
              description={
                loadError
                  ? t('empty.loadFailed')
                  : hasActiveFilters
                    ? t('empty.noMatch')
                    : t('empty.none')
              }
            >
              <Space>
                <Button onClick={load}>{ts('reload')}</Button>
                {hasActiveFilters && <Button onClick={clearAllFilters}>{t('clearFilters')}</Button>}
              </Space>
            </Empty>
          ),
        }}
        pagination={{
          current: page,
          pageSize,
          total,
          showSizeChanger: true,
          showTotal: (totalCount) => t('paginationTotal', { count: totalCount }),
          onChange: (p, ps) => {
            setPage(p);
            setPageSize(ps);
          },
        }}
        size="middle"
      />

      <ProductFormDrawer
        open={drawerOpen}
        editing={editing}
        onClose={handleDrawerClose}
        onCreated={handleProductCreated}
        onUpdated={handleProductUpdated}
      />
    </Card>
  );
}
