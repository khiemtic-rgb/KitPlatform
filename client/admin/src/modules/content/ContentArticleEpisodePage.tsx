import { useEffect, useState } from "react";
import { Alert, App, Button, Card, Collapse, Form, Input, Space, Tag, Typography } from "antd";
import { Link, useParams } from "react-router-dom";
import { apiErrorMessage } from "@/shared/api/api-error";
import {
  fetchContentArticleEpisodeDetail,
  generateContentArticleEpisode,
  updateContentArticleEpisode,
  type ContentArticleEpisodeDetail,
} from "@/shared/api/content.api";
import { contentTopicHref } from "@/modules/content/content-generate-issue";
import {
  ContentCanonFields,
  ContentEpisodeContinuity,
  ContentKindTag,
  ContentLineageTrail,
  EpisodeCreativeBrief,
  contentStatusApproved,
  episodeDetailTrail,
  episodeSequenceLabel,
  packageForTopic,
  qualityGateLabel,
  seriesContentTrail,
  usePackagesForBrand,
  variantKindLabel,
} from "@/modules/content/content-lineage";

export function ContentArticleEpisodePage() {
  const { seriesId = "", episodeId = "" } = useParams();
  const { message } = App.useApp();
  const [detail, setDetail] = useState<ContentArticleEpisodeDetail | null>(null);
  const [creating, setCreating] = useState(false);
  const [form] = Form.useForm();
  const packages = usePackagesForBrand(detail?.series.brandId);

  const load = async () => {
    try {
      const row = await fetchContentArticleEpisodeDetail(seriesId, episodeId);
      setDetail(row);
      form.setFieldsValue({
        title: row.episode.title,
        objective: row.episode.objective,
        angle: row.episode.angle,
        keyMessage: row.episode.keyMessage,
      });
    } catch (e) {
      message.error(apiErrorMessage(e, "Lỗi"));
    }
  };

  useEffect(() => {
    void load();
  }, [seriesId, episodeId]);

  if (!detail) return <Typography.Text>Đang tải…</Typography.Text>;
  const ep = detail.episode;
  const contentPackage = packageForTopic(packages, ep.contentTopicId);
  const topic = detail.topicDetail?.topic;
  const contentStatus = topic?.status || ep.topicStatus || ep.status;
  const episodeCanonLocked = Boolean(ep.contentTopicId) && contentStatusApproved(contentStatus);

  return (
    <Space direction="vertical" size={16} style={{ width: "100%" }}>
      <div>
        <ContentLineageTrail items={episodeDetailTrail(detail.series, ep)} />
        <Typography.Title level={3} style={{ marginTop: 8 }}>
          {episodeSequenceLabel(ep.episodeNo)} · {ep.title}
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Episode này là nguồn của content thuộc series.
        </Typography.Paragraph>
      </div>

      <Card title="Episode">
        <ContentCanonFields
          rows={[
            { label: "Sequence", value: episodeSequenceLabel(ep.episodeNo) },
            { label: "Title", value: ep.title },
            { label: "Status", value: <Tag color="blue">{ep.status}</Tag> },
            { label: "Objective", value: ep.objective },
            { label: "Key Message", value: ep.keyMessage },
          ]}
        />
      </Card>

      {episodeCanonLocked ? (
        <Alert type="info" showIcon message="Episode đã được duyệt. Canon đã khóa." />
      ) : null}

      <Collapse
        items={[
          {
            key: "edit",
            label: "Sửa episode",
            children: (
              <Form
                form={form}
                layout="vertical"
                disabled={episodeCanonLocked}
                onFinish={async (values) => {
                  if (episodeCanonLocked) return;
                  try {
                    await updateContentArticleEpisode(seriesId, episodeId, values);
                    message.success("Đã lưu");
                    await load();
                  } catch (e) {
                    message.error(apiErrorMessage(e, "Lỗi"));
                  }
                }}
              >
                <Form.Item name="title" label="Title"><Input /></Form.Item>
                <Form.Item name="objective" label="Objective"><Input.TextArea rows={2} /></Form.Item>
                <Form.Item name="angle" label="Angle"><Input.TextArea rows={2} /></Form.Item>
                <Form.Item name="keyMessage" label="Key Message"><Input.TextArea rows={2} /></Form.Item>
                <Button htmlType="submit" disabled={episodeCanonLocked}>Lưu</Button>
              </Form>
            ),
          },
        ]}
      />

      <Card title="Continuity">
        <ContentEpisodeContinuity
          continuity={ep.continuity}
          previous={detail.previous}
          next={detail.next}
          seriesId={seriesId}
        />
      </Card>

      <Card title="Creative Brief">
        <EpisodeCreativeBrief
          brief={contentPackage?.creativeBrief}
          audience={contentPackage?.audience || detail.series.audience}
          corePoint={ep.keyMessage}
        />
      </Card>

      <Card title="Content">
        {ep.contentTopicId ? (
          <Space direction="vertical" size={8} style={{ width: "100%" }}>
            <ContentKindTag kind="series" />
            <ContentLineageTrail
              items={seriesContentTrail(
                { series: detail.series, episode: ep },
                topic?.title || ep.title,
              )}
            />
            <ContentCanonFields
              rows={[
                { label: "Content title", value: topic?.title || ep.title },
                { label: "Status", value: topic?.status || ep.topicStatus || ep.status },
                { label: "Quality Gate", value: qualityGateLabel(contentPackage?.qualityGate) },
              ]}
            />
            {(detail.topicDetail?.variants.length ?? 0) > 0 ? (
              <div>
                {detail.topicDetail!.variants.map((variant) => (
                  <div key={variant.id}>
                    {variantKindLabel(variant.kind)} · {variant.bodyMarkdown.length} ký tự
                  </div>
                ))}
              </div>
            ) : (
              <Typography.Text type="secondary">Chưa có bản viết</Typography.Text>
            )}
            <Space>
              <Link to={contentTopicHref(ep.contentTopicId)}>Xem bài</Link>
              {episodeCanonLocked ? null : (
                <Button
                  loading={creating}
                  onClick={async () => {
                    setCreating(true);
                    try {
                      const r = await generateContentArticleEpisode(seriesId, episodeId);
                      message.success(r.message);
                      await load();
                    } catch (e) {
                      message.error(apiErrorMessage(e, "Lỗi"));
                    } finally {
                      setCreating(false);
                    }
                  }}
                >
                  Regenerate
                </Button>
              )}
            </Space>
          </Space>
        ) : (
          <Space direction="vertical" size={8}>
            <Typography.Text type="secondary">Chưa có bài.</Typography.Text>
            <Button
              type="primary"
              loading={creating}
              onClick={async () => {
                setCreating(true);
                try {
                  const r = await generateContentArticleEpisode(seriesId, episodeId);
                  message.success(r.message);
                  await load();
                } catch (e) {
                  message.error(apiErrorMessage(e, "Lỗi"));
                } finally {
                  setCreating(false);
                }
              }}
            >
              Tạo bài
            </Button>
          </Space>
        )}
      </Card>
    </Space>
  );
}
