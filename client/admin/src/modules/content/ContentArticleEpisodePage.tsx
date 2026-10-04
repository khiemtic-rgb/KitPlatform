import { useEffect, useState } from "react";
import { Alert, App, Button, Card, Collapse, Divider, Form, Image, Input, Space, Statistic, Tag, Typography } from "antd";
import { ArrowLeftOutlined, ArrowRightOutlined, CheckOutlined, CloudUploadOutlined } from "@ant-design/icons";
import { Link, useNavigate, useParams } from "react-router-dom";
import { apiErrorMessage } from "@/shared/api/api-error";
import {
  approveContentTopic,
  fetchContentAssetObjectUrl,
  fetchContentArticleEpisodeDetail,
  generateContentArticleEpisode,
  publishContentTopic,
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

const STATUS_LABEL: Record<string, { text: string; color: string }> = {
  Draft: { text: 'Nháp', color: 'default' },
  Generating: { text: 'Đang tạo…', color: 'processing' },
  Review: { text: 'Chờ duyệt', color: 'blue' },
  Approved: { text: 'Đã duyệt', color: 'cyan' },
  Scheduled: { text: 'Đã lên lịch', color: 'geekblue' },
  Published: { text: 'Đã đăng', color: 'green' },
  BudgetBlocked: { text: 'Hết ngân sách', color: 'red' },
  Rejected: { text: 'Từ chối', color: 'orange' },
};

function StatusTag({ status }: { status: string }) {
  const m = STATUS_LABEL[status] ?? { text: status, color: 'default' };
  return <Tag color={m.color}>{m.text}</Tag>;
}

export function ContentArticleEpisodePage() {
  const { seriesId = "", episodeId = "" } = useParams();
  const navigate = useNavigate();
  const { message, modal } = App.useApp();
  const [detail, setDetail] = useState<ContentArticleEpisodeDetail | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [form] = Form.useForm();
  const packages = usePackagesForBrand(detail?.series.brandId);
  const [assetUrls, setAssetUrls] = useState<Record<string, string>>({});

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

      // Load asset preview URLs
      if (row.topicDetail?.assets) {
        const urls: Record<string, string> = {};
        for (const asset of row.topicDetail.assets) {
          try {
            urls[asset.id] = await fetchContentAssetObjectUrl(asset.id);
          } catch {
            // Skip failed assets
          }
        }
        setAssetUrls(urls);
      }
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

  const onGenerateContent = async () => {
    setActionLoading('generate');
    try {
      const r = await generateContentArticleEpisode(seriesId, episodeId);
      message.success(r.message);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, "Lỗi tạo nội dung"));
    } finally {
      setActionLoading(null);
    }
  };

  const onApprove = async () => {
    if (!ep.contentTopicId) return;
    setActionLoading('approve');
    try {
      await approveContentTopic(ep.contentTopicId);
      message.success('Đã duyệt — có thể xuất bản');
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Duyệt thất bại'));
    } finally {
      setActionLoading(null);
    }
  };

  const onPublish = async () => {
    if (!ep.contentTopicId || !topic) return;
    
    // Auto-approve if needed
    if (topic.status === 'Review' || topic.status === 'Draft') {
      try {
        await approveContentTopic(ep.contentTopicId);
      } catch {
        /* may already be approved */
      }
    }

    const publishAt = topic.displayAt ?? undefined;
    const whenLabel = publishAt
      ? new Date(publishAt).toLocaleString('vi-VN')
      : 'ngay lập tức (không có ngày hiển thị)';

    const hasServerImage = detail.topicDetail?.assets.some((a) => a.isSelected) || (detail.topicDetail?.assets.length ?? 0) > 0;

    if (!hasServerImage) {
      message.error('Chưa có ảnh — cần tạo ảnh hoặc upload ảnh trước khi xuất bản.');
      return;
    }

    const okConfirm = await new Promise<boolean>((resolve) => {
      modal.confirm({
        title: 'Đẩy bài lên web / Facebook?',
        width: 480,
        content: (
          <div>
            <p>Ảnh: dùng ảnh đã có trên bài (AI / đã chọn).</p>
            <p>
              Lịch đăng: <strong>{whenLabel}</strong> — WP/FB tự lên vào giờ đó, không cần mở lại KitPlatform.
            </p>
            <p style={{ color: '#64748b', marginBottom: 0 }}>
              Ảnh gửi kèm lần này lên WP/FB/novixa — không lưu kho lâu trên server KitPlatform.
            </p>
          </div>
        ),
        okText: 'Đẩy lịch đăng',
        cancelText: 'Huỷ',
        onOk: () => resolve(true),
        onCancel: () => resolve(false),
      });
    });
    if (!okConfirm) return;

    setActionLoading('publish');
    message.loading({ content: 'Đang xuất bản…', key: 'content-publish', duration: 0 });
    try {
      const res = await publishContentTopic(ep.contentTopicId, {
        includeManualExport: true,
        runImmediately: true,
        publishAt: publishAt ?? null,
      });
      message.destroy('content-publish');
      
      const failed = res.jobs.filter((j) => j.status === 'Failed').length;
      const succeeded = res.jobs.filter((j) => j.status === 'Succeeded').length;
      
      if (failed > 0) message.warning(`Xuất bản: ${succeeded} thành công, ${failed} lỗi`);
      else message.success(`Đã đẩy lịch đăng (${succeeded} kênh)`);
      
      await load();
    } catch (e) {
      message.destroy('content-publish');
      message.error(apiErrorMessage(e, 'Xuất bản thất bại'));
    } finally {
      setActionLoading(null);
    }
  };

  return (
    <div style={{ maxWidth: 1400, margin: '0 auto' }}>
      {/* HEADER */}
      <Space direction="vertical" size={8} style={{ width: "100%", marginBottom: 24 }}>
        <ContentLineageTrail items={episodeDetailTrail(detail.series, ep)} />
        <Typography.Title level={2} style={{ margin: 0 }}>
          {episodeSequenceLabel(ep.episodeNo)} · {ep.title}
        </Typography.Title>
        <Space size={16} wrap>
          <StatusTag status={contentStatus} />
          {ep.contentTopicId && (
            <Typography.Text type="secondary">
              Quality Gate: {qualityGateLabel(contentPackage?.qualityGate)}
            </Typography.Text>
          )}
        </Space>
        <Space>
          {detail.previous && (
            <Button
              size="small"
              icon={<ArrowLeftOutlined />}
              onClick={() => navigate(`/content/article-series/${seriesId}/episodes/${detail.previous!.id}`)}
            >
              E{String(detail.previous.episodeNo).padStart(2, '0')}
            </Button>
          )}
          {detail.next && (
            <Button
              size="small"
              icon={<ArrowRightOutlined />}
              onClick={() => navigate(`/content/article-series/${seriesId}/episodes/${detail.next!.id}`)}
            >
              E{String(detail.next.episodeNo).padStart(2, '0')}
            </Button>
          )}
        </Space>
      </Space>

      {episodeCanonLocked && (
        <Alert
          type="info"
          showIcon
          message="Episode đã được duyệt. Canon đã khóa."
          style={{ marginBottom: 16 }}
        />
      )}

      {/* MAIN WORKSPACE */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 24, alignItems: 'start' }}>
        
        {/* LEFT: STORY CONTROL */}
        <Space direction="vertical" size={16} style={{ width: "100%" }}>
          
          <Card title="Story / Canon" size="small">
            <ContentCanonFields
              rows={[
                { label: "Sequence", value: episodeSequenceLabel(ep.episodeNo) },
                { label: "Title", value: ep.title },
                { label: "Objective", value: ep.objective },
                { label: "Angle", value: ep.angle },
                { label: "Key Message", value: ep.keyMessage },
                { label: "Brand Angle", value: contentPackage?.angle || detail.series.sourcePackageTitle },
                { label: "Series", value: <Link to={`/content/article-series/${detail.series.id}`}>{detail.series.name}</Link> },
                { label: "Audience", value: contentPackage?.audience || detail.series.audience },
                { label: "Quality Gate", value: qualityGateLabel(contentPackage?.qualityGate) },
              ]}
            />
            
            {!episodeCanonLocked && (
              <>
                <Divider style={{ margin: '12px 0' }} />
                <Collapse
                  ghost
                  size="small"
                  items={[
                    {
                      key: "edit",
                      label: "Sửa episode",
                      children: (
                        <Form
                          form={form}
                          layout="vertical"
                          size="small"
                          onFinish={async (values) => {
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
                          <Button size="small" htmlType="submit">Lưu</Button>
                        </Form>
                      ),
                    },
                  ]}
                />
              </>
            )}
          </Card>

          <Card title="Continuity" size="small">
            <ContentEpisodeContinuity
              continuity={ep.continuity}
              previous={detail.previous}
              next={detail.next}
              seriesId={seriesId}
            />
          </Card>

          <Card title="Creative Brief" size="small">
            <EpisodeCreativeBrief
              brief={contentPackage?.creativeBrief}
              audience={contentPackage?.audience || detail.series.audience}
              corePoint={ep.keyMessage}
            />
          </Card>

        </Space>

        {/* RIGHT: CONTENT PRODUCTION */}
        <Space direction="vertical" size={16} style={{ width: "100%" }}>
          
          {!ep.contentTopicId ? (
            <Card title="Content" size="small">
              <Space direction="vertical" size={12} align="center" style={{ width: '100%', padding: '32px 0' }}>
                <Typography.Text type="secondary">Episode chưa có nội dung</Typography.Text>
                <Button
                  type="primary"
                  size="large"
                  loading={actionLoading === 'generate'}
                  onClick={onGenerateContent}
                >
                  Tạo bài
                </Button>
              </Space>
            </Card>
          ) : (
            <>
              <Card
                title="Content Status"
                size="small"
                extra={
                  <Space>
                    {!episodeCanonLocked && (
                      <Button
                        size="small"
                        loading={actionLoading === 'generate'}
                        onClick={onGenerateContent}
                      >
                        Regenerate
                      </Button>
                    )}
                    {(contentStatus === 'Draft' || contentStatus === 'Review') && (
                      <Button
                        size="small"
                        type="primary"
                        icon={<CheckOutlined />}
                        loading={actionLoading === 'approve'}
                        onClick={onApprove}
                      >
                        Duyệt
                      </Button>
                    )}
                    {(contentStatus === 'Approved' || contentStatus === 'Scheduled') && (
                      <Button
                        size="small"
                        type="primary"
                        icon={<CloudUploadOutlined />}
                        loading={actionLoading === 'publish'}
                        onClick={onPublish}
                      >
                        Đặt lịch / Xuất bản
                      </Button>
                    )}
                    <Link to={contentTopicHref(ep.contentTopicId)}>
                      <Button size="small">Xem bài</Button>
                    </Link>
                  </Space>
                }
              >
                <Space direction="vertical" size={8} style={{ width: '100%' }}>
                  <ContentKindTag kind="series" />
                  <ContentLineageTrail
                    items={seriesContentTrail(
                      { series: detail.series, episode: ep },
                      topic?.title || ep.title,
                    )}
                  />
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 12, marginTop: 8 }}>
                    <Statistic title="Trạng thái" value={STATUS_LABEL[contentStatus]?.text || contentStatus} />
                    <Statistic
                      title="Quality Gate"
                      value={qualityGateLabel(contentPackage?.qualityGate)}
                      valueStyle={{ color: contentPackage?.qualityGate?.passed ? '#52c41a' : undefined }}
                    />
                  </div>
                  {topic?.displayAt && (
                    <div style={{ marginTop: 8 }}>
                      <Typography.Text type="secondary">Lịch đăng: </Typography.Text>
                      <Typography.Text strong>{new Date(topic.displayAt).toLocaleString('vi-VN')}</Typography.Text>
                    </div>
                  )}
                </Space>
              </Card>

              <Card title={`Bản viết (${detail.topicDetail?.variants.length ?? 0})`} size="small">
                {(detail.topicDetail?.variants.length ?? 0) === 0 ? (
                  <Typography.Text type="secondary">Chưa có bản viết</Typography.Text>
                ) : (
                  <Space direction="vertical" size={12} style={{ width: '100%' }}>
                    {detail.topicDetail!.variants.map((variant) => (
                      <div key={variant.id}>
                        <Typography.Text strong>
                          {variantKindLabel(variant.kind)}
                          {variant.title ? ` — ${variant.title}` : ''}
                        </Typography.Text>
                        <Typography.Paragraph
                          ellipsis={{ rows: 3, expandable: true, symbol: 'xem thêm' }}
                          style={{ marginBottom: 0, marginTop: 4, whiteSpace: 'pre-wrap', fontSize: 13 }}
                        >
                          {variant.bodyMarkdown}
                        </Typography.Paragraph>
                      </div>
                    ))}
                  </Space>
                )}
              </Card>

              <Card title={`Ảnh (${detail.topicDetail?.assets.length ?? 0})`} size="small">
                {(detail.topicDetail?.assets.length ?? 0) === 0 ? (
                  <Typography.Text type="secondary">Chưa có ảnh</Typography.Text>
                ) : (
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12 }}>
                    {detail.topicDetail!.assets.map((asset) => (
                      <div
                        key={asset.id}
                        style={{
                          width: 140,
                          border: asset.isSelected ? '2px solid #1677ff' : '1px solid #e2e8f0',
                          borderRadius: 8,
                          padding: 8,
                          background: asset.isSelected ? '#f0f5ff' : '#fff',
                        }}
                      >
                        {assetUrls[asset.id] ? (
                          <Image
                            src={assetUrls[asset.id]}
                            alt={asset.fileName}
                            style={{ width: '100%', height: 100, objectFit: 'cover', borderRadius: 4 }}
                            preview={{ mask: 'Xem ảnh' }}
                          />
                        ) : (
                          <div style={{ width: '100%', height: 100, background: '#f0f0f0', borderRadius: 4 }} />
                        )}
                        <Typography.Text
                          ellipsis
                          style={{ display: 'block', fontSize: 11, marginTop: 6 }}
                        >
                          {asset.fileName}
                        </Typography.Text>
                        {asset.isSelected && (
                          <Tag color="blue" style={{ marginTop: 4, fontSize: 10 }}>Đang dùng</Tag>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </Card>
            </>
          )}

        </Space>

      </div>
    </div>
  );
}
