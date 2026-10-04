import { useEffect, useState } from "react";
import { App, Button, DatePicker, Form, Input, Modal, Select, Space, Table, Tag, Typography } from "antd";
import type { Dayjs } from "dayjs";
import { PlusOutlined, ReloadOutlined } from "@ant-design/icons";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { apiErrorMessage } from "@/shared/api/api-error";
import {
  createContentArticleSeries,
  fetchContentArticleSeries,
  fetchContentBrands,
  fetchContentPackages,
  type ContentArticleSeries,
  type ContentBrand,
  type ContentPackage,
} from "@/shared/api/content.api";
import {
  ContentLineageTrail,
  seriesBrandAngle,
  seriesProgressLabel,
  seriesTerritory,
} from "@/modules/content/content-lineage";

const STATUSES = ["DRAFT", "PLANNED", "ACTIVE", "PAUSED", "COMPLETED", "CANCELLED"];

export function ContentArticleSeriesPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const prefillPackageId = searchParams.get("packageId") ?? undefined;
  const [rows, setRows] = useState<ContentArticleSeries[]>([]);
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [packages, setPackages] = useState<ContentPackage[]>([]);
  const [cores, setCores] = useState<ContentPackage[]>([]);
  const [loading, setLoading] = useState(false);
  const [brandId, setBrandId] = useState<string>();
  const [status, setStatus] = useState<string>();
  const [corePackageId, setCorePackageId] = useState<string>();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();

  const load = async () => {
    setLoading(true);
    try {
      const [list, brandRows, coreRows] = await Promise.all([
        fetchContentArticleSeries({
          brandId,
          status,
          corePackageId,
          from: range?.[0]?.startOf("day").toISOString(),
          to: range?.[1]?.endOf("day").toISOString(),
        }),
        fetchContentBrands(),
        fetchContentPackages({ coresOnly: true }),
      ]);
      setRows(list);
      setBrands(brandRows);
      setCores(coreRows);
    } catch (e) {
      message.error(apiErrorMessage(e, "Lỗi"));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, [brandId, status, corePackageId, range]);

  useEffect(() => {
    if (!prefillPackageId) return;
    void (async () => {
      const packs = await fetchContentPackages({ coresOnly: false });
      setPackages(packs);
      form.setFieldsValue({ sourcePackageId: prefillPackageId, episodeCount: 10 });
      setOpen(true);
    })();
  }, [prefillPackageId, form]);

  return (
    <Space direction="vertical" size={16} style={{ width: "100%" }}>
      <Space wrap style={{ justifyContent: "space-between", width: "100%" }}>
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>
            Series — Story Control Center
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            Mỗi Series là một câu chuyện đang tiến triển. Episode là từng bước. Content là sản phẩm.
          </Typography.Paragraph>
          <ContentLineageTrail
            items={[
              { label: "Ý tưởng", to: "/content/pool" },
              { label: "Góc brand", to: "/content/packages" },
              { label: "Series" },
            ]}
          />
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={async () => {
              const packs = await fetchContentPackages({ coresOnly: false });
              setPackages(packs);
              setOpen(true);
            }}
          >
            Tạo từ góc brand
          </Button>
        </Space>
      </Space>
      <Space wrap>
        <Select
          allowClear
          placeholder="Brand"
          style={{ width: 180 }}
          value={brandId}
          onChange={setBrandId}
          options={brands.map((b) => ({ value: b.id, label: b.name }))}
        />
        <Select
          allowClear
          placeholder="Status"
          style={{ width: 160 }}
          value={status}
          onChange={setStatus}
          options={STATUSES.map((s) => ({ value: s, label: s }))}
        />
        <Select
          allowClear
          showSearch
          optionFilterProp="label"
          placeholder="Core Idea"
          style={{ width: 280 }}
          value={corePackageId}
          onChange={setCorePackageId}
          options={cores.map((p) => ({ value: p.id, label: p.title }))}
        />
        <DatePicker.RangePicker value={range} onChange={setRange} />
      </Space>
      
      {loading ? (
        <div style={{ textAlign: "center", padding: 48 }}>
          <Typography.Text type="secondary">Đang tải...</Typography.Text>
        </div>
      ) : rows.length === 0 ? (
        <div style={{ textAlign: "center", padding: 48 }}>
          <Typography.Text type="secondary">Chưa có Series</Typography.Text>
        </div>
      ) : (
        <Space direction="vertical" size={16} style={{ width: "100%" }}>
          {rows.map((series) => {
            const brandAngle = seriesBrandAngle(series);
            const territory = seriesTerritory(series);
            const progress = seriesProgressLabel(series);
            
            return (
              <div
                key={series.id}
                onClick={() => navigate(`/content/article-series/${series.id}`)}
                style={{
                  border: "1px solid #f0f0f0",
                  borderRadius: 8,
                  padding: "16px 20px",
                  cursor: "pointer",
                  transition: "all 0.2s",
                  background: "#fff",
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.borderColor = "#1677ff";
                  e.currentTarget.style.boxShadow = "0 2px 8px rgba(0,0,0,0.08)";
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.borderColor = "#f0f0f0";
                  e.currentTarget.style.boxShadow = "none";
                }}
              >
                {/* Brand */}
                <Typography.Text type="secondary" style={{ display: "block", fontSize: 12, marginBottom: 4 }}>
                  {series.brandName}
                </Typography.Text>
                
                {/* Series Name - Visual Anchor */}
                <Typography.Title level={4} style={{ margin: "0 0 8px", fontSize: 18 }}>
                  {series.name}
                </Typography.Title>
                
                {/* Story Premise / Objective */}
                {series.objective && (
                  <Typography.Paragraph 
                    type="secondary" 
                    style={{ margin: "0 0 12px", fontSize: 13, lineHeight: 1.5 }}
                    ellipsis={{ rows: 2 }}
                  >
                    {series.objective}
                  </Typography.Paragraph>
                )}
                
                <Space split={<span style={{ color: "#d9d9d9" }}>·</span>} wrap>
                  {/* Episode Progress */}
                  <Typography.Text strong style={{ color: "#1677ff" }}>
                    {series.plannedEpisodeRows}/{series.episodeCount} Episodes
                  </Typography.Text>
                  
                  {/* Production State */}
                  {progress && (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {progress}
                    </Typography.Text>
                  )}
                  
                  {/* Status */}
                  <Tag color={series.status === "ACTIVE" ? "green" : series.status === "DRAFT" ? "default" : "blue"}>
                    {series.status}
                  </Tag>
                </Space>
                
                {/* Context: Brand Angle & Territory (Secondary) */}
                {(brandAngle || territory) && (
                  <div style={{ marginTop: 12, paddingTop: 12, borderTop: "1px solid #f5f5f5" }}>
                    <Space direction="vertical" size={4} style={{ width: "100%" }}>
                      {brandAngle && (
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          <span style={{ color: "#8c8c8c" }}>Brand Angle:</span> {brandAngle}
                        </Typography.Text>
                      )}
                      {territory && (
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          <span style={{ color: "#8c8c8c" }}>Territory:</span> {territory}
                        </Typography.Text>
                      )}
                    </Space>
                  </div>
                )}
              </div>
            );
          })}
        </Space>
      )}
      <Modal
        title="Tạo Series từ Brand Adaptation"
        open={open}
        onCancel={() => setOpen(false)}
        onOk={async () => {
          const values = await form.validateFields();
          try {
            const created = await createContentArticleSeries(values);
            setOpen(false);
            form.resetFields();
            navigate(`/content/article-series/${created.id}`);
          } catch (e) {
            message.error(apiErrorMessage(e, "Lỗi"));
          }
        }}
      >
        <Form form={form} layout="vertical" initialValues={{ episodeCount: 10 }}>
          <Form.Item name="sourcePackageId" label="Góc brand" rules={[{ required: true }]}>
            <Select
              showSearch
              optionFilterProp="label"
              options={packages.map((p) => ({
                value: p.id,
                label: `${p.brandName} · ${p.title}`,
              }))}
            />
          </Form.Item>
          <Form.Item name="name" label="Tên Series">
            <Input />
          </Form.Item>
          <Form.Item name="episodeCount" label="Độ dài Blueprint">
            <Select options={[5, 10, 20, 30, 50, 100].map((n) => ({ value: n, label: `${n} episode` }))} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
