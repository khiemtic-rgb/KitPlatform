import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Drawer,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Steps,
  Tag,
  Typography,
} from 'antd';
import {
  approveFamixaCharacter,
  createFamixaCharacterVersion,
  generateContentSeriesStill,
  lockFamixaCharacter,
  putFamixaCharacterCanon,
  unlockFamixaCharacter,
  type FamixaCharacterRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  FAMIXA_VISUAL_STYLE,
  parseFamixaCanon,
  serializeFamixaCanon,
  type FamixaCharacterCanon,
  type FamixaCharacterRecord,
  type FamixaOutfit,
  type FamixaWorkspaceStepId,
} from './content-famixa-character-memory';
import {
  CORE_MASTER_VIEWS,
  WARDROBE_SETS,
  WORKSPACE_STEPS,
  canApproveCharacter,
  canGenerateMasterRef,
  canLockCharacter,
  characterQa,
  compileMasterRefPrompt,
  definitionComplete,
  nextMasterRefKind,
  proposeVisualDna,
  relationshipMustUseExisting,
  workspaceProgress,
} from './content-famixa-character-create';
import { mergeMinhBriefIntoCanon } from './content-famixa-character-brief';
import { mergeMinhVisualDnaDraft } from './content-famixa-minh-visual-dna';
import { FAMIXA_VISUAL_STYLE_V1 } from './content-famixa-visual-style';

function toRecord(row: FamixaCharacterRow): FamixaCharacterRecord {
  return {
    id: row.id,
    characterCode: row.characterCode,
    name: row.name,
    role: row.role,
    universe: row.universe,
    visual: row.visual,
    lifecycle: row.lifecycle,
    currentVersionId: row.currentVersionId ?? undefined,
    currentEra: row.currentEra,
    version: row.version,
    isCurrentCanon: row.isCurrentCanon,
    approvedAt: row.approvedAt ?? undefined,
    approvedBy: row.approvedBy ?? undefined,
    canon: parseFamixaCanon(row.canon),
    references: (row.references ?? []).map((r) => ({ kind: r.kind, path: r.path, label: r.label ?? undefined })),
    updatedAt: row.updatedAt,
  };
}

function listField(raw: string) {
  return raw
    .split(/[,;\n]/)
    .map((s) => s.trim())
    .filter(Boolean);
}

