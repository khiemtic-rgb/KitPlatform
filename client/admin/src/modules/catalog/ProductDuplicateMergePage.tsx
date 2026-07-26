import { useCallback, useEffect, useMemo, useState, type Dispatch, type SetStateAction } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, Navigate } from 'react-router-dom';
import {
  Alert,
  App,
  Button,
  Card,
  InputNumber,
  Popconfirm,
  Radio,
  Space,
  Table,
  Tabs,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  ArrowLeftOutlined,
  DeleteOutlined,
  EyeInvisibleOutlined,
  MergeCellsOutlined,
  ReloadOutlined,
  UndoOutlined,
} from '@ant-design/icons';
import {
  fetchDuplicateProductClusters,
  fetchHiddenProducts,
  fetchProductMergeHistory,
  fetchSimilarProductClusters,
  hideProductFromMerge,
  mergeDuplicateProductStock,
  permanentDeleteHiddenProduct,
  restoreHiddenProduct,
  type DuplicateProductCluster,
  type DuplicateProductMember,
  type HiddenProductItem,
  type ProductMergeHistoryItem,
} from '@/shared/api/catalog.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useCanCatalogMerge } from '@/shared/auth/usePermission';
import { formatDisplayDateTime } from '@/shared/utils/date';
import { formatDisplayQuantity } from '@/shared/utils/money';
import './ProductDuplicateMergePage.css';

/**
 * Họ ĐVT có thể quy đổi nội bộ (vd Viên↔Vỉ↔Hộp).
 * Khác họ (vd Hộp vs Lọ) → coi là SKU khác, không gộp tồn.
 */
function unitFamily(unitName?: string | null): string {
  const u = (unitName ?? '').trim().toLowerCase();
  if (!u) return 'unknown';

  const hit = (...needles: string[]) => needles.some((n) => u.includes(n));

  // Cùng chuỗi đóng gói rắn: Viên → Vỉ → Gói → Hộp → Thùng
  if (hit('viên', 'vien', 'vỉ', 'blister', 'gói', 'goi', 'hộp', 'hop', 'box', 'thùng', 'thung', 'lốc', 'loc'))
    return 'solid_pack';
  // Chai / lọ / hũ — khác họ với Hộp (thường là SKU khác)
  if (hit('chai', 'lọ', 'hũ', 'bottle', 'jar') || u === 'lo') return 'bottle';
  if (hit('tuýp', 'tuyp', 'tube', 'ống') || u === 'ong') return 'tube';
  if (hit('ml', 'lít', 'lit', 'kg', 'mg') || u === 'g') return 'measure';
  return `other:${u}`;
}

function clusterUnitsCompatible(products: { unitName?: string | null }[]): boolean {
  const families = new Set(products.map((p) => unitFamily(p.unitName)));
  return families.size <= 1;
}

type RowModel = DuplicateProductMember & {
  key: string;
  clusterKey: string;
  clusterName: string;
  isFirstInCluster: boolean;
  clusterSize: number;
  clusterIndex: number;
  maxSimilarity?: number | null;
  unitsCompatible: boolean;
};

function toRows(clusterList: DuplicateProductCluster[]): RowModel[] {
  const list: RowModel[] = [];
  clusterList.forEach((cluster, clusterIndex) => {
    const compatible = clusterUnitsCompatible(cluster.products);
    cluster.products.forEach((p, index) => {
      list.push({
        ...p,
        key: p.id,
        clusterKey: cluster.normalizedName,
        clusterName: cluster.displayName,
        isFirstInCluster: index === 0,
        clusterSize: cluster.products.length,
        clusterIndex,
        maxSimilarity: cluster.maxSimilarity,
        unitsCompatible: compatible,
      });
    });
  });
  return list;
}

function splitRows(all: RowModel[]) {
  return {
    convertible: all.filter((r) => r.unitsCompatible),
    incompatible: all.filter((r) => !r.unitsCompatible),
  };
}

/** Đánh lại chỉ số nhóm theo thứ tự hiển thị — tránh hai nhóm liền kề cùng màu sau khi lọc. */
function reindexClusterTones(rows: RowModel[]): RowModel[] {
  const order = new Map<string, number>();
  return rows.map((row) => {
    if (!order.has(row.clusterKey)) order.set(row.clusterKey, order.size);
    return { ...row, clusterIndex: order.get(row.clusterKey)! };
  });
}

