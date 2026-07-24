import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  App,
  Button,
  Drawer,
  Dropdown,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Table,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { MenuProps } from 'antd';
import {
  EditOutlined,
  EyeInvisibleOutlined,
  GiftOutlined,
  HistoryOutlined,
  MoreOutlined,
  PlusOutlined,
  ReloadOutlined,
  StarFilled,
  TeamOutlined,
} from '@ant-design/icons';
import { Link } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  createRewardCatalogItem,
  deactivateRewardCatalogItem,
  fetchFamilies,
  fetchRewardCatalog,
  fetchTeamUnlocks,
  updateRewardCatalogItem,
  type FamilySummary,
  type RewardCatalogItem,
  type TeamUnlock,
} from '@/shared/api/family-os.api';
import './family-os-routines.css';

const TONE_OPTIONS = [
  { value: 'pink', label: 'Pink' },
  { value: 'lemon', label: 'Lemon' },
  { value: 'sky', label: 'Sky' },
  { value: 'mint', label: 'Mint' },
  { value: 'lilac', label: 'Lilac' },
];

const TEAM_UNLOCK_DESCRIPTION: Record<string, string> = {
  'Bố mẹ chọn': 'Cả đội hoàn thành hết Mission trong tuần',
};

type RewardFormValues = {
  title: string;
  icon: string;
  description?: string;
  cost: number;
  tone?: string;
  sortOrder: number;
};

function toneLabel(tone?: string): string {
  return TONE_OPTIONS.find((o) => o.value === tone)?.label ?? tone ?? '—';
}

function teamUnlockStatusLabel(status: string): string {
  if (status === 'pending_confirm') return 'Chờ xác nhận';
  if (status === 'confirmed') return 'Đã xác nhận';
  if (status === 'deferred') return 'Hoãn';
  return status || '—';
}

function teamUnlockHistoryStatus(status: string): { label: string; tone: string } {
  if (status === 'confirmed') return { label: 'Đã xác nhận', tone: 'is-green' };
  if (status === 'deferred') return { label: 'Hoãn', tone: 'is-off' };
  return { label: teamUnlockStatusLabel(status), tone: 'is-warm' };
}

function rewardSubtitle(item: RewardCatalogItem): string {
  if (item.description?.trim()) return item.description.trim();
  if (item.isSpecial) return 'Phần thưởng do bố mẹ lựa chọn cho cả gia đình';
  return 'Phần thưởng đổi bằng sao trên app';
}

function teamUnlockDescription(item: RewardCatalogItem): string {
  if (item.description?.trim()) return item.description.trim();
  return TEAM_UNLOCK_DESCRIPTION[item.title] ?? 'Khi cả đội hoàn thành Mission';
}

function familyPageSubtitle(displayName: string, suffix: string): string {
  return `${displayName.trim()} — ${suffix}`;
}

