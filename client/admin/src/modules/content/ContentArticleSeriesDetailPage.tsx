import { useEffect, useMemo, useState } from "react";
import { Alert, App, Button, Card, Collapse, Form, Input, Space, Tag, Typography } from "antd";
import { Link, useNavigate, useParams } from "react-router-dom";
import { apiErrorMessage } from "@/shared/api/api-error";
import {
  addContentArticleEpisode,
  approveContentArticleSeries,
  generateContentArticleSeriesBlueprint,
  generateNextContentArticleEpisodes,
  fetchContentArticleSeriesDetail,
  fetchContentPackages,
  updateContentArticleSeries,
  type ContentArticleEpisode,
  type ContentArticleSeriesDetail,
  type ContentPackage,
} from "@/shared/api/content.api";
import {
  brandViewPath,
  ContentCanonFields,
  ContentLineageTrail,
  ContentSeriesNarrative,
  asTextList,
  episodeContinuityStatus,
  episodeSequenceLabel,
  packageForTopic,
  qualityGateLabel,
  readBlueprintField,
  seriesBrandAngle,
  seriesCtaStrategy,
  seriesDetailTrail,
  seriesHasEpisodeContent,
  seriesProgressLabel,
  seriesTerritory,
} from "@/modules/content/content-lineage";

const SERIES_CANON_LOCK = "Series đã có nội dung. Canon được khóa để bảo vệ tính liên tục.";
const SERIES_NEXT_LOCK = "Series đã có nội dung. Không tạo lại các Episode đã có bài.";

const DOT: Record<string, string> = {
  PUBLISHED: "#1677ff",
  SCHEDULED: "#13c2c2",
  APPROVED: "#52c41a",
  REVIEW: "#faad14",
  GENERATING: "#722ed1",
  READY: "#8c8c8c",
  PLANNED: "#d9d9d9",
};

const SERIES_STATUS_LABEL: Record<string, string> = {
  DRAFT: "Nháp",
  PLANNED: "Đã lên kế hoạch",
  ACTIVE: "Đang hoạt động",
  PAUSED: "Tạm dừng",
  COMPLETED: "Hoàn thành",
  CANCELLED: "Đã huỷ",
};

const EPISODE_STATUS_LABEL: Record<string, string> = {
  PLANNED: "Đã lên kế hoạch",
  READY: "Sẵn sàng",
  GENERATING: "Đang tạo",
  REVIEW: "Chờ duyệt",
  APPROVED: "Đã duyệt",
  SCHEDULED: "Đã lên lịch",
  PUBLISHED: "Đã đăng",
};

type ProgressKey = "none" | "review" | "approved" | "scheduled" | "published";

const PROGRESS_ITEMS: { key: ProgressKey; label: string; color: string }[] = [
  { key: "none", label: "Chưa có Content", color: "default" },
  { key: "review", label: "Đang Review", color: "gold" },
  { key: "approved", label: "Đã duyệt", color: "green" },
  { key: "scheduled", label: "Đã lên lịch", color: "cyan" },
  { key: "published", label: "Đã đăng", color: "blue" },
];

function episodeProgressKey(ep: ContentArticleEpisode): ProgressKey | null {
  if (!ep.contentTopicId) return "none";
  const status = (ep.topicStatus ?? "").trim().toLowerCase();
  if (status === "review") return "review";
  if (status === "approved") return "approved";
  if (status === "scheduled") return "scheduled";
  if (status === "published") return "published";
  return null;
}

