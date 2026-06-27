import { useCallback, useEffect, useMemo, useState } from 'react';
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
      message.error(apiErrorMessage(error, 'Không tải được cấu hình tra cứu dược QG'));
    }
  }, [message]);

  const loadResults = useCallback(async () => {
    setLoading(true);
    try {
      const result = await searchNationalDrugs({ search: search || undefined, page, pageSize });
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tra cứu được danh mục dược QG'));
    } finally {
      setLoading(false);
    }
  }, [search, page, pageSize, message]);

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
        message.error(apiErrorMessage(error, 'Không tải được chi tiết thuốc QG'));
        setDetail(null);
      })
      .finally(() => setDetailLoading(false));
  }, [selectedDrugId, message]);

  const columns: ColumnsType<NationalDrugListItem> = useMemo(
    () => [
      { title: 'Mã QG', dataIndex: 'drugId', width: 130 },
      { title: 'Số ĐK', dataIndex: 'registrationNumber', width: 110 },
      { title: 'Tên thuốc', dataIndex: 'productName' },
      {
        title: 'Hoạt chất',
        key: 'ingredient',
        render: (_, row) =>
          [row.activeIngredient, row.strength].filter(Boolean).join(' · ') || '—',
      },
      { title: 'ĐVT', dataIndex: 'unitName', width: 70 },
      { title: 'Loại', dataIndex: 'drugCategoryLabel', width: 90 },
    ],
    [],
  );

  const fieldMapColumns: ColumnsType<NationalDrugFieldMap> = [
    { title: 'Trường QG (QĐ 522)', dataIndex: 'nationalLabel', width: 180 },
    { title: 'Mã API', dataIndex: 'nationalField', width: 120, render: (v) => <Typography.Text code>{v}</Typography.Text> },
    { title: '→ Trường ERP', dataIndex: 'productLabel', width: 160 },
    { title: 'Mã ERP', dataIndex: 'productField', width: 130, render: (v) => <Typography.Text code>{v}</Typography.Text> },
    { title: 'Ghi chú', dataIndex: 'notes', render: (v) => v ?? '—' },
  ];

  const handleCreateProduct = async () => {
    if (!selectedDrugId) return;
    setPrefillLoading(true);
    try {
      const prefill = await fetchNationalDrugPrefill(selectedDrugId);
      setProductPrefill(mapNationalPrefillToProductForm(prefill));
      setCreatedProduct(null);
      setDrawerOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tạo được bản ghi điền sẵn'));
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
            <span>Tra cứu CSDL Dược QG — MVP mock (QĐ 522)</span>
            {connection && (
              <Tag color={connection.isLive ? 'green' : 'gold'}>{connection.modeLabel}</Tag>
            )}
          </Space>
        }
        description={
          connection?.message ??
          'Dữ liệu mẫu nội bộ. Khi có tài khoản liên thông: cấu hình NationalDrugCatalog:Mode = sandbox hoặc live.'
        }
      />

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={14}>
          <Card
            title="Danh mục CSDL Dược QG"
            extra={
              <Space>
                <Input
                  allowClear
                  placeholder="Mã QG, số ĐK, tên, hoạt chất, barcode…"
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
                  Tìm
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
            title="Chi tiết bản ghi QG"
            extra={
              <Button
                type="primary"
                icon={<PlusOutlined />}
                disabled={!selectedDrugId}
                loading={prefillLoading}
                onClick={() => void handleCreateProduct()}
              >
                Tạo sản phẩm
              </Button>
            }
          >
            {!detail ? (
              <Typography.Text type="secondary">Chọn một dòng trong bảng để xem chi tiết.</Typography.Text>
            ) : (
              <Descriptions
                size="small"
                column={1}
                bordered
                loading={detailLoading}
                styles={{ label: { width: 130 } }}
              >
                <Descriptions.Item label="Mã QG">{detail.drugId}</Descriptions.Item>
                <Descriptions.Item label="Số ĐK">{detail.registrationNumber}</Descriptions.Item>
                <Descriptions.Item label="Tên thuốc">{detail.productName}</Descriptions.Item>
                <Descriptions.Item label="Hoạt chất">
                  {[detail.activeIngredient, detail.strength].filter(Boolean).join(' · ') || '—'}
                </Descriptions.Item>
                <Descriptions.Item label="Dạng bào chế">{detail.dosageForm ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="Quy cách">{detail.packaging ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="ĐVT">{detail.unitName ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="NSX">{detail.manufacturer ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="Xuất xứ">{detail.countryOfOrigin ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="Loại">{detail.drugCategoryLabel}</Descriptions.Item>
                <Descriptions.Item label="Barcode">{detail.barcode ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="ATC">{detail.atcCode ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="HSD ĐK">
                  {detail.registrationExpiryDate ? formatDisplayDate(detail.registrationExpiryDate) : '—'}
                </Descriptions.Item>
              </Descriptions>
            )}
          </Card>
        </Col>
      </Row>

      <Card title="Map field QĐ 522 → Danh mục nhà thuốc" style={{ marginTop: 16 }}>
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          Tham chiếu khi triển khai connector thật. MVP mock dùng cùng mapping để điền form tạo sản phẩm.
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
          message.success(`Đã tạo ${product.productCode} — liên kết QG ${product.nationalDrugId ?? ''}`.trim());
        }}
        onUpdated={() => {}}
      />
    </>
  );
}
