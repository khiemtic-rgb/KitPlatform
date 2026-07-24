import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Button,
  DatePicker,
  Drawer,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Table,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  DeleteOutlined,
  EditOutlined,
  HomeOutlined,
  PlusOutlined,
  ReloadOutlined,
  TeamOutlined,
  UserAddOutlined,
  UserOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  addFamilyMember,
  fetchFamilies,
  updateFamily,
  updateFamilyMember,
  type FamilyMembership,
  type FamilySummary,
} from '@/shared/api/family-os.api';
import './family-os-members.css';

const ROLE_OPTIONS = [
  { value: 'guardian', label: 'Cha/mẹ (guardian)' },
  { value: 'caregiver', label: 'Người chăm (caregiver)' },
  { value: 'child', label: 'Con (child)' },
  { value: 'viewer', label: 'Xem (viewer)' },
];

const STATUS_OPTIONS = [
  { value: 'active', label: 'Đang hoạt động' },
  { value: 'invited', label: 'Đã mời' },
  { value: 'archived', label: 'Đã lưu trữ' },
];

const GIRL_TOKENS = [
  'nhi', 'linh', 'vy', 'my', 'mai', 'lan', 'ha', 'an', 'anh', 'chau', 'chi', 'diem', 'dung',
  'hanh', 'hien', 'hoa', 'huong', 'khanh', 'lien', 'loan', 'ly', 'ngoc', 'oanh', 'phuong',
  'quynh', 'thao', 'thu', 'thuy', 'tram', 'trang', 'tuyen', 'uyen', 'van', 'xuan', 'yen', 'me',
];
const BOY_TOKENS = [
  'huy', 'nam', 'minh', 'duc', 'khoa', 'khang', 'bao', 'dat', 'tuan', 'hung', 'long', 'phong',
  'quan', 'son', 'tai', 'thanh', 'thien', 'trung', 'tung', 'viet', 'vu', 'bo', 'ba', 'ong',
];

function stripDiacritics(s: string) {
  return s.normalize('NFD').replace(/[\u0300-\u036f]/g, '');
}

function inferGender(name: string): 'girl' | 'boy' | 'neutral' {
  const parts = stripDiacritics(name).toLowerCase().trim().split(/[\s._-]+/).filter(Boolean);
  const given = parts[parts.length - 1] ?? '';
  const all = [...new Set([given, ...parts])];
  if (all.some((t) => GIRL_TOKENS.includes(t))) return 'girl';
  if (all.some((t) => BOY_TOKENS.includes(t))) return 'boy';
  if (/(nhi|linh|vy|my)$/.test(given)) return 'girl';
  return 'neutral';
}

function memberAvatar(name: string, roleCode: string): { emoji: string; cls: string } {
  const gender = inferGender(name);
  if (roleCode !== 'child') {
    if (/^m[eẹ]$/i.test(name.trim()) || gender === 'girl')
      return { emoji: '👩', cls: 'is-adult' };
    if (/^b[oố]$/i.test(name.trim()) || gender === 'boy')
      return { emoji: '👨', cls: 'is-adult' };
    return { emoji: '🧑', cls: 'is-adult' };
  }
  if (gender === 'girl') return { emoji: '👧', cls: 'is-girl' };
  if (gender === 'boy') return { emoji: '👦', cls: 'is-boy' };
  return { emoji: '🧒', cls: 'is-neutral' };
}

function roleShort(role: string) {
  switch (role) {
    case 'guardian':
      return 'Phụ huynh';
    case 'caregiver':
      return 'Người chăm';
    case 'child':
      return 'Con';
    case 'viewer':
      return 'Xem';
    default:
      return role;
  }
}

function roleBadge(role: string) {
  const label = ROLE_OPTIONS.find((r) => r.value === role)?.label ?? role;
  const cls =
    role === 'child' ? 'is-child' : role === 'guardian' || role === 'caregiver' ? 'is-adult' : 'is-other';
  return <span className={`fm-role ${cls}`}>{label}</span>;
}

function statusLabel(status: string) {
  if (status === 'active') return 'Đang hoạt động';
  return STATUS_OPTIONS.find((s) => s.value === status)?.label ?? status;
}

