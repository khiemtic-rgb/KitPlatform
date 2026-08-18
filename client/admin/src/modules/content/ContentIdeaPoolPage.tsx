import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { App, Button, Card, Checkbox, Input, Select, Space, Table, Tag, Typography } from 'antd';
import { ClusterOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  analyzeContentPool,
  applyContentPoolFits,
  createContentPool,
  fetchContentBrands,
  fetchContentPackages,
  type ContentBrand,
  type ContentBrandFit,
  type ContentPackage,
} from '@/shared/api/content.api';

type CellKey = string;
const cellKey = (packageId: string, brandId: string) => `${packageId}:${brandId}`;

function parseBulk(text: string) {
  return text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .slice(0, 20)
    .map((line) => {
      const [title, insight, problem, coreMessage, source, factOrOpinion] = line
        .split('|')
        .map((s) => s.trim());
      return {
        title,
        insight: insight || undefined,
        problem: problem || undefined,
        coreMessage: coreMessage || undefined,
        source: source || undefined,
        factOrOpinion: factOrOpinion || undefined,
      };
    });
}

function verdictTag(fit?: ContentBrandFit) {
  if (!fit) return <Tag>Chưa chấm</Tag>;
  if (fit.verdict === 'fit') return <Tag color="green">Phù hợp {fit.score}</Tag>;
  if (fit.verdict === 'maybe') return <Tag color="gold">Có thể {fit.score}</Tag>;
  return <Tag>Bỏ {fit.score}</Tag>;
}

