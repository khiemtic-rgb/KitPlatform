import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { isAxiosError } from 'axios';
import {
  Alert,
  App,
  Button,
  Card,
  Col,
  Collapse,
  Input,
  Row,
  Select,
  Space,
  Spin,
  Table,
  Tag,
  Typography,
  Upload,
} from 'antd';
import {
  ArrowLeftOutlined,
  CheckOutlined,
  CloudUploadOutlined,
  CopyOutlined,
  FolderOpenOutlined,
  PictureOutlined,
  ReloadOutlined,
  ThunderboltOutlined,
  UploadOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  approveContentTopic,
  fetchContentAssetObjectUrl,
  fetchContentChannels,
  fetchContentSites,
  fetchContentTopicDetail,
  generateContentArticleEpisode,
  generateContentTopic,
  publishContentTopic,
  runContentPublishJob,
  selectContentAsset,
  startFacebookOAuth,
  uploadContentTopicAsset,
  type ContentAsset,
  type ContentChannelTarget,
  type ContentPublishJob,
  type ContentSiteTarget,
  type ContentTopicDetail,
  type ContentVariant,
} from '@/shared/api/content.api';
import { FB_RETURN_KEY } from '@/modules/content/ContentFacebookCallbackPage';
import { ContentManualPostTab } from '@/modules/content/ContentManualPostTab';
import { writeClipboardImage } from '@/modules/content/content-manual-dest';
import { buildStaffImagePrompt } from '@/modules/content/content-staff-image-prompt';
import {
  getLocalImageLibraryStatus,
  isConfidentLocalMatch,
  isLocalImageLibrarySupported,
  listLocalImages,
  loadLocalImagePreviews,
  pickBestLocalImage,
  prepareLocalImageForPublish,
  rankLocalImages,
  requestLocalImageLibraryPermission,
  revokeLocalPreviewUrls,
} from '@/modules/content/content-local-image-library';
import {
  brandViewPath,
  ContentCanonFields,
  ContentKindTag,
  ContentLineageTrail,
  contentStatusApproved,
  episodeSequenceLabel,
  packageForTopic,
  qualityGateLabel,
  seriesBrandAngle,
  seriesContentTrail,
  standaloneContentTrail,
  usePackagesForBrand,
  useTopicLineageIndex,
  variantKindLabel,
} from '@/modules/content/content-lineage';
import {
  PublishJobStatusTag,
  StatusTag,
  connectorLabel,
  contentPublishJobColumns,
  topicRewriteBlocked,
} from '@/modules/content/ContentTopicsPage';

type LocalPreview = { name: string; url: string; confidence: number; reason: string };

type WorkspaceAction = 'write' | 'writeText' | 'images' | 'approve' | 'publish' | 'pickImage' | null;

function formatDisplayAt(value?: string | null) {
  if (!value) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? value : d.toLocaleString('vi-VN');
}

function VariantCard({ variant }: { variant: ContentVariant }) {
  return (
    <Card
      size="small"
      title={
        <Space size={8} wrap>
          <Tag color="blue">{variantKindLabel(variant.kind)}</Tag>
          {variant.title ? <Typography.Text strong>{variant.title}</Typography.Text> : null}
        </Space>
      }
    >
      <Typography.Paragraph
        type="secondary"
        ellipsis={{ rows: 2 }}
        style={{ marginBottom: 4, whiteSpace: 'pre-wrap' }}
      >
        {variant.bodyMarkdown}
      </Typography.Paragraph>
      <Collapse
        ghost
        size="small"
        items={[
          {
            key: 'full',
            label: 'Xem toàn bộ',
            children: (
              <Typography.Paragraph style={{ marginBottom: 0, whiteSpace: 'pre-wrap' }}>
                {variant.bodyMarkdown}
              </Typography.Paragraph>
            ),
          },
        ]}
      />
    </Card>
  );
}

function AssetThumb({
  asset,
  url,
  busy,
  onSelect,
}: {
  asset: ContentAsset;
  url?: string;
  busy: boolean;
  onSelect: (asset: ContentAsset) => void;
}) {
  return (
    <div
      style={{
        width: 150,
        border: asset.isSelected ? '2px solid #1677ff' : '1px solid #e2e8f0',
        borderRadius: 8,
        padding: 8,
        background: asset.isSelected ? '#f0f5ff' : '#fff',
      }}
    >
      {url ? (
        <img
          src={url}
          alt={asset.fileName}
          style={{ width: '100%', height: 100, objectFit: 'cover', borderRadius: 4 }}
        />
      ) : (
        <div
          style={{
            height: 100,
            background: '#f1f5f9',
            borderRadius: 4,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 11,
            color: '#64748b',
            padding: 4,
            wordBreak: 'break-all',
            textAlign: 'center',
          }}
        >
          Không tải được ảnh
        </div>
      )}
      <Typography.Text ellipsis style={{ display: 'block', marginTop: 6, fontSize: 11 }} title={asset.fileName}>
        {asset.fileName}
      </Typography.Text>
      <Button
        size="small"
        block
        type={asset.isSelected ? 'primary' : 'default'}
        style={{ marginTop: 6 }}
        loading={busy}
        onClick={() => onSelect(asset)}
      >
        {asset.isSelected ? 'Đang dùng' : 'Chọn ảnh này'}
      </Button>
    </div>
  );
}

