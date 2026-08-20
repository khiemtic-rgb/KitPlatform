import { useState } from 'react';
import { Alert, Button, Card, Input, Select, Space, Tag, Typography } from 'antd';
import {
  STORY_BEATS,
  addStoryThread,
  approveEpisodeNarrative,
  inheritanceReview,
  openThreads,
  patchStoryMemory,
  resolveStoryThread,
  storyContinuityWarnings,
  type FamixaCharacterState,
  type FamixaEpisodeNarrative,
  type FamixaRelationshipState,
} from './content-famixa-story-memory';
import { episodeCodeOf, type SeriesPilotState } from './content-famixa-series';

type Props = {
  state: SeriesPilotState;
  onChange: (next: SeriesPilotState) => void;
};

function Field({
  label,
  value,
  onChange,
  rows = 2,
}: {
  label: string;
  value?: string;
  onChange: (v: string) => void;
  rows?: number;
}) {
  return (
    <div style={{ marginBottom: 8 }}>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {label}
      </Typography.Text>
      <Input.TextArea rows={rows} value={value ?? ''} onChange={(e) => onChange(e.target.value)} />
    </div>
  );
}

export function ContentFamixaStoryMemoryCard({ state, onChange }: Props) {
  const mem = state.storyMemory;
  const ep = episodeCodeOf(state.episode?.episode || state.episode?.title) || 'EP01';
  const review = inheritanceReview(state);
  const warnings = storyContinuityWarnings(state);
  const [threadName, setThreadName] = useState('');
  const [threadCause, setThreadCause] = useState('');
  if (!mem) return null;

  const setMem = (next: NonNullable<SeriesPilotState['storyMemory']>) => onChange({ ...state, storyMemory: next });
  const narr = mem.episodeNarrative;
  const patchNarr = (patch: Partial<FamixaEpisodeNarrative>) =>
    setMem({ ...mem, episodeNarrative: { ...(narr ?? { episode: ep, approved: false }), episode: ep, ...patch } });
  const patchChar = (characterId: string, patch: Partial<FamixaCharacterState>) =>
    setMem({
      ...mem,
      characterStates: mem.characterStates.map((s) => (s.characterId === characterId ? { ...s, ...patch } : s)),
    });
  const patchRel = (id: string, patch: Partial<FamixaRelationshipState>) =>
    setMem({
      ...mem,
      relationships: mem.relationships.map((r) => (r.id === id ? { ...r, ...patch } : r)),
    });
  const nameOf = (id: string) => (state.characters ?? []).find((c) => c.id === id)?.name || id;

  return (
    <Card size="small" title="Trạng thái chuyện — Series nhớ, không viết" style={{ marginTop: 12 }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        Tập là bước chuyển trạng thái, không phải bài học khép. KIT nhớ / cảnh báo / kế thừa. Không tự xin lỗi, tha
        thứ, đóng thread, hay reset gia đình.
      </Typography.Paragraph>
      {warnings.map((w) => (
        <Alert key={w} type="warning" showIcon style={{ marginBottom: 8 }} message={w} />
      ))}
      {review.fromEpisode && review.toEpisode && review.fromEpisode !== review.toEpisode ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          message={`${review.toEpisode} kế thừa từ ${review.fromEpisode}`}
          description={
            <div>
              <div>1. Đã xảy ra: {review.whatHappened || '—'}</div>
              <div>2. Đã đổi: {review.whatChanged || '—'}</div>
              <div>3. CHAR: {review.characterChanges || '—'}</div>
              <div>4. Quan hệ: {review.relationshipChanges || '—'}</div>
              <div>5. Conflict OPEN: {review.openConflicts || '—'}</div>
              <div>
                6. Thread OPEN:{' '}
                {review.openThreads.length
                  ? review.openThreads.map((t) => `${t.id} ${t.name}`).join(' · ')
                  : '—'}
              </div>
              <div>7. Hậu quả: {review.consequences || '—'}</div>
              <div>8. {review.inheritLines.join(' · ')}</div>
              {mem.inheritReviewed ? (
                <Tag color="green" style={{ marginTop: 8 }}>
                  Đã duyệt kế thừa
                </Tag>
              ) : (
                <Button
                  type="primary"
                  style={{ marginTop: 8 }}
                  onClick={() => setMem({ ...mem, inheritReviewed: true })}
                >
                  Đúng — {review.toEpisode} kế thừa trạng thái này
                </Button>
              )}
            </div>
          }
        />
      ) : null}

      <Space wrap style={{ marginBottom: 8 }}>
        <Tag>Series → Arc → Season → Arc → {ep} → Scene → Shot</Tag>
        <Tag>{openThreads(mem).length} thread OPEN</Tag>
        {narr?.approved ? <Tag color="green">{ep} đã khóa trạng thái</Tag> : <Tag>{ep} chưa khóa trạng thái</Tag>}
      </Space>

      <Space wrap style={{ marginBottom: 8, width: '100%' }}>
        <div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Series beat
          </Typography.Text>
          <Select
            allowClear
            style={{ minWidth: 160, display: 'block' }}
            value={mem.seriesArc.currentBeat || undefined}
            options={STORY_BEATS.map((b) => ({ value: b, label: b }))}
            onChange={(v) => setMem(patchStoryMemory(state, { seriesArc: { ...mem.seriesArc, currentBeat: v } }).storyMemory!)}
          />
        </div>
        <div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Season beat
          </Typography.Text>
          <Select
            allowClear
            style={{ minWidth: 160, display: 'block' }}
            value={mem.seasonArc.currentBeat || undefined}
            options={STORY_BEATS.map((b) => ({ value: b, label: b }))}
            onChange={(v) => setMem(patchStoryMemory(state, { seasonArc: { ...mem.seasonArc, currentBeat: v } }).storyMemory!)}
          />
        </div>
      </Space>
      <Field
        label="Series Arc"
        value={mem.seriesArc.premise}
        onChange={(v) => setMem({ ...mem, seriesArc: { ...mem.seriesArc, premise: v } })}
      />
      <Field
        label="Season Arc"
        value={mem.seasonArc.premise}
        onChange={(v) => setMem({ ...mem, seasonArc: { ...mem.seasonArc, premise: v } })}
      />

      <Typography.Text strong>Trạng thái {ep}</Typography.Text>
      <Field label="Đã xảy ra" value={narr?.whatHappened} onChange={(v) => patchNarr({ whatHappened: v })} />
      <Field label="Đã đổi" value={narr?.whatChanged} onChange={(v) => patchNarr({ whatChanged: v })} />
      <Field label="CHAR đổi" value={narr?.characterChanges} onChange={(v) => patchNarr({ characterChanges: v })} />
      <Field
        label="Quan hệ đổi"
        value={narr?.relationshipChanges}
        onChange={(v) => patchNarr({ relationshipChanges: v })}
      />
      <Field label="Conflict mới" value={narr?.newConflicts} onChange={(v) => patchNarr({ newConflicts: v })} />
      <Field
        label="Conflict chưa giải"
        value={narr?.unresolvedConflicts}
        onChange={(v) => patchNarr({ unresolvedConflicts: v })}
      />
      <Field label="Hậu quả" value={narr?.consequences} onChange={(v) => patchNarr({ consequences: v })} />
      <Field label="Bí mật lộ" value={narr?.secretsRevealed} onChange={(v) => patchNarr({ secretsRevealed: v })} />
      <Field label="Bí mật còn giấu" value={narr?.secretsHidden} onChange={(v) => patchNarr({ secretsHidden: v })} />
      <Field label="Lời hứa" value={narr?.promises} onChange={(v) => patchNarr({ promises: v })} />
      <Field label="Đồ vật quan trọng" value={narr?.objects} onChange={(v) => patchNarr({ objects: v })} />
      <Field label="Cliff / móc tập sau" value={narr?.hook} onChange={(v) => patchNarr({ hook: v })} />

      <Typography.Text strong>Character State</Typography.Text>
      {mem.characterStates.map((s) => (
        <div key={s.characterId} style={{ marginTop: 8, marginBottom: 8 }}>
          <Typography.Text>
            {s.characterId} {nameOf(s.characterId)}
          </Typography.Text>
          <Space wrap style={{ width: '100%', marginTop: 4 }}>
            <Input
              placeholder="mục tiêu"
              value={s.goal ?? ''}
              onChange={(e) => patchChar(s.characterId, { goal: e.target.value })}
            />
            <Input
              placeholder="nỗi sợ"
              value={s.fear ?? ''}
              onChange={(e) => patchChar(s.characterId, { fear: e.target.value })}
            />
            <Input
              placeholder="niềm tin"
              value={s.belief ?? ''}
              onChange={(e) => patchChar(s.characterId, { belief: e.target.value })}
            />
            <Input
              placeholder="cảm xúc"
              value={s.emotion ?? ''}
              onChange={(e) => patchChar(s.characterId, { emotion: e.target.value })}
            />
            <Input
              placeholder="bí mật / kiến thức"
              value={s.secrets ?? ''}
              onChange={(e) => patchChar(s.characterId, { secrets: e.target.value })}
            />
            <Input
              placeholder="conflict nội"
              value={s.internalConflict ?? ''}
              onChange={(e) => patchChar(s.characterId, { internalConflict: e.target.value })}
            />
          </Space>
        </div>
      ))}

      <Typography.Text strong>Relationship State</Typography.Text>
      {mem.relationships.map((r) => (
        <div key={r.id} style={{ marginTop: 8 }}>
          <Typography.Text>
            {nameOf(r.a)} ↔ {nameOf(r.b)}
          </Typography.Text>
          <Space wrap style={{ width: '100%', marginTop: 4 }}>
            <Input placeholder="tin tưởng" value={r.trust ?? ''} onChange={(e) => patchRel(r.id, { trust: e.target.value })} />
            <Input
              placeholder="giao tiếp"
              value={r.communication ?? ''}
              onChange={(e) => patchRel(r.id, { communication: e.target.value })}
            />
            <Input
              placeholder="conflict"
              value={r.conflict ?? ''}
              onChange={(e) => patchRel(r.id, { conflict: e.target.value })}
            />
            <Input
              placeholder="khoảng cách"
              value={r.distance ?? ''}
              onChange={(e) => patchRel(r.id, { distance: e.target.value })}
            />
            <Input
              placeholder="chưa giải"
              value={r.unresolved ?? ''}
              onChange={(e) => patchRel(r.id, { unresolved: e.target.value })}
            />
          </Space>
        </div>
      ))}

      <Typography.Text strong style={{ display: 'block', marginTop: 12 }}>
        Thread còn mở
      </Typography.Text>
      {mem.threads.map((t) => (
        <div key={t.id} style={{ marginTop: 6 }}>
          <Tag color={t.status === 'OPEN' ? 'gold' : 'green'}>
            {t.id} · {t.status}
          </Tag>
          <Typography.Text>
            {t.name}
            {t.createdEpisode ? ` · tạo ${t.createdEpisode}` : ''}
            {t.resolvedEpisode ? ` · đóng ${t.resolvedEpisode}` : ''}
          </Typography.Text>
          {t.status === 'OPEN' ? (
            <Button size="small" style={{ marginLeft: 8 }} onClick={() => setMem(resolveStoryThread(mem, t.id, ep))}>
              Story Director đóng
            </Button>
          ) : null}
        </div>
      ))}
      <Space wrap style={{ marginTop: 8 }}>
        <Input
          placeholder="Tên thread OPEN"
          value={threadName}
          onChange={(e) => setThreadName(e.target.value)}
          style={{ minWidth: 220 }}
        />
        <Input placeholder="Nguyên nhân" value={threadCause} onChange={(e) => setThreadCause(e.target.value)} />
        <Button
          onClick={() => {
            setMem(addStoryThread(mem, { name: threadName, createdEpisode: ep, cause: threadCause }));
            setThreadName('');
            setThreadCause('');
          }}
        >
          Thêm thread OPEN
        </Button>
      </Space>

      <Space wrap style={{ marginTop: 12 }}>
        <Button
          type="primary"
          disabled={narr?.approved}
          onClick={() => setMem(approveEpisodeNarrative(mem, state.episode))}
        >
          Khóa trạng thái {ep}
        </Button>
        <Typography.Text type="secondary">
          Không tự đóng thread. Tập sau nhận state này khi Nhận pack EP mới.
        </Typography.Text>
      </Space>
    </Card>
  );
}
