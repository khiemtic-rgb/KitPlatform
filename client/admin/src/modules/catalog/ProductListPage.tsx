import { useCallback, useEffect, useMemo, useState } from 'react';
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
import { FilterOutlined, PlusOutlined, ReloadOutlined, SearchOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
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
import { DRUG_TYPE_LABELS, STATUS_LABELS } from '@/shared/api/catalog.types';
import { formatDisplayMoney } from '@/shared/utils/money';
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
  const { message: msg } = App.useApp();
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
      const text = apiErrorMessage(error, 'Không tải được danh sách sản phẩm');
      setLoadError(text);
      msg.error(text);
    } finally {
      setLoading(false);
    }
  }, [search, page, pageSize, advancedFilters, msg]);

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
      msg.error('Không tải được chi tiết sản phẩm');
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
        msg.error('Không tải lại được chi tiết sản phẩm');
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
      msg.success('Đã xóa sản phẩm');
      setSelectedRowKeys((keys) => keys.filter((k) => k !== id));
      load();
    } catch (error) {
      msg.error(apiErrorMessage(error, 'Không xóa được sản phẩm'));
    }
  };

  const handleBulkDelete = async () => {
    if (selectedRowKeys.length === 0) return;
    setBulkDeleting(true);
    try {
      const count = await bulkDeleteProducts(selectedRowKeys);
      msg.success(`Đã xóa ${count} sản phẩm`);
      setSelectedRowKeys([]);
      load();
    } catch (error) {
      msg.error(apiErrorMessage(error, 'Không xóa được các sản phẩm đã chọn'));
    } finally {
      setBulkDeleting(false);
    }
  };

  const columns: ColumnsType<ProductListItem> = [
    {
      title: 'Ảnh',
      dataIndex: 'primaryImageUrl',
      width: 56,
      render: (url?: string) =>
        url ? (
          <Avatar shape="square" size={40} src={url} alt="" />
        ) : (
          <Avatar shape="square" size={40}>—</Avatar>
        ),
    },
    {
      title: 'Barcode',
      dataIndex: 'primaryBarcode',
      width: 130,
      render: (v?: string) => v ?? '—',
    },
    { title: 'Tên sản phẩm', dataIndex: 'productName', ellipsis: true },
    {
      title: 'Hoạt chất',
      dataIndex: 'genericName',
      ellipsis: true,
      render: (v?: string) => v ?? '—',
    },
    {
      title: 'ĐVT bán lẻ',
      dataIndex: 'saleUnitName',
      width: 100,
      render: (v?: string) => v ?? '—',
    },
    {
      title: 'Giá lẻ',
      dataIndex: 'retailPrice',
      width: 110,
      align: 'right',
      render: (v?: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 100,
      render: (v: number) => (
        <Tag color={v === 1 ? 'green' : 'default'}>{STATUS_LABELS[v] ?? v}</Tag>
      ),
    },
    {
      title: 'Thao tác',
      key: 'actions',
      width: 140,
      fixed: 'right',
      render: (_, row) => (
        <Space size={0}>
          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row.id)}>
            Sửa
          </Button>
          <Popconfirm title="Xóa sản phẩm này?" onConfirm={() => handleDeleteOne(row.id)}>
            <Button type="link" size="small" danger icon={<DeleteOutlined />}>
              Xóa
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <Card
      title="Danh mục sản phẩm"
      extra={
        <Space wrap>
          {selectedRowKeys.length > 0 && (
            <Popconfirm
              title={`Xóa ${selectedRowKeys.length} sản phẩm đã chọn?`}
              onConfirm={handleBulkDelete}
            >
              <Button danger loading={bulkDeleting}>
                Xóa đã chọn ({selectedRowKeys.length})
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
                placeholder="Tìm tên hoặc mã vạch..."
                prefix={<SearchOutlined />}
                onPressEnter={applySearch}
                allowClear
              />
            </AutoComplete>
            <Button type="primary" icon={<SearchOutlined />} onClick={applySearch}>
              Tìm
            </Button>
          </Space.Compact>
          <Button icon={<ReloadOutlined />} onClick={load} />
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            Thêm sản phẩm
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
              Thử lại
            </Button>
          }
          style={{ marginBottom: 16 }}
        />
      )}
      {hasActiveFilters && !loadError && (
        <Alert
          type="info"
          showIcon
          message="Đang lọc sản phẩm — có thể không thấy đủ danh sách."
          action={
            <Button size="small" onClick={clearAllFilters}>
              Xóa bộ lọc
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
                Bộ lọc nâng cao
              </Space>
            ),
            children: (
              <>
                <Row gutter={[16, 8]}>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">Loại thuốc</Typography.Text>
                    <Select
                      mode="multiple"
                      allowClear
                      style={{ width: '100%' }}
                      placeholder="Tất cả"
                      value={filterDraft.drugTypes}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, drugTypes: v }))}
                      options={Object.entries(DRUG_TYPE_LABELS).map(([k, v]) => ({
                        value: Number(k),
                        label: v,
                      }))}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">Danh mục</Typography.Text>
                    <Select
                      mode="multiple"
                      allowClear
                      style={{ width: '100%' }}
                      placeholder="Tất cả"
                      value={filterDraft.categoryIds}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, categoryIds: v }))}
                      options={filterLookups.categories.map((c) => ({ value: c.id, label: c.name }))}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">Thương hiệu</Typography.Text>
                    <Select
                      mode="multiple"
                      allowClear
                      style={{ width: '100%' }}
                      placeholder="Tất cả"
                      value={filterDraft.brandIds}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, brandIds: v }))}
                      options={filterLookups.brands.map((b) => ({ value: b.id, label: b.name }))}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">Trạng thái</Typography.Text>
                    <Select
                      allowClear
                      style={{ width: '100%' }}
                      placeholder="Tất cả"
                      value={filterDraft.status}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, status: v }))}
                      options={Object.entries(STATUS_LABELS).map(([k, v]) => ({
                        value: Number(k),
                        label: v,
                      }))}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">Giá bán lẻ từ</Typography.Text>
                    <InputNumber
                      min={0}
                      style={{ width: '100%' }}
                      value={filterDraft.priceMin}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, priceMin: v ?? undefined }))}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">Giá bán lẻ đến</Typography.Text>
                    <InputNumber
                      min={0}
                      style={{ width: '100%' }}
                      value={filterDraft.priceMax}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, priceMax: v ?? undefined }))}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">Barcode</Typography.Text>
                    <Select
                      allowClear
                      style={{ width: '100%' }}
                      placeholder="Tất cả"
                      value={filterDraft.hasBarcode}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, hasBarcode: v }))}
                      options={[
                        { value: true, label: 'Có barcode' },
                        { value: false, label: 'Chưa có barcode' },
                      ]}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <Typography.Text type="secondary">Giá bán</Typography.Text>
                    <Select
                      allowClear
                      style={{ width: '100%' }}
                      placeholder="Tất cả"
                      value={filterDraft.hasPrice}
                      onChange={(v) => setFilterDraft((f) => ({ ...f, hasPrice: v }))}
                      options={[
                        { value: true, label: 'Đã có giá' },
                        { value: false, label: 'Chưa có giá' },
                      ]}
                    />
                  </Col>
                </Row>
                <Space style={{ marginTop: 12 }}>
                  <Button type="primary" onClick={applyFilters}>
                    Áp dụng bộ lọc
                  </Button>
                  <Button
                    onClick={() => {
                      setFilterDraft(emptyAdvancedFilters);
                      setAdvancedFilters(emptyAdvancedFilters);
                      setPage(1);
                    }}
                  >
                    Xóa bộ lọc
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
                  ? 'Không tải được dữ liệu'
                  : hasActiveFilters
                    ? 'Không có sản phẩm khớp bộ lọc'
                    : 'Chưa có sản phẩm'
              }
            >
              <Space>
                <Button onClick={load}>Tải lại</Button>
                {hasActiveFilters && <Button onClick={clearAllFilters}>Xóa bộ lọc</Button>}
              </Space>
            </Empty>
          ),
        }}
        pagination={{
          current: page,
          pageSize,
          total,
          showSizeChanger: true,
          showTotal: (t) => `${t} sản phẩm`,
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
