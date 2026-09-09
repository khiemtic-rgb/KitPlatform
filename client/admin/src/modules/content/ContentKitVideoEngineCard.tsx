import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Card, Collapse, Input, Space, Tag, Typography } from 'antd';
import {
  compileKitVideoStory,
  decideKitVideoKeyframe,
  ensureKitVideoProduction,
  fetchKitVideoAssets,
  fetchKitVideoProductions,
  fetchKitVideoProjects,
  fetchKitVideoStory,
  fetchKitVideoAttemptImageBlob,
  fetchKitVideoVisualProvider,
  generateKitVideoKeyframe,
  pollKitVideoMotion,
  preflightKitVideoMotion,
  revalidateKitVideoKeyframe,
  reviseKitVideoKeyframe,
  submitKitVideoMotion,
  type KitVideoAssetRow,
  type KitVideoMotionTakeRow,
  type KitVideoPixelGenerateRow,
  type KitVideoProductionRow,
  type KitVideoProjectRow,
  type KitVideoProviderStatus,
  type KitVideoStoryGraphRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatAssetLine } from './kit-video-asset';
import { compileStoryGraph, formatContinuityBoard, KIT_VIDEO_CONTINUITY } from './kit-video-continuity';
import { formatShotBoard, KIT_VIDEO_ENGINE } from './kit-video-engine';
import { formatVisionBoard, KIT_VIDEO_VISION } from './kit-video-vision';
import { FAMIXA_PROJECT, FAMIXA_UNIVERSE } from './kit-video-famixa-adapter';
import { FAMIXA_VISUAL_STYLE, famixaGoldenContract, formatPixelBoard, KIT_VIDEO_PIXEL } from './kit-video-pixel';
import { famixaGoldenMotion, formatMotionBoard, KIT_VIDEO_MOTION } from './kit-video-motion';

const DEMO_SCRIPT =
  'Minh chạy vào phòng khách, trên tay cầm bài kiểm tra. Cậu bé vui vẻ đưa bài cho mẹ và nói: “Mẹ ơi, con được 9 điểm!”';