export function ContentFamixaCharacterWorkspace({
  open,
  row,
  registry,
  busy,
  onClose,
  onChanged,
}: {
  open: boolean;
  row?: FamixaCharacterRecord;
  registry: FamixaCharacterRecord[];
  busy?: boolean;
  onClose: () => void;
  onChanged: (row: FamixaCharacterRow) => void;
}) {
  const [step, setStep] = useState(0);
  const [draft, setDraft] = useState<FamixaCharacterCanon | undefined>();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string>();
  const [revision, setRevision] = useState('');
  const [preview, setPreview] = useState<string>();
  const [genBusy, setGenBusy] = useState(false);

  useEffect(() => {
    if (!row) {
      setDraft(undefined);
      return;
    }
    const raw = serializeFamixaCanon(row.canon);
    const withBrief =
      row.characterCode === 'CHAR-001' && !(row.canon.brief?.documentId)
        ? mergeMinhBriefIntoCanon(raw as Parameters<typeof mergeMinhBriefIntoCanon>[0])
        : raw;
    const withDna =
      row.characterCode === 'CHAR-001'
        ? mergeMinhVisualDnaDraft(withBrief as Parameters<typeof mergeMinhVisualDnaDraft>[0])
        : withBrief;
    setDraft(parseFamixaCanon(withDna));
    setStep(0);
    setError(undefined);
    setPreview(undefined);
    setRevision(row.canon.workspace?.revisionReason ?? '');
  }, [row?.characterCode, row?.version, open]);

  const record = useMemo(() => {
    if (!row || !draft) return row;
    return { ...row, canon: draft, references: draft.references ?? row.references };
  }, [row, draft]);

  if (!row || !draft || !record) return null;

  const locked = row.lifecycle === 'locked';
  const progress = workspaceProgress(record);
  const qa = characterQa(record);
  const approveGate = canApproveCharacter(record);
  const lockGate = canLockCharacter(record);
  const refGate = canGenerateMasterRef(record);
  const nextView = nextMasterRefKind(record);
  const styleWarn = draft.visualDna?.visualStyle && draft.visualDna.visualStyle !== 'FAMIXA_VISUAL_STYLE';

  const patch = (next: Partial<FamixaCharacterCanon>) => setDraft((cur) => (cur ? { ...cur, ...next } : cur));

  const saveCanon = async (next = draft) => {
    setSaving(true);
    setError(undefined);
    try {
      const saved = await putFamixaCharacterCanon(row.characterCode, serializeFamixaCanon(next));
      onChanged(saved);
      return saved;
    } catch (e) {
      const msg = apiErrorMessage(e, 'Không lưu Canon.');
      setError(msg);
      throw e;
    } finally {
      setSaving(false);
    }
  };

  const mark = (id: FamixaWorkspaceStepId, extra?: Partial<FamixaCharacterCanon>) => {
    const next: FamixaCharacterCanon = {
      ...draft,
      ...extra,
      workspace: {
        ...draft.workspace,
        ...extra?.workspace,
        completed: { ...draft.workspace?.completed, ...extra?.workspace?.completed, [id]: true },
      },
    };
    setDraft(next);
    return next;
  };

  const saveStep = async (id: FamixaWorkspaceStepId, extra?: Partial<FamixaCharacterCanon>) => {
    await saveCanon(mark(id, extra));
  };

  const runQa = async () => {
    const result = characterQa({ ...record, canon: draft });
    await saveStep('qa', { qa: result, workspace: { ...draft.workspace, qa: result } });
  };

  const generateNextRef = async () => {
    if (!refGate.ok || !nextView) return;
    setGenBusy(true);
    setError(undefined);
    try {
      const inherit = (draft.references ?? []).find((r) => r.kind.toUpperCase() === 'FRONT')?.path;
      const prompt = compileMasterRefPrompt({ canon: draft, kind: nextView, inheritFrom: inherit });
      const still = await generateContentSeriesStill({
        prompt,
        aspect: nextView === 'FULL_BODY' || nextView === 'EXPRESSION' ? '3:4' : '1:1',
        references: inherit && inherit.startsWith('data:') ? [{ name: row.name, imageDataUrl: inherit, role: row.role }] : [],
      });
      setPreview(still.imageDataUrl);
      const path = `local:${row.characterCode}:${row.currentEra}:${nextView}`;
      const refs = [...(draft.references ?? []).filter((r) => r.kind.toUpperCase() !== nextView), { kind: nextView, path }];
      setDraft({ ...draft, references: refs });
    } catch (e) {
      setError(apiErrorMessage(e, 'Không tạo được reference. Credit không trừ nếu BLOCK.'));
    } finally {
      setGenBusy(false);
    }
  };

  const others = registry.filter((r) => r.characterCode !== row.characterCode);

  return (
    <Drawer
      title={`${row.characterCode} · ${row.name} · ${row.currentEra} · ${row.version}`}
      width={760}
      open={open}
      onClose={onClose}
      extra={
        <Tag color={locked ? 'green' : row.lifecycle === 'approved' ? 'blue' : 'gold'}>
          {row.lifecycle.toUpperCase()}
        </Tag>
      }
    >
      <Typography.Paragraph type="secondary">
        {progress.completed} / {progress.total} completed · AI đề xuất, người duyệt. Character chưa LOCK không phải Canon.
      </Typography.Paragraph>
      {draft.brief?.status === 'BRIEF' ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          message={`${draft.brief.documentId} · BRIEF — READY FOR VISUAL DESIGN`}
          description={
            <>
              <div>{draft.brief.oneLine}</div>
              <div style={{ marginTop: 8 }}>{draft.brief.statement}</div>
              <div style={{ marginTop: 8 }}>
                Chưa khóa: {(draft.brief.notLocked ?? []).join(', ') || 'face, hair, Voice ID, Master Reference'}.
                Không đưa Minh vào production từ Brief. Bước tiếp: {draft.brief.nextStep}.
              </div>
            </>
          }
        />
      ) : null}
      {locked ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          message="This character is now Canon."
          description="Không sửa Visual DNA / Voice / Master trực tiếp. UNLOCK → version mới → REVIEW → APPROVE → LOCK."
        />
      ) : null}
      {error ? (
        <Alert type="error" showIcon style={{ marginBottom: 12 }} message={error} />
      ) : null}
      <Steps
        size="small"
        current={step}
        onChange={setStep}
        items={WORKSPACE_STEPS.map((s) => ({
          title: s.n.toString().padStart(2, '0'),
          description: s.title,
          status: workspaceProgress(record).steps.includes(s.id) ? 'finish' : step === s.n - 1 ? 'process' : 'wait',
        }))}
        style={{ marginBottom: 16 }}
      />

      {step === 0 ? (
        <Form layout="vertical">
          <Form.Item label="Character ID">
            <Input value={row.characterCode} disabled />
          </Form.Item>
          <Form.Item label="Name">
            <Input
              value={draft.identity.name}
              disabled={locked}
              onChange={(e) => patch({ identity: { ...draft.identity, name: e.target.value } })}
            />
          </Form.Item>
          <Form.Item label="Role">
            <Input
              value={draft.identity.role}
              disabled={locked}
              onChange={(e) => patch({ identity: { ...draft.identity, role: e.target.value } })}
            />
          </Form.Item>
          <Form.Item label="Gender">
            <Select
              value={draft.identity.gender}
              disabled={locked}
              options={['male', 'female', 'unspecified'].map((v) => ({ value: v, label: v }))}
              onChange={(gender) => patch({ identity: { ...draft.identity, gender } })}
            />
          </Form.Item>
          <Form.Item label="Initial Age">
            <InputNumber
              value={draft.identity.currentAge}
              disabled={locked}
              min={1}
              max={80}
              onChange={(n) =>
                patch({
                  identity: {
                    ...draft.identity,
                    currentAge: n ?? undefined,
                    initialAge: n ?? undefined,
                    currentEra: n ? `A${n}` : draft.identity.currentEra,
                  },
                })
              }
            />
          </Form.Item>
          <Form.Item label="Family Role">
            <Input
              value={draft.identity.familyRole}
              disabled={locked}
              onChange={(e) => patch({ identity: { ...draft.identity, familyRole: e.target.value } })}
            />
          </Form.Item>
          <Form.Item label="Short Biography">
            <Input.TextArea
              rows={3}
              value={draft.identity.biography}
              disabled={locked}
              onChange={(e) => patch({ identity: { ...draft.identity, biography: e.target.value } })}
            />
          </Form.Item>
          <Typography.Text type="secondary">Không đưa emotion/scene vào Identity. Sad chỉ ở Shot State.</Typography.Text>
          <div style={{ marginTop: 12 }}>
            <Button type="primary" disabled={locked} loading={saving} onClick={() => void saveStep('identity')}>
              Lưu Identity
            </Button>
          </div>
        </Form>
      ) : null}

      {step === 1 ? (
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          {row.characterCode === 'CHAR-001' ? (
            <Alert
              type="info"
              showIcon
              message={`${FAMIXA_VISUAL_STYLE_V1.documentId} · ${draft.famixaVisualStyle?.status || 'DRAFT'}`}
              description={`${FAMIXA_VISUAL_STYLE_V1.summary} Không LOCK từ ảnh Minh hiện tại. Không dùng Golden / take-01 làm face.`}
            />
          ) : null}
          {styleWarn ? (
            <Alert type="warning" showIcon message="Visual Style phải kế thừa Famixa Visual System. Đang lệch style." />
          ) : null}
          {draft.workspace?.visualProposal ? (
            <Alert type="info" showIcon message="CURRENT PROPOSAL — Approve mới ghi vào Canon. Regenerate chỉ sửa proposal." />
          ) : null}
          <Space wrap>
            <Button
              disabled={locked}
              onClick={() =>
                patch({
                  workspace: {
                    ...draft.workspace,
                    visualProposal: proposeVisualDna(draft.identity),
                    visualProposalStatus: 'draft',
                  },
                })
              }
            >
              AI đề xuất
            </Button>
            <Button
              disabled={locked || !draft.workspace?.visualProposal}
              onClick={() =>
                patch({
                  visualDna: {
                    ...(draft.workspace?.visualProposal ?? {}),
                    visualStyle: 'FAMIXA_VISUAL_STYLE',
                  },
                  workspace: { ...draft.workspace, visualProposalStatus: 'approved' },
                })
              }
            >
              APPROVE proposal
            </Button>
            <Button
              disabled={locked}
              onClick={() =>
                patch({
                  workspace: {
                    ...draft.workspace,
                    visualProposal: proposeVisualDna(draft.identity),
                    visualProposalStatus: 'draft',
                  },
                })
              }
            >
              REGENERATE
            </Button>
          </Space>
          {(['whoVisually', 'identityPrinciple', 'headShape', 'eyeShape', 'eyebrows', 'noseShape', 'mouthShape', 'hairLock', 'skin', 'bodyProportion', 'silhouette', 'posture', 'emotionalSignature', 'wardrobeBaseline', 'colorPersonality', 'signatureExpression', 'headToBody', 'face', 'eyes', 'nose', 'mouth', 'hair', 'hairStyle', 'body', 'height', 'build', 'distinctiveFeatures'] as const).map(
            (key) => (
              <Form.Item key={key} label={key} style={{ marginBottom: 8 }}>
                <Input
                  value={(draft.visualDna?.[key] as string | undefined) ?? (draft.workspace?.visualProposal?.[key] as string | undefined)}
                  disabled={locked}
                  onChange={(e) => patch({ visualDna: { ...draft.visualDna, [key]: e.target.value } })}
                />
              </Form.Item>
            ),
          )}
          <Typography.Paragraph type="secondary">{FAMIXA_VISUAL_STYLE.summary}</Typography.Paragraph>
          <Button type="primary" disabled={locked} loading={saving} onClick={() => void saveStep('visualDna')}>
            Lưu Visual DNA
          </Button>
        </Space>
      ) : null}

      {step === 2 ? (
        <Form layout="vertical">
          {(
            [
              ['core', 'Core Personality'],
              ['strengths', 'Strengths'],
              ['weaknesses', 'Weaknesses'],
              ['fears', 'Fears'],
              ['needs', 'Needs'],
              ['desires', 'Desires'],
              ['values', 'Values'],
              ['triggers', 'Triggers'],
              ['emotionalPatterns', 'Emotional Patterns'],
            ] as const
          ).map(([key, label]) => (
            <Form.Item key={key} label={label}>
              <Input
                disabled={locked}
                value={(draft.personality?.[key] ?? []).join(', ')}
                onChange={(e) => {
                  const personality = { ...draft.personality, [key]: listField(e.target.value) };
                  patch({ personality, personalityDna: personality.core ?? draft.personalityDna });
                }}
              />
            </Form.Item>
          ))}
          <Typography.Text type="secondary">Không biến đặc điểm thành hành động bắt buộc mọi scene.</Typography.Text>
          <div style={{ marginTop: 12 }}>
            <Button type="primary" disabled={locked} loading={saving} onClick={() => void saveStep('personality')}>
              Lưu Personality
            </Button>
          </div>
        </Form>
      ) : null}

      {step === 3 ? (
        <Form layout="vertical">
          <Form.Item label="Behavior (when → does, mỗi dòng)">
            <Input.TextArea
              rows={6}
              disabled={locked}
              value={(draft.behaviorDna ?? []).map((b) => `${b.when} → ${b.does}`).join('\n')}
              onChange={(e) =>
                patch({
                  behaviorDna: e.target.value
                    .split('\n')
                    .map((line) => {
                      const [when, does] = line.split(/→|->/).map((s) => s.trim());
                      return { when: when ?? '', does: does ?? '' };
                    })
                    .filter((b) => b.when),
                })
              }
            />
          </Form.Item>
          <Button type="primary" disabled={locked} loading={saving} onClick={() => void saveStep('behavior')}>
            Lưu Behavior
          </Button>
        </Form>
      ) : null}

      {step === 4 ? (
        <Form layout="vertical">
          <Form.Item label="Provider">
            <Input
              disabled={locked}
              value={draft.voiceDna?.provider}
              onChange={(e) => patch({ voiceDna: { ...draft.voiceDna, provider: e.target.value } })}
            />
          </Form.Item>
          <Form.Item label="Voice ID">
            <Input
              disabled={locked}
              value={draft.voiceDna?.voiceId}
              onChange={(e) => patch({ voiceDna: { ...draft.voiceDna, voiceId: e.target.value } })}
            />
          </Form.Item>
          <Form.Item label="Language">
            <Input
              disabled={locked}
              value={draft.voiceDna?.language ?? 'vi'}
              onChange={(e) => patch({ voiceDna: { ...draft.voiceDna, language: e.target.value } })}
            />
          </Form.Item>
          <Form.Item label="Tone / Style">
            <Input
              disabled={locked}
              value={draft.voiceDna?.tone}
              onChange={(e) => patch({ voiceDna: { ...draft.voiceDna, tone: e.target.value } })}
            />
          </Form.Item>
          <Typography.Text type="secondary">Một Character + một Era = một Voice ID Canon. Đổi voice phải xin duyệt.</Typography.Text>
          <div style={{ marginTop: 12 }}>
            <Button type="primary" disabled={locked} loading={saving} onClick={() => void saveStep('voice')}>
              Lưu Voice
            </Button>
          </div>
        </Form>
      ) : null}

      {step === 5 ? (
        <Form layout="vertical">
          {WARDROBE_SETS.map((set) => {
            const outfit = (draft.wardrobe ?? []).find((w) => w.set === set);
            return (
              <Form.Item key={set} label={set}>
                <Input
                  disabled={locked}
                  placeholder={`${set} outfit name`}
                  value={outfit?.label ?? ''}
                  onChange={(e) => {
                    const next: FamixaOutfit[] = (draft.wardrobe ?? []).filter((w) => w.set !== set);
                    if (e.target.value.trim()) {
                      next.push({
                        id: outfit?.id || `OUTFIT-${row.name.toUpperCase()}-${set}-01`,
                        set,
                        label: e.target.value,
                        era: row.currentEra,
                        status: 'DRAFT',
                      });
                    }
                    patch({ wardrobe: next });
                  }}
                />
              </Form.Item>
            );
          })}
          <Button type="primary" disabled={locked} loading={saving} onClick={() => void saveStep('wardrobe')}>
            Lưu Wardrobe
          </Button>
        </Form>
      ) : null}

      {step === 6 ? (
        <Form layout="vertical">
          <Form.Item label="Relationships (chọn Character đã có)">
            <Select
              mode="multiple"
              disabled={locked}
              value={(draft.relationships ?? []).map((r) => r.to)}
              options={others.map((r) => ({ value: r.characterCode, label: `${r.characterCode} · ${r.name}` }))}
              onChange={(codes) =>
                patch({
                  relationships: codes.map((to) => {
                    const hit = relationshipMustUseExisting(registry, to);
                    return { to: hit.ok ? hit.characterCode : to, type: 'related' };
                  }),
                })
              }
            />
          </Form.Item>
          <Typography.Text type="secondary">
            Mẹ = CHAR-003 Linh. Bố = CHAR-002 Nam. CHAR-004 An = bạn (mention-only), không phải em gái.
          </Typography.Text>
          {(draft.plannedFamily ?? []).length ? (
            <div style={{ marginTop: 8 }}>
              {(draft.plannedFamily ?? []).map((f) => (
                <Tag key={f.role}>
                  {f.label} · chưa có ID
                </Tag>
              ))}
            </div>
          ) : null}
          <div style={{ marginTop: 12 }}>
            <Button type="primary" disabled={locked} loading={saving} onClick={() => void saveStep('relationships')}>
              Lưu Relationships
            </Button>
          </div>
        </Form>
      ) : null}

      {step === 7 ? (
        <Space direction="vertical" style={{ width: '100%' }}>
          <Alert
            type="info"
            showIcon
            message={`${row.characterCode} evolution`}
            description="A11 → A16 → A23 là cùng Character ID, Era riêng. Không tạo CHAR-015 Minh 16 tuổi."
          />
          <Space wrap>
            {(draft.evolution?.eras ?? [{ era: row.currentEra, status: 'active' as const }]).map((e) => (
              <Tag key={e.era} color={e.status === 'active' ? 'blue' : undefined}>
                {e.era} · {e.status}
              </Tag>
            ))}
          </Space>
          <Button type="primary" disabled={locked} loading={saving} onClick={() => void saveStep('evolution')}>
            Lưu Evolution
          </Button>
        </Space>
      ) : null}

      {step === 8 ? (
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          {!definitionComplete(record) ? (
            <Alert type="error" showIcon message="Chưa xong Character Definition (01–08). Không tạo Master Reference." />
          ) : null}
          {CORE_MASTER_VIEWS.map((v) => {
            const hit = (draft.references ?? []).find((r) => r.kind.toUpperCase() === v.kind);
            return (
              <div key={v.kind}>
                <Tag color={hit ? 'green' : nextView === v.kind ? 'gold' : undefined}>
                  {v.label}
                  {hit ? ' ✓' : nextView === v.kind ? ' ← next' : ''}
                </Tag>
                {hit ? (
                  <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
                    {hit.path}
                  </Typography.Text>
                ) : null}
              </div>
            );
          })}
          <Typography.Text type="secondary">
            Reference sau phải kế thừa reference trước. Không random mặt mới.
          </Typography.Text>
          {preview ? <img src={preview} alt="preview" style={{ width: 180, borderRadius: 8 }} /> : null}
          <Space wrap>
            <Button
              type="primary"
              disabled={locked || !refGate.ok || !nextView}
              loading={genBusy}
              onClick={() => void generateNextRef()}
            >
              Generate {nextView ?? 'done'}
            </Button>
            <Button
              disabled={locked}
              loading={saving}
              onClick={() => void saveStep('references', { references: draft.references })}
            >
              Lưu references (path only, không dataUrl)
            </Button>
          </Space>
        </Space>
      ) : null}

      {step === 9 ? (
        <Space direction="vertical" style={{ width: '100%' }}>
          <Tag color={qa.status === 'PASS' ? 'green' : qa.status === 'WARNING' ? 'gold' : 'red'}>{qa.status}</Tag>
          {qa.ticks.map((t) => (
            <div key={t.key}>
              {t.ok ? '✓' : '✗'} {t.key}
            </div>
          ))}
          {qa.status === 'FAIL' ? <Alert type="error" showIcon message="FAIL — không LOCK." /> : null}
          {qa.status === 'WARNING' ? <Alert type="warning" showIcon message="WARNING — có thể tiếp tục, phải hiện rõ." /> : null}
          <Button type="primary" loading={saving} onClick={() => void runQa()}>
            Chạy QA
          </Button>
        </Space>
      ) : null}

      {step === 10 || step === 11 ? (
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          <Typography.Title level={5}>CHARACTER FINAL REVIEW</Typography.Title>
          <Typography.Text>
            {row.characterCode} · {row.name.toUpperCase()} · {row.currentEra}
          </Typography.Text>
          {WORKSPACE_STEPS.filter((s) => s.id !== 'review' && s.id !== 'lock').map((s) => (
            <div key={s.id}>
              {s.title.padEnd(16, ' ')} {workspaceProgress(record).steps.includes(s.id) ? '✓' : '·'}
            </div>
          ))}
          <div>QA {qa.status}</div>
          {(draft.references ?? []).find((r) => r.kind.toUpperCase() === 'FRONT') ? (
            <img
              src={(draft.references ?? []).find((r) => r.kind.toUpperCase() === 'FRONT')?.path}
              alt="master"
              style={{ width: 160, borderRadius: 8 }}
            />
          ) : (
            <Tag>Chưa Master FRONT</Tag>
          )}
          <Input.TextArea
            rows={3}
            placeholder="Revision Reason — AI chỉ sửa phần này"
            value={revision}
            onChange={(e) => setRevision(e.target.value)}
          />
          <Space wrap>
            <Button
              disabled={locked || !approveGate.ok}
              loading={busy || saving}
              onClick={() => {
                void saveCanon({
                  ...draft,
                  workspace: { ...draft.workspace, revisionReason: undefined, qa },
                  qa,
                }).then(() =>
                  approveFamixaCharacter(row.characterCode)
                    .then(onChanged)
                    .catch((e) => setError(apiErrorMessage(e, 'Không APPROVE.'))),
                );
              }}
            >
              APPROVE CHARACTER
            </Button>
            <Button
              disabled={locked || !revision.trim()}
              onClick={() =>
                void saveCanon({
                  ...draft,
                  workspace: { ...draft.workspace, revisionReason: revision.trim() },
                })
              }
            >
              REQUEST REVISION
            </Button>
            <Button
              type="primary"
              disabled={!lockGate.ok}
              loading={busy || saving}
              onClick={() =>
                void lockFamixaCharacter(row.characterCode)
                  .then(onChanged)
                  .catch((e) => setError(apiErrorMessage(e, 'Không LOCK.')))
              }
            >
              LOCK CHARACTER
            </Button>
            {locked ? (
              <Button
                onClick={() =>
                  void unlockFamixaCharacter(row.characterCode)
                    .then(onChanged)
                    .catch((e) => setError(apiErrorMessage(e, 'Không UNLOCK.')))
                }
              >
                UNLOCK → NEW VERSION
              </Button>
            ) : (
              <Button
                onClick={() =>
                  void createFamixaCharacterVersion(row.characterCode)
                    .then(onChanged)
                    .catch((e) => setError(apiErrorMessage(e, 'Không tạo version.')))
                }
              >
                CREATE NEW VERSION
              </Button>
            )}
          </Space>
          {locked ? <Alert type="success" showIcon message="This character is now Canon." /> : null}
          {!lockGate.ok && lockGate.blocked ? <Typography.Text type="secondary">{lockGate.blocked}</Typography.Text> : null}
        </Space>
      ) : null}
    </Drawer>
  );
}
