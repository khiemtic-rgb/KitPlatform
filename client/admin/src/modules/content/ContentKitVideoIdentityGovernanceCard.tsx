import { useEffect, useState } from 'react';
import { Alert, Button, Card, Input, Select, Space, Tag, Typography } from 'antd';
import {
  checkCharacterIdentityGovernance,
  fetchCharacterIdentityGovernance,
  fetchCharacterIdentityGovernanceAudit,
  fetchFamixaCharacters,
  type CharacterIdentityGovernanceAuditRow,
  type CharacterIdentityGovernanceRow,
  type FamixaCharacterRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { GOVERNANCE_GATES, IDENTITY_HIERARCHY } from './kit-video-identity-governance';

const gateColor = (v?: string) => {
  const s = (v || '').toUpperCase();
  if (s === 'PASS' || s === 'LOCKED') return 'green';
  if (s === 'BLOCKED' || s === 'FAIL' || s === 'MISSING') return 'red';
  return 'orange';
};

export function ContentKitVideoIdentityGovernanceCard() {
  const [characters, setCharacters] = useState<FamixaCharacterRow[]>([]);
  const [characterId, setCharacterId] = useState<string>();
  const [row, setRow] = useState<CharacterIdentityGovernanceRow>();
  const [audit, setAudit] = useState<CharacterIdentityGovernanceAuditRow[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [prompt, setPrompt] = useState('');
  const [shotSpec, setShotSpec] = useState('');

  const load = (id?: string) => {
    const character = id || characterId;
    if (!character) return;
    setBusy(true);
    void Promise.all([
      fetchCharacterIdentityGovernance(character),
      fetchCharacterIdentityGovernanceAudit(character).catch(() => []),
    ])
      .then(([gov, events]) => {
        setRow(gov);
        setAudit(events);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Identity Governance. Restart API :5290 rồi Tải lại.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    void fetchFamixaCharacters()
      .then((rows) => {
        setCharacters(rows);
        const first = rows.find((r) => r.characterCode.startsWith('CHAR-'))?.characterCode || rows[0]?.characterCode;
        if (first) {
          setCharacterId(first);
          load(first);
        }
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa tải được danh sách Character.')));
  }, []);

  const parseShot = () => {
    const raw = shotSpec.trim();
    if (!raw) return undefined;
    try {
      return JSON.parse(raw) as Record<string, unknown>;
    } catch {
      setError('Shot Specification phải là JSON object.');
      return null;
    }
  };

  const runCheck = () => {
    if (!characterId) return;
    const spec = parseShot();
    if (spec === null) return;
    setBusy(true);
    void checkCharacterIdentityGovernance(characterId, { userPrompt: prompt, shotSpec: spec })
      .then((gov) => {
        setRow(gov);
        setError(undefined);
        return fetchCharacterIdentityGovernanceAudit(characterId);
      })
      .then((events) => setAudit(events))
      .catch((e) => setError(apiErrorMessage(e, 'Check governance thất bại.')))
      .finally(() => setBusy(false));
  };

  const overall = row?.governanceEngine || row?.status || '—';
  const ready = !!row?.productionAllowed && overall === 'PASS';
  const missingAuthority = row?.master === 'MISSING' || row?.dna === 'MISSING' || row?.prp === 'MISSING';
  const selected = characters.find((c) => c.characterCode === characterId);

  return (
    <Card size="small" title="CHARACTER IDENTITY GOVERNANCE — FAMIXA V1.1" style={{ marginBottom: 12 }}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        Hierarchy: {IDENTITY_HIERARCHY.join(' > ')}. C# API là SoT. Engine DETECT / NORMALIZE / EVALUATE / REPORT / BLOCK.
        Không auto-fix. Không generate. {GOVERNANCE_GATES.length} gates.
      </Typography.Paragraph>
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}

      <Space wrap style={{ marginBottom: 8 }}>
        <span>Character</span>
        <Select
          style={{ minWidth: 220 }}
          value={characterId}
          options={characters.map((c) => ({ value: c.characterCode, label: `${c.name} (${c.characterCode})` }))}
          onChange={(v) => {
            setCharacterId(v);
            load(v);
          }}
        />
      </Space>

      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color={gateColor(row?.master)}>MASTER [{row?.master || '—'}]</Tag>
        <Tag color={gateColor(row?.dna)}>DNA [{row?.dna || '—'}]</Tag>
        <Tag color={gateColor(row?.prp)}>PRP [{row?.prp || '—'}]</Tag>
        <Tag color={gateColor(row?.identity)}>IDENTITY [{row?.identity || '—'}]</Tag>
        <Tag color={gateColor(row?.stress)}>STRESS [{row?.stress || '—'}] {row?.stressState ? `· ${row.stressState}` : ''}</Tag>
        <Tag color={gateColor(row?.continuity)}>CONTINUITY [{row?.continuity || '—'}]</Tag>
        <Tag color={gateColor(row?.regression)}>REGRESSION [{row?.regression || '—'}]</Tag>
        <Tag color={gateColor(overall)}>GOVERNANCE ENGINE [{overall}]</Tag>
        <Tag color="orange">DIRECTOR APPROVAL [{row?.directorApproval || 'PENDING'}]</Tag>
        {ready && !missingAuthority ? (
          <Tag>PRODUCTION ALLOWED · GENERATE NO</Tag>
        ) : (
          <Tag color="red">PRODUCTION BLOCKED</Tag>
        )}
        <Tag>GENERATE {row?.generate ? 'YES' : 'NO'}</Tag>
        <Tag color="blue">P0 {row?.p0 ?? '—'}</Tag>
      </Space>

      <div style={{ fontSize: 12, color: '#334155', marginBottom: 8 }}>
        <div>Character: {selected?.name || characterId || '—'}</div>
        <div>Master: {row?.masterSha256 || '—'} · {row?.master || '—'}</div>
        <div>DNA: {row?.dnaSha256 || '—'} · {row?.dna || '—'}</div>
        <div>PRP: {row?.prpSha256 || '—'} · {row?.prp || '—'}</div>
        <div>Prompt contract: {row?.promptContract?.mode || '—'} — {row?.promptContract?.rule}</div>
      </div>

      <Typography.Text strong>GATES</Typography.Text>
      <div style={{ border: '1px solid #e2e8f0', borderRadius: 8, padding: 8, margin: '8px 0', fontSize: 12 }}>
        {(row?.gates || []).map((item) => (
          <div key={item.code} style={{ display: 'flex', gap: 8, alignItems: 'flex-start', marginBottom: 4 }}>
            <Tag color={item.pass ? 'green' : 'red'} style={{ minWidth: 56, textAlign: 'center' }}>
              {item.pass ? 'PASS' : 'FAIL'}
            </Tag>
            <div>
              <div>{item.label}</div>
              {!item.pass && item.reason ? <div style={{ color: '#b91c1c' }}>{item.reason}</div> : null}
            </div>
          </div>
        ))}
      </div>

      <Typography.Text strong>SHOT SPECIFICATION (JSON)</Typography.Text>
      <Input.TextArea
        rows={4}
        value={shotSpec}
        onChange={(e) => setShotSpec(e.target.value)}
        placeholder='{"identity":{"age":"14"},"camera":"low angle"}'
        style={{ margin: '8px 0' }}
      />
      <Typography.Text strong>USER PROMPT</Typography.Text>
      <Input.TextArea
        rows={2}
        value={prompt}
        onChange={(e) => setPrompt(e.target.value)}
        placeholder="Tóc ngang vai — normalize HAIR_LENGTH, không auto-fix"
        style={{ margin: '8px 0' }}
      />
      <Space>
        <Button onClick={() => load()} loading={busy}>
          Tải lại
        </Button>
        <Button type="primary" onClick={runCheck} loading={busy}>
          Check governance
        </Button>
      </Space>

      {(row?.conflicts || []).length ? (
        <div style={{ marginTop: 12 }}>
          <Typography.Text strong>{row?.conflicts?.length} conflicts detected</Typography.Text>
          {(row?.conflicts || []).map((c, i) => (
            <Alert
              key={`${c.code}-${c.attribute}-${i}`}
              type="warning"
              showIcon
              style={{ marginTop: 8 }}
              message={`${c.code} · ${c.source} · ${c.attribute}`}
              description={
                <div>
                  <div>{c.message}</div>
                  <div>REQUESTED: {c.requestedValue || '—'}</div>
                  <div>AUTHORITATIVE: {c.authoritativeValue || '—'}</div>
                </div>
              }
            />
          ))}
        </div>
      ) : null}

      {audit.length ? (
        <div style={{ marginTop: 12, fontSize: 12 }}>
          <Typography.Text strong>AUDIT</Typography.Text>
          {audit.slice(0, 12).map((a) => (
            <div key={a.id} style={{ color: '#475569', marginTop: 4 }}>
              {a.characterId} · {a.gate} · {a.result}
              {a.code ? ` · ${a.code}` : ''} · {a.reason} · {a.actor || '—'} · {a.createdAt}
            </div>
          ))}
        </div>
      ) : null}
    </Card>
  );
}