export function FamilyOsMembersPage() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [family, setFamily] = useState<FamilySummary | null>(null);
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<FamilyMembership | null>(null);
  const [familyOpen, setFamilyOpen] = useState(false);
  const [form] = Form.useForm();
  const [familyForm] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const families = await fetchFamilies();
      setFamily(families[0] ?? null);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được thành viên'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const members = useMemo(
    () => (family?.members ?? []).filter((m) => m.status !== 'archived'),
    [family],
  );

  const stats = useMemo(() => {
    const adults = members.filter((m) => m.roleCode !== 'child').length;
    const children = members.filter((m) => m.roleCode === 'child').length;
    return { total: members.length, adults, children };
  }, [members]);

  const openEditFamily = () => {
    if (!family) return;
    familyForm.setFieldsValue({
      displayName: family.displayName,
      timezone: family.timezone || 'Asia/Ho_Chi_Minh',
    });
    setFamilyOpen(true);
  };

  const saveFamily = async () => {
    if (!family) return;
    const values = await familyForm.validateFields();
    setSaving(true);
    try {
      const updated = await updateFamily(family.id, {
        displayName: values.displayName,
        timezone: values.timezone,
      });
      setFamily(updated);
      message.success('Đã đổi tên gia đình');
      setFamilyOpen(false);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không đổi được tên gia đình'));
    } finally {
      setSaving(false);
    }
  };

  const openCreate = () => {
    setEditing(null);
    form.setFieldsValue({
      displayName: '',
      roleCode: 'child',
      dateOfBirth: null,
      sortOrder: (family?.members.length ?? 0) + 1,
      status: 'active',
    });
    setOpen(true);
  };

  const openEdit = (row: FamilyMembership) => {
    setEditing(row);
    form.setFieldsValue({
      displayName: row.displayName,
      roleCode: row.roleCode,
      dateOfBirth: row.dateOfBirth ? dayjs(row.dateOfBirth) : null,
      sortOrder: row.sortOrder ?? 0,
      status: row.status || 'active',
    });
    setOpen(true);
  };

  const archiveMember = (row: FamilyMembership) => {
    if (!family) return;
    Modal.confirm({
      title: `Gỡ ${row.displayName} khỏi nhà?`,
      content: 'Thành viên sẽ được lưu trữ — có thể khôi phục bằng cách sửa trạng thái sau.',
      okText: 'Gỡ khỏi nhà',
      okButtonProps: { danger: true },
      cancelText: 'Giữ lại',
      onOk: async () => {
        try {
          await updateFamilyMember(family.id, row.id, { status: 'archived' });
          message.success('Đã gỡ thành viên khỏi nhà');
          await load();
        } catch (error) {
          message.error(apiErrorMessage(error, 'Không gỡ được thành viên'));
        }
      },
    });
  };

  const save = async () => {
    if (!family) return;
    const values = await form.validateFields();
    setSaving(true);
    try {
      const dob =
        values.dateOfBirth != null ? dayjs(values.dateOfBirth).format('YYYY-MM-DD') : null;
      if (editing) {
        await updateFamilyMember(family.id, editing.id, {
          displayName: values.displayName,
          roleCode: values.roleCode,
          dateOfBirth: dob,
          clearDateOfBirth: dob == null,
          sortOrder: values.sortOrder,
          status: values.status,
        });
        message.success('Đã cập nhật thành viên');
      } else {
        await addFamilyMember(family.id, {
          displayName: values.displayName,
          roleCode: values.roleCode,
          dateOfBirth: dob,
          sortOrder: values.sortOrder,
        });
        message.success('Đã thêm thành viên');
      }
      setOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được thành viên'));
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<FamilyMembership> = [
    {
      title: 'Thành viên',
      dataIndex: 'displayName',
      render: (name: string, row) => {
        const av = memberAvatar(name, row.roleCode);
        return (
          <div className="fm-person">
            <div className={`fm-avatar ${av.cls}`} aria-hidden>
              {av.emoji}
            </div>
            <div>
              <strong>{name}</strong>
              <span>{roleShort(row.roleCode)}</span>
            </div>
          </div>
        );
      },
    },
    {
      title: 'Vai trò',
      dataIndex: 'roleCode',
      width: 180,
      render: (role: string) => roleBadge(role),
    },
    {
      title: 'Ngày sinh',
      dataIndex: 'dateOfBirth',
      width: 130,
      render: (v?: string) => (v ? String(v).slice(0, 10) : '—'),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 150,
      render: (status: string) => (
        <span className={`fm-status ${status === 'active' ? 'is-active' : ''}`}>
          <i />
          {statusLabel(status)}
        </span>
      ),
    },
    {
      title: 'Thao tác',
      key: 'actions',
      width: 110,
      align: 'right',
      render: (_, row) => (
        <Space size={4}>
          <Button
            type="text"
            className="fm-icon-btn"
            icon={<EditOutlined />}
            aria-label={`Sửa ${row.displayName}`}
            onClick={() => openEdit(row)}
          />
          <Button
            type="text"
            danger
            className="fm-icon-btn"
            icon={<DeleteOutlined />}
            aria-label={`Gỡ ${row.displayName}`}
            onClick={() => archiveMember(row)}
          />
        </Space>
      ),
    },
  ];

  return (
    <div className={`fm-page${loading ? ' is-loading' : ''}`}>
      <header className="fm-header">
        <div>
          <h1>
            Thành viên nhà <span aria-hidden>👨‍👩‍👧‍👦</span>
          </h1>
          <p>Thêm cha/mẹ, con — chưa cần tài khoản đăng nhập</p>
        </div>
        <div className="fm-header-actions">
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Làm mới
          </Button>
          <Button icon={<EditOutlined />} onClick={openEditFamily} disabled={!family}>
            Đổi tên nhà
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} disabled={!family}>
            Thêm thành viên
          </Button>
        </div>
      </header>

      <section className="fm-hero">
        <div className="fm-hero-illust" aria-hidden>
          <span className="fm-hero-family">👨‍👩‍👧‍👦</span>
        </div>
        <div className="fm-hero-body">
          <div className="fm-hero-name">
            <h2>{family?.displayName ?? 'Chưa có gia đình'}</h2>
            {family ? (
              <button type="button" className="fm-edit-name" onClick={openEditFamily} title="Đổi tên nhà">
                <EditOutlined />
              </button>
            ) : null}
          </div>
          <p className="fm-hero-motto">
            <HomeOutlined /> Gia đình hạnh phúc bắt đầu từ việc hiểu và đồng hành cùng nhau.
          </p>
          <div className="fm-stats">
            <span className="fm-stat is-total">
              <TeamOutlined /> {stats.total} Thành viên
            </span>
            <span className="fm-stat is-adult">
              <UserOutlined /> {stats.adults} Người lớn
            </span>
            <span className="fm-stat is-child">
              <span aria-hidden>🧒</span> {stats.children} Trẻ em
            </span>
          </div>
        </div>
        <button
          type="button"
          className="fm-invite"
          onClick={openCreate}
          disabled={!family}
        >
          <UserAddOutlined className="fm-invite-icon" />
          <strong>Thêm thành viên</strong>
          <span>Mời thêm người thân tham gia cùng gia đình</span>
        </button>
      </section>

      <section className="fm-table-card">
        <Table
          rowKey="id"
          loading={loading}
          columns={columns}
          dataSource={members}
          pagination={false}
          locale={{ emptyText: 'Chưa có thành viên.' }}
        />
      </section>

      <Drawer
        title={editing ? 'Sửa thành viên' : 'Thêm thành viên'}
        width={420}
        open={open}
        onClose={() => setOpen(false)}
        destroyOnClose
        extra={
          <Button type="primary" loading={saving} onClick={() => void save()}>
            Lưu
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item name="displayName" label="Tên hiển thị" rules={[{ required: true }]}>
            <Input placeholder="Ví dụ: Bảo Nhi" />
          </Form.Item>
          <Form.Item name="roleCode" label="Vai trò" rules={[{ required: true }]}>
            <Select options={ROLE_OPTIONS} />
          </Form.Item>
          <Form.Item name="dateOfBirth" label="Ngày sinh">
            <DatePicker style={{ width: '100%' }} format="YYYY-MM-DD" />
          </Form.Item>
          <Form.Item name="sortOrder" label="Thứ tự">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          {editing ? (
            <Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}>
              <Select options={STATUS_OPTIONS} />
            </Form.Item>
          ) : null}
        </Form>
      </Drawer>

      <Drawer
        title="Đổi tên gia đình"
        width={420}
        open={familyOpen}
        onClose={() => setFamilyOpen(false)}
        destroyOnClose
        extra={
          <Button type="primary" loading={saving} onClick={() => void saveFamily()}>
            Lưu
          </Button>
        }
      >
        <Form form={familyForm} layout="vertical">
          <Form.Item
            name="displayName"
            label="Tên nhà"
            rules={[{ required: true, message: 'Nhập tên gia đình' }]}
            extra="Hiện trên Tổng quan (vd. Chào buổi tối · …)."
          >
            <Input placeholder="Ví dụ: Gia đình Khiêm - Hiền" maxLength={120} />
          </Form.Item>
          <Form.Item name="timezone" label="Múi giờ" rules={[{ required: true }]}>
            <Select
              options={[
                { value: 'Asia/Ho_Chi_Minh', label: 'Asia/Ho_Chi_Minh' },
                { value: 'UTC', label: 'UTC' },
              ]}
            />
          </Form.Item>
        </Form>
      </Drawer>
    </div>
  );
}