export function ContentTopicWorkspacePage() {
  const { message, modal } = App.useApp();
  const navigate = useNavigate();
  const { id = '' } = useParams<{ id: string }>();

  const [detail, setDetail] = useState<ContentTopicDetail | null>(null);
  const [initialLoading, setInitialLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [action, setAction] = useState<WorkspaceAction>(null);
  const [busy, setBusy] = useState(false);
  const [assetUrls, setAssetUrls] = useState<Record<string, string>>({});
  const [channels, setChannels] = useState<ContentChannelTarget[]>([]);
  const [sites, setSites] = useState<ContentSiteTarget[]>([]);
  const [localLibName, setLocalLibName] = useState<string | null>(null);
  const [localLibCount, setLocalLibCount] = useState(0);
  const [localLibNeedsPermission, setLocalLibNeedsPermission] = useState(false);
  const [localPreviews, setLocalPreviews] = useState<LocalPreview[]>([]);
  const [localPreviewLoading, setLocalPreviewLoading] = useState(false);
  const [pickedLocalName, setPickedLocalName] = useState<string | null>(null);
  const [localMatchConfident, setLocalMatchConfident] = useState(false);
  const assetUrlsRef = useRef<Record<string, string>>({});
  const localPreviewsRef = useRef<LocalPreview[]>([]);

  const lineageIndex = useTopicLineageIndex();
  const topicPackages = usePackagesForBrand(detail?.topic.brandId);
  const topicLineage = detail?.topic.id ? lineageIndex?.get(detail.topic.id) ?? null : null;
  const openPackage = packageForTopic(topicPackages, detail?.topic.id);
  const standaloneAngleLabel = openPackage?.angle?.trim() || openPackage?.title?.trim() || '';
  const libBrandId = detail?.topic.brandId ?? undefined;

  const rewriteBlocked = (topicId?: string | null, status?: string | null) =>
    topicRewriteBlocked(lineageIndex, topicId, status);

  useEffect(() => {
    assetUrlsRef.current = assetUrls;
  }, [assetUrls]);

  useEffect(() => {
    localPreviewsRef.current = localPreviews;
  }, [localPreviews]);

  useEffect(() => {
    return () => {
      Object.values(assetUrlsRef.current).forEach((u) => URL.revokeObjectURL(u));
      revokeLocalPreviewUrls(localPreviewsRef.current);
    };
  }, []);

  const refreshLocalLib = useCallback(async () => {
    if (!isLocalImageLibrarySupported() || !libBrandId) {
      setLocalLibName(null);
      setLocalLibCount(0);
      setLocalLibNeedsPermission(false);
      return;
    }
    const status = await getLocalImageLibraryStatus(libBrandId);
    setLocalLibName(status.name);
    if (!status.hasHandle) {
      setLocalLibCount(0);
      setLocalLibNeedsPermission(false);
      return;
    }
    if (status.permission !== 'granted') {
      setLocalLibCount(0);
      setLocalLibNeedsPermission(true);
      return;
    }
    setLocalLibNeedsPermission(false);
    try {
      const imgs = await listLocalImages(libBrandId);
      setLocalLibCount(imgs.length);
    } catch {
      setLocalLibCount(0);
    }
  }, [libBrandId]);

  const loadLocalGallery = useCallback(
    async (brandId: string, topicTitle?: string, opts?: { requestPermission?: boolean }) => {
      if (!brandId) {
        setLocalLibName(null);
        setLocalLibCount(0);
        setLocalLibNeedsPermission(false);
        setLocalPreviews((prev) => {
          revokeLocalPreviewUrls(prev);
          return [];
        });
        return;
      }
      setLocalPreviewLoading(true);
      try {
        if (opts?.requestPermission) {
          const ok = await requestLocalImageLibraryPermission(brandId);
          if (!ok) {
            setLocalLibNeedsPermission(true);
            setLocalLibCount(0);
            message.warning(
              'Trình duyệt chưa cho phép đọc thư mục ảnh. Bấm «Cho phép đọc lại» hoặc chọn lại thư mục.',
            );
            return;
          }
          setLocalLibNeedsPermission(false);
        }
        const imgs = await listLocalImages(brandId, { requestPermission: opts?.requestPermission });
        setLocalLibCount(imgs.length);
        setLocalPreviews((prev) => {
          revokeLocalPreviewUrls(prev);
          return [];
        });
        const status = await getLocalImageLibraryStatus(brandId);
        setLocalLibName(status.name);
        if (imgs.length === 0) {
          setPickedLocalName(null);
          setLocalMatchConfident(false);
          setLocalLibNeedsPermission(status.hasHandle && status.permission !== 'granted');
          return;
        }
        setLocalLibNeedsPermission(false);

        const title = topicTitle ?? '';
        const ranked = title
          ? rankLocalImages(title, imgs)
          : imgs.map((entry) => ({ entry, score: 0, confidence: 0, reason: '' }));
        const ordered = ranked.map((r) => r.entry);
        const previewsRaw = await loadLocalImagePreviews(ordered, 48);
        const scoreByName = new Map(ranked.map((r) => [r.entry.name, r]));
        setLocalPreviews(
          previewsRaw.map((p) => {
            const s = scoreByName.get(p.name);
            return { name: p.name, url: p.url, confidence: s?.confidence ?? 0, reason: s?.reason ?? '' };
          }),
        );

        const confident = title ? isConfidentLocalMatch(title, imgs) : false;
        setLocalMatchConfident(confident);
        const best = title ? pickBestLocalImage(title, imgs) : ordered[0];
        setPickedLocalName(best?.name ?? ordered[0]!.name);
      } catch {
        setLocalLibCount(0);
        setLocalMatchConfident(false);
        setLocalPreviews((prev) => {
          revokeLocalPreviewUrls(prev);
          return [];
        });
      } finally {
        setLocalPreviewLoading(false);
      }
    },
    [message],
  );

  const loadDetail = useCallback(
    async (topicId: string, opts?: { silent?: boolean }) => {
      if (!opts?.silent) setInitialLoading(true);
      try {
        const d = await fetchContentTopicDetail(topicId);
        setDetail(d);
        setNotFound(false);
        setLoadError(null);
        try {
          const [ch, st] = await Promise.all([
            fetchContentChannels(d.topic.brandId),
            fetchContentSites(d.topic.brandId),
          ]);
          setChannels(ch);
          setSites(st);
        } catch {
          setChannels([]);
          setSites([]);
        }
        const urls: Record<string, string> = {};
        for (const a of d.assets) {
          try {
            urls[a.id] = await fetchContentAssetObjectUrl(a.id);
          } catch {
            /* preview optional */
          }
        }
        setAssetUrls((prev) => {
          Object.values(prev).forEach((u) => URL.revokeObjectURL(u));
          return urls;
        });
      } catch (e) {
        if (opts?.silent) {
          message.error(apiErrorMessage(e, 'Không tải được chi tiết bài'));
        } else if (isAxiosError(e) && e.response?.status === 404) {
          setDetail(null);
          setNotFound(true);
        } else {
          setDetail(null);
          setLoadError(apiErrorMessage(e, 'Không tải được chi tiết bài'));
        }
      } finally {
        if (!opts?.silent) setInitialLoading(false);
      }
    },
    [message],
  );

  useEffect(() => {
    if (!id) return;
    void loadDetail(id);
  }, [id, loadDetail]);

  useEffect(() => {
    void refreshLocalLib();
  }, [refreshLocalLib]);

  useEffect(() => {
    if (detail?.topic.brandId) {
      void loadLocalGallery(detail.topic.brandId, detail.topic.title);
    }
  }, [detail?.topic.brandId, detail?.topic.id]); // eslint-disable-line react-hooks/exhaustive-deps

  const reload = () => (detail ? loadDetail(detail.topic.id, { silent: true }) : Promise.resolve());

  const onGenerate = async (skipImages = false) => {
    if (!detail || rewriteBlocked(detail.topic.id, detail.topic.status)) return;
    setAction(skipImages ? 'writeText' : 'write');
    try {
      const res = await generateContentTopic(detail.topic.id, { skipImages });
      message.success(res.message ?? (skipImages ? 'AI đã viết xong' : 'AI đã viết + tạo ảnh'));
      await reload();
    } catch (e) {
      message.error(apiErrorMessage(e, 'AI tạo bài thất bại'));
    } finally {
      setAction(null);
    }
  };

  const confirmGenerate = (skipImages: boolean) => {
    if (!detail || rewriteBlocked(detail.topic.id, detail.topic.status)) return;
    if (detail.variants.length === 0) {
      void onGenerate(skipImages);
      return;
    }
    modal.confirm({
      title: skipImages ? 'Viết lại chữ?' : 'AI viết lại chữ + ảnh?',
      content: skipImages
        ? 'Bài đã có bản viết. Tiếp tục sẽ viết lại các kênh — không giữ bản cũ. Ảnh giữ nguyên.'
        : 'Bài đã có chữ/ảnh. Tiếp tục sẽ viết lại các kênh và tạo ảnh mới theo chủ đề — không giữ bản cũ.',
      okText: skipImages ? 'Viết lại chữ' : 'Viết + tạo ảnh lại',
      cancelText: 'Giữ bản hiện tại',
      onOk: () => onGenerate(skipImages),
    });
  };

  const regenerateFromEpisode = async () => {
    if (!detail || !topicLineage || contentStatusApproved(detail.topic.status)) return;
    setAction('write');
    try {
      const result = await generateContentArticleEpisode(topicLineage.series.id, topicLineage.episode.id);
      message.success(result.message);
      await reload();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Lỗi'));
    } finally {
      setAction(null);
    }
  };

  const onGenerateImages = async () => {
    if (!detail || rewriteBlocked(detail.topic.id, detail.topic.status)) return;
    setAction('images');
    try {
      const res = await generateContentTopic(detail.topic.id, { imagesOnly: true });
      const msg = res.message ?? 'Đã tạo ảnh';
      if (/không tạo được ảnh|ảnh lỗi/i.test(msg)) {
        message.error(msg);
        return;
      }
      message.success(msg);
      await reload();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Tạo ảnh thất bại'));
    } finally {
      setAction(null);
    }
  };

  const confirmGenerateImages = () => {
    if (!detail || rewriteBlocked(detail.topic.id, detail.topic.status)) return;
    if (detail.assets.length === 0) {
      void onGenerateImages();
      return;
    }
    modal.confirm({
      title: 'Tạo ảnh mới?',
      content: 'Sẽ thay ảnh AI hiện có. Bản viết giữ nguyên — không chạy lại cả quy trình.',
      okText: 'Tạo ảnh mới',
      cancelText: 'Giữ ảnh cũ',
      onOk: () => onGenerateImages(),
    });
  };

  const staffImagePrompt = detail ? buildStaffImagePrompt(detail.topic, detail.variants) : '';

  const onCopyStaffImagePrompt = async () => {
    if (!staffImagePrompt) return;
    try {
      await navigator.clipboard.writeText(staffImagePrompt);
      message.success('Đã copy hướng dẫn tạo ảnh — dán vào ChatGPT / Gemini / Midjourney…');
    } catch {
      message.warning('Không copy được — hãy bôi đen ô text và Ctrl+C');
    }
  };

  const onUploadStaffImage = async (file: File) => {
    if (!detail) return false;
    setAction('pickImage');
    try {
      await uploadContentTopicAsset(detail.topic.id, file);
      message.success('Đã tải ảnh lên và chọn làm ảnh đăng');
      await reload();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được ảnh lên'));
    } finally {
      setAction(null);
    }
    return false;
  };

  const onSelectAsset = async (asset: ContentAsset) => {
    if (!detail) return;
    setAction('pickImage');
    try {
      await selectContentAsset(detail.topic.id, asset.id);
      message.success('Đã chọn ảnh này');
      await reload();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không chọn được ảnh'));
    } finally {
      setAction(null);
    }
  };

  const onCopyManualImage = async () => {
    if (!detail) throw new Error('Chưa mở bài');
    if (libBrandId && (pickedLocalName || localPreviews.length > 0)) {
      const imgs = await listLocalImages(libBrandId, { requestPermission: true });
      const entry =
        (pickedLocalName && imgs.find((i) => i.name === pickedLocalName)) ||
        pickBestLocalImage(detail.topic.title, imgs) ||
        imgs[0];
      if (entry) {
        const file = await prepareLocalImageForPublish(entry);
        await writeClipboardImage(file.blob);
        return;
      }
    }
    const selected = detail.assets.find((a) => a.isSelected) ?? detail.assets[0];
    if (selected && assetUrls[selected.id]) {
      const blob = await fetch(assetUrls[selected.id]).then((r) => r.blob());
      await writeClipboardImage(blob);
      return;
    }
    throw new Error('Chưa chọn ảnh — chọn ở mục Ảnh trước.');
  };

  const onApprove = async () => {
    if (!detail) return;
    setAction('approve');
    try {
      await approveContentTopic(detail.topic.id);
      message.success('Đã duyệt — có thể xuất bản');
      await reload();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Duyệt thất bại'));
    } finally {
      setAction(null);
    }
  };

  const onAllowLocalLibrary = async () => {
    if (!libBrandId) return;
    await loadLocalGallery(libBrandId, detail?.topic.title, { requestPermission: true });
  };

  const onPublish = async () => {
    if (!detail) return;
    setAction('publish');
    try {
      if (detail.topic.status === 'Review' || detail.topic.status === 'Draft') {
        try {
          await approveContentTopic(detail.topic.id);
        } catch {
          /* may already be approved / allowed by API */
        }
      }

      const publishAt = detail.topic.displayAt ?? undefined;
      const whenLabel = publishAt
        ? new Date(publishAt).toLocaleString('vi-VN')
        : 'ngay lập tức (không có ngày hiển thị)';

      const hasServerImage = detail.assets.some((a) => a.isSelected) || detail.assets.length > 0;

      let pickedFileName: string | undefined;
      let localImages: Awaited<ReturnType<typeof listLocalImages>> = [];

      if (localLibName && libBrandId) {
        localImages = await listLocalImages(libBrandId, { requestPermission: true });
        if (localImages.length === 0) {
          if (localLibNeedsPermission) {
            message.warning('Bấm «Cho phép đọc lại» kho ảnh, rồi xuất bản lại để kèm ảnh.');
          } else {
            message.warning('Kho ảnh trống — đổi thư mục ở Thương hiệu (cột Kho ảnh máy).');
          }
          return;
        }
        const preferred =
          (pickedLocalName && localImages.find((i) => i.name === pickedLocalName)) ||
          pickBestLocalImage(detail.topic.title, localImages) ||
          localImages[0]!;
        pickedFileName = preferred.name;
      } else if (!hasServerImage) {
        message.error(
          'Chưa có ảnh — bấm «Tạo ảnh» hoặc gắn kho ở Thương hiệu (Kho ảnh máy), rồi Đẩy lịch đăng. Không đăng chỉ chữ.',
        );
        return;
      }

      const okConfirm = await new Promise<boolean>((resolve) => {
        modal.confirm({
          title: 'Đẩy bài lên web / Facebook?',
          width: 480,
          content: (
            <div>
              {localLibName && localImages.length > 0 ? (
                <div style={{ marginBottom: 12 }}>
                  <Typography.Text>Ảnh từ kho máy «{localLibName}» (bắt buộc):</Typography.Text>
                  <Select
                    style={{ width: '100%', marginTop: 6 }}
                    defaultValue={pickedFileName}
                    options={rankLocalImages(detail.topic.title, localImages).map((r) => ({
                      value: r.entry.name,
                      label: `${r.entry.name}${r.reason ? ` — ${r.reason}` : ''}`,
                    }))}
                    showSearch
                    optionFilterProp="label"
                    onChange={(v) => {
                      pickedFileName = v;
                      setPickedLocalName(v);
                    }}
                  />
                </div>
              ) : (
                <p>Ảnh: dùng ảnh đã có trên bài (AI / đã chọn).</p>
              )}
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

      let imageBlob: Blob | undefined;
      let imageFileName: string | undefined;
      let imageContentType: string | undefined;
      if (localLibName && pickedFileName) {
        const entry = localImages.find((i) => i.name === pickedFileName) ?? localImages[0];
        if (!entry) {
          message.warning('Không đọc được ảnh đã chọn');
          return;
        }
        const file = await prepareLocalImageForPublish(entry);
        imageBlob = file.blob;
        imageFileName = file.fileName;
        imageContentType = file.contentType;
        if (!imageBlob || imageBlob.size < 100) {
          message.error('Ảnh quá nhỏ / không đọc được — chọn ảnh khác.');
          return;
        }
      } else if (!hasServerImage) {
        message.error('Thiếu ảnh — Tạo ảnh hoặc gắn kho ở Thương hiệu trước khi đăng.');
        return;
      }

      message.loading({
        content: imageFileName ? `Đang gửi ảnh «${imageFileName}»…` : 'Đang xuất bản…',
        key: 'content-publish',
        duration: 0,
      });

      const res = await publishContentTopic(detail.topic.id, {
        includeManualExport: true,
        runImmediately: true,
        publishAt: publishAt ?? null,
        imageBlob,
        imageFileName,
        imageContentType,
      });
      message.destroy('content-publish');
      const failed = res.jobs.filter((j) => j.status === 'Failed').length;
      const succeeded = res.jobs.filter((j) => j.status === 'Succeeded').length;
      const fbPhoto = res.jobs.some((j) => {
        if (j.connectorType !== 'facebook_page' || j.status !== 'Succeeded') return false;
        try {
          return (JSON.parse(j.resultJson) as { facebook?: string }).facebook === 'photo';
        } catch {
          return false;
        }
      });
      const fbTextOnly = res.jobs.some((j) => {
        if (j.connectorType !== 'facebook_page' || j.status !== 'Succeeded') return false;
        try {
          return (JSON.parse(j.resultJson) as { facebook?: string }).facebook === 'feed';
        } catch {
          return false;
        }
      });
      const astroHasImage = res.jobs.some((j) => {
        if (j.connectorType !== 'astro_git' || j.status !== 'Succeeded') return false;
        try {
          return (JSON.parse(j.resultJson) as { hasImage?: boolean }).hasImage === true;
        } catch {
          return false;
        }
      });
      if (failed > 0) message.warning(`Xuất bản: ${succeeded} thành công, ${failed} lỗi`);
      else if (fbTextOnly && !fbPhoto)
        message.error(
          'Facebook vẫn chỉ chữ — ảnh không tới server. Kiểm tra kho ảnh / Cho phép đọc lại, rồi Đẩy lịch đăng lại.',
        );
      else if (imageFileName && !fbPhoto && !astroHasImage && !hasServerImage)
        message.warning('Đã gửi ảnh nhưng kênh chưa xác nhận có ảnh — mở Chi tiết job để kiểm tra.');
      else
        message.success(
          `Đã đẩy lịch đăng (${succeeded} kênh)` +
            (fbPhoto || astroHasImage || imageFileName
              ? ` · có ảnh${imageFileName ? ` ${imageFileName}` : ''}`
              : hasServerImage
                ? ' · ảnh trên bài'
                : ''),
        );
      await reload();
    } catch (e) {
      message.destroy('content-publish');
      message.error(apiErrorMessage(e, 'Xuất bản thất bại'));
    } finally {
      setAction(null);
    }
  };

  const onRetryJob = async (job: ContentPublishJob) => {
    if (!detail) return;
    setBusy(true);
    try {
      let imageBlob: Blob | undefined;
      let imageFileName: string | undefined;
      const needsImage =
        job.connectorType === 'facebook_page' ||
        job.connectorType === 'astro_git' ||
        job.connectorType === 'wordpress_rest';

      if (needsImage && localLibName && libBrandId) {
        const localImages = await listLocalImages(libBrandId, { requestPermission: true });
        const preferred =
          (pickedLocalName && localImages.find((i) => i.name === pickedLocalName)) ||
          pickBestLocalImage(detail.topic.title, localImages) ||
          localImages[0];
        if (preferred) {
          const file = await prepareLocalImageForPublish(preferred);
          imageBlob = file.blob;
          imageFileName = file.fileName;
          message.loading({
            content: `Đang chạy lại ${job.connectorType} + ảnh «${imageFileName}»…`,
            key: 'content-retry',
            duration: 0,
          });
        } else {
          message.warning('Chưa chọn được ảnh local — sẽ chạy lại kênh này không kèm ảnh mới.');
        }
      }

      const updated = await runContentPublishJob(job.id, {
        imageBlob,
        imageFileName,
        publishAt: detail.topic.displayAt ?? undefined,
      });
      message.destroy('content-retry');
      if (updated.status === 'Succeeded') {
        message.success(`Đã chạy lại ${job.connectorType} — thành công`);
      } else {
        message.error(updated.lastError || `Chạy lại ${job.connectorType} thất bại`);
      }
      await reload();
    } catch (e) {
      message.destroy('content-retry');
      message.error(apiErrorMessage(e, 'Chạy lại thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const onReconnectFacebook = async (job: ContentPublishJob) => {
    const brandId = job.brandId || detail?.topic.brandId;
    if (!brandId) {
      message.error('Thiếu thương hiệu để kết nối Facebook.');
      return;
    }
    setBusy(true);
    try {
      if (detail?.topic.id) {
        sessionStorage.setItem(FB_RETURN_KEY, `/content/topics/${detail.topic.id}`);
      }
      const r = await startFacebookOAuth(brandId);
      window.location.href = r.url;
    } catch (e) {
      const msg = apiErrorMessage(e, 'Chưa mở được Facebook Login. Cấu hình App ở Model AI → Facebook.');
      message.error(msg);
      if (/chưa cấu hình facebook app|model ai/i.test(msg)) {
        if (detail?.topic.id) {
          sessionStorage.setItem(FB_RETURN_KEY, `/content/topics/${detail.topic.id}`);
        }
        navigate('/content/ai#facebook');
      }
      setBusy(false);
    }
  };

  const backToList = (
    <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/content/topics')}>
      Danh sách bài
    </Button>
  );

  if (initialLoading && !detail) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, padding: 64 }}>
        <Spin size="large" />
        <Typography.Text type="secondary">Đang tải bài…</Typography.Text>
      </div>
    );
  }

  if (notFound) {
    return (
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Alert type="warning" showIcon message="Không tìm thấy bài" description="Bài đã bị xóa hoặc đường dẫn không đúng." />
        {backToList}
      </Space>
    );
  }

  if (!detail) {
    return (
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Alert type="error" showIcon message="Không tải được bài" description={loadError ?? undefined} />
        <Space>
          {backToList}
          <Button icon={<ReloadOutlined />} onClick={() => void loadDetail(id)}>
            Thử lại
          </Button>
        </Space>
      </Space>
    );
  }

  const topic = detail.topic;
  const qualityGate = openPackage?.qualityGate;
  const canonLocked = Boolean(topicLineage) && contentStatusApproved(topic.status);
  const displayAtLabel = formatDisplayAt(topic.displayAt);
  const primaryVariant = detail.variants.find((v) => v.kind === 'web_long') ?? detail.variants[0];
  const selectedAsset = detail.assets.find((a) => a.isSelected);

  const lineageTrail = topicLineage
    ? seriesContentTrail(topicLineage, topic.title)
    : [
        ...standaloneContentTrail(
          topic.brandName,
          standaloneAngleLabel ? { label: standaloneAngleLabel, packageId: openPackage?.id } : null,
          topic.brandId,
        ),
        { label: topic.title },
      ];

  const jobs = detail.jobs;
  const succeededJobs = jobs.filter((j) => j.status === 'Succeeded').length;
  const failedJobs = jobs.filter((j) => j.status === 'Failed').length;
  const latestJob = [...jobs].sort(
    (a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime(),
  )[0];

  const writeBlocked = rewriteBlocked(topic.id, topic.status);

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      {/* HEADER */}
      <Space direction="vertical" size={8} style={{ width: '100%' }}>
        <Space wrap>
          {backToList}
          {topicLineage ? (
            <>
              <Link to={`/content/article-series/${topicLineage.series.id}/episodes/${topicLineage.episode.id}`}>
                <Button>Quay lại Episode</Button>
              </Link>
              <Link to={`/content/article-series/${topicLineage.series.id}`}>
                <Button>Quay lại Series</Button>
              </Link>
            </>
          ) : null}
        </Space>
        {lineageIndex ? (
          <ContentLineageTrail items={lineageTrail} />
        ) : (
          <Typography.Text type="secondary">Đang nối nguồn bài…</Typography.Text>
        )}
        <Typography.Title level={2} style={{ margin: 0 }}>
          {topic.title}
        </Typography.Title>
        <Space size={16} wrap>
          <StatusTag status={topic.status} />
          {lineageIndex ? <ContentKindTag kind={topicLineage ? 'series' : 'standalone'} /> : null}
          <Typography.Text type="secondary">Quality Gate: {qualityGateLabel(qualityGate)}</Typography.Text>
          <Typography.Text type="secondary">Lịch đăng: {displayAtLabel ?? 'Chưa có'}</Typography.Text>
        </Space>
      </Space>

      {canonLocked ? (
        <Alert type="info" showIcon message="Episode đã được duyệt. Canon đã khóa." />
      ) : topicLineage ? (
        <Alert type="info" showIcon message="Series content. Viết lại chỉ từ Episode." />
      ) : null}

      {/* ACTION BAR */}
      <Card size="small">
        <Space wrap>
          <Button
            type={detail.variants.length === 0 ? 'primary' : 'default'}
            icon={<ThunderboltOutlined />}
            loading={action === 'write'}
            disabled={writeBlocked || (!!action && action !== 'write')}
            onClick={() => confirmGenerate(false)}
          >
            AI viết + ảnh
          </Button>
          <Button
            loading={action === 'writeText'}
            disabled={writeBlocked || (!!action && action !== 'writeText')}
            onClick={() => confirmGenerate(true)}
          >
            Chỉ viết chữ
          </Button>
          <Button
            icon={<PictureOutlined />}
            loading={action === 'images'}
            disabled={writeBlocked || (!!action && action !== 'images')}
            onClick={() => confirmGenerateImages()}
          >
            Tạo ảnh
          </Button>
          {topicLineage && !contentStatusApproved(topic.status) ? (
            <Button loading={action === 'write'} onClick={() => void regenerateFromEpisode()}>
              Regenerate từ Episode
            </Button>
          ) : null}
          <Button
            icon={<CheckOutlined />}
            loading={action === 'approve'}
            disabled={!!action && action !== 'approve'}
            onClick={() => void onApprove()}
          >
            Duyệt
          </Button>
          <Button
            type="primary"
            icon={<CloudUploadOutlined />}
            loading={action === 'publish'}
            disabled={!!action && action !== 'publish'}
            onClick={() => void onPublish()}
          >
            {localLibName ? 'Đẩy lịch đăng' : 'Xuất bản'}
          </Button>
          <Button icon={<ReloadOutlined />} onClick={() => void reload()}>
            Tải lại
          </Button>
        </Space>
      </Card>

      {/* MAIN */}
      <Row gutter={[16, 16]} align="top">
        {/* LEFT / PRIMARY */}
        <Col xs={24} xl={15}>
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Card size="small" title="Nội dung chính">
              {primaryVariant ? (
                <Space direction="vertical" size={4} style={{ width: '100%' }}>
                  <Space size={8} wrap>
                    <Tag color="blue">{variantKindLabel(primaryVariant.kind)}</Tag>
                    {primaryVariant.title ? <Typography.Text strong>{primaryVariant.title}</Typography.Text> : null}
                  </Space>
                  <Typography.Paragraph
                    ellipsis={{ rows: 8, expandable: true, symbol: 'xem thêm' }}
                    style={{ marginBottom: 0, whiteSpace: 'pre-wrap' }}
                  >
                    {primaryVariant.bodyMarkdown}
                  </Typography.Paragraph>
                </Space>
              ) : (
                <Alert
                  type="info"
                  showIcon
                  message="Bài này chưa có chữ"
                  description={
                    topicLineage
                      ? 'Bài thuộc Episode — tạo nội dung từ Episode.'
                      : 'Bấm «Chỉ viết chữ» hoặc «AI viết + ảnh» để tạo bản viết.'
                  }
                />
              )}
            </Card>

            <Card size="small" title={`Bản viết (${detail.variants.length})`}>
              {detail.variants.length === 0 ? (
                <Typography.Text type="secondary">Chưa có bản viết</Typography.Text>
              ) : (
                <Space direction="vertical" size={12} style={{ width: '100%' }}>
                  {detail.variants.map((v) => (
                    <VariantCard key={v.id} variant={v} />
                  ))}
                </Space>
              )}
            </Card>

            <Card size="small" title="Đăng tay">
              <ContentManualPostTab
                channels={channels}
                sites={sites}
                variants={detail.variants}
                hasImage={
                  !!pickedLocalName ||
                  localPreviews.length > 0 ||
                  detail.assets.some((a) => a.isSelected) ||
                  detail.assets.length > 0
                }
                onCopyImage={onCopyManualImage}
                onCopied={(ok, detailMsg) => {
                  if (ok) message.success(detailMsg);
                  else message.warning(detailMsg);
                }}
              />
            </Card>
          </Space>
        </Col>

        {/* RIGHT / CONTEXT */}
        <Col xs={24} xl={9}>
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Card size="small" title="Trạng thái bài">
              <ContentCanonFields
                rows={[
                  { label: 'Tiêu đề', value: topic.title },
                  { label: 'Trạng thái', value: <StatusTag status={topic.status} /> },
                  { label: 'Thương hiệu', value: topic.brandName },
                  {
                    label: 'Loại bài',
                    value: lineageIndex ? <ContentKindTag kind={topicLineage ? 'series' : 'standalone'} /> : null,
                  },
                  { label: 'Quality Gate', value: qualityGateLabel(qualityGate) },
                  { label: 'Lịch đăng', value: displayAtLabel },
                  {
                    label: 'Series / Episode',
                    value: topicLineage
                      ? `${topicLineage.series.code} · ${episodeSequenceLabel(topicLineage.episode.episodeNo)}`
                      : lineageIndex
                        ? 'Bài độc lập'
                        : null,
                  },
                ]}
              />
            </Card>

            <Card size="small" title="Nguồn bài">
              {!lineageIndex ? (
                <Typography.Text type="secondary">Đang nối nguồn bài…</Typography.Text>
              ) : topicLineage ? (
                <Space direction="vertical" size={12} style={{ width: '100%' }}>
                  <ContentCanonFields
                    rows={[
                      {
                        label: 'Series',
                        value: (
                          <Link to={`/content/article-series/${topicLineage.series.id}`}>
                            {topicLineage.series.code} · {topicLineage.series.name}
                          </Link>
                        ),
                      },
                      {
                        label: 'Episode',
                        value: (
                          <Link
                            to={`/content/article-series/${topicLineage.series.id}/episodes/${topicLineage.episode.id}`}
                          >
                            {topicLineage.episode.title}
                          </Link>
                        ),
                      },
                      { label: 'Sequence', value: episodeSequenceLabel(topicLineage.episode.episodeNo) },
                      { label: 'Brand Angle', value: seriesBrandAngle(topicLineage.series) },
                      { label: 'Key Message', value: topicLineage.episode.keyMessage },
                    ]}
                  />
                  <Space wrap>
                    <Link to={`/content/article-series/${topicLineage.series.id}/episodes/${topicLineage.episode.id}`}>
                      Quay lại Episode
                    </Link>
                    <Link to={`/content/article-series/${topicLineage.series.id}`}>Quay lại Series</Link>
                  </Space>
                </Space>
              ) : (
                <ContentCanonFields
                  rows={[
                    { label: 'Thương hiệu', value: <Link to={brandViewPath(topic.brandId)}>{topic.brandName}</Link> },
                    {
                      label: 'Brand Angle',
                      value: standaloneAngleLabel ? (
                        <Link to={openPackage?.id ? `/content/packages?package=${openPackage.id}` : '/content/packages'}>
                          {standaloneAngleLabel}
                        </Link>
                      ) : null,
                    },
                    { label: 'Loại', value: 'Bài độc lập' },
                  ]}
                />
              )}
            </Card>

            <Card size="small" title="Quality Gate">
              <Space direction="vertical" size={6} style={{ width: '100%' }}>
                <Typography.Text strong style={{ color: qualityGate?.passed ? '#52c41a' : undefined }}>
                  {qualityGateLabel(qualityGate)}
                </Typography.Text>
                {qualityGate && !qualityGate.passed && qualityGate.issues.length > 0 ? (
                  <ul style={{ margin: 0, paddingLeft: 18 }}>
                    {qualityGate.issues.map((issue) => (
                      <li key={issue}>
                        <Typography.Text type="secondary">{issue}</Typography.Text>
                      </li>
                    ))}
                  </ul>
                ) : null}
              </Space>
            </Card>

            <Card
              size="small"
              title={localLibName ? `Ảnh · ${localLibName} (${localLibCount})` : `Ảnh (${detail.assets.length})`}
              extra={
                <Space size={4} wrap>
                  {localLibName ? (
                    localLibNeedsPermission ? (
                      <Button
                        size="small"
                        type="primary"
                        loading={localPreviewLoading}
                        onClick={() => void onAllowLocalLibrary()}
                      >
                        Cho phép đọc lại
                      </Button>
                    ) : (
                      <Button
                        size="small"
                        loading={localPreviewLoading}
                        onClick={() => void loadLocalGallery(topic.brandId, topic.title)}
                      >
                        Tải lại ảnh
                      </Button>
                    )
                  ) : null}
                  <Upload
                    accept="image/jpeg,image/png,image/webp,image/gif,.jpg,.jpeg,.png,.webp,.gif"
                    showUploadList={false}
                    beforeUpload={(file) => {
                      void onUploadStaffImage(file);
                      return false;
                    }}
                  >
                    <Button
                      size="small"
                      icon={<UploadOutlined />}
                      loading={action === 'pickImage'}
                      disabled={!!action && action !== 'pickImage'}
                    >
                      Tải ảnh lên
                    </Button>
                  </Upload>
                  <Button
                    size="small"
                    type="primary"
                    ghost
                    icon={<PictureOutlined />}
                    loading={action === 'images'}
                    disabled={writeBlocked || (!!action && action !== 'images')}
                    onClick={() => confirmGenerateImages()}
                  >
                    Tạo ảnh
                  </Button>
                </Space>
              }
            >
              <Space direction="vertical" size={12} style={{ width: '100%' }}>
                {localLibName ? (
                  localPreviewLoading ? (
                    <Typography.Text type="secondary">Đang đọc ảnh từ máy…</Typography.Text>
                  ) : localLibNeedsPermission ? (
                    <Space direction="vertical" size={8}>
                      <Typography.Text type="warning">
                        Trình duyệt đã nhớ thư mục «{localLibName}», nhưng sau khi tải lại trang cần bấm một lần để
                        cho phép đọc.
                      </Typography.Text>
                      <Button type="primary" icon={<FolderOpenOutlined />} onClick={() => void onAllowLocalLibrary()}>
                        Cho phép đọc lại
                      </Button>
                    </Space>
                  ) : localPreviews.length === 0 ? (
                    <Typography.Text type="warning">
                      Chưa thấy ảnh trong thư mục. Đổi kho ở Thương hiệu (cột Kho ảnh máy) hoặc bấm{' '}
                      <strong>Tạo ảnh</strong>.
                    </Typography.Text>
                  ) : (
                    <>
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        Kho máy · xếp theo độ khớp tiêu đề
                        {localMatchConfident ? ' · khớp mạnh' : ' · khớp yếu — chọn tay nếu sai'}
                        {pickedLocalName ? ` · đang chọn: ${pickedLocalName}` : ''}. Phải bấm «Đẩy lịch đăng» mới gửi
                        ảnh lên kênh.
                      </Typography.Text>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, maxHeight: 520, overflow: 'auto' }}>
                        {localPreviews.map((p) => {
                          const selected = pickedLocalName === p.name;
                          return (
                            <div
                              key={p.name}
                              style={{
                                width: 150,
                                border: selected ? '2px solid #1677ff' : '1px solid #e2e8f0',
                                borderRadius: 8,
                                padding: 8,
                                cursor: 'pointer',
                              }}
                              onClick={() => setPickedLocalName(p.name)}
                            >
                              <img
                                src={p.url}
                                alt={p.name}
                                style={{ width: '100%', height: 100, objectFit: 'cover', borderRadius: 4 }}
                              />
                              <Typography.Text
                                ellipsis
                                style={{ display: 'block', marginTop: 6, fontSize: 11 }}
                                title={p.name}
                              >
                                {p.name}
                              </Typography.Text>
                              {p.reason ? (
                                <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                                  {p.reason}
                                  {p.confidence > 0 ? ` · ${p.confidence}%` : ''}
                                </Typography.Text>
                              ) : null}
                              <Button
                                size="small"
                                block
                                type={selected ? 'primary' : 'default'}
                                style={{ marginTop: 6 }}
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setPickedLocalName(p.name);
                                }}
                              >
                                {selected ? 'Đang dùng' : 'Chọn ảnh này'}
                              </Button>
                            </div>
                          );
                        })}
                      </div>
                    </>
                  )
                ) : null}

                {localLibName ? (
                  <Typography.Text strong>Ảnh trên server ({detail.assets.length})</Typography.Text>
                ) : null}
                {detail.assets.length === 0 ? (
                  <Typography.Text type="secondary">
                    Chưa có ảnh. Bấm «Tải ảnh lên» hoặc «Tạo ảnh».
                  </Typography.Text>
                ) : (
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12 }}>
                    {detail.assets.map((a) => (
                      <AssetThumb
                        key={a.id}
                        asset={a}
                        url={assetUrls[a.id]}
                        busy={action === 'pickImage'}
                        onSelect={(asset) => void onSelectAsset(asset)}
                      />
                    ))}
                  </div>
                )}
                {selectedAsset ? (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Ảnh đang dùng trên bài: {selectedAsset.fileName}
                  </Typography.Text>
                ) : null}

                <Collapse
                  ghost
                  size="small"
                  items={[
                    {
                      key: 'prompt',
                      label: 'Hướng dẫn tạo ảnh (copy sang AI chat)',
                      children: (
                        <Space direction="vertical" size={8} style={{ width: '100%' }}>
                          <Button size="small" icon={<CopyOutlined />} onClick={() => void onCopyStaffImagePrompt()}>
                            Copy
                          </Button>
                          <Input.TextArea
                            value={staffImagePrompt}
                            readOnly
                            autoSize={{ minRows: 4, maxRows: 12 }}
                            onFocus={(e) => e.target.select()}
                            style={{
                              fontFamily: 'ui-monospace, SFMono-Regular, Menlo, Consolas, monospace',
                              fontSize: 12,
                            }}
                          />
                        </Space>
                      ),
                    },
                  ]}
                />
              </Space>
            </Card>

            <Card size="small" title="Xuất bản">
              {jobs.length === 0 ? (
                <Typography.Text type="secondary">Chưa xuất bản</Typography.Text>
              ) : (
                <ContentCanonFields
                  rows={[
                    { label: 'Tổng job', value: String(jobs.length) },
                    {
                      label: 'Thành công',
                      value: <Typography.Text type="success">{succeededJobs}</Typography.Text>,
                    },
                    {
                      label: 'Lỗi',
                      value: (
                        <Typography.Text type={failedJobs > 0 ? 'danger' : 'secondary'}>{failedJobs}</Typography.Text>
                      ),
                    },
                    {
                      label: 'Gần nhất',
                      value: latestJob ? (
                        <Space size={4} wrap>
                          <Tag color={connectorLabel(latestJob.connectorType).color}>
                            {connectorLabel(latestJob.connectorType).title}
                          </Tag>
                          <PublishJobStatusTag status={latestJob.status} />
                          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            {formatDisplayAt(latestJob.updatedAt)}
                          </Typography.Text>
                        </Space>
                      ) : null,
                    },
                  ]}
                />
              )}
            </Card>
          </Space>
        </Col>
      </Row>

      {/* LOWER: JOBS */}
      <Card size="small" title={`Lịch sử xuất bản (${jobs.length})`}>
        <Table
          size="middle"
          rowKey="id"
          pagination={false}
          dataSource={jobs}
          locale={{ emptyText: 'Chưa xuất bản' }}
          scroll={{ x: 880 }}
          columns={contentPublishJobColumns({
            busy,
            onRetry: (j) => void onRetryJob(j),
            onReconnectFacebook: (j) => void onReconnectFacebook(j),
          })}
        />
      </Card>
    </Space>
  );
}