export function ContentKitVideoEngineCard() {
  const [projects, setProjects] = useState<KitVideoProjectRow[]>([]);
  const [productions, setProductions] = useState<KitVideoProductionRow[]>([]);
  const [assets, setAssets] = useState<KitVideoAssetRow[]>([]);
  const [story, setStory] = useState<KitVideoStoryGraphRow>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [provider, setProvider] = useState<KitVideoProviderStatus>();
  const [pixel, setPixel] = useState<KitVideoPixelGenerateRow>();
  const [revisionNote, setRevisionNote] = useState('');
  const [revision, setRevision] = useState<string>();
  const [previewUrl, setPreviewUrl] = useState<string>();
  const [motionTake, setMotionTake] = useState<KitVideoMotionTakeRow>();
  const [motionKey] = useState(() => `ui-sh01-01-i2v-${Date.now()}`);

  const demo = useMemo(() => compileStoryGraph(DEMO_SCRIPT), []);
  const golden = useMemo(() => famixaGoldenContract(), []);
  const goldenMotion = useMemo(() => famixaGoldenMotion(), []);
  const visionBoard = useMemo(() => formatVisionBoard(golden, {
    status: pixel?.qa?.status === 'PASS' ? 'PASS' : pixel?.qa?.status === 'FAIL' ? 'FAIL' : 'WARNING',
    scores: pixel?.qa?.scores ?? { character: 0, action: 0, location: 0, props: 0, composition: 0, continuity: 0, integrity: 0 },
    p0Fail: pixel?.qa?.p0Fail ?? [],
    warnings: pixel?.qa?.warnings ?? ['Chưa generate pixel — Phase 05 cần Gemini thật.'],
    reasons: pixel?.qa?.p0Fail ?? [],
    allowI2v: pixel?.qa?.allowI2v === true && pixel?.status === 'APPROVED',
    canBeReference: pixel?.qa?.canBeReference === true && pixel?.status === 'APPROVED',
  }), [golden, pixel]);

  const load = () => {
    Promise.all([
      fetchKitVideoProjects(),
      fetchKitVideoProductions(),
      fetchKitVideoAssets(FAMIXA_PROJECT),
      fetchKitVideoVisualProvider().catch(() => undefined),
    ])
      .then(([p, prod, a, prov]) => {
        setProjects(p);
        setProductions(prod);
        setAssets(a);
        if (prov) setProvider(prov);
        setError(undefined);
        const first = prod[0];
        if (first) {
          return fetchKitVideoStory(first.id)
            .then(setStory)
            .catch(() => setStory(undefined));
        }
        setStory(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Video Engine chưa sẵn (mig 339).')));
  };

  useEffect(() => {
    load();
  }, []);

  useEffect(() => {
    if (!pixel?.attemptId || !pixel.imagePath) {
      setPreviewUrl(undefined);
      return;
    }
    let revoked = false;
    void fetchKitVideoAttemptImageBlob(pixel.attemptId)
      .then((blob) => {
        if (revoked) return;
        setPreviewUrl(URL.createObjectURL(blob));
      })
      .catch(() => setPreviewUrl(undefined));
    return () => {
      revoked = true;
    };
  }, [pixel?.attemptId, pixel?.imagePath]);

  const attachEp01 = () => {
    setBusy(true);
    void ensureKitVideoProduction({
      projectCode: FAMIXA_PROJECT,
      universeCode: FAMIXA_UNIVERSE,
      productionCode: 'EP01',
      title: 'Famixa EP01',
    })
      .then(() => load())
      .catch((e) => setError(apiErrorMessage(e, 'Không gắn được production.')))
      .finally(() => setBusy(false));
  };

  const generateSh0101 = () => {
    const first = productions[0];
    if (!first) {
      setError('Gắn production EP01 trước khi generate.');
      return;
    }
    if (provider && !provider.apiKeyConfigured) {
      setError('Gemini API key chưa cấu hình trên server. Không gọi generate.');
      return;
    }
    setBusy(true);
    void generateKitVideoKeyframe({
      productionId: first.id,
      shotCode: 'SH01-01',
      contract: golden,
      confirmed: true,
      idempotencyKey: `ui-sh01-01-${first.id}-${Date.now()}`,
      projectStyle: FAMIXA_VISUAL_STYLE,
    })
      .then((row) => {
        setPixel(row);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không generate được keyframe.')))
      .finally(() => setBusy(false));
  };

  const decide = (decision: 'APPROVE' | 'REJECT') => {
    const first = productions[0];
    if (!first || !pixel?.attemptId) return;
    setBusy(true);
    void decideKitVideoKeyframe({
      productionId: first.id,
      shotCode: pixel.shotCode || 'SH01-01',
      attemptId: pixel.attemptId,
      decision,
    })
      .then((row) => {
        setPixel(row);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Director decision thất bại.')))
      .finally(() => setBusy(false));
  };

  const preflightMotion = () => {
    if (!pixel?.attemptId || !pixel.i2v?.ready) return;
    setBusy(true);
    void preflightKitVideoMotion({
      keyframeAttemptId: pixel.attemptId,
      motionContract: goldenMotion,
      confirmed: false,
      idempotencyKey: motionKey,
    })
      .then((row) => {
        setMotionTake(row);
        setError(row.preflight?.ok ? undefined : (row.preflight?.blocked || []).join(' | '));
      })
      .catch((e) => setError(apiErrorMessage(e, 'Preflight chặn — 0 credit.')))
      .finally(() => setBusy(false));
  };

  const confirmRunway = () => {
    if (!pixel?.attemptId || !pixel.i2v?.ready) return;
    setBusy(true);
    void submitKitVideoMotion({
      keyframeAttemptId: pixel.attemptId,
      motionContract: goldenMotion,
      confirmed: true,
      idempotencyKey: motionKey,
    })
      .then((row) => {
        setMotionTake(row);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không gửi Runway.')))
      .finally(() => setBusy(false));
  };

  const pollMotion = () => {
    if (!motionTake?.takeId) return;
    setBusy(true);
    void pollKitVideoMotion(motionTake.takeId)
      .then((row) => {
        setMotionTake(row);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Poll Runway thất bại.')))
      .finally(() => setBusy(false));
  };

  const revalidate = () => {
    if (!pixel?.attemptId) return;
    setBusy(true);
    void revalidateKitVideoKeyframe(pixel.attemptId, golden)
      .then((row) => {
        setPixel(row);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Revalidate thất bại — không generate ảnh mới.')))
      .finally(() => setBusy(false));
  };

  const revise = () => {
    setBusy(true);
    void reviseKitVideoKeyframe(revisionNote)
      .then((row) => setRevision(row.instruction))
      .catch((e) => setError(apiErrorMessage(e, 'Không biên được revision.')))
      .finally(() => setBusy(false));
  };

  const persistDemo = () => {
    const first = productions[0];
    if (!first) return;
    setBusy(true);
    void compileKitVideoStory(first.id, DEMO_SCRIPT)
      .then((row) => {
        setStory(row);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không biên kịch được graph.')))
      .finally(() => setBusy(false));
  };

  const boards = story?.graph.shots?.length
    ? story.graph.shots.map((s) =>
        [
          s.displayCode,
          '',
          'ACTION',
          s.action || '(none)',
          '',
          'CHARACTERS',
          s.characters.join('\n') || '—',
          '',
          'PROPS',
          s.requiredProps.join('\n') || '—',
          '',
          'DIALOGUE',
          s.dialogueSegmentIds.join(', ') || 'UNASSIGNED',
          '',
          'CONTINUITY',
          s.continuity,
          '',
          'STATUS',
          s.status,
        ].join('\n'),
      )
    : demo.shots.map((s) => formatContinuityBoard(s));

  return (
    <Collapse
      className="fx-engine-fold"
      items={[
        {
          key: 'engine',
          label: 'Dành cho kỹ thuật',
          children: (
    <Card size="small" title="KIT Video Engine" style={{ marginBottom: 0 }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        {KIT_VIDEO_ENGINE} · {KIT_VIDEO_CONTINUITY} · {KIT_VIDEO_VISION} · {KIT_VIDEO_PIXEL} Phase 05.1 ·
        APPROVED_KEYFRAME chỉ khi artifact + hash + Vision + P0 + PRODUCTION_STILL + Director. Chưa gọi Runway.
      </Typography.Paragraph>
      {error ? <Alert type="warning" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      <Space wrap>
        {projects.map((p) => (
          <Tag key={p.projectCode} color={p.status === 'active' ? 'blue' : undefined}>
            {p.projectCode} · {p.status}
          </Tag>
        ))}
      </Space>
      {assets.length > 0 ? (
        <pre style={{ marginTop: 8, marginBottom: 0, fontSize: 12, lineHeight: 1.4 }}>
          {assets
            .map((a) => formatAssetLine({ assetCode: a.assetCode, name: a.name, lifecycle: a.lifecycle, version: a.version }))
            .join('\n')}
        </pre>
      ) : null}
      <div style={{ marginTop: 8 }}>
        {productions.length === 0 ? (
          <Space>
            <Typography.Text type="secondary">Chưa có production trên engine.</Typography.Text>
            <Button size="small" loading={busy} onClick={attachEp01}>
              Gắn FAMIXA / FAMILY_A / EP01
            </Button>
          </Space>
        ) : (
          productions.map((row) => (
            <div key={row.id} style={{ marginTop: 8 }}>
              <Tag>
                {row.projectCode}/{row.universeCode}/{row.productionCode} · {row.state}
                {row.runStatus ? ` · run ${row.runStatus}` : ''}
              </Tag>
              {row.shots.length === 0 ? (
                <Typography.Text type="secondary" style={{ display: 'block', marginTop: 6 }}>
                  Chưa có shot trên engine. HOLD nếu không có Action.
                </Typography.Text>
              ) : (
                <pre
                  style={{
                    marginTop: 8,
                    marginBottom: 0,
                    fontSize: 12,
                    lineHeight: 1.45,
                    whiteSpace: 'pre-wrap',
                  }}
                >
                  {row.shots
                    .map((s) =>
                      formatShotBoard({
                        shotCode: s.shotCode,
                        state: s.state,
                        lastProvider: s.lastProvider,
                        lastFailureCode: s.lastFailureCode,
                      }),
                    )
                    .join('\n\n')}
                </pre>
              )}
            </div>
          ))
        )}
      </div>
      <div style={{ marginTop: 12 }}>
        <Space wrap>
          <Typography.Text type="secondary">
            SC01 · {demo.scenes[0]?.objective || 'Demo beat'} · {boards.length} shot (không cố định)
          </Typography.Text>
          {productions[0] ? (
            <Button size="small" loading={busy} onClick={persistDemo}>
              Gắn graph demo vào production
            </Button>
          ) : null}
        </Space>
        {demo.unassigned.length || story?.unassigned?.length ? (
          <Alert
            type="warning"
            showIcon
            style={{ marginTop: 8 }}
            message={`Dialogue UNASSIGNED: ${(story?.unassigned ?? demo.unassigned).join(', ')}`}
          />
        ) : null}
        <pre
          style={{
            marginTop: 8,
            marginBottom: 0,
            fontSize: 12,
            lineHeight: 1.45,
            whiteSpace: 'pre-wrap',
          }}
        >
          {boards.join('\n\n')}
        </pre>
      </div>
      <div style={{ marginTop: 12 }}>
        <Typography.Text type="secondary">Phase 05.1 · SH01-01 integrity (revalidate, không gửi Runway)</Typography.Text>
        <Space wrap style={{ marginTop: 8 }}>
          <Tag>{provider?.imageProvider || 'gemini'}</Tag>
          <Tag>{provider?.imageModel || 'model from server config'}</Tag>
          <Tag color={provider?.apiKeyConfigured ? 'green' : 'red'}>
            {provider?.apiKeyConfigured ? 'API key configured' : 'API key missing'}
          </Tag>
          <Tag color={motionTake?.runwayCalled ? 'orange' : undefined}>
            {motionTake?.runwayCalled ? `RUNWAY ${motionTake.status}` : 'RUNWAY idle · SH01-01 only'}
          </Tag>
        </Space>
        <Space wrap style={{ marginTop: 8 }}>
          <Button size="small" type="primary" loading={busy} disabled={!productions[0]} onClick={generateSh0101}>
            Generate SH01-01 (USER CONFIRM)
          </Button>
          <Button size="small" disabled={!pixel?.attemptId} loading={busy} onClick={revalidate}>
            Revalidate (no generate)
          </Button>
          <Button
            size="small"
            disabled={pixel?.qa?.status !== 'PASS' || pixel?.imageType === 'CHARACTER_SHEET' || pixel?.status === 'APPROVED'}
            onClick={() => decide('APPROVE')}
          >
            Approve
          </Button>
          <Button size="small" disabled={!pixel?.attemptId} onClick={() => decide('REJECT')}>
            Reject
          </Button>
        </Space>
        <Space.Compact style={{ marginTop: 8, width: '100%' }}>
          <Input
            size="small"
            placeholder="Director: Minh nhìn hơi già / Mẹ chưa nhìn Minh / bài kiểm tra quá nhỏ"
            value={revisionNote}
            onChange={(e) => setRevisionNote(e.target.value)}
          />
          <Button size="small" onClick={revise} disabled={!revisionNote.trim()}>
            Revise
          </Button>
        </Space.Compact>
        {revision ? (
          <Typography.Paragraph style={{ marginTop: 8, marginBottom: 0 }} type="secondary">
            RevisionInstruction: {revision}
          </Typography.Paragraph>
        ) : null}
        {pixel?.status === 'VISION_FAIL' || pixel?.qa?.status === 'FAIL' ? (
          <Alert
            type="error"
            showIcon
            style={{ marginTop: 8 }}
            message={`FAIL · ${(pixel.qa?.p0Fail || []).join(' | ') || pixel.failureClass}`}
            description={pixel.repair?.repair}
          />
        ) : null}
        {pixel?.persistStatus === 'PERSISTENCE_FAILED' ? (
          <Alert type="warning" showIcon style={{ marginTop: 8 }} message="PERSISTENCE_FAILED · Revalidate artifact cũ, không generate lại" />
        ) : null}
        {pixel?.i2v?.ready ? (
          <Alert type="success" showIcon style={{ marginTop: 8 }} message="I2V READY · APPROVED_KEYFRAME · hashes match · Runway not called" />
        ) : pixel?.status === 'APPROVED' ? (
          <Alert type="error" showIcon style={{ marginTop: 8 }} message={`NOT I2V READY · ${(pixel.i2v?.blocked || []).join(' | ')}`} />
        ) : null}
        {previewUrl ? (
          <img
            alt="SH01-01 keyframe"
            src={previewUrl}
            style={{ marginTop: 8, maxWidth: 480, width: '100%', height: 'auto', borderRadius: 8 }}
          />
        ) : null}
        <pre
          style={{
            marginTop: 8,
            marginBottom: 0,
            fontSize: 12,
            lineHeight: 1.45,
            whiteSpace: 'pre-wrap',
          }}
        >
          {formatPixelBoard({
            shotCode: pixel?.shotCode || 'SH01-01',
            jobState: pixel?.jobState || 'QUEUED',
            provider: pixel?.provider || provider?.generatorProvider || 'GEMINI',
            model: pixel?.model || provider?.imageModel || '',
            artifactOk: Boolean(pixel?.imagePath),
            qa: pixel?.qa,
            repair: pixel?.repair?.repair,
            i2vReady: pixel?.i2v?.ready === true,
            runwayCalled: false,
            imageType: pixel?.imageType || pixel?.qa?.imageType,
            artifactHash: pixel?.artifactHash,
            persistStatus: pixel?.persistStatus,
          })}
        </pre>
        <pre
          style={{
            marginTop: 8,
            marginBottom: 0,
            fontSize: 12,
            lineHeight: 1.45,
            whiteSpace: 'pre-wrap',
          }}
        >
          {visionBoard}
        </pre>
        <div style={{ marginTop: 12 }}>
          <Typography.Text type="secondary">
            {KIT_VIDEO_MOTION} · Golden SH01-01 only · Preflight 0 cr · Confirm mới trừ credit
          </Typography.Text>
          <Space wrap style={{ marginTop: 8 }}>
            <Button size="small" disabled={!pixel?.i2v?.ready} loading={busy} onClick={preflightMotion}>
              Preflight SH01-01 (0 cr)
            </Button>
            <Button
              size="small"
              type="primary"
              disabled={!pixel?.i2v?.ready || motionTake?.preflight?.ok === false}
              loading={busy}
              onClick={confirmRunway}
            >
              Confirm Runway 5s
            </Button>
            <Button size="small" disabled={!motionTake?.takeId || motionTake.takeId === '00000000-0000-0000-0000-000000000000'} loading={busy} onClick={pollMotion}>
              Poll status
            </Button>
          </Space>
          {motionTake?.preflight && !motionTake.preflight.ok ? (
            <Alert type="error" showIcon style={{ marginTop: 8 }} message={`PREFLIGHT BLOCK · 0 credit · ${motionTake.preflight.blocked.join(' | ')}`} />
          ) : null}
          {motionTake?.videoReady ? (
            <Alert type="success" showIcon style={{ marginTop: 8 }} message={`${motionTake.status} · Video QA ${motionTake.qa?.status || 'pending'} · ${motionTake.creditState}`} />
          ) : null}
          {motionTake?.status === 'DIAGNOSE' || motionTake?.status === 'FAILED' ? (
            <Alert type="warning" showIcon style={{ marginTop: 8 }} message={motionTake.diagnose || motionTake.failureClass} />
          ) : null}
          <pre
            style={{
              marginTop: 8,
              marginBottom: 0,
              fontSize: 12,
              lineHeight: 1.45,
              whiteSpace: 'pre-wrap',
            }}
          >
            {formatMotionBoard({
              shotCode: 'SH01-01',
              status: motionTake?.status || 'READY',
              prompt: motionTake?.prompt || goldenMotion.motion,
              credit: motionTake?.creditState || 'NONE',
              videoReady: motionTake?.videoReady === true,
              qa: motionTake?.qa,
              diagnose: motionTake?.diagnose,
            })}
          </pre>
        </div>
      </div>
    </Card>
          ),
        },
      ]}
    />
  );
}