function clusterRowClassName(row: RowModel, keeperId?: string): string {
  const tone = row.clusterIndex % 2 === 0 ? 'product-dup-cluster-a' : 'product-dup-cluster-b';
  const parts = [tone];
  // Vạch ngăn giữa các nhóm — tránh hai nhóm liền kề trông như một khối.
  if (row.isFirstInCluster && row.clusterIndex > 0) parts.push('product-dup-cluster-start');
  if (keeperId === row.id) parts.push('product-dup-keeper-row');
  return parts.join(' ');
}

function applyClusterKeepers(
  clusterList: DuplicateProductCluster[],
  setKeepers: Dispatch<SetStateAction<Record<string, string>>>,
  setFactors: Dispatch<SetStateAction<Record<string, number | null>>>,
) {
  const nextKeepers: Record<string, string> = {};
  const nextFactors: Record<string, number | null> = {};
  for (const cluster of clusterList) {
    const suggested = cluster.products.find((p) => p.suggestedKeeper) ?? cluster.products[0];
    nextKeepers[cluster.normalizedName] = suggested.id;
    for (const p of cluster.products) {
      if (p.id !== suggested.id) nextFactors[p.id] = null;
    }
  }
  setKeepers((prev) => ({ ...prev, ...nextKeepers }));
  setFactors((prev) => ({ ...prev, ...nextFactors }));
}