export function ContentIdeaPoolPage() {
  const { message } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [cores, setCores] = useState<ContentPackage[]>([]);
  const [homeBrandId, setHomeBrandId] = useState<string>();
  const [scoreBrandIds, setScoreBrandIds] = useState<string[]>([]);
  const [bulk, setBulk] = useState('');
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [picked, setPicked] = useState<Record<CellKey, boolean>>({});
  const [generateFits, setGenerateFits] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [brandList, pkg] = await Promise.all([
        fetchContentBrands(true),
        fetchContentPackages({ coresOnly: true }),
      ]);
      setBrands(brandList);
      setCores(pkg);
      setHomeBrandId((prev) => {
        if (prev && brandList.some((b) => b.id === prev)) return prev;
        return (
          brandList.find((b) => /kit/i.test(b.code))?.id ?? brandList[0]?.id
        );
      });
      setScoreBrandIds((prev) =>
        prev.length > 0 ? prev.filter((id) => brandList.some((b) => b.id === id)) : brandList.map((b) => b.id),
      );
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được Idea Pool'));
    } finally {
      setLoading(false);
    }
  }, [message]);

  useEffect(() => {
    void load();
  }, [load]);

  const defaultPicks = useMemo(() => {
    const next: Record<CellKey, boolean> = {};
    for (const pkg of cores) {
      for (const fit of pkg.brandFits ?? []) {
        next[cellKey(pkg.id, fit.brandId)] = fit.verdict === 'fit';
      }
    }
    return next;
  }, [cores]);

  const isPicked = (packageId: string, brandId: string) => {
    const key = cellKey(packageId, brandId);
    return key in picked ? picked[key] : Boolean(defaultPicks[key]);
  };

  const selectedCores = cores.filter((c) => selectedIds.includes(c.id));
  const matrixRows = selectedCores.length > 0 ? selectedCores : cores;
  const scoreBrands = brands.filter((b) => scoreBrandIds.includes(b.id));

  const plannedItems = matrixRows.flatMap((pkg) =>
    scoreBrands
      .filter((b) => isPicked(pkg.id, b.id))
      .map((b) => ({ packageId: pkg.id, brandId: b.id })),
  );

  const onAdd = async () => {
    const ideas = parseBulk(bulk);
    if (ideas.length === 0) {
      message.warning('Mỗi dòng một ý tưởng. Có thể thêm insight sau dấu |');
      return;
    }
    setBusy(true);
    try {
      const res = await createContentPool({ homeBrandId, ideas });
      message.success(res.message ?? `Đã thêm ${res.packages.length} ý tưởng`);
      setBulk('');
      setSelectedIds(res.packages.map((p) => p.id));
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Thêm pool thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const onAnalyze = async () => {
    const ids = (selectedIds.length > 0 ? selectedIds : cores.map((c) => c.id)).slice(0, 20);
    if (ids.length === 0) {
      message.warning('Thêm ý tưởng trước khi chấm Fit');
      return;
    }
    if (scoreBrandIds.length === 0) {
      message.warning('Chọn ít nhất một brand để chấm');
      return;
    }
    setBusy(true);
    try {
      await analyzeContentPool({ packageIds: ids, brandIds: scoreBrandIds, includeMaybe: true });
      message.success('Đã chấm Fit. Tick ô rồi tạo góc — brand skip mặc định bỏ.');
      setPicked({});
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Chấm Brand Fit thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const scoredCount = matrixRows.filter((p) => (p.brandFits ?? []).length > 0).length;

  const onApply = async () => {
    if (plannedItems.length === 0) {
      message.warning(
        scoredCount === 0
          ? 'Các ô còn «Chưa chấm». Bấm Chấm Brand Fit trước — ô Phù hợp sẽ được tick sẵn.'
          : 'Chưa tick ô Dùng. Ô Phù hợp được tick sẵn; Bỏ thì để trống.',
      );
      return;
    }
    setBusy(true);
    try {
      const res = await applyContentPoolFits({
        items: plannedItems,
        generateFits,
      });
      message.success(res.message ?? `Đã tạo ${res.created} góc brand`);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Tạo góc thất bại'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, marginBottom: 16, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            Idea Pool
          </Typography.Title>
          <Typography.Text type="secondary">
            Nghĩ ý tưởng gốc — AI chấm Fit từng brand. Không ép 1 idea × 6 bài. Bạn chỉ duyệt ô nào dùng.
          </Typography.Text>
        </div>
        <Space wrap>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
          <Button icon={<ClusterOutlined />} loading={busy} onClick={() => void onAnalyze()}>
            Chấm Brand Fit
          </Button>
          <Button
            type="primary"
            loading={busy}
            disabled={plannedItems.length === 0}
            onClick={() => void onApply()}
          >
            Tạo {plannedItems.length} góc đã chọn
          </Button>
        </Space>
      </div>

      <Card size="small" style={{ marginBottom: 16 }}>
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          <Space wrap>
            <Typography.Text>Lưu Core Idea vào</Typography.Text>
            <Select
              style={{ minWidth: 200 }}
              value={homeBrandId}
              onChange={setHomeBrandId}
              options={brands.map((b) => ({ value: b.id, label: `${b.name} (sổ tay)` }))}
            />
            <Typography.Text type="secondary">không có nghĩa brand này phải viết bài</Typography.Text>
          </Space>
          <div>
            <Typography.Text>Chấm Fit cho</Typography.Text>
            <Checkbox.Group
              style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginTop: 8 }}
              value={scoreBrandIds}
              onChange={(v) => setScoreBrandIds(v.map(String))}
              options={brands.map((b) => ({ value: b.id, label: b.name }))}
            />
          </div>
          <Input.TextArea
            rows={6}
            value={bulk}
            onChange={(e) => setBulk(e.target.value)}
            placeholder={
              'Mỗi dòng: Title | Insight | Problem | Core message | Nguồn | fact|opinion\n' +
              'Khách hàng không chỉ mua sản phẩm | Muốn được đồng hành | Chỉ tập trung bán | Giá trị ở mối quan hệ | opinion | opinion\n' +
              'FEFO giúp nhà thuốc giảm hàng cận hạn | Hết hạn là lỗ thật | Xuất lô mới trước | FEFO 3 bước | GPP / kho | fact'
            }
          />
          <Space wrap>
            <Button icon={<PlusOutlined />} loading={busy} onClick={() => void onAdd()}>
              Thêm vào pool
            </Button>
            <Checkbox checked={generateFits} onChange={(e) => setGenerateFits(e.target.checked)}>
              Generate theo nơi đăng sau khi tạo góc
            </Checkbox>
            {generateFits ? (
              <Typography.Text type="secondary">
                Mỗi góc chỉ viết Website / Fanpage / nhóm đã khai báo ở Thương hiệu.
              </Typography.Text>
            ) : null}
          </Space>
        </Space>
      </Card>

      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        {cores.length} ý tưởng gốc → sẽ tạo <strong>{plannedItems.length}</strong> góc brand (không phải{' '}
        {cores.length} × {scoreBrands.length || brands.length}). Chi tiết góc ở{' '}
        <Link to="/content/packages">Ý tưởng</Link>.
      </Typography.Paragraph>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={cores}
        pagination={false}
        scroll={{ x: 720 }}
        rowSelection={{
          selectedRowKeys: selectedIds,
          onChange: (keys) => setSelectedIds(keys.map(String)),
        }}
        columns={[
          {
            title: 'Core Idea',
            dataIndex: 'title',
            width: 260,
            fixed: 'left',
            render: (title: string, row: ContentPackage) => (
              <div>
                <Typography.Text strong>{title}</Typography.Text>
                {row.coreIdea?.insight ? (
                  <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 12 }}>
                    {row.coreIdea.insight}
                  </Typography.Paragraph>
                ) : null}
                {(row.adaptationCount ?? 0) > 0 ? (
                  <Tag color="blue" style={{ marginTop: 4 }}>
                    {row.adaptationCount} góc
                  </Tag>
                ) : null}
              </div>
            ),
          },
          ...scoreBrands.map((brand) => ({
            title: brand.name,
            width: 150,
            render: (_: unknown, row: ContentPackage) => {
              const fit = (row.brandFits ?? []).find((f) => f.brandId === brand.id);
              const checked = isPicked(row.id, brand.id);
              return (
                <Space direction="vertical" size={4}>
                  {verdictTag(fit)}
                  <Checkbox
                    checked={checked}
                    onChange={(e) =>
                      setPicked((prev) => ({ ...prev, [cellKey(row.id, brand.id)]: e.target.checked }))
                    }
                  >
                    Dùng
                  </Checkbox>
                  {fit?.angle ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {fit.angle}
                    </Typography.Text>
                  ) : null}
                </Space>
              );
            },
          })),
        ]}
      />
    </div>
  );
}
