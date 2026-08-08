import { Typography } from 'antd';
import type { OwnerPack } from '@/shared/api/assessment.api';

const { Title, Paragraph, Text } = Typography;

export function OwnerPackPanel({ pack }: { pack: OwnerPack }) {
  return (
    <div className="score-card owner-pack" style={{ marginBottom: '1.25rem' }}>
      <Text type="secondary" style={{ textTransform: 'uppercase', letterSpacing: '0.04em', fontSize: 12 }}>
        Dành cho chủ nhà thuốc
      </Text>
      <Title level={4} style={{ marginTop: 8, marginBottom: 4 }}>
        Kết quả tóm tắt
      </Title>
      <Paragraph style={{ marginBottom: 8 }}>
        <strong>
          {Math.round(pack.overallScorePct)}/100 · {pack.maturityLabel}
        </strong>
      </Paragraph>
      <Paragraph type="secondary">{pack.overallHeadline}</Paragraph>

      <Title level={5}>Đang làm tốt</Title>
      <ul style={{ paddingLeft: '1.1rem', marginTop: 0 }}>
        {pack.strengths.slice(0, 3).map((s) => (
          <li key={s.title} style={{ marginBottom: 8 }}>
            <strong>{s.title}</strong>
            <div style={{ color: 'rgba(0,0,0,.55)', fontSize: 14 }}>{s.body}</div>
          </li>
        ))}
      </ul>

      <Title level={5}>Nỗi đau / cơ hội lớn</Title>
      <ol style={{ paddingLeft: '1.1rem', marginTop: 0 }}>
        {pack.pains.slice(0, 3).map((p) => (
          <li key={p.title} style={{ marginBottom: 10 }}>
            <strong>{p.title}</strong>
            <div style={{ color: 'rgba(0,0,0,.65)', fontSize: 14 }}>→ {p.businessConsequence}</div>
          </li>
        ))}
      </ol>

      <div
        style={{
          background: 'rgba(255, 152, 0, 0.12)',
          borderRadius: 8,
          padding: '12px 14px',
          marginBottom: 12,
        }}
      >
        <Text strong>Nên làm trước (30 ngày)</Text>
        <Paragraph style={{ marginBottom: 0, marginTop: 6 }}>{pack.oneThingFirst}</Paragraph>
      </div>

      {pack.actions30Days.length > 0 ? (
        <>
          <Title level={5}>Việc làm trong 30 ngày</Title>
          {pack.actions30Days.map((a) => (
            <div
              key={a.title}
              style={{
                border: '1px solid #e8e8e8',
                borderRadius: 8,
                padding: 12,
                marginBottom: 8,
              }}
            >
              <Text strong>{a.title}</Text>
              <div style={{ fontSize: 13, marginTop: 4, color: 'rgba(0,0,0,.55)' }}>
                Ai: {a.who} · Khi: {a.when}
                <br />
                Xong khi: {a.doneWhen}
              </div>
            </div>
          ))}
        </>
      ) : null}

      <Title level={5}>Nếu muốn có người đồng hành</Title>
      <Paragraph>{pack.pilotHinge.howToTalk}</Paragraph>
      <Paragraph>
        <strong>Gợi ý Pilot:</strong> {pack.pilotHinge.recommendedFocus}
      </Paragraph>
      <Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {pack.nextStepCta}
      </Paragraph>
    </div>
  );
}
