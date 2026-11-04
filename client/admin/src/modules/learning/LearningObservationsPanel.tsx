import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Drawer,
  Form,
  Input,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import { EyeOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchPendingLearningObservations,
  submitLearningObservation,
  type LearningObservationPending,
} from '@/shared/api/learning.api';
import {
  OBSERVE_PASS_PCT,
  emptyObserveCriteria,
  getObserveCriteria,
  observePassStats,
} from '@/modules/learning/counter-observe';

const PREVIEW_LIMIT = 5;

type GroupRow = {
  employeeId: string;
  employeeName: string;
  pendingCount: number;
  items: LearningObservationPending[];
};

/** Hàng đợi quan sát tại quầy — gọn, gộp theo NV, không khóa bán. */
export function LearningObservationsPanel() {
  const { message } = App.useApp();
  const [rows, setRows] = useState<LearningObservationPending[]>([]);
  const [loading, setLoading] = useState(false);
  const [showAll, setShowAll] = useState(false);
  const [active, setActive] = useState<LearningObservationPending | null>(null);
  const [criteria, setCriteria] = useState<Record<string, boolean>>({});
  const [note, setNote] = useState('');
  const [saving, setSaving] = useState(false);

  const defs = useMemo(
    () => getObserveCriteria(active?.levelCode),
    [active?.levelCode],
  );
  const stats = useMemo(
    () => observePassStats(criteria, active?.levelCode),
    [criteria, active?.levelCode],
  );

  const groups = useMemo(() => {
    const map = new Map<string, GroupRow>();
    for (const r of rows) {
      const g = map.get(r.employeeId);
      if (g) {
        g.items.push(r);
        g.pendingCount += 1;
      } else {
        map.set(r.employeeId, {
          employeeId: r.employeeId,
          employeeName: r.employeeName,
          pendingCount: 1,
          items: [r],
        });
      }
    }
    return [...map.values()].sort((a, b) => b.pendingCount - a.pendingCount);
  }, [rows]);

  const visibleGroups = showAll ? groups : groups.slice(0, PREVIEW_LIMIT);
  const hiddenCount = Math.max(0, groups.length - PREVIEW_LIMIT);

  const reload = async () => {
    setLoading(true);
    try {
      setRows(await fetchPendingLearningObservations());
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải hàng đợi quan sát'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const open = (row: LearningObservationPending) => {
    setActive(row);
    setCriteria(emptyObserveCriteria(row.levelCode));
    setNote('');
  };

  const markAllCriteria = () => {
    if (!active) return;
    setCriteria(Object.fromEntries(getObserveCriteria(active.levelCode).map((c) => [c.key, true])));
  };

  const onSubmit = async () => {
    if (!active) return;
    setSaving(true);
    try {
      const saved = await submitLearningObservation({
        employeeId: active.employeeId,
        moduleId: active.moduleId,
        criteria,
        note: note.trim() || null,
      });
      if (saved.passed === false) {
        message.info(
          `Đã lưu lần quan sát chưa đạt (${saved.scorePct ?? stats.pct}% / cần ≥${OBSERVE_PASS_PCT}%) — quan sát lại khi nhân viên sẵn sàng.`,
        );
      } else {
        message.success('Đã ghi nhận «Đã áp dụng tại quầy»');
      }
      setActive(null);
      await reload();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không lưu quan sát'));
    } finally {
      setSaving(false);
    }
  };

  const levelHint = (level?: string | null) => {
    switch ((level ?? '').toUpperCase()) {
      case 'L0':
        return 'Xác nhận đăng nhập, màn hình chính, checklist, thông báo, đăng xuất. Cần ≥80%.';
      case 'L1':
        return 'Quan sát phục vụ ≥3 khách nếu có thể. Tick Đạt — cần ≥80%.';
      case 'L2':
        return 'Quan sát tìm khách, điểm thưởng, nhắc thuốc, không ép bán. Cần ≥80%.';
      case 'L3':
        return 'Quan sát xuất hàng gần hết hạn trước, lô/hạn dùng, nhập/kiểm kê, báo hàng lỗi. Cần ≥80%.';
      case 'L4':
        return 'Quan sát đầu–giữa–cuối ca, bàn giao, đăng xuất. Cần ≥80%.';
      case 'L5':
        return 'Quan sát một ca tư vấn: nghe–đề xuất–hướng dẫn–cập nhật khách, không ép bán. Cần ≥80%.';
      case 'L6':
        return 'Quan sát điều phối ca: phân công, hỗ trợ, sự cố, chất lượng, báo cáo. Cần ≥80%.';
      default:
        return 'Tick Đạt khi đã thấy trên ca. Cần ≥80% tiêu chí.';
    }
  };

  return (
    <>
      <Card
        title={
          <Space>
            <EyeOutlined style={{ color: '#1677ff' }} />
            Quan sát tại quầy
            {rows.length > 0 ? <Tag color="blue">{rows.length} chờ</Tag> : null}
          </Space>
        }
        extra={
          <Button size="small" onClick={() => void reload()} loading={loading}>
            Làm mới
          </Button>
        }
        style={{ borderRadius: 12 }}
      >
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          Nhân viên đạt câu hỏi kiểm tra → bạn quan sát trên ca thật. Đạt ≥{OBSERVE_PASS_PCT}% tiêu
          chí → xác nhận. Không khóa bán hàng.
        </Typography.Paragraph>
        {groups.length > PREVIEW_LIMIT && !showAll ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message={`Ưu tiên ${PREVIEW_LIMIT} người cần xem trước (còn ${hiddenCount} người khác).`}
            action={
              <Button size="small" type="link" onClick={() => setShowAll(true)}>
                Xem tất cả
              </Button>
            }
          />
        ) : null}
        {showAll && groups.length > PREVIEW_LIMIT ? (
          <Button size="small" type="link" style={{ paddingLeft: 0, marginBottom: 8 }} onClick={() => setShowAll(false)}>
            Chỉ hiện {PREVIEW_LIMIT} người đầu
          </Button>
        ) : null}
        <Table
          size="small"
          rowKey={(r) => r.employeeId}
          loading={loading}
          dataSource={visibleGroups}
          pagination={false}
          locale={{ emptyText: 'Không còn ai chờ quan sát — tốt!' }}
          expandable={{
            expandedRowRender: (g) => (
              <Space direction="vertical" style={{ width: '100%' }} size={8}>
                {g.items.map((item) => (
                  <div
                    key={`${item.employeeId}-${item.moduleId}`}
                    style={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      gap: 12,
                      flexWrap: 'wrap',
                      alignItems: 'center',
                      padding: '6px 0',
                      borderBottom: '1px solid #f5f5f5',
                    }}
                  >
                    <div>
                      <Typography.Text>
                        {item.levelCode} · {item.moduleTitle}
                      </Typography.Text>
                      <Typography.Text type="secondary" style={{ marginLeft: 8, fontSize: 12 }}>
                        {item.scorePct != null ? `Kiểm tra ${item.scorePct}%` : ''}
                      </Typography.Text>
                    </div>
                    <Button type="primary" size="small" onClick={() => open(item)}>
                      Quan sát
                    </Button>
                  </div>
                ))}
              </Space>
            ),
            defaultExpandAllRows: groups.length <= 3,
          }}
          columns={[
            {
              title: 'Nhân viên',
              dataIndex: 'employeeName',
            },
            {
              title: 'Bài chờ',
              width: 100,
              render: (_, g) => <Tag>{g.pendingCount}</Tag>,
            },
            {
              title: '',
              width: 140,
              render: (_, g) => (
                <Button size="small" onClick={() => open(g.items[0]!)}>
                  Quan sát bài đầu
                </Button>
              ),
            },
          ]}
        />
      </Card>

      <Drawer
        title={active ? `Quan sát: ${active.employeeName}` : 'Quan sát tại quầy'}
        open={!!active}
        onClose={() => setActive(null)}
        width={440}
        destroyOnClose
      >
        {active ? (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Alert type="info" showIcon message={active.moduleTitle} description={levelHint(active.levelCode)} />
            <Form layout="vertical">
              <Form.Item
                label={`Tiêu chí (${stats.passed}/${stats.total} · ${stats.pct}% — cần ≥${OBSERVE_PASS_PCT}%)`}
                extra={
                  <Button type="link" size="small" style={{ padding: 0 }} onClick={markAllCriteria}>
                    Đánh dấu đủ tiêu chí (≥{OBSERVE_PASS_PCT}%)
                  </Button>
                }
              >
                <Space direction="vertical" style={{ width: '100%' }}>
                  {defs.map((c) => (
                    <Checkbox
                      key={c.key}
                      checked={!!criteria[c.key]}
                      onChange={(e) =>
                        setCriteria((prev) => ({ ...prev, [c.key]: e.target.checked }))
                      }
                    >
                      {c.label}
                    </Checkbox>
                  ))}
                </Space>
              </Form.Item>
              <Form.Item label="Nhận xét ngắn (tuỳ chọn)">
                <Input.TextArea
                  rows={3}
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                  placeholder="VD: Chào tốt; cần nhắc xuất hàng gần hết hạn khi đông khách"
                  maxLength={500}
                />
              </Form.Item>
              <Button
                type="primary"
                block
                loading={saving}
                onClick={() => void onSubmit()}
              >
                {stats.ok ? 'Xác nhận đã áp dụng tại quầy' : 'Lưu lần quan sát (chưa đạt)'}
              </Button>
            </Form>
          </Space>
        ) : null}
      </Drawer>
    </>
  );
}
