import { useEffect, useState } from "react";
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
  type ContentArticleSeriesDetail,
  type ContentPackage,
} from "@/shared/api/content.api";
import { contentTopicHref } from "@/modules/content/content-generate-issue";
import {
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

export function ContentArticleSeriesDetailPage() {
  const { id = "" } = useParams();
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [detail, setDetail] = useState<ContentArticleSeriesDetail | null>(null);
  const [packages, setPackages] = useState<ContentPackage[]>([]);
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

  if (!detail) return <Typography.Text>Đang tải…</Typography.Text>;
  const s = detail.series;
  const canonLocked = seriesHasEpisodeContent(detail.episodes);

  return (
    <Space direction="vertical" size={16} style={{ width: "100%" }}>
      <Space wrap style={{ justifyContent: "space-between", width: "100%" }}>
        <div>
          <ContentLineageTrail items={seriesDetailTrail(s)} />
          <Typography.Text type="secondary">{s.brandName}</Typography.Text>
          <Typography.Title level={3} style={{ margin: "4px 0 0" }}>
            {s.code} · {s.name}
          </Typography.Title>
          <Space wrap>
            <Tag color="blue">{s.status}</Tag>
            <Typography.Text type="secondary">{seriesProgressLabel(s)}</Typography.Text>
          </Space>
        </div>
        <Space wrap>
          <Button onClick={() => navigate("/content/article-series")}>Danh sách</Button>
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
      </Space>

      {canonLocked ? <Alert type="info" showIcon message={SERIES_CANON_LOCK} /> : null}

      <Card title="Series">
        <ContentCanonFields
          rows={[
            { label: "Brand", value: s.brandName },
            { label: "Series code", value: s.code },
            { label: "Series name", value: s.name },
            { label: "Core Idea", value: s.coreIdeaTitle },
            { label: "Brand Angle", value: seriesBrandAngle(s) },
            { label: "Territory", value: seriesTerritory(s) },
            { label: "Objective", value: s.objective },
            { label: "Audience", value: s.audience },
            { label: "Core Message", value: s.coreMessage },
            {
              label: "Narrative Direction",
              value: asTextList(readBlueprintField(s.blueprint, "narrativeArc")).join(" → "),
            },
            { label: "CTA Strategy", value: seriesCtaStrategy(s) },
          ]}
        />
      </Card>

      <Card title="Narrative">
        <ContentSeriesNarrative blueprint={s.blueprint} />
      </Card>

      <Collapse
        items={[
          {
            key: "strategy",
            label: "Sửa strategy",
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
          <Button htmlType="submit" disabled={canonLocked}>Lưu strategy</Button>
        </Form>
      </Card>
            ),
          },
        ]}
      />

      <Card
        title="Episode plan"
        extra={
          <Space direction="vertical" size={4} align="end">
            <Space>
              <Button
                disabled={canonLocked}
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
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {SERIES_NEXT_LOCK}
              </Typography.Text>
            ) : null}
          </Space>
        }
      >
        {detail.episodes.length === 0 ? (
          <Typography.Text type="secondary">Chưa có Episode</Typography.Text>
        ) : (
          <Space direction="vertical" size={12} style={{ width: "100%" }}>
            {detail.episodes.map((ep, i) => {
              const gate = packageForTopic(packages, ep.contentTopicId)?.qualityGate;
              const episodeHref = `/content/article-series/${s.id}/episodes/${ep.id}`;
              return (
                <div key={ep.id}>
                  {i > 0 ? (
                    <Typography.Text type="secondary" style={{ display: "block", marginLeft: 8 }}>
                      ↓
                    </Typography.Text>
                  ) : null}
                  <Card
                    size="small"
                    title={
                      <Space>
                        <span
                          style={{
                            width: 10,
                            height: 10,
                            borderRadius: 10,
                            background: DOT[ep.status] ?? "#d9d9d9",
                            display: "inline-block",
                          }}
                        />
                        <Link to={episodeHref}>
                          {episodeSequenceLabel(ep.episodeNo)} · {ep.title}
                        </Link>
                      </Space>
                    }
                    extra={
                      <Space>
                        {ep.contentTopicId ? (
                          <Link to={contentTopicHref(ep.contentTopicId)}>Xem bài</Link>
                        ) : null}
                        <Link to={episodeHref}>Mở Episode</Link>
                      </Space>
                    }
                  >
                    <ContentCanonFields
                      rows={[
                        { label: "Sequence", value: episodeSequenceLabel(ep.episodeNo) },
                        { label: "Title", value: ep.title },
                        { label: "Objective", value: ep.objective },
                        { label: "Key Message", value: ep.keyMessage },
                        { label: "Continuity", value: episodeContinuityStatus(ep) },
                        {
                          label: "Content",
                          value: ep.topicStatus || (ep.contentTopicId ? "Đã có bài" : "Chưa có bài"),
                        },
                        {
                          label: "Quality Gate",
                          value: ep.contentTopicId ? qualityGateLabel(gate) : "Chưa có bài",
                        },
                      ]}
                    />
                  </Card>
                </div>
              );
            })}
          </Space>
        )}
      </Card>
    </Space>
  );
}
