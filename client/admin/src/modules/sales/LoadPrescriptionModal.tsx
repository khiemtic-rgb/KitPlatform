import { useEffect, useMemo, useState } from 'react';
import { Button, Input, Modal, Space, Table, Tabs, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { SearchOutlined } from '@ant-design/icons';
import {
  fetchPrescriptionPosLoad,
  fetchPrescriptions,
  type RxPrescriptionListItem,
  type RxPrescriptionPosLoad,
} from '@/shared/api/rx.api';
import {
  fetchConnectRxHandoffs,
  type ConnectRxHandoff,
} from '@/shared/api/connect.api';
import { apiErrorMessage } from '@/shared/api/api-error';

const LOADABLE_STATUSES = new Set([
  'pending_verification',
  'verified',
  'signed',
  'partially_dispensed',
]);

const STATUS_LABELS: Record<string, string> = {
  draft: 'Nháp',
  pending_verification: 'Chờ xác minh',
  verified: 'Đã xác minh',
  signed: 'Đã ký',
  partially_dispensed: 'Đã bán một phần',
  dispensed: 'Đã bán hết',
  expired: 'Hết hạn',
  cancelled: 'Đã hủy',
};

const HANDOFF_LABELS: Record<string, string> = {
  pending_pharmacy: 'Chờ bán',
  consumed: 'Đã xử lý',
  dismissed: 'Bỏ qua',
};

type Props = {
  open: boolean;
  warehouseId?: string;
  onCancel: () => void;
  onLoaded: (payload: RxPrescriptionPosLoad) => void;
  onLoadedConnectHandoff?: (handoffId: string) => void;
};

export function LoadPrescriptionModal({
  open,
  warehouseId,
  onCancel,
  onLoaded,
  onLoadedConnectHandoff,
}: Props) {
  const [tab, setTab] = useState<'rx' | 'connect'>('connect');
  const [phoneSearch, setPhoneSearch] = useState('');
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<RxPrescriptionListItem[]>([]);
  const [handoffs, setHandoffs] = useState<ConnectRxHandoff[]>([]);
  const [selectedId, setSelectedId] = useState<string>();
  const [selectedHandoffId, setSelectedHandoffId] = useState<string>();
  const [submitting, setSubmitting] = useState(false);

  const selected = useMemo(() => items.find((row) => row.id === selectedId), [items, selectedId]);

  useEffect(() => {
    if (!open) return;
    setTab(onLoadedConnectHandoff ? 'connect' : 'rx');
    setPhoneSearch('');
    setItems([]);
    setSelectedId(undefined);
    setSelectedHandoffId(undefined);
    if (onLoadedConnectHandoff) {
      void loadHandoffs();
    }
  }, [open, onLoadedConnectHandoff]);

  const columns: ColumnsType<RxPrescriptionListItem> = [
    {
      title: 'Mã đơn',
      dataIndex: 'prescriptionCode',
      width: 140,
    },
    {
      title: 'Bệnh nhân',
      key: 'patient',
      render: (_, row) => (
        <div>
          <div>{row.patientName || '—'}</div>
          <small>{row.patientPhone || '—'}</small>
        </div>
      ),
    },
    {
      title: 'Bác sĩ',
      dataIndex: 'prescriberName',
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 140,
      render: (value: string) => (
        <Tag color={LOADABLE_STATUSES.has(value) ? 'green' : 'default'}>
          {STATUS_LABELS[value] || value}
        </Tag>
      ),
    },
  ];

  const handoffColumns: ColumnsType<ConnectRxHandoff> = [
    {
      title: 'Mã đơn',
      dataIndex: 'prescriptionCode',
      width: 150,
    },
    {
      title: 'Bệnh nhân',
      render: (_, row) => (
        <div>
          <div>{row.patientDisplayName || '—'}</div>
          <small>{row.patientPhone || '—'}</small>
        </div>
      ),
    },
    {
      title: 'Bác sĩ',
      dataIndex: 'providerDisplayName',
      render: (v?: string) => v || '—',
    },
    {
      title: 'PK',
      render: (_, row) => row.clinicTenantCode || row.clinicTenantName,
      width: 120,
    },
    {
      title: 'Trạng thái',
      dataIndex: 'handoffStatus',
      width: 110,
      render: (s: string) => (
        <Tag color={s === 'pending_pharmacy' ? 'orange' : s === 'consumed' ? 'green' : 'default'}>
          {HANDOFF_LABELS[s] || s}
        </Tag>
      ),
    },
  ];

  const search = async () => {
    const q = phoneSearch.trim();
    if (!q) {
      message.warning('Nhập số điện thoại bệnh nhân');
      return;
    }
    setLoading(true);
    try {
      const result = await fetchPrescriptions({
        phoneSearch: q,
        page: 1,
        pageSize: 50,
      });
      setItems(result.items.filter((row) => LOADABLE_STATUSES.has(row.status)));
      setSelectedId(undefined);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tìm được đơn thuốc'));
    } finally {
      setLoading(false);
    }
  };

  const loadHandoffs = async () => {
    setLoading(true);
    try {
      const list = await fetchConnectRxHandoffs();
      setHandoffs(
        list.filter(
          (h) => h.handoffStatus === 'pending_pharmacy' || h.handoffStatus === 'consumed',
        ),
      );
      setSelectedHandoffId(undefined);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được đơn phòng khám'));
    } finally {
      setLoading(false);
    }
  };

  const loadToPos = async () => {
    if (!warehouseId) {
      message.warning('Chọn kho bán trước khi nạp đơn');
      return;
    }
    if (tab === 'connect') {
      if (!selectedHandoffId || !onLoadedConnectHandoff) {
        message.warning('Chọn đơn phòng khám cần nạp');
        return;
      }
      setSubmitting(true);
      try {
        onLoadedConnectHandoff(selectedHandoffId);
      } finally {
        setSubmitting(false);
      }
      return;
    }
    if (!selected) {
      message.warning('Chọn đơn cần nạp');
      return;
    }
    setSubmitting(true);
    try {
      const payload = await fetchPrescriptionPosLoad(selected.id, warehouseId);
      onLoaded(payload);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không nạp được đơn thuốc vào POS'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal
      open={open}
      title="Bán theo đơn phòng khám"
      width={920}
      onCancel={onCancel}
      onOk={() => void loadToPos()}
      okText="Nạp vào quầy"
      cancelText="Đóng"
      okButtonProps={{ loading: submitting }}
    >
      <Tabs
        activeKey={tab}
        onChange={(k) => setTab(k as 'rx' | 'connect')}
        items={[
          ...(onLoadedConnectHandoff
            ? [
                {
                  key: 'connect',
                  label: 'Đơn phòng khám (Connect)',
                  children: (
                    <Space direction="vertical" style={{ width: '100%' }} size={12}>
                      <Button onClick={() => void loadHandoffs()} loading={loading}>
                        Làm mới danh sách
                      </Button>
                      <Table
                        rowKey="id"
                        loading={loading}
                        columns={handoffColumns}
                        dataSource={handoffs}
                        size="small"
                        pagination={{ pageSize: 8, showSizeChanger: false }}
                        rowSelection={{
                          type: 'radio',
                          selectedRowKeys: selectedHandoffId ? [selectedHandoffId] : [],
                          onChange: (keys) => setSelectedHandoffId(String(keys[0] ?? '')),
                        }}
                        onRow={(record) => ({
                          onClick: () => setSelectedHandoffId(record.id),
                          style: { cursor: 'pointer' },
                        })}
                        locale={{ emptyText: 'Chưa có đơn từ phòng khám' }}
                      />
                    </Space>
                  ),
                },
              ]
            : []),
          {
            key: 'rx',
            label: 'Đơn Rx nhà thuốc',
            children: (
              <>
                <Space.Compact style={{ width: '100%', marginBottom: 12 }}>
                  <Input
                    placeholder="SĐT bệnh nhân"
                    value={phoneSearch}
                    onChange={(event) => setPhoneSearch(event.target.value)}
                    onPressEnter={() => void search()}
                  />
                  <Button icon={<SearchOutlined />} loading={loading} onClick={() => void search()}>
                    Tìm
                  </Button>
                </Space.Compact>
                <Table
                  rowKey="id"
                  loading={loading}
                  columns={columns}
                  dataSource={items}
                  size="small"
                  pagination={{ pageSize: 8, showSizeChanger: false }}
                  rowSelection={{
                    type: 'radio',
                    selectedRowKeys: selectedId ? [selectedId] : [],
                    onChange: (keys) => setSelectedId(String(keys[0] ?? '')),
                  }}
                  onRow={(record) => ({
                    onClick: () => setSelectedId(record.id),
                    style: { cursor: 'pointer' },
                  })}
                />
              </>
            ),
          },
        ]}
      />
    </Modal>
  );
}