export function ContentArticleSeriesDetailPage() {
  const { id = "" } = useParams();
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [detail, setDetail] = useState<ContentArticleSeriesDetail | null>(null);
  const [packages, setPackages] = useState<ContentPackage[]>([]);
  const [progressFilter, setProgressFilter] = useState<ProgressKey | null>(null);
  const [form] = Form.useForm();

  const load = async () => {
    try {
      const row = await fetchContentArticleSeriesDetail(id);
      const packs = await fetchContentPackages({ brandId: row.series.brandId });
      setPackages(packs);
      setDetail(row);
      form.setFieldsValue({
        name: row.series.name,
        objective: row.series.objective,
        audience: row.series.audience,
        coreMessage: row.series.coreMessage,
        description: row.series.description,
        blueprint: JSON.stringify(row.series.blueprint ?? {}, null, 2),
      });
    } catch (e) {
      message.error(apiErrorMessage(e, "Lỗi"));
    }
  };

  useEffect(() => {
    void load();
  }, [id]);

  const progressCounts = useMemo(() => {
    const counts: Record<ProgressKey, number> = { none: 0, review: 0, approved: 0, scheduled: 0, published: 0 };
    for (const ep of detail?.episodes ?? []) {
      const key = episodeProgressKey(ep);
      if (key) counts[key] += 1;
    }
    return counts;
  }, [detail]);

  const nextEpisode = useMemo(() => {
    const ordered = [...(detail?.episodes ?? [])].sort((a, b) => a.episodeNo - b.episodeNo);
    const review = ordered.find((ep) => episodeProgressKey(ep) === "review");
    if (review) return { episode: review, reason: "Content đang chờ duyệt" };
    const empty = ordered.find((ep) => !ep.contentTopicId);
    if (empty) return { episode: empty, reason: "Chưa có Content" };
    return null;
  }, [detail]);

  if (!detail) return <Typography.Text>Đang tải…</Typography.Text>;
  const s = detail.series;
  const canonLocked = seriesHasEpisodeContent(detail.episodes);
  const visibleEpisodes = progressFilter
    ? detail.episodes.filter((ep) => episodeProgressKey(ep) === progressFilter)
    : detail.episodes;
  const nextEpisodeHref = nextEpisode
    ? `/content/article-series/${s.id}/episodes/${nextEpisode.episode.id}`
    : null;

  return (
    <Space direction="vertical" size={20} style={{ width: "100%" }}>
      {/* HEADER - Story Control Center */}
      <div>
        <ContentLineageTrail items={seriesDetailTrail(s)} />
        
        {/* Brand */}
        <Link
          to={brandViewPath(s.brandId)}
          style={{ display: "block", marginTop: 8, fontSize: 13, textTransform: "uppercase", letterSpacing: "0.5px" }}
        >
          {s.brandName}
        </Link>
        
        {/* Series Name - Visual Anchor */}
        <Typography.Title level={2} style={{ margin: "8px 0", fontSize: 28, lineHeight: 1.3 }}>
          {s.name}
        </Typography.Title>
        
        {/* Story Premise / Objective */}
        {s.objective && (
          <Typography.Paragraph style={{ fontSize: 15, lineHeight: 1.6, maxWidth: 800, margin: "12px 0", color: "rgba(0,0,0,0.65)" }}>
            {s.objective}
          </Typography.Paragraph>
        )}

        <div style={{ maxWidth: 800, marginTop: 12 }}>
          <ContentCanonFields
            rows={[
              { label: "Brand Angle", value: seriesBrandAngle(s) },
              { label: "Core Message", value: s.coreMessage },
              { label: "Audience", value: s.audience },
              {
                label: "Narrative Direction",
                value: asTextList(readBlueprintField(s.blueprint, "narrativeArc")).join(" → "),
              },
            ]}
          />
        </div>
        
        {/* Episode Progress & Status */}
        <Space wrap style={{ marginTop: 12 }}>
          <Tag color="blue" style={{ fontSize: 14, padding: "4px 12px" }}>
            {s.plannedEpisodeRows}/{s.episodeCount} Episodes
          </Tag>
          <Tag color={s.status === "ACTIVE" ? "green" : s.status === "DRAFT" ? "default" : "purple"} style={{ fontSize: 14, padding: "4px 12px" }}>
            {SERIES_STATUS_LABEL[s.status] ?? s.status}
          </Tag>
          {seriesProgressLabel(s) && (
            <Typography.Text type="secondary" style={{ fontSize: 13 }}>
              {seriesProgressLabel(s)}
            </Typography.Text>
          )}
        </Space>
        
        {/* Actions */}
        <Space wrap style={{ marginTop: 16 }}>
          <Button onClick={() => navigate("/content/article-series")}>← Danh sách</Button>
          <Button
            disabled={canonLocked}
            onClick={async () => {
              if (canonLocked) return;
              try {
                await generateContentArticleSeriesBlueprint(id, { episodeCount: s.episodeCount, planBatch: 10 });
                message.success("Đã tạo Blueprint + plan (lazy)");
                await load();
              } catch (e) {
                message.error(apiErrorMessage(e, "Lỗi"));
              }
            }}
          >
            Tạo Blueprint
          </Button>
          <Button
            disabled={canonLocked}
            onClick={async () => {
              if (canonLocked) return;
              try {
                await generateContentArticleSeriesBlueprint(id, { episodeCount: s.episodeCount, planBatch: 10 }, true);
                message.success("Đã làm lại Blueprint");
                await load();
              } catch (e) {
                message.error(apiErrorMessage(e, "Lỗi"));
              }
            }}
          >
            Làm lại Blueprint
          </Button>
          <Button
            type="primary"
            onClick={async () => {
              try {
                await approveContentArticleSeries(id);
                message.success("Series PLANNED — mới được generate content hàng loạt");
                await load();
              } catch (e) {
                message.error(apiErrorMessage(e, "Lỗi"));
              }
            }}
          >
            Duyệt Series
          </Button>
          {(s.status === "ACTIVE" || s.status === "PLANNED") && (
            <Button
              onClick={async () => {
                try {
                  await updateContentArticleSeries(id, { status: "PAUSED" });
                  message.success("Đã tạm dừng — không generate Episode mới");
                  await load();
                } catch (e) {
                  message.error(apiErrorMessage(e, "Lỗi"));
                }
              }}
            >
              Tạm dừng
            </Button>
          )}
          {s.status === "PAUSED" && (
            <Button
              onClick={async () => {
                try {
                  await updateContentArticleSeries(id, { status: "ACTIVE" });
                  message.success("Đã chạy lại Series");
                  await load();
                } catch (e) {
                  message.error(apiErrorMessage(e, "Lỗi"));
                }
              }}
            >
              Tiếp tục
            </Button>
          )}
        </Space>
      </div>

      {canonLocked ? <Alert type="info" showIcon message={SERIES_CANON_LOCK} style={{ background: "#e6f4ff", borderColor: "#91caff" }} /> : null}

      {detail.episodes.length > 0 ? (
        <Space wrap size={8}>
          {PROGRESS_ITEMS.map((item) => {
            const active = progressFilter === item.key;
            return (
              <Tag
                key={item.key}
                color={active ? "processing" : item.color}
                style={{ cursor: "pointer", fontSize: 13, padding: "2px 10px", fontWeight: active ? 600 : undefined }}
                onClick={() => setProgressFilter(active ? null : item.key)}
              >
                {item.label}: {progressCounts[item.key]}
              </Tag>
            );
          })}
        </Space>
      ) : null}

      {nextEpisode && nextEpisodeHref ? (
        <Alert
          type="warning"
          showIcon
          message="Việc tiếp theo"
          description={
            <Space wrap>
              <Typography.Text strong>
                {episodeSequenceLabel(nextEpisode.episode.episodeNo)} · {nextEpisode.episode.title}
              </Typography.Text>
              <Typography.Text type="secondary">{nextEpisode.reason}</Typography.Text>
            </Space>
          }
          action={
            <Button size="small" type="primary" onClick={() => navigate(nextEpisodeHref)}>
              Mở {episodeSequenceLabel(nextEpisode.episode.episodeNo)}
            </Button>
          }
        />
      ) : null}

      {/* EPISODE STORY TIMELINE - Centerpiece */}
      <Card
        title={
          <Space>
            <Typography.Title level={4} style={{ margin: 0 }}>
              Episode Timeline
            </Typography.Title>
            <Typography.Text type="secondary" style={{ fontSize: 13, fontWeight: "normal" }}>
              — Câu chuyện đang tiến triển
            </Typography.Text>
          </Space>
        }
        extra={
          <Space direction="vertical" size={4} align="end">
            <Space>
              <Button
                disabled={canonLocked}
                size="small"
                onClick={async () => {
                  if (canonLocked) return;
                  await addContentArticleEpisode(id);
                  await load();
                }}
              >
                Thêm episode
              </Button>
              <Button
                disabled={canonLocked}
                size="small"
                type="primary"
                onClick={async () => {
                  if (canonLocked) return;
                  const r = await generateNextContentArticleEpisodes(id, { count: 10, mode: "plan" });
                  message.success(r.message);
                  await load();
                }}
              >
                Generate next 10
              </Button>
            </Space>
            {canonLocked ? (
              <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                {SERIES_NEXT_LOCK}
              </Typography.Text>
            ) : null}
          </Space>
        }
      >
        {detail.episodes.length === 0 ? (
          <div style={{ textAlign: "center", padding: 32 }}>
            <Typography.Text type="secondary">Chưa có Episode. Tạo Blueprint để bắt đầu.</Typography.Text>
          </div>
        ) : visibleEpisodes.length === 0 ? (
          <div style={{ textAlign: "center", padding: 32 }}>
            <Typography.Text type="secondary">Không có Episode ở trạng thái này.</Typography.Text>
          </div>
        ) : (
          <Space direction="vertical" size={0} style={{ width: "100%" }}>
            {visibleEpisodes.map((ep, i) => {
              const gate = packageForTopic(packages, ep.contentTopicId)?.qualityGate;
              const episodeHref = `/content/article-series/${s.id}/episodes/${ep.id}`;
              const isNext = nextEpisode?.episode.id === ep.id;
              const restBorder = isNext ? "#1677ff" : "#f0f0f0";
              
              // Production state
              let productionState = "Chưa có Content";
              let stateColor = "#8c8c8c";
              if (ep.topicStatus) {
                if (ep.topicStatus === "Review") {
                  productionState = "Content Review";
                  stateColor = "#faad14";
                } else if (ep.topicStatus === "Approved") {
                  productionState = "Approved";
                  stateColor = "#52c41a";
                } else if (ep.topicStatus === "Scheduled") {
                  productionState = "Scheduled";
                  stateColor = "#13c2c2";
                } else if (ep.topicStatus === "Published") {
                  productionState = "Published";
                  stateColor = "#1677ff";
                } else if (ep.topicStatus === "Generating") {
                  productionState = "Generating...";
                  stateColor = "#722ed1";
                } else {
                  productionState = ep.topicStatus;
                }
              } else if (ep.contentTopicId) {
                productionState = "Content Available";
                stateColor = "#52c41a";
              }
              
              const continuityText = episodeContinuityStatus(ep);
              
              return (
                <div key={ep.id}>
                  {i > 0 && !progressFilter && (
                    <div style={{ display: "flex", alignItems: "center", paddingLeft: 24, height: 24 }}>
                      <div style={{ width: 2, height: "100%", background: "#e8e8e8" }} />
                      <Typography.Text type="secondary" style={{ marginLeft: 12, fontSize: 11, color: "#bfbfbf" }}>
                        ↓ {continuityText || "Tiếp nối"}
                      </Typography.Text>
                    </div>
                  )}
                  <div
                    style={{
                      border: `${isNext ? 2 : 1}px solid ${restBorder}`,
                      borderRadius: 8,
                      padding: 16,
                      cursor: "pointer",
                      transition: "all 0.2s",
                      background: isNext ? "#f0f5ff" : ep.contentTopicId ? "#fafafa" : "#fff",
                    }}
                    onClick={() => navigate(episodeHref)}
                    onMouseEnter={(e) => {
                      e.currentTarget.style.borderColor = "#1677ff";
                      e.currentTarget.style.boxShadow = "0 2px 4px rgba(0,0,0,0.06)";
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.borderColor = restBorder;
                      e.currentTarget.style.boxShadow = "none";
                    }}
                  >
                    <Space direction="vertical" size={8} style={{ width: "100%" }}>
                      {/* Episode Header */}
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                        <Space>
                          <div
                            style={{
                              width: 10,
                              height: 10,
                              borderRadius: 10,
                              background: DOT[ep.status] ?? "#d9d9d9",
                            }}
                          />
                          <Typography.Text strong style={{ fontSize: 15 }}>
                            {episodeSequenceLabel(ep.episodeNo)}
                          </Typography.Text>
                          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            {EPISODE_STATUS_LABEL[ep.status] ?? ep.status}
                          </Typography.Text>
                          {isNext ? <Tag color="blue">Việc tiếp theo</Tag> : null}
                        </Space>
                        <Space size={8}>
                          {ep.contentTopicId && (
                            <Link to={`/content/topics/${ep.contentTopicId}`} onClick={(e) => e.stopPropagation()}>
                              <Button size="small" type="link">Xem Content</Button>
                            </Link>
                          )}
                          {!ep.contentTopicId && (
                            <Button size="small" type={isNext ? "primary" : "default"} onClick={(e) => { e.stopPropagation(); navigate(episodeHref); }}>
                              Tạo nội dung
                            </Button>
                          )}
                        </Space>
                      </div>
                      
                      {/* Episode Title */}
                      <Typography.Title level={5} style={{ margin: 0, fontSize: 14, lineHeight: 1.4 }}>
                        {ep.title}
                      </Typography.Title>
                      
                      {/* Episode Objective */}
                      {ep.objective && (
                        <Typography.Paragraph 
                          type="secondary" 
                          style={{ margin: 0, fontSize: 12, lineHeight: 1.5 }}
                          ellipsis={{ rows: 2 }}
                        >
                          {ep.objective}
                        </Typography.Paragraph>
                      )}
                      
                      {/* Production State & Quality Gate */}
                      <Space split={<span style={{ color: "#d9d9d9" }}>·</span>} wrap style={{ fontSize: 12 }}>
                        <Typography.Text style={{ color: stateColor }}>
                          {productionState}
                        </Typography.Text>
                        <Typography.Text type={!gate ? "secondary" : gate.passed ? "success" : "warning"}>
                          Quality Gate: {qualityGateLabel(gate)}
                        </Typography.Text>
                        {detail.episodes[0]?.id === ep.id && (
                          <Typography.Text type="secondary">Mở đầu</Typography.Text>
                        )}
                      </Space>
                    </Space>
                  </div>
                </div>
              );
            })}
          </Space>
        )}
      </Card>

      {/* CANON SECTION - Secondary */}
      <Card 
        title="Series Canon"
        style={{ background: "#fafafa" }}
      >
        <ContentCanonFields
          rows={[
            { label: "Core Idea", value: s.coreIdeaTitle },
            { label: "Territory", value: seriesTerritory(s) },
            { label: "CTA Strategy", value: seriesCtaStrategy(s) },
          ]}
        />
      </Card>

      {/* Narrative Blueprint (collapsed by default) */}
      <Collapse
        items={[
          {
            key: "narrative",
            label: "Blueprint Narrative",
            children: <ContentSeriesNarrative blueprint={s.blueprint} />,
          },
        ]}
      />

      {/* Strategy Editor (collapsed by default) */}
      <Collapse
        items={[
          {
            key: "strategy",
            label: "Sửa Strategy",
            children: (
      <Card title="Strategy" bordered={false}>
        <Form
          form={form}
          layout="vertical"
          disabled={canonLocked}
          onFinish={async (values) => {
            if (canonLocked) return;
            let blueprint: unknown = s.blueprint;
            try {
              blueprint = values.blueprint ? JSON.parse(values.blueprint) : {};
            } catch {
              message.error("Blueprint JSON không hợp lệ");
              return;
            }
            try {
              await updateContentArticleSeries(id, { ...values, blueprint });
              message.success("Đã lưu");
              await load();
            } catch (e) {
              message.error(apiErrorMessage(e, "Lỗi"));
            }
          }}
        >
          <Form.Item name="name" label="Tên"><Input /></Form.Item>
          <Form.Item name="objective" label="Objective"><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="audience" label="Audience"><Input /></Form.Item>
          <Form.Item name="coreMessage" label="Core Message"><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="description" label="Mô tả"><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="blueprint" label="Blueprint JSON"><Input.TextArea rows={8} /></Form.Item>
          <Button htmlType="submit" type="primary" disabled={canonLocked}>Lưu strategy</Button>
        </Form>
      </Card>
            ),
          },
        ]}
      />
    </Space>
  );
}
