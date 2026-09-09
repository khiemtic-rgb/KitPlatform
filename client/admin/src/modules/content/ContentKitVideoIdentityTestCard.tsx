import { useEffect, useState } from 'react';
import { Alert, Button, Card, Space, Tag, Typography } from 'antd';
import {
  analyzeKitVideoIdentityTest,
  compareKitVideoIdentityTest,
  createKitVideoIdentityTest,
  decideKitVideoIdentityTest,
  fetchFamixaCharacter,
  fetchKitVideoIdentityArtifactObjectUrl,
  fetchKitVideoMasterReference,
  listKitVideoIdentityTests,
  runKitVideoIdentityTest,
  type KitVideoIdentityTestRow,
  type KitVideoMasterCandidateRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { dnaApproved } from './content-famixa-minh-visual-dna';
import { parseFamixaCanon } from './content-famixa-character-memory';
import { EMOTION_VARIANTS, PRIORITY_CANDIDATES, VIEW_VARIANTS } from './kit-video-identity-test';

function labelOf(variant: string) {
  if (variant === 'THREE_QUARTER_LEFT') return '3/4 LEFT';
  if (variant === 'THREE_QUARTER_RIGHT') return '3/4 RIGHT';
  if (variant === 'LIGHT_SMILE') return 'LIGHT SMILE';
  return variant;
}

export function ContentKitVideoIdentityTestCard() {
  const [candidates, setCandidates] = useState<KitVideoMasterCandidateRow[]>([]);
  const [tests, setTests] = useState<KitVideoIdentityTestRow[]>([]);
  const [dnaOk, setDnaOk] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [previews, setPreviews] = useState<Record<string, string>>({});
  const [pick, setPick] = useState<string>();

  const load = () => {
    setBusy(true);
    void Promise.all([fetchKitVideoMasterReference('FAMIXA', 'CHAR-001'), fetchFamixaCharacter('CHAR-001'), listKitVideoIdentityTests()])
      .then(([master, character, rows]) => {
        const canon = parseFamixaCanon(character.canon);
        setDnaOk(dnaApproved(canon.visualDna));
        setCandidates(master.candidates || []);
        setTests(rows);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Identity Test.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, []);

  useEffect(() => {
    const ids = tests.flatMap((t) => t.artifacts.filter((a) => a.sha256).map((a) => a.id));
    let gone = false;
    const urls: Record<string, string> = {};
    void Promise.all(
      ids.map((id) =>
        fetchKitVideoIdentityArtifactObjectUrl(id)
          .then((url) => {
            urls[id] = url;
          })
          .catch(() => undefined),
      ),
    ).then(() => {
      if (gone) {
        Object.values(urls).forEach((u) => URL.revokeObjectURL(u));
        return;
      }
      setPreviews((prev) => {
        Object.values(prev).forEach((u) => URL.revokeObjectURL(u));
        return urls;
      });
    });
    return () => {
      gone = true;
    };
  }, [tests]);

  const eligible = candidates.filter((c) => {
    const code = (c.candidateCode || '').toUpperCase();
    if (/CANDIDATE-001$/.test(code) && !/CANDIDATE-001-/.test(code)) return false;
    return c.frontRunner || c.lifecycle === 'FRONT_RUNNER' || (PRIORITY_CANDIDATES as readonly string[]).includes(c.candidateCode);
  });

  const apply = (row: KitVideoIdentityTestRow) => {
    setTests((cur) => {
      const rest = cur.filter((t) => t.id !== row.id);
      return [row, ...rest];
    });
    setNotice(undefined);
    setError(undefined);
  };

  return (
    <Card size="small" title="Kiểm định nhận diện — Identity Test" style={{ marginBottom: 12 }}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        Kiểm tra cùng một Minh qua góc nhìn và cảm xúc. Ảnh test không phải hộ chiếu, không phải Canon, không khóa nhân vật.
      </Typography.Paragraph>
      {!dnaOk ? <Alert type="warning" showIcon message="DNA chưa APPROVED — không chạy Identity Test." style={{ marginBottom: 8 }} /> : null}
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {notice ? <Alert type="info" showIcon message={notice} style={{ marginBottom: 8 }} /> : null}

      <Space wrap style={{ marginBottom: 8 }}>
        {eligible.map((c) => (
          <Button
            key={c.id}
            size="small"
            type={pick === c.id ? 'primary' : 'default'}
            disabled={busy || !dnaOk}
            onClick={() => {
              setPick(c.id);
              setBusy(true);
              void createKitVideoIdentityTest(c.id, true)
                .then(apply)
                .catch((e) => setError(apiErrorMessage(e, 'Chưa mở được Identity Test.')))
                .finally(() => setBusy(false));
            }}
          >
            Mở test {c.candidateCode}
          </Button>
        ))}
        <Button size="small" loading={busy} onClick={load}>
          Tải lại
        </Button>
      </Space>

      {tests.map((t) => (
        <TestBlock
          key={t.id}
          row={t}
          previews={previews}
          busy={busy}
          dnaOk={dnaOk}
          onRun={(variant) => {
            setBusy(true);
            void runKitVideoIdentityTest(t.id, variant)
              .then(apply)
              .catch((e) => setError(apiErrorMessage(e, 'Chưa chạy được test.')))
              .finally(() => setBusy(false));
          }}
          onAnalyze={() => {
            setBusy(true);
            void analyzeKitVideoIdentityTest(t.id)
              .then(apply)
              .catch((e) => setError(apiErrorMessage(e, 'Chưa phân tích được.')))
              .finally(() => setBusy(false));
          }}
          onCompare={() => {
            setBusy(true);
            void compareKitVideoIdentityTest(t.id)
              .then(apply)
              .catch((e) => setError(apiErrorMessage(e, 'Chưa so sánh được.')))
              .finally(() => setBusy(false));
          }}
          onDecide={(decision) => {
            setBusy(true);
            void decideKitVideoIdentityTest(t.id, decision)
              .then((row) => {
                apply(row);
                setNotice(
                  decision === 'PROMOTE_TO_MASTER_REVIEW'
                    ? 'Đưa sang Master Review — chưa phải hộ chiếu, chưa khóa.'
                    : `Director: ${decision}. Không tự Canon.`,
                );
              })
              .catch((e) => setError(apiErrorMessage(e, 'Chưa ghi được quyết định.')))
              .finally(() => setBusy(false));
          }}
        />
      ))}
    </Card>
  );
}

function TestBlock({
  row,
  previews,
  busy,
  dnaOk,
  onRun,
  onAnalyze,
  onCompare,
  onDecide,
}: {
  row: KitVideoIdentityTestRow;
  previews: Record<string, string>;
  busy: boolean;
  dnaOk: boolean;
  onRun: (variant?: string) => void;
  onAnalyze: () => void;
  onCompare: () => void;
  onDecide: (decision: string) => void;
}) {
  const latest = new Map(row.artifacts.map((a) => [a.testVariant, a]));
  const mark = (variant: string) => {
    const art = latest.get(variant);
    if (!art) return '○';
    if (art.qaStatus === 'PASS') return '✓';
    if (art.qaStatus === 'FAIL' || art.qaStatus === 'VISION_FAIL' || art.qaStatus === 'ARTIFACT_FAILED') return '✗';
    return '…';
  };
  const stabilityOf = (variants: readonly string[]) => {
    const arts = variants.map((v) => latest.get(v));
    if (arts.some((a) => !a)) return 'PENDING';
    if (arts.some((a) => a && ['FAIL', 'VISION_FAIL', 'ARTIFACT_FAILED'].includes(a.qaStatus))) return 'FAIL';
    if (arts.every((a) => a?.qaStatus === 'PASS')) return 'PASS';
    return 'PENDING';
  };
  const viewStab = row.viewStability && row.viewStability !== 'PENDING' ? row.viewStability : stabilityOf(VIEW_VARIANTS);
  const emoStab = row.emotionStability && row.emotionStability !== 'PENDING' ? row.emotionStability : stabilityOf(EMOTION_VARIANTS);
  const visionStab =
    row.identityStability && row.identityStability !== 'PENDING'
      ? row.identityStability
      : stabilityOf([...VIEW_VARIANTS, ...EMOTION_VARIANTS]);
  return (
    <div style={{ border: '1px solid #e2e8f0', borderRadius: 8, padding: 10, marginBottom: 8 }}>
      <Space wrap size={4}>
        <Tag>{row.candidateCode}</Tag>
        <Tag color={row.status === 'PASS' ? 'green' : row.status === 'FAIL' ? 'red' : 'default'}>{row.status}</Tag>
        <Tag>
          {row.have} / {row.required}
        </Tag>
        {row.complete ? <Tag color="blue">IDENTITY_TEST_COMPLETE</Tag> : <Tag>INCOMPLETE</Tag>}
        {row.directorDecision ? <Tag>Director {row.directorDecision}</Tag> : <Tag>Director PENDING</Tag>}
      </Space>
      <div style={{ fontSize: 12, color: '#475569', margin: '8px 0' }}>
        VIEW {VIEW_VARIANTS.map((v) => `${mark(v)} ${labelOf(v)}`).join('   ')}
        <br />
        EMOTION {EMOTION_VARIANTS.map((v) => `${mark(v)} ${labelOf(v)}`).join('   ')}
        <br />
        Vision {visionStab} · View {viewStab} · Emotion {emoStab}
      </div>
      {[
        { title: 'VIEW', variants: VIEW_VARIANTS },
        { title: 'EMOTION', variants: EMOTION_VARIANTS },
      ].map((rowGroup) => (
        <div key={rowGroup.title} style={{ marginBottom: 12 }}>
          <div style={{ fontSize: 12, color: '#64748b', marginBottom: 6 }}>{rowGroup.title}</div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, overflowX: 'auto' }}>
            {rowGroup.variants.map((v) => {
              const art = latest.get(v);
              return (
                <div key={v} style={{ width: 240, flex: '0 0 240px', textAlign: 'center' }}>
                  {art && previews[art.id] ? (
                    <img
                      src={previews[art.id]}
                      alt={v}
                      style={{ width: 240, height: 320, objectFit: 'contain', borderRadius: 8, background: '#f8fafc' }}
                    />
                  ) : (
                    <div style={{ width: 240, height: 320, background: '#f1f5f9', borderRadius: 8, fontSize: 13, color: '#94a3b8', display: 'grid', placeItems: 'center' }}>
                      {labelOf(v)}
                    </div>
                  )}
                  <div style={{ fontSize: 13, color: '#334155', marginTop: 4 }}>{labelOf(v)}</div>
                  <Button size="small" type="link" disabled={busy || !dnaOk} onClick={() => onRun(v)} style={{ padding: 0, height: 'auto' }}>
                    Chạy
                  </Button>
                </div>
              );
            })}
          </div>
        </div>
      ))}
      <Space wrap>
        <Button size="small" type="primary" disabled={busy || !dnaOk} onClick={() => onRun()}>
          Chạy 7 test
        </Button>
        <Button size="small" disabled={busy} onClick={onAnalyze}>
          Phân tích
        </Button>
        <Button size="small" disabled={busy} onClick={onCompare}>
          So sánh
        </Button>
        <Button size="small" disabled={busy} onClick={() => onDecide('PASS')}>
          Director PASS
        </Button>
        <Button size="small" disabled={busy} onClick={() => onDecide('CONDITIONAL')}>
          CONDITIONAL
        </Button>
        <Button size="small" danger disabled={busy} onClick={() => onDecide('FAIL')}>
          FAIL
        </Button>
        <Button size="small" disabled={busy} onClick={() => onDecide('PROMOTE_TO_MASTER_REVIEW')}>
          Đưa sang Master Review
        </Button>
      </Space>
    </div>
  );
}
