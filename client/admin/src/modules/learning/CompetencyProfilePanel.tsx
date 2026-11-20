import type { CSSProperties } from 'react';
import { useMemo, useState } from 'react';
import {
  Button,
  Card,
  Col,
  Modal,
  Progress,
  Row,
  Space,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import { AppstoreOutlined, TrophyOutlined } from '@ant-design/icons';
import type { LearningBadge, LearningModuleProgress } from '@/shared/api/learning.api';
import {
  buildCompetencyProfile,
  type ClassifiedBadge,
} from '@/modules/learning/competency-profile';

function BadgeTile({
  b,
  compact,
  onClick,
}: {
  b: ClassifiedBadge;
  compact?: boolean;
  onClick?: () => void;
}) {
  return (
    <Tooltip title={`${b.tierLabel}: ${b.visual.tip}`}>
      <button
        type="button"
        onClick={onClick}
        style={{
          width: '100%',
          textAlign: 'center',
          padding: compact ? '8px 6px' : '12px 8px',
          borderRadius: 12,
          border: `1px solid ${b.visual.color}44`,
          background: `${b.visual.color}12`,
          minHeight: compact ? 72 : 96,
          cursor: onClick ? 'pointer' : 'default',
        }}
      >
        <div style={{ fontSize: compact ? 22 : 28, lineHeight: 1.2 }}>{b.visual.icon}</div>
        <Typography.Text
          strong
          style={{ color: b.visual.color, display: 'block', marginTop: 4, fontSize: compact ? 12 : 13 }}
        >
          {b.title}
        </Typography.Text>
        {!compact ? (
          <Tag
            style={{ marginTop: 4 }}
            color={b.tier === 'achievement' ? 'gold' : b.tier === 'certification' ? 'blue' : 'default'}
          >
            {b.tierLabel}
          </Tag>
        ) : null}
      </button>
    </Tooltip>
  );
}

export type RecognitionBreakdown = {
  customer: number;
  peer: number;
  manager: number;
};

/** Hồ sơ năng lực — chống quá nhiều huy hiệu + xu hướng + tách nguồn ghi nhận. */
export function CompetencyProfilePanel({
  badges,
  modules,
  recognitionCount30d = 0,
  recognitionBreakdown,
  scoreTrend,
  loading,
  compactSummary,
}: {
  badges: LearningBadge[];
  modules: LearningModuleProgress[];
  recognitionCount30d?: number;
  recognitionBreakdown?: RecognitionBreakdown;
  /** Điểm năng lực tháng này so với tháng trước (ước lượng). */
  scoreTrend?: number;
  loading?: boolean;
  compactSummary?: boolean;
}) {
  const [collectionOpen, setCollectionOpen] = useState(false);
  const [detailKey, setDetailKey] = useState<'achievement' | 'certification' | 'all' | null>(null);
  const profile = useMemo(
    () => buildCompetencyProfile(badges, modules, { recognitionCount30d }),
    [badges, modules, recognitionCount30d],
  );
  const { summary } = profile;

  const trendLabel =
    scoreTrend == null || scoreTrend === 0
      ? null
      : scoreTrend > 0
        ? `↑ +${scoreTrend} tháng này`
        : `↓ ${scoreTrend} tháng này`;

  const detailList =
    detailKey === 'achievement'
      ? profile.achievements
      : detailKey === 'certification'
        ? profile.certifications
        : profile.meaningful;

  return (
    <>
      <Card
        loading={loading}
        title={
          <Space>
            <TrophyOutlined style={{ color: '#faad14' }} />
            Hồ sơ năng lực
          </Space>
        }
        extra={
          <Button
            type="default"
            size="small"
            icon={<AppstoreOutlined />}
            onClick={() => setCollectionOpen(true)}
          >
            Bộ sưu tập ({summary.unlockedCount}/{summary.catalogSize})
          </Button>
        }
        size="small"
      >
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))',
            gap: 10,
          }}
        >
          <button type="button" onClick={() => setDetailKey('achievement')} style={tileBtn('#faad14')}>
            <div style={{ fontSize: 18, lineHeight: 1, color: '#faad14' }}>
              <TrophyOutlined />
            </div>
            <Typography.Title level={3} style={{ margin: '6px 0 2px', color: '#faad14', fontSize: 26 }}>
              {summary.achievementCount}
            </Typography.Title>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Thành tựu
            </Typography.Text>
            <Typography.Text
              type="secondary"
              ellipsis
              style={{ fontSize: 11, display: 'block', marginTop: 6, minHeight: 16 }}
            >
              {profile.achievements[0] ? profile.achievements[0].title : ' '}
            </Typography.Text>
          </button>

          <button type="button" onClick={() => setDetailKey('certification')} style={tileBtn('#1677ff')}>
            <div style={{ fontSize: 18, lineHeight: 1, color: '#faad14' }}>★</div>
            <Typography.Title level={3} style={{ margin: '6px 0 2px', color: '#1677ff', fontSize: 26 }}>
              {summary.certificationCount}
            </Typography.Title>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Chứng nhận
            </Typography.Text>
            <Typography.Text
              type="secondary"
              style={{ fontSize: 11, display: 'block', marginTop: 6, minHeight: 16 }}
            >
              {' '}
            </Typography.Text>
          </button>

          <button type="button" onClick={() => setCollectionOpen(true)} style={tileBtn('#722ed1')}>
            <div style={{ fontSize: 18, lineHeight: 1, color: '#1677ff' }}>◆</div>
            <Typography.Title level={3} style={{ margin: '6px 0 2px', color: '#722ed1', fontSize: 26 }}>
              {formatLevelLabel(summary.currentLevel)}
            </Typography.Title>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Bậc hiện tại
            </Typography.Text>
            <Typography.Text
              type="secondary"
              style={{ fontSize: 11, display: 'block', marginTop: 6, minHeight: 16 }}
            >
              {' '}
            </Typography.Text>
          </button>

          <button type="button" onClick={() => setDetailKey('all')} style={tileBtn('#52c41a')}>
            <div style={{ fontSize: 18, lineHeight: 1, color: '#52c41a' }}>↗</div>
            <Typography.Title level={3} style={{ margin: '6px 0 2px', color: '#52c41a', fontSize: 26 }}>
              {summary.competencyScore != null ? `${summary.competencyScore}%` : '—'}
            </Typography.Title>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Điểm năng lực
            </Typography.Text>
            <Typography.Text
              style={{
                fontSize: 11,
                display: 'block',
                marginTop: 6,
                minHeight: 16,
                color: (scoreTrend ?? 0) > 0 ? '#52c41a' : (scoreTrend ?? 0) < 0 ? '#ff4d4f' : undefined,
              }}
              type={(scoreTrend ?? 0) === 0 ? 'secondary' : undefined}
            >
              {trendLabel ?? ' '}
            </Typography.Text>
          </button>

          <div style={{ ...tileBtn('#eb2f96'), cursor: 'default' }}>
            <div style={{ fontSize: 18, lineHeight: 1, color: '#eb2f96' }}>♥</div>
            <Typography.Title level={3} style={{ margin: '6px 0 2px', color: '#eb2f96', fontSize: 26 }}>
              {recognitionBreakdown
                ? recognitionBreakdown.customer +
                  recognitionBreakdown.peer +
                  recognitionBreakdown.manager
                : recognitionCount30d}
            </Typography.Title>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Ghi nhận (30 ngày)
            </Typography.Text>
            <Typography.Text
              type="secondary"
              style={{ fontSize: 11, display: 'block', marginTop: 6, minHeight: 16 }}
            >
              {recognitionBreakdown
                ? `Khách ${recognitionBreakdown.customer} · Đồng nghiệp ${recognitionBreakdown.peer} · Quản lý ${recognitionBreakdown.manager}`
                : ' '}
            </Typography.Text>
          </div>
        </div>

        {!compactSummary ? (
          <Space direction="vertical" size={16} style={{ width: '100%', marginTop: 16 }}>
            <div>
              <Typography.Text strong>Thành tích nổi bật</Typography.Text>
              {profile.featured.length ? (
                <Row gutter={[8, 8]} style={{ marginTop: 8 }}>
                  {profile.featured.map((b) => (
                    <Col xs={12} sm={8} key={b.badgeCode}>
                      <BadgeTile b={b} onClick={() => setDetailKey('achievement')} />
                    </Col>
                  ))}
                </Row>
              ) : (
                <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
                  Chưa có thành tích nổi bật — được ghi nhận hoặc hoàn thành xuất sắc sẽ hiện tại đây.
                </Typography.Paragraph>
              )}
            </div>
            <div>
              <Typography.Text strong>Đang phấn đấu</Typography.Text>
              {profile.striving.length ? (
                <Space direction="vertical" style={{ width: '100%', marginTop: 8 }} size={10}>
                  {profile.striving.map((g) => (
                    <div key={g.key}>
                      <Space style={{ width: '100%', justifyContent: 'space-between' }}>
                        <Typography.Text>{g.title}</Typography.Text>
                        <Typography.Text type="secondary">
                          {g.pct != null ? `${g.pct}%` : 'chưa có dữ liệu'}
                        </Typography.Text>
                      </Space>
                      {g.pct != null ? (
                        <Progress percent={g.pct} strokeColor="#722ed1" showInfo={false} size="small" />
                      ) : null}
                      <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                        {g.hint}
                      </Typography.Text>
                    </div>
                  ))}
                </Space>
              ) : (
                <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
                  Lộ trình đã xong — giữ nhịp ghi nhận & checklist.
                </Typography.Paragraph>
              )}
            </div>
            <Button
              block
              size="large"
              type="default"
              icon={<AppstoreOutlined />}
              onClick={() => setCollectionOpen(true)}
            >
              Mở bộ sưu tập ({summary.unlockedCount}/{summary.catalogSize})
            </Button>
          </Space>
        ) : (
          <Button
            block
            size="large"
            type="default"
            icon={<AppstoreOutlined />}
            style={{ marginTop: 12 }}
            onClick={() => setCollectionOpen(true)}
          >
            Xem bộ sưu tập & chi tiết
          </Button>
        )}
      </Card>

      <Modal
        title={
          detailKey === 'achievement'
            ? `Thành tựu (${profile.achievements.length})`
            : detailKey === 'certification'
              ? `Chứng nhận (${profile.certifications.length})`
              : 'Hồ sơ năng lực'
        }
        open={!!detailKey}
        onCancel={() => setDetailKey(null)}
        footer={
          <Button type="primary" onClick={() => setDetailKey(null)}>
            Đóng
          </Button>
        }
      >
        {detailList.length ? (
          <Row gutter={[8, 8]}>
            {detailList.map((b) => (
              <Col span={12} key={`${b.badgeCode}-${b.earnedAt}`}>
                <BadgeTile b={b} compact />
                <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', textAlign: 'center' }}>
                  {new Date(b.earnedAt).toLocaleDateString('vi-VN')}
                </Typography.Text>
              </Col>
            ))}
          </Row>
        ) : (
          <Typography.Text type="secondary">Chưa có mục trong nhóm này.</Typography.Text>
        )}
      </Modal>

      <Modal
        title={`Bộ sưu tập năng lực · ${summary.unlockedCount}/${summary.catalogSize}`}
        open={collectionOpen}
        onCancel={() => setCollectionOpen(false)}
        footer={
          <Button type="primary" onClick={() => setCollectionOpen(false)}>
            Đóng
          </Button>
        }
        width={720}
      >
        <Typography.Paragraph type="secondary">
          Sự kiện ghi nhận cập nhật hồ sơ và góp phần phát triển nghề. Không trưng bày mã kỹ thuật.
        </Typography.Paragraph>
        {(
          [
            ['Thành tựu', profile.achievements],
            ['Chứng nhận', profile.certifications],
            ['Cột mốc (lộ trình cũ)', profile.milestones],
          ] as const
        ).map(([label, list]) => (
          <div key={label} style={{ marginBottom: 16 }}>
            <Typography.Text strong>
              {label} ({list.length})
            </Typography.Text>
            {list.length ? (
              <Row gutter={[8, 8]} style={{ marginTop: 8 }}>
                {list.map((b) => (
                  <Col xs={12} sm={8} key={`${b.badgeCode}-${b.earnedAt}`}>
                    <BadgeTile b={b} compact />
                  </Col>
                ))}
              </Row>
            ) : (
              <Typography.Paragraph type="secondary">Chưa có</Typography.Paragraph>
            )}
          </div>
        ))}
      </Modal>
    </>
  );
}

function formatLevelLabel(level: string) {
  const m = level?.trim().match(/^L?(\d+)$/i);
  if (m) return `Bậc ${m[1]}`;
  return level || '—';
}

function tileBtn(color: string): CSSProperties {
  return {
    width: '100%',
    height: '100%',
    minHeight: 118,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'flex-start',
    textAlign: 'center',
    padding: '12px 10px',
    borderRadius: 12,
    border: `1px solid ${color}33`,
    background: `${color}10`,
    cursor: 'pointer',
    boxSizing: 'border-box',
  };
}