export function FamilyOsRewardsPage() {
  const { modal, message: toast } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [family, setFamily] = useState<FamilySummary | null>(null);
  const [catalog, setCatalog] = useState<RewardCatalogItem[]>([]);
  const [teamUnlocks, setTeamUnlocks] = useState<TeamUnlock[]>([]);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<RewardCatalogItem | null>(null);
  const [form] = Form.useForm<RewardFormValues>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const families = await fetchFamilies();
      const first = families[0] ?? null;
      setFamily(first);
      if (!first) {
        setCatalog([]);
        setTeamUnlocks([]);
        return;
      }
      const catalogRows = await fetchRewardCatalog(first.id);
      setCatalog(catalogRows);

      try {
        const unlockRows = await fetchTeamUnlocks(first.id);
        setTeamUnlocks(unlockRows);
      } catch (unlockError) {
        setTeamUnlocks([]);
        message.warning(
          apiErrorMessage(unlockError, 'Không tải được lịch sử Team Unlock — catalog vẫn hiển thị'),
        );
      }
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được kho thưởng'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const teamRewards = useMemo(() => catalog.filter((c) => c.isSpecial), [catalog]);
  const starRewards = useMemo(
    () => catalog.filter((c) => !c.isSpecial).sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0)),
    [catalog],
  );

  const openCreate = () => {
    setEditing(null);
    const nextOrder = starRewards.reduce((max, row) => Math.max(max, row.sortOrder ?? 0), 0) + 1;
    form.setFieldsValue({
      title: '',
      icon: '🎁',
      description: '',
      cost: 100,
      tone: 'pink',
      sortOrder: nextOrder,
    });
    setEditorOpen(true);
  };

  const openEdit = (item: RewardCatalogItem) => {
    setEditing(item);
    form.setFieldsValue({
      title: item.title,
      icon: item.icon,
      description: item.description ?? '',
      cost: item.cost ?? 100,
      tone: item.tone ?? 'pink',
      sortOrder: item.sortOrder ?? 0,
    });
    setEditorOpen(true);
  };

  const saveReward = async () => {
    if (!family) return;
    const values = await form.validateFields();
    setSaving(true);
    try {
      const payload = {
        title: values.title.trim(),
        icon: values.icon.trim() || '🎁',
        description: values.description?.trim() || undefined,
        cost: values.cost,
        tone: values.tone,
        sortOrder: values.sortOrder,
      };
      if (editing) {
        await updateRewardCatalogItem(family.id, editing.id, payload);
        message.success('Đã cập nhật phần thưởng');
      } else {
        await createRewardCatalogItem(family.id, payload);
        message.success('Đã thêm phần thưởng');
      }
      setEditorOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được phần thưởng'));
    } finally {
      setSaving(false);
    }
  };

  const confirmHideReward = (item: RewardCatalogItem) => {
    if (!family) return;
    // Dropdown đóng trước khi mở confirm — defer để modal không bị nuốt click/focus.
    window.setTimeout(() => {
      modal.confirm({
        title: 'Ẩn phần thưởng?',
        content: `Phần thưởng "${item.title}" sẽ không hiển thị trên app. Có thể thêm lại sau.`,
        okText: 'Ẩn',
        cancelText: 'Huỷ',
        okButtonProps: { danger: true },
        onOk: async () => {
          try {
            await deactivateRewardCatalogItem(family.id, item.id);
          } catch (error) {
            toast.error(apiErrorMessage(error, 'Không ẩn được phần thưởng'));
            throw error;
          }
          setCatalog((prev) => prev.filter((c) => c.id !== item.id));
          toast.success('Đã ẩn phần thưởng');
          try {
            await load();
          } catch (error) {
            toast.warning(
              apiErrorMessage(error, 'Đã ẩn nhưng không tải lại được catalog — bấm Làm mới'),
            );
          }
        },
      });
    }, 0);
  };

  const starMenu = (item: RewardCatalogItem): MenuProps['items'] => [
    {
      key: 'edit',
      icon: <EditOutlined />,
      label: 'Sửa',
      onClick: () => openEdit(item),
    },
    {
      key: 'hide',
      icon: <EyeInvisibleOutlined />,
      label: 'Ẩn',
      danger: true,
      onClick: ({ domEvent }) => {
        domEvent.stopPropagation();
        confirmHideReward(item);
      },
    },
  ];

  const teamColumns: ColumnsType<RewardCatalogItem> = [
    {
      title: 'Phần thưởng',
      render: (_, row) => (
        <div className="fr-commit">
          <span className="fr-commit-icon" aria-hidden>
            {row.icon}
          </span>
          <div>
            <strong>{row.title}</strong>
            <span>{rewardSubtitle(row)}</span>
          </div>
        </div>
      ),
    },
    {
      title: 'Mã (Code)',
      dataIndex: 'id',
      width: 240,
      render: (id: string) => <span className="fr-code">{id}</span>,
    },
    {
      title: 'Mô tả',
      render: (_, row) => teamUnlockDescription(row),
    },
    {
      title: 'Thứ tự',
      dataIndex: 'sortOrder',
      width: 90,
      render: (v: number) => <span className="fr-order-badge">{v}</span>,
    },
    {
      title: 'Trạng thái',
      width: 120,
      render: () => <span className="fr-active is-green">Đang dùng</span>,
    },
    {
      title: '',
      width: 48,
      render: (_, row) => (
        <Dropdown
          menu={{
            items: [
              {
                key: 'agreements',
                label: <Link to="/family-os/agreements">Mở Thỏa thuận nhà</Link>,
              },
              {
                key: 'code',
                label: `Mã: ${row.id.slice(0, 8)}…`,
                disabled: true,
              },
            ],
          }}
          trigger={['click']}
        >
          <button type="button" className="fr-icon-btn" aria-label="Tuỳ chọn">
            <MoreOutlined />
          </button>
        </Dropdown>
      ),
    },
  ];

  const historyColumns: ColumnsType<TeamUnlock> = [
    {
      title: 'Ngày',
      dataIndex: 'flowDate',
      width: 110,
    },
    {
      title: 'Phần thưởng',
      render: (_, row) => (
        <span>
          {row.labelVi || row.rewardCode}
        </span>
      ),
    },
    {
      title: 'Tiến độ',
      width: 120,
      render: (_, row) => `${row.teamDone}/${row.teamTotal} (${row.teamPercent}%)`,
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 130,
      render: (status: string) => {
        const meta = teamUnlockHistoryStatus(status);
        return <span className={`fr-active ${meta.tone}`}>{meta.label}</span>;
      },
    },
  ];

  return (
    <div className={`fr-page${loading ? ' is-loading' : ''}`}>
      <header className="fr-header">
        <div className="fr-header-brand">
          <span className="fr-header-ico" aria-hidden>
            <GiftOutlined />
          </span>
          <div>
            <h1>Kho thưởng</h1>
            <p>
              {family
                ? familyPageSubtitle(
                    family.displayName,
                    'Catalog đồng bộ với app Mẹ & Con (cùng API, không job riêng)',
                  )
                : 'Chưa có gia đình'}
            </p>
          </div>
        </div>
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          Làm mới
        </Button>
      </header>

      <section className="fr-banner">
        <div className="fr-banner-copy">
          <span className="fr-banner-i" aria-hidden>
            i
          </span>
          <div>
            <strong>Admin có thể Thêm / Sửa / Ẩn phần thưởng đổi sao.</strong>
            <p>
              Thay đổi hiển thị ngay trên Kho báu (app con) và bảng Mẹ — cùng bảng{' '}
              <code>reward_catalog</code>, không đồng bộ riêng. Lần đầu mở hoặc bấm Làm mới, API tự
              seed catalog mặc định nếu nhà chưa có. Thưởng đội (Team Unlock) lấy mục{' '}
              <em>Bố mẹ chọn</em> trong catalog; gắn với{' '}
              <Link to="/family-os/agreements">Thỏa thuận nhà</Link>.
            </p>
          </div>
        </div>
        <span className="fr-banner-treasure" aria-hidden>
          🎁✨
        </span>
      </section>

      {!family ? (
        <section className="fr-card">
          <Typography.Text type="secondary">
            Chưa có gia đình — thêm thành viên hoặc seed trước.
          </Typography.Text>
        </section>
      ) : (
        <>
          <section className="fr-card">
            <div className="fr-card-head">
              <div className="fr-card-title">
                <span className="fr-section-ico is-purple" aria-hidden>
                  <TeamOutlined />
                </span>
                <div>
                  <h2>Thưởng đội (Team Unlock)</h2>
                  <p className="fr-card-meta">
                    Hiển thị trên hero nhà khi cả đội hoàn thành Mission
                  </p>
                </div>
              </div>
              <div className="fr-card-actions">
                <Button icon={<HistoryOutlined />} onClick={() => setHistoryOpen(true)}>
                  Xem lịch sử
                </Button>
              </div>
            </div>
            <Table
              className="fr-table"
              size="middle"
              rowKey="id"
              pagination={false}
              loading={loading}
              dataSource={teamRewards}
              locale={{
                emptyText:
                  'Chưa có thưởng đội — bấm Làm mới để seed catalog mặc định, hoặc kiểm tra Thỏa thuận nhà.',
              }}
              columns={teamColumns}
            />
          </section>

          <section className="fr-card">
            <div className="fr-card-head">
              <div className="fr-card-title">
                <span className="fr-section-ico is-gold" aria-hidden>
                  <StarFilled />
                </span>
                <div>
                  <h2>Đổi sao (reward_catalog)</h2>
                  <p className="fr-card-meta">Danh sách phần thưởng để con đổi bằng sao</p>
                </div>
              </div>
              <div className="fr-card-actions">
                <Button
                  type="primary"
                  className="fr-btn-purple"
                  icon={<PlusOutlined />}
                  onClick={openCreate}
                >
                  Thêm phần thưởng
                </Button>
              </div>
            </div>

            {starRewards.length > 0 ? (
              <>
                <div className="fr-reward-labels" aria-hidden>
                  <span>Phần thưởng</span>
                  <span>Giá sao</span>
                  <span>Tone</span>
                  <span>Thứ tự</span>
                  <span>Trạng thái</span>
                </div>
                <div className="fr-reward-grid">
                  {starRewards.map((item) => {
                    const tone = item.tone ?? 'lilac';
                    return (
                      <article key={item.id} className="fr-reward-card">
                        <div className={`fr-reward-card-top tone-${tone}`}>
                          <span className="fr-reward-card-art" aria-hidden>
                            {item.icon}
                          </span>
                          <div className="fr-reward-card-copy">
                            <strong>{item.title}</strong>
                            <span>{rewardSubtitle(item)}</span>
                            <em>
                              <StarFilled /> {item.cost ?? 0}
                            </em>
                          </div>
                        </div>
                        <div className="fr-reward-card-foot">
                          <div className="fr-reward-card-meta">
                            <span className="fr-tone-dot">
                              <i className={`tone-${tone}`} aria-hidden />
                              {toneLabel(tone)}
                            </span>
                            <span className="fr-order-badge">{item.sortOrder ?? 0}</span>
                            <span className="fr-active is-green">Đang dùng</span>
                          </div>
                          <Dropdown menu={{ items: starMenu(item) }} trigger={['click']}>
                            <button type="button" className="fr-icon-btn" aria-label="Tuỳ chọn">
                              <MoreOutlined />
                            </button>
                          </Dropdown>
                        </div>
                      </article>
                    );
                  })}
                </div>
              </>
            ) : (
              <p className="fr-empty">
                Chưa có phần thưởng đổi sao — bấm Làm mới để seed mặc định, hoặc &quot;Thêm phần
                thưởng&quot;.
              </p>
            )}

            <p className="fr-foot-note">
              Thứ tự càng nhỏ ưu tiên hiển thị càng cao trên app. Tone dùng để phân biệt màu sắc
              hiển thị trên app.
            </p>
          </section>
        </>
      )}

      <Drawer
        title="Lịch sử Team Unlock"
        width={720}
        open={historyOpen}
        onClose={() => setHistoryOpen(false)}
      >
        <Table
          className="fr-table"
          size="middle"
          rowKey="id"
          pagination={{ pageSize: 12 }}
          loading={loading}
          dataSource={teamUnlocks}
          locale={{ emptyText: 'Chưa có lịch sử Team Unlock.' }}
          columns={historyColumns}
        />
      </Drawer>

      <Modal
        title={editing ? 'Sửa phần thưởng' : 'Thêm phần thưởng'}
        open={editorOpen}
        onCancel={() => setEditorOpen(false)}
        onOk={() => void saveReward()}
        confirmLoading={saving}
        okText={editing ? 'Lưu' : 'Thêm'}
        cancelText="Huỷ"
        destroyOnClose
      >
        <Form form={form} layout="vertical" style={{ marginTop: 8 }}>
          <Form.Item
            name="title"
            label="Tên phần thưởng"
            rules={[{ required: true, message: 'Nhập tên phần thưởng' }]}
          >
            <Input placeholder="VD: Kem yêu thích" maxLength={160} />
          </Form.Item>
          <Form.Item name="icon" label="Biểu tượng (emoji)">
            <Input placeholder="🎁" maxLength={8} />
          </Form.Item>
          <Form.Item name="description" label="Mô tả ngắn">
            <Input.TextArea rows={2} placeholder="VD: 1 ly kem tùy chọn" maxLength={280} />
          </Form.Item>
          <Form.Item
            name="cost"
            label="Giá sao"
            rules={[{ required: true, message: 'Nhập giá sao' }]}
          >
            <InputNumber min={1} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="tone" label="Tone (màu app)">
            <Select options={TONE_OPTIONS} />
          </Form.Item>
          <Form.Item
            name="sortOrder"
            label="Thứ tự"
            rules={[{ required: true, message: 'Nhập thứ tự' }]}
          >
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