export function ProductDuplicateMergePage() {
  const { t } = useTranslation('catalog', { keyPrefix: 'duplicateMerge' });
  const { message: msg } = App.useApp();
  const canMerge = useCanCatalogMerge();

  const [activeTab, setActiveTab] = useState('merge');
  const [loading, setLoading] = useState(false);
  const [clusters, setClusters] = useState<DuplicateProductCluster[]>([]);
  const [clusterCount, setClusterCount] = useState(0);
  const [productCount, setProductCount] = useState(0);

  const [similarLoading, setSimilarLoading] = useState(false);
  const [similarClusters, setSimilarClusters] = useState<DuplicateProductCluster[]>([]);
  const [similarClusterCount, setSimilarClusterCount] = useState(0);
  const [similarProductCount, setSimilarProductCount] = useState(0);

  const [keepers, setKeepers] = useState<Record<string, string>>({});
  const [factors, setFactors] = useState<Record<string, number | null>>({});
  const [mergingId, setMergingId] = useState<string | null>(null);

  const [historyLoading, setHistoryLoading] = useState(false);
  const [history, setHistory] = useState<ProductMergeHistoryItem[]>([]);

  const [hiddenLoading, setHiddenLoading] = useState(false);
  const [hidden, setHidden] = useState<HiddenProductItem[]>([]);
  const [actingId, setActingId] = useState<string | null>(null);

  const clusterByKey = useMemo(() => {
    const map = new Map<string, DuplicateProductCluster>();
    for (const c of clusters) map.set(c.normalizedName, c);
    for (const c of similarClusters) map.set(c.normalizedName, c);
    return map;
  }, [clusters, similarClusters]);

  const loadClusters = useCallback(async () => {
    setLoading(true);
    try {
      const data = await fetchDuplicateProductClusters();
      setClusters(data.clusters);
      setClusterCount(data.clusterCount);
      setProductCount(data.productCount);
      applyClusterKeepers(data.clusters, setKeepers, setFactors);
    } catch (error) {
      msg.error(apiErrorMessage(error, t('loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [msg, t]);

  const loadSimilarClusters = useCallback(async () => {
    setSimilarLoading(true);
    try {
      const data = await fetchSimilarProductClusters(0.8);
      setSimilarClusters(data.clusters);
      setSimilarClusterCount(data.clusterCount);
      setSimilarProductCount(data.productCount);
      applyClusterKeepers(data.clusters, setKeepers, setFactors);
    } catch (error) {
      msg.error(apiErrorMessage(error, t('similarLoadFailed')));
    } finally {
      setSimilarLoading(false);
    }
  }, [msg, t]);

  const loadHistory = useCallback(async () => {
    setHistoryLoading(true);
    try {
      setHistory(await fetchProductMergeHistory());
    } catch (error) {
      msg.error(apiErrorMessage(error, t('historyLoadFailed')));
    } finally {
      setHistoryLoading(false);
    }
  }, [msg, t]);

  const loadHidden = useCallback(async () => {
    setHiddenLoading(true);
    try {
      setHidden(await fetchHiddenProducts());
    } catch (error) {
      msg.error(apiErrorMessage(error, t('hiddenLoadFailed')));
    } finally {
      setHiddenLoading(false);
    }
  }, [msg, t]);

  useEffect(() => {
    if (activeTab === 'merge') void loadClusters();
    if (activeTab === 'similar') void loadSimilarClusters();
    if (activeTab === 'manualReview') {
      void loadClusters();
      void loadSimilarClusters();
    }
    if (activeTab === 'history') void loadHistory();
    if (activeTab === 'hidden') void loadHidden();
  }, [activeTab, loadClusters, loadSimilarClusters, loadHistory, loadHidden]);

  const rows = useMemo(() => toRows(clusters), [clusters]);
  const similarRows = useMemo(() => toRows(similarClusters), [similarClusters]);
  const mergeSplit = useMemo(() => {
    const split = splitRows(rows);
    return {
      convertible: reindexClusterTones(split.convertible),
      incompatible: split.incompatible,
    };
  }, [rows]);
  const similarSplit = useMemo(() => {
    const split = splitRows(similarRows);
    return {
      convertible: reindexClusterTones(split.convertible),
      incompatible: split.incompatible,
    };
  }, [similarRows]);

  /** Gộp các nhóm khác họ ĐVT từ cả «trùng» và «gần giống», đánh lại chỉ số nền để tô xen kẽ. */
  const manualReviewRows = useMemo(
    () => reindexClusterTones([...mergeSplit.incompatible, ...similarSplit.incompatible]),
    [mergeSplit.incompatible, similarSplit.incompatible],
  );

  const incompatibleClusterCount = (list: RowModel[]) =>
    new Set(list.map((r) => r.clusterKey)).size;

  const reloadAfterMerge = async () => {
    if (activeTab === 'manualReview') {
      await Promise.all([loadClusters(), loadSimilarClusters()]);
    } else if (activeTab === 'similar') {
      await loadSimilarClusters();
    } else {
      await loadClusters();
    }
  };

  const handleMerge = async (row: RowModel) => {
    const keeperId = keepers[row.clusterKey];
    if (!keeperId || keeperId === row.id) {
      msg.warning(t('pickKeeperFirst'));
      return;
    }
    const factor = factors[row.id];
    if (factor == null || factor <= 0) {
      msg.warning(t('factorRequired'));
      return;
    }

    setMergingId(row.id);
    try {
      const result = await mergeDuplicateProductStock({
        keeperProductId: keeperId,
        sourceProductId: row.id,
        conversionFactor: factor,
        softDeleteSource: true,
      });
      msg.success(
        t('mergeSuccess', {
          source: row.productCode,
          added: formatDisplayQuantity(result.totalKeeperQuantityAdded),
          deleted: result.sourceSoftDeleted ? t('sourceHidden') : '',
        }),
      );
      await reloadAfterMerge();
    } catch (error) {
      msg.error(apiErrorMessage(error, t('mergeFailed')));
    } finally {
      setMergingId(null);
    }
  };

  const handleRestore = async (row: HiddenProductItem) => {
    setActingId(row.id);
    try {
      await restoreHiddenProduct(row.id);
      msg.success(t('restoreSuccess', { code: row.productCode }));
      await loadHidden();
    } catch (error) {
      msg.error(apiErrorMessage(error, t('restoreFailed')));
    } finally {
      setActingId(null);
    }
  };

  const handlePermanentDelete = async (row: HiddenProductItem) => {
    setActingId(row.id);
    try {
      await permanentDeleteHiddenProduct(row.id);
      msg.success(t('permanentDeleteSuccess', { code: row.productCode }));
      await loadHidden();
    } catch (error) {
      msg.error(apiErrorMessage(error, t('permanentDeleteFailed')));
    } finally {
      setActingId(null);
    }
  };

  const handleHide = async (row: RowModel) => {
    setActingId(row.id);
    try {
      await hideProductFromMerge(row.id);
      msg.success(t('hideSuccess', { code: row.productCode }));
      await reloadAfterMerge();
    } catch (error) {
      msg.error(apiErrorMessage(error, t('hideFailed')));
    } finally {
      setActingId(null);
    }
  };

  /** Ẩn trước; nếu đủ điều kiện thì xóa cứng. Còn tồn/lịch sử → chỉ ẩn. */
  const handleHideOrPurge = async (row: RowModel) => {
    setActingId(row.id);
    try {
      await hideProductFromMerge(row.id);
      try {
        await permanentDeleteHiddenProduct(row.id);
        msg.success(t('permanentDeleteSuccess', { code: row.productCode }));
      } catch (purgeError) {
        msg.warning(
          apiErrorMessage(purgeError, t('hideOnlyAfterDeleteBlocked', { code: row.productCode })),
        );
      }
      await reloadAfterMerge();
    } catch (error) {
      msg.error(apiErrorMessage(error, t('hideFailed')));
    } finally {
      setActingId(null);
    }
  };

  if (!canMerge) {
    return <Navigate to="/catalog/products" replace />;
  }

  const mergeColumns: ColumnsType<RowModel> = [
    {
      title: t('columns.group'),
      dataIndex: 'clusterName',
      width: 220,
      onCell: (row) => (row.isFirstInCluster ? { rowSpan: row.clusterSize } : { rowSpan: 0 }),
      render: (name: string, row) => (
        <div>
          <Typography.Text strong>{name}</Typography.Text>
          <div>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t('groupSize', { count: row.clusterSize })}
            </Typography.Text>
            {row.maxSimilarity != null && row.maxSimilarity > 0 ? (
              <div>
                <Tag color="orange" style={{ marginTop: 4 }}>
                  {t('similarityPct', { pct: Math.round(row.maxSimilarity * 100) })}
                </Tag>
              </div>
            ) : null}
            {!row.unitsCompatible ? (
              <div>
                <Tag color="warning" style={{ marginTop: 4 }}>
                  {t('incompatibleUnits.tag')}
                </Tag>
              </div>
            ) : null}
          </div>
        </div>
      ),
    },
    {
      title: t('columns.code'),
      dataIndex: 'productCode',
      width: 120,
      render: (code: string, row) => (
        <div>
          <Typography.Text code>{code}</Typography.Text>
          <div>
            <Typography.Text type="secondary" style={{ fontSize: 11 }} ellipsis>
              {row.productName}
            </Typography.Text>
          </div>
        </div>
      ),
    },
    {
      title: t('columns.unit'),
      dataIndex: 'unitName',
      width: 90,
      render: (unit: string) => unit || '—',
    },
    {
      title: t('columns.stock'),
      dataIndex: 'totalQuantity',
      width: 100,
      align: 'right',
      render: (qty: number, row) => (
        <span>
          {formatDisplayQuantity(qty)}
          {row.warehouseCount > 1 ? (
            <Typography.Text type="secondary" style={{ display: 'block', fontSize: 11 }}>
              {t('warehouses', { count: row.warehouseCount })}
            </Typography.Text>
          ) : null}
        </span>
      ),
    },
    {
      title: t('columns.keeper'),
      width: 110,
      render: (_, row) => (
        <Radio
          checked={keepers[row.clusterKey] === row.id}
          onChange={() => {
            setKeepers((prev) => ({ ...prev, [row.clusterKey]: row.id }));
            setFactors((prev) => {
              const next = { ...prev };
              delete next[row.id];
              const cluster = clusterByKey.get(row.clusterKey);
              for (const p of cluster?.products ?? []) {
                if (p.id !== row.id && next[p.id] === undefined) next[p.id] = null;
              }
              return next;
            });
          }}
          disabled={!canMerge}
        >
          {t('keep')}
        </Radio>
      ),
    },
    {
      title: t('columns.factor'),
      width: 160,
      render: (_, row) => {
        const isKeeper = keepers[row.clusterKey] === row.id;
        if (isKeeper) return <Tag color="green">{t('keeperHint')}</Tag>;
        return (
          <InputNumber
            min={0.0001}
            step={1}
            style={{ width: 120 }}
            placeholder={t('factorPlaceholder')}
            value={factors[row.id] ?? undefined}
            disabled={!canMerge}
            onChange={(v) =>
              setFactors((prev) => ({
                ...prev,
                [row.id]: v == null ? null : Number(v),
              }))
            }
          />
        );
      },
    },
    {
      title: t('columns.converted'),
      width: 120,
      align: 'right',
      render: (_, row) => {
        const isKeeper = keepers[row.clusterKey] === row.id;
        if (isKeeper) return '—';
        const factor = factors[row.id];
        if (factor == null || factor <= 0) return '—';
        return (
          <Typography.Text strong>{formatDisplayQuantity(row.totalQuantity * factor)}</Typography.Text>
        );
      },
    },
    {
      title: t('columns.action'),
      width: 260,
      fixed: 'right',
      render: (_, row) => {
        const isKeeper = keepers[row.clusterKey] === row.id;
        const factor = factors[row.id];
        const ready = canMerge && !isKeeper && factor != null && factor > 0;
        const needsManualReview = !row.unitsCompatible;
        const busy = actingId === row.id || mergingId === row.id;
        return (
          <Space size={4} wrap>
            {!isKeeper ? (
              <Popconfirm
                title={t(needsManualReview ? 'incompatibleUnits.confirmTitle' : 'confirmTitle')}
                description={
                  needsManualReview
                    ? t('incompatibleUnits.confirmBody', {
                        source: row.productCode,
                        factor: factor ?? '?',
                        qty: formatDisplayQuantity(row.totalQuantity * (factor ?? 0)),
                      })
                    : t('confirmBody', {
                        source: row.productCode,
                        factor: factor ?? '?',
                        qty: formatDisplayQuantity(row.totalQuantity * (factor ?? 0)),
                      })
                }
                okText={needsManualReview ? t('incompatibleUnits.confirmOk') : undefined}
                okButtonProps={needsManualReview ? { danger: true } : undefined}
                disabled={!ready}
                onConfirm={() => void handleMerge(row)}
              >
                <Button
                  type="primary"
                  size="small"
                  icon={<MergeCellsOutlined />}
                  disabled={!ready}
                  loading={mergingId === row.id}
                >
                  {t('merge')}
                </Button>
              </Popconfirm>
            ) : null}
            <Popconfirm
              title={t('hideConfirmTitle')}
              description={
                row.totalQuantity > 0
                  ? t('hideConfirmBodyWithStock', {
                      code: row.productCode,
                      qty: formatDisplayQuantity(row.totalQuantity),
                    })
                  : t('hideConfirmBody', { code: row.productCode })
              }
              disabled={!canMerge || busy}
              onConfirm={() => void handleHide(row)}
            >
              <Button
                size="small"
                icon={<EyeInvisibleOutlined />}
                disabled={!canMerge || busy}
                loading={actingId === row.id}
              >
                {t('hide')}
              </Button>
            </Popconfirm>
            <Popconfirm
              title={t('purgeConfirmTitle')}
              description={
                row.totalQuantity > 0
                  ? t('purgeConfirmBodyWithStock', {
                      code: row.productCode,
                      qty: formatDisplayQuantity(row.totalQuantity),
                    })
                  : t('purgeConfirmBody', { code: row.productCode })
              }
              okButtonProps={{ danger: true }}
              disabled={!canMerge || busy}
              onConfirm={() => void handleHideOrPurge(row)}
            >
              <Button
                size="small"
                danger
                icon={<DeleteOutlined />}
                disabled={!canMerge || busy}
                loading={actingId === row.id}
              >
                {t('purge')}
              </Button>
            </Popconfirm>
          </Space>
        );
      },
    },
  ];

  const historyColumns: ColumnsType<ProductMergeHistoryItem> = [
    {
      title: t('history.columns.time'),
      dataIndex: 'mergedAt',
      width: 160,
      render: (v: string) => formatDisplayDateTime(v),
    },
    {
      title: t('history.columns.source'),
      render: (_, row) => (
        <span>
          <Typography.Text code>{row.sourceProductCode || '—'}</Typography.Text>
          <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
            {row.sourceProductName}
          </Typography.Text>
          {row.sourceStillHidden ? <Tag color="orange">{t('history.stillHidden')}</Tag> : null}
        </span>
      ),
    },
    {
      title: t('history.columns.keeper'),
      render: (_, row) => (
        <span>
          <Typography.Text code>{row.keeperProductCode || '—'}</Typography.Text>
          <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
            {row.keeperProductName}
          </Typography.Text>
        </span>
      ),
    },
    {
      title: t('history.columns.sourceQty'),
      dataIndex: 'sourceQuantity',
      width: 100,
      align: 'right',
      render: (v: number) => formatDisplayQuantity(v),
    },
    {
      title: t('history.columns.addedQty'),
      dataIndex: 'keeperQuantityAdded',
      width: 110,
      align: 'right',
      render: (v: number) => formatDisplayQuantity(v),
    },
    {
      title: t('history.columns.notes'),
      dataIndex: 'notes',
      ellipsis: true,
      render: (v?: string) => v || '—',
    },
  ];

  const hiddenColumns: ColumnsType<HiddenProductItem> = [
    {
      title: t('columns.code'),
      dataIndex: 'productCode',
      width: 120,
      render: (code: string) => <Typography.Text code>{code}</Typography.Text>,
    },
    { title: t('hidden.columns.name'), dataIndex: 'productName' },
    {
      title: t('columns.unit'),
      dataIndex: 'unitName',
      width: 80,
      render: (v: string) => v || '—',
    },
    {
      title: t('hidden.columns.deletedAt'),
      dataIndex: 'deletedAt',
      width: 160,
      render: (v?: string) => (v ? formatDisplayDateTime(v) : '—'),
    },
    {
      title: t('hidden.columns.stock'),
      dataIndex: 'remainingStock',
      width: 90,
      align: 'right',
      render: (v: number) => formatDisplayQuantity(v),
    },
    {
      title: t('hidden.columns.flags'),
      width: 140,
      render: (_, row) => (
        <Space size={4} wrap>
          {row.mergedAway ? <Tag color="blue">{t('hidden.mergedAway')}</Tag> : null}
          {row.canHardDelete ? (
            <Tag color="green">{t('hidden.canPurge')}</Tag>
          ) : (
            <Tooltip title={row.blockReasons.join(' ')}>
              <Tag color="default">{t('hidden.keepHidden')}</Tag>
            </Tooltip>
          )}
        </Space>
      ),
    },
    {
      title: t('columns.action'),
      width: 220,
      fixed: 'right',
      render: (_, row) => (
        <Space wrap>
          <Popconfirm
            title={t('hidden.restoreConfirm')}
            onConfirm={() => void handleRestore(row)}
          >
            <Button size="small" icon={<UndoOutlined />} loading={actingId === row.id}>
              {t('hidden.restore')}
            </Button>
          </Popconfirm>
          <Popconfirm
            title={t('hidden.permanentConfirm')}
            description={
              row.canHardDelete
                ? t('hidden.permanentBodyOk')
                : row.blockReasons.join(' ') || t('hidden.permanentBodyBlocked')
            }
            disabled={!row.canHardDelete}
            onConfirm={() => void handlePermanentDelete(row)}
          >
            <Button
              size="small"
              danger
              icon={<DeleteOutlined />}
              disabled={!row.canHardDelete}
              loading={actingId === row.id}
            >
              {t('hidden.permanent')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  const reload = () => {
    if (activeTab === 'merge') return loadClusters();
    if (activeTab === 'similar') return loadSimilarClusters();
    if (activeTab === 'manualReview') return Promise.all([loadClusters(), loadSimilarClusters()]);
    if (activeTab === 'history') return loadHistory();
    return loadHidden();
  };
  const reloading =
    activeTab === 'merge'
      ? loading
      : activeTab === 'similar'
        ? similarLoading
        : activeTab === 'manualReview'
          ? loading || similarLoading
          : activeTab === 'history'
            ? historyLoading
            : hiddenLoading;

  return (
    <Card
      title={
        <Space>
          <Link to="/catalog/products">
            <Button type="text" icon={<ArrowLeftOutlined />} />
          </Link>
          <span>{t('title')}</span>
        </Space>
      }
      extra={<Button icon={<ReloadOutlined />} onClick={() => void reload()} loading={reloading} />}
    >
      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        items={[
          {
            key: 'merge',
            label: t('tabs.merge'),
            children: (
              <>
                <Alert
                  type="info"
                  showIcon
                  style={{ marginBottom: 16 }}
                  message={t('guideTitle')}
                  description={
                    <ol style={{ margin: 0, paddingLeft: 18 }}>
                      <li>{t('guide1')}</li>
                      <li>{t('guide2')}</li>
                      <li>{t('guide3')}</li>
                      <li>{t('guide4')}</li>
                    </ol>
                  }
                />
                <Typography.Paragraph type="secondary">
                  {t('summary', { clusters: clusterCount, products: productCount })}
                  {mergeSplit.incompatible.length > 0
                    ? ` · ${t('incompatibleUnits.movedHint', {
                        products: mergeSplit.incompatible.length,
                      })}`
                    : ''}
                </Typography.Paragraph>
                <Table
                  rowKey="key"
                  size="middle"
                  loading={loading}
                  dataSource={mergeSplit.convertible}
                  columns={mergeColumns}
                  pagination={false}
                  scroll={{ x: 1100 }}
                  locale={{ emptyText: t('empty') }}
                  rowClassName={(row) => clusterRowClassName(row, keepers[row.clusterKey])}
                />
              </>
            ),
          },
          {
            key: 'similar',
            label: t('tabs.similar'),
            children: (
              <>
                <Alert
                  type="warning"
                  showIcon
                  style={{ marginBottom: 16 }}
                  message={t('similarGuideTitle')}
                  description={
                    <ol style={{ margin: 0, paddingLeft: 18 }}>
                      <li>{t('similarGuide1')}</li>
                      <li>{t('similarGuide2')}</li>
                      <li>{t('similarGuide3')}</li>
                      <li>{t('similarGuide4')}</li>
                      <li>{t('similarGuide5')}</li>
                    </ol>
                  }
                />
                <Typography.Paragraph type="secondary">
                  {t('similarSummary', {
                    clusters: similarClusterCount,
                    products: similarProductCount,
                  })}
                  {similarSplit.incompatible.length > 0
                    ? ` · ${t('incompatibleUnits.movedHint', {
                        products: similarSplit.incompatible.length,
                      })}`
                    : ''}
                </Typography.Paragraph>
                <Table
                  rowKey="key"
                  size="middle"
                  loading={similarLoading}
                  dataSource={similarSplit.convertible}
                  columns={mergeColumns}
                  pagination={{ pageSize: 50, hideOnSinglePage: true }}
                  scroll={{ x: 1100 }}
                  locale={{ emptyText: t('similarEmpty') }}
                  rowClassName={(row) => clusterRowClassName(row, keepers[row.clusterKey])}
                />
              </>
            ),
          },
          {
            key: 'manualReview',
            label: t('incompatibleUnits.tabLabel'),
            children: (
              <>
                <Alert
                  type="warning"
                  showIcon
                  style={{ marginBottom: 16 }}
                  message={t('incompatibleUnits.guideTitle')}
                  description={t('incompatibleUnits.guideBody')}
                />
                <Typography.Paragraph type="secondary">
                  {t('incompatibleUnits.summaryInline', {
                    clusters: incompatibleClusterCount(manualReviewRows),
                    products: manualReviewRows.length,
                  })}
                </Typography.Paragraph>
                <Table
                  rowKey="key"
                  size="middle"
                  loading={loading || similarLoading}
                  dataSource={manualReviewRows}
                  columns={mergeColumns}
                  pagination={{ pageSize: 50, hideOnSinglePage: true }}
                  scroll={{ x: 1100 }}
                  locale={{ emptyText: t('incompatibleUnits.empty') }}
                  rowClassName={(row) => clusterRowClassName(row, keepers[row.clusterKey])}
                />
              </>
            ),
          },
          {
            key: 'history',
            label: t('tabs.history'),
            children: (
              <>
                <Alert
                  type="info"
                  showIcon
                  style={{ marginBottom: 16 }}
                  message={t('history.guide')}
                />
                <Table
                  rowKey="mergeId"
                  size="middle"
                  loading={historyLoading}
                  dataSource={history}
                  columns={historyColumns}
                  pagination={{ pageSize: 20 }}
                  scroll={{ x: 960 }}
                  locale={{ emptyText: t('history.empty') }}
                />
              </>
            ),
          },
          {
            key: 'hidden',
            label: t('tabs.hidden'),
            children: (
              <>
                <Alert
                  type="warning"
                  showIcon
                  style={{ marginBottom: 16 }}
                  message={t('hidden.guideTitle')}
                  description={t('hidden.guideBody')}
                />
                <Table
                  rowKey="id"
                  size="middle"
                  loading={hiddenLoading}
                  dataSource={hidden}
                  columns={hiddenColumns}
                  pagination={{ pageSize: 20 }}
                  scroll={{ x: 1000 }}
                  locale={{ emptyText: t('hidden.empty') }}
                />
              </>
            ),
          },
        ]}
      />
    </Card>
  );
}
