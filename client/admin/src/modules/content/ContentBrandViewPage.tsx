import { useEffect, useState } from "react";
import { App, Select, Space, Statistic, Tag, Typography } from "antd";
import { useNavigate, useSearchParams } from "react-router-dom";
import { apiErrorMessage } from "@/shared/api/api-error";
import {
  fetchContentArticleSeries,
  fetchContentBrands,
  type ContentArticleSeries,
  type ContentBrand,
} from "@/shared/api/content.api";
import {
  seriesBrandAngle,
  seriesProgressLabel,
  seriesTerritory,
} from "@/modules/content/content-lineage";

export function ContentBrandViewPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const brandParam = searchParams.get("brand");
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [selectedBrandId, setSelectedBrandId] = useState<string>();
  const [series, setSeries] = useState<ContentArticleSeries[]>([]);
  const [loading, setLoading] = useState(false);

  const selectedBrand = brands.find((b) => b.id === selectedBrandId);

  useEffect(() => {
    const loadBrands = async () => {
      try {
        const brandRows = await fetchContentBrands(true);
        setBrands(brandRows);
        if (brandRows.length > 0 && !selectedBrandId) {
          const fromParam = brandParam ? brandRows.find((b) => b.id === brandParam) : undefined;
          setSelectedBrandId(fromParam?.id ?? brandRows[0].id);
        }
      } catch (e) {
        message.error(apiErrorMessage(e, "Lỗi tải thương hiệu"));
      }
    };
    void loadBrands();
  }, []);

  useEffect(() => {
    if (brandParam && brands.some((b) => b.id === brandParam)) {
      setSelectedBrandId(brandParam);
    }
  }, [brandParam, brands]);

  useEffect(() => {
    if (!selectedBrandId) {
      setSeries([]);
      return;
    }
    const loadSeries = async () => {
      setLoading(true);
      try {
        const seriesRows = await fetchContentArticleSeries({
          brandId: selectedBrandId,
        });
        setSeries(seriesRows);
      } catch (e) {
        message.error(apiErrorMessage(e, "Lỗi tải Series"));
      } finally {
        setLoading(false);
      }
    };
    void loadSeries();
  }, [selectedBrandId, message]);

  const totalSeries = series.length;
  const activeSeries = series.filter((s) => s.status === "ACTIVE").length;
  const totalEpisodes = series.reduce((sum, s) => sum + (s.episodeCount ?? 0), 0);
  const publishedEpisodes = series.reduce((sum, s) => sum + (s.publishedCount ?? 0), 0);

  return (
    <Space direction="vertical" size={16} style={{ width: "100%" }}>
      <div>
        <Typography.Title level={3} style={{ margin: 0 }}>
          Thương hiệu
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Quản trị câu chuyện của từng Brand: Series → Episode → Content
        </Typography.Paragraph>
      </div>

      <Select
        showSearch
        placeholder="Chọn thương hiệu"
        style={{ width: 280 }}
        value={selectedBrandId}
        onChange={setSelectedBrandId}
        options={brands.map((b) => ({ value: b.id, label: b.name }))}
      />

      {!selectedBrandId && brands.length === 0 ? (
        <div style={{ textAlign: "center", padding: 48 }}>
          <Typography.Text type="secondary">Chưa có thương hiệu đang hoạt động.</Typography.Text>
        </div>
      ) : !selectedBrandId ? (
        <div style={{ textAlign: "center", padding: 48 }}>
          <Typography.Text type="secondary">Vui lòng chọn thương hiệu.</Typography.Text>
        </div>
      ) : (
        <>
          {selectedBrand && (
            <div>
              <Typography.Title level={4} style={{ margin: "0 0 8px" }}>
                {selectedBrand.name}
              </Typography.Title>
              {selectedBrand.operationalBrief && (
                <Typography.Paragraph
                  type="secondary"
                  style={{ margin: "0 0 16px", fontSize: 13 }}
                  ellipsis={{ rows: 2 }}
                >
                  {selectedBrand.operationalBrief}
                </Typography.Paragraph>
              )}
            </div>
          )}

          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
              gap: 16,
            }}
          >
            <div
              style={{
                border: "1px solid #f0f0f0",
                borderRadius: 8,
                padding: 16,
                background: "#fff",
              }}
            >
              <Statistic title="Tổng Series" value={totalSeries} />
            </div>
            <div
              style={{
                border: "1px solid #f0f0f0",
                borderRadius: 8,
                padding: 16,
                background: "#fff",
              }}
            >
              <Statistic title="Series đang hoạt động" value={activeSeries} />
            </div>
            <div
              style={{
                border: "1px solid #f0f0f0",
                borderRadius: 8,
                padding: 16,
                background: "#fff",
              }}
            >
              <Statistic title="Tổng Episodes dự kiến" value={totalEpisodes} />
            </div>
            <div
              style={{
                border: "1px solid #f0f0f0",
                borderRadius: 8,
                padding: 16,
                background: "#fff",
              }}
            >
              <Statistic title="Episodes đã đăng" value={publishedEpisodes} />
            </div>
          </div>

          {loading ? (
            <div style={{ textAlign: "center", padding: 48 }}>
              <Typography.Text type="secondary">Đang tải...</Typography.Text>
            </div>
          ) : series.length === 0 ? (
            <div style={{ textAlign: "center", padding: 48 }}>
              <Typography.Text type="secondary">Brand này chưa có Series.</Typography.Text>
            </div>
          ) : (
            <Space direction="vertical" size={16} style={{ width: "100%" }}>
              {series.map((seriesRow) => {
                const brandAngle = seriesBrandAngle(seriesRow);
                const territory = seriesTerritory(seriesRow);
                const progress = seriesProgressLabel(seriesRow);

                return (
                  <div
                    key={seriesRow.id}
                    onClick={() => navigate(`/content/article-series/${seriesRow.id}`)}
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
                    <Typography.Title level={4} style={{ margin: "0 0 8px", fontSize: 18 }}>
                      {seriesRow.name}
                    </Typography.Title>

                    {seriesRow.objective && (
                      <Typography.Paragraph
                        type="secondary"
                        style={{ margin: "0 0 12px", fontSize: 13, lineHeight: 1.5 }}
                        ellipsis={{ rows: 2 }}
                      >
                        {seriesRow.objective}
                      </Typography.Paragraph>
                    )}

                    <Space split={<span style={{ color: "#d9d9d9" }}>·</span>} wrap>
                      <Typography.Text strong style={{ color: "#1677ff" }}>
                        {seriesRow.plannedEpisodeRows}/{seriesRow.episodeCount} Episodes
                      </Typography.Text>

                      {progress && (
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          {progress}
                        </Typography.Text>
                      )}

                      <Tag
                        color={
                          seriesRow.status === "ACTIVE"
                            ? "green"
                            : seriesRow.status === "DRAFT"
                            ? "default"
                            : "blue"
                        }
                      >
                        {seriesRow.status === "ACTIVE"
                          ? "Đang hoạt động"
                          : seriesRow.status === "PLANNED"
                          ? "Đã lên kế hoạch"
                          : seriesRow.status === "PAUSED"
                          ? "Tạm dừng"
                          : seriesRow.status === "COMPLETED"
                          ? "Hoàn thành"
                          : seriesRow.status === "CANCELLED"
                          ? "Đã huỷ"
                          : seriesRow.status}
                      </Tag>

                      {seriesRow.currentEpisodeNo != null && (
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          Hiện tại: E{String(seriesRow.currentEpisodeNo).padStart(2, "0")}
                        </Typography.Text>
                      )}
                    </Space>

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
        </>
      )}
    </Space>
  );
}
