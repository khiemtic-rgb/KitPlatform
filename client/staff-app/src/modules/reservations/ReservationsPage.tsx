import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  App,
  Alert,
  Button,
  Empty,
  Input,
  Modal,
  Popconfirm,
  Segmented,
  Spin,
  Tag,
  Typography,
} from 'antd';
import {
  PhoneOutlined,
  ReloadOutlined,
  SearchOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import {
  RESERVATION_FULFILLMENT_LABEL,
  RESERVATION_STATUS,
  RESERVATION_STATUS_COLOR,
  RESERVATION_STATUS_LABEL,
  confirmReservation,
  fetchReservations,
  loadReservationForPos,
  markReservationReady,
  rejectReservation,
  updateReservationStaffNotes,
  type ReservationListItem,
} from '@/shared/api/reservations.api';
import { fetchCustomerById } from '@/shared/api/customer.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { buildReservationCartLines } from '@/modules/reservations/reservation-pos-load';
import { usePosSession } from '@/modules/pos/pos-session.store';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

const ACTIVE = [RESERVATION_STATUS.Pending, RESERVATION_STATUS.Confirmed, RESERVATION_STATUS.Ready];

type StatusFilter = 'all' | 'pending' | 'confirmed' | 'ready';

function digitsOnly(value: string): string {
  return value.replace(/\D/g, '');
}

function hasUsablePhone(phone?: string | null): boolean {
  const d = digitsOnly(phone ?? '');
  return d.length >= 9 && d.length <= 12;
}

function formatWhen(value?: string | null): string {
  if (!value) return '—';
  const parsed = dayjs(value);
  if (!parsed.isValid()) return '—';
  const now = dayjs();
  if (now.isSame(parsed, 'day')) return `Hôm nay ${parsed.format('HH:mm')}`;
  if (now.subtract(1, 'day').isSame(parsed, 'day')) return `Hôm qua ${parsed.format('HH:mm')}`;
  return parsed.format('DD/MM HH:mm');
}

function sortReservations(items: ReservationListItem[]): ReservationListItem[] {
  const rank: Record<number, number> = {
    [RESERVATION_STATUS.Pending]: 0,
    [RESERVATION_STATUS.Confirmed]: 1,
    [RESERVATION_STATUS.Ready]: 2,
  };
  return [...items].sort((a, b) => {
    const ra = rank[a.status] ?? 9;
    const rb = rank[b.status] ?? 9;
    if (ra !== rb) return ra - rb;
    const ta = dayjs(a.submittedAt).valueOf() || 0;
    const tb = dayjs(b.submittedAt).valueOf() || 0;
    return tb - ta;
  });
}

export function ReservationsPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const { setWarehouseId, replaceCart, setCustomer, setLoadedReservation } = usePosSession();
  const [items, setItems] = useState<ReservationListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState<StatusFilter>('all');
  const [loadingPos, setLoadingPos] = useState<string | null>(null);
  const [actingId, setActingId] = useState<string | null>(null);
  const [rejectingId, setRejectingId] = useState<string | null>(null);
  const [notesOpen, setNotesOpen] = useState(false);
  const [notesTarget, setNotesTarget] = useState<ReservationListItem | null>(null);
  const [staffNotes, setStaffNotes] = useState('');
  const [savingNotes, setSavingNotes] = useState(false);
  const [blockedPosIds, setBlockedPosIds] = useState<Record<string, string>>({});
  const [posBlock, setPosBlock] = useState<{
    item: ReservationListItem;
    reason: string;
    warehouseId: string;
    customerId: string;
  } | null>(null);
  const [openingPosCustomer, setOpeningPosCustomer] = useState(false);

  const load = useCallback(
    async (mode: 'full' | 'refresh' = 'full') => {
      if (mode === 'full') {
        setLoading(true);
        setLoadError(null);
      } else {
        setRefreshing(true);
      }
      try {
        setItems(await fetchReservations(ACTIVE));
        setLoadError(null);
      } catch (error) {
        const text = apiErrorMessage(error, 'Không tải được giữ hàng');
        if (mode === 'full') {
          setItems([]);
          setLoadError(text);
        } else {
          message.error(text);
        }
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [message],
  );

  useEffect(() => {
    void load('full');
  }, [load]);

  const counts = useMemo(() => {
    const pending = items.filter((i) => i.status === RESERVATION_STATUS.Pending).length;
    const confirmed = items.filter((i) => i.status === RESERVATION_STATUS.Confirmed).length;
    const ready = items.filter((i) => i.status === RESERVATION_STATUS.Ready).length;
    return { pending, confirmed, ready, total: items.length };
  }, [items]);

  const visible = useMemo(() => {
    const q = query.trim().toLowerCase();
    const qDigits = digitsOnly(query);
    let rows = sortReservations(items);
    if (filter === 'pending') rows = rows.filter((i) => i.status === RESERVATION_STATUS.Pending);
    if (filter === 'confirmed') rows = rows.filter((i) => i.status === RESERVATION_STATUS.Confirmed);
    if (filter === 'ready') rows = rows.filter((i) => i.status === RESERVATION_STATUS.Ready);
    if (q || qDigits.length >= 3) {
      rows = rows.filter((i) => {
        const phoneDigits = digitsOnly(i.customerPhone ?? '');
        return (
          i.reservationNumber.toLowerCase().includes(q) ||
          i.customerName.toLowerCase().includes(q) ||
          (i.customerPhone ?? '').toLowerCase().includes(q) ||
          (qDigits.length >= 3 && phoneDigits.includes(qDigits))
        );
      });
    }
    return rows;
  }, [items, query, filter]);

  const sendToPos = async (item: ReservationListItem) => {
    setLoadingPos(item.id);
    try {
      const payload = await loadReservationForPos(item.id);
      let lines;
      try {
        lines = await buildReservationCartLines(payload);
      } catch (lineError) {
        const reason = apiErrorMessage(
          lineError,
          'Sản phẩm trên đơn giữ không còn bán được trên POS',
        );
        setBlockedPosIds((prev) => ({ ...prev, [item.id]: reason }));
        setPosBlock({
          item,
          reason,
          warehouseId: payload.warehouseId,
          customerId: payload.customerId,
        });
        return;
      }
      if (lines.length === 0) {
        message.warning('Đơn giữ không có sản phẩm hợp lệ để đưa vào POS');
        return;
      }
      setBlockedPosIds((prev) => {
        const next = { ...prev };
        delete next[item.id];
        return next;
      });
      setWarehouseId(payload.warehouseId);
      replaceCart(lines);
      const customer = await fetchCustomerById(payload.customerId);
      setCustomer({
        id: customer.id,
        customerCode: customer.customerCode,
        fullName: customer.fullName,
        phone: customer.phone,
        allowCredit: customer.allowCredit,
      });
      setLoadedReservation(payload.reservationId, payload.reservationNumber);
      message.success(`Đã nạp giữ hàng ${payload.reservationNumber} vào POS`);
      navigate('/pos');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không đưa được vào POS'));
    } finally {
      setLoadingPos(null);
    }
  };

  const openPosWithCustomerOnly = async () => {
    if (!posBlock?.warehouseId || !posBlock.customerId) return;
    setOpeningPosCustomer(true);
    try {
      setWarehouseId(posBlock.warehouseId);
      replaceCart([]);
      setLoadedReservation(null);
      const customer = await fetchCustomerById(posBlock.customerId);
      setCustomer({
        id: customer.id,
        customerCode: customer.customerCode,
        fullName: customer.fullName,
        phone: customer.phone,
        allowCredit: customer.allowCredit,
      });
      setPosBlock(null);
      message.info('Đã mở POS với khách — thêm sản phẩm đang bán, rồi thanh toán.');
      navigate('/pos');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không mở được POS với khách'));
    } finally {
      setOpeningPosCustomer(false);
    }
  };

  const openPosWithCustomerFromCard = async (item: ReservationListItem) => {
    setOpeningPosCustomer(true);
    try {
      const payload = await loadReservationForPos(item.id);
      setWarehouseId(payload.warehouseId);
      replaceCart([]);
      setLoadedReservation(null);
      const customer = await fetchCustomerById(payload.customerId);
      setCustomer({
        id: customer.id,
        customerCode: customer.customerCode,
        fullName: customer.fullName,
        phone: customer.phone,
        allowCredit: customer.allowCredit,
      });
      message.info('Đã mở POS với khách — thêm sản phẩm đang bán, rồi thanh toán.');
      navigate('/pos');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không mở được POS với khách'));
    } finally {
      setOpeningPosCustomer(false);
    }
  };

  const rejectBlockedAndClose = async () => {
    if (!posBlock) return;
    const id = posBlock.item.id;
    setPosBlock(null);
    await rejectItem(id);
  };

  const quickAction = async (item: ReservationListItem) => {
    setActingId(item.id);
    try {
      if (item.status === RESERVATION_STATUS.Pending) {
        await confirmReservation(item.id);
        message.success('Đã xác nhận giữ hàng');
      } else if (item.status === RESERVATION_STATUS.Confirmed) {
        await markReservationReady(item.id);
        message.success('Đã đánh dấu sẵn sàng lấy');
      }
      await load('refresh');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không cập nhật được trạng thái'));
    } finally {
      setActingId(null);
    }
  };

  const rejectItem = async (id: string) => {
    setRejectingId(id);
    try {
      await rejectReservation(id);
      setBlockedPosIds((prev) => {
        const next = { ...prev };
        delete next[id];
        return next;
      });
      message.success('Đã hủy/từ chối giữ hàng');
      await load('refresh');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hủy được giữ hàng'));
    } finally {
      setRejectingId(null);
    }
  };

  const openNotes = (item: ReservationListItem) => {
    setNotesTarget(item);
    setStaffNotes('');
    setNotesOpen(true);
  };

  const saveNotes = async () => {
    if (!notesTarget) return;
    setSavingNotes(true);
    try {
      await updateReservationStaffNotes(notesTarget.id, staffNotes.trim() || undefined);
      message.success('Đã lưu ghi chú');
      setNotesOpen(false);
      setNotesTarget(null);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được ghi chú'));
    } finally {
      setSavingNotes(false);
    }
  };

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Giữ hàng"
        subtitle={
          loadError
            ? 'Không tải được danh sách'
            : counts.pending > 0
              ? `${counts.pending} chờ xác nhận · ${counts.total} đơn`
              : `${counts.total} đơn đang xử lý`
        }
        backTo="/"
        right={
          <Button
            type="text"
            className="chat-header-refresh"
            icon={<ReloadOutlined spin={refreshing || loading} />}
            aria-label="Tải lại"
            onClick={() => void load(loadError ? 'full' : 'refresh')}
          />
        }
      />
      <main className="staff-body">
        {loadError ? (
          <Alert
            type="error"
            showIcon
            message="Không tải được giữ hàng"
            description={loadError}
            action={
              <Button size="small" type="primary" loading={loading} onClick={() => void load('full')}>
                Thử lại
              </Button>
            }
            style={{ marginBottom: 12 }}
          />
        ) : (
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 10, fontSize: 12 }}>
            Quy trình: xác nhận → sẵn sàng → Vào POS bán cho khách.
          </Typography.Text>
        )}

        <Input
          size="large"
          allowClear
          prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
          placeholder="Tìm mã đơn, tên, SĐT…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          disabled={Boolean(loadError) && items.length === 0}
        />

        <div className="reservation-toolbar">
          <Segmented
            size="middle"
            value={filter}
            onChange={(v) => setFilter(v as StatusFilter)}
            disabled={Boolean(loadError) && items.length === 0}
            options={[
              { label: `Tất cả (${counts.total})`, value: 'all' },
              { label: `Chờ (${counts.pending})`, value: 'pending' },
              { label: `Đã xác nhận (${counts.confirmed})`, value: 'confirmed' },
              { label: `Sẵn sàng (${counts.ready})`, value: 'ready' },
            ]}
          />
        </div>

        {loading ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : loadError && items.length === 0 ? (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Bấm Thử lại khi mạng ổn" />
        ) : visible.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={
              query.trim()
                ? 'Không tìm thấy đơn khớp'
                : filter === 'all'
                  ? 'Không có đơn giữ đang chờ'
                  : 'Không có đơn ở trạng thái này'
            }
          />
        ) : (
          visible.map((item) => {
            const canAdvance =
              item.status === RESERVATION_STATUS.Pending ||
              item.status === RESERVATION_STATUS.Confirmed;
            const canReject =
              item.status === RESERVATION_STATUS.Pending ||
              item.status === RESERVATION_STATUS.Confirmed ||
              item.status === RESERVATION_STATUS.Ready;
            const rejectLabel =
              item.status === RESERVATION_STATUS.Pending ? 'Từ chối' : 'Hủy đơn';
            const canPos = item.status !== RESERVATION_STATUS.Pending;
            const fulfillment =
              RESERVATION_FULFILLMENT_LABEL[item.fulfillmentType] ?? 'Đến lấy';

            return (
              <article key={item.id} className="reservation-card">
                <div className="reservation-card__head">
                  <div className="reservation-card__ids">
                    <Typography.Text strong className="reservation-card__number">
                      {item.reservationNumber}
                    </Typography.Text>
                    <Typography.Text type="secondary" className="reservation-card__when">
                      {formatWhen(item.submittedAt)}
                    </Typography.Text>
                  </div>
                  <Tag color={RESERVATION_STATUS_COLOR[item.status] ?? 'default'}>
                    {RESERVATION_STATUS_LABEL[item.status] ?? item.status}
                  </Tag>
                </div>

                <div className="reservation-card__customer">
                  <Typography.Text strong>{item.customerName || 'Khách'}</Typography.Text>
                  <div className="reservation-card__meta">
                    {hasUsablePhone(item.customerPhone) ? (
                      <a className="reservation-card__phone" href={`tel:${digitsOnly(item.customerPhone!)}`}>
                        <PhoneOutlined /> {item.customerPhone}
                      </a>
                    ) : (
                      <span className="reservation-card__phone-missing">Chưa có SĐT</span>
                    )}
                    <span>· {item.itemCount} SP</span>
                    <span>· {fulfillment}</span>
                    {item.status === RESERVATION_STATUS.Ready && item.readyAt ? (
                      <span>· sẵn sàng {formatWhen(item.readyAt)}</span>
                    ) : null}
                  </div>
                </div>

                <div className="reservation-card__actions">
                  {blockedPosIds[item.id] ? (
                    <>
                      <Button
                        className="reservation-card__btn"
                        danger
                        loading={rejectingId === item.id}
                        onClick={() => void rejectItem(item.id)}
                      >
                        {rejectLabel}
                      </Button>
                      <Button
                        className="reservation-card__btn reservation-card__btn-pos"
                        type="primary"
                        icon={<ShoppingCartOutlined />}
                        loading={openingPosCustomer}
                        onClick={() => void openPosWithCustomerFromCard(item)}
                      >
                        Mở POS với khách
                      </Button>
                    </>
                  ) : (
                    <>
                      {canAdvance ? (
                        <Button
                          className="reservation-card__btn"
                          loading={actingId === item.id}
                          onClick={() => void quickAction(item)}
                        >
                          {item.status === RESERVATION_STATUS.Pending ? 'Xác nhận' : 'Sẵn sàng'}
                        </Button>
                      ) : null}
                      <Button className="reservation-card__btn" onClick={() => openNotes(item)}>
                        Ghi chú
                      </Button>
                      {canReject ? (
                        <Popconfirm
                          title={`${rejectLabel} giữ hàng này?`}
                          description="Khách sẽ thấy trạng thái từ chối trên app."
                          onConfirm={() => void rejectItem(item.id)}
                        >
                          <Button
                            className="reservation-card__btn"
                            danger
                            loading={rejectingId === item.id}
                          >
                            {rejectLabel}
                          </Button>
                        </Popconfirm>
                      ) : null}
                      <Button
                        className="reservation-card__btn reservation-card__btn-pos"
                        type="primary"
                        icon={<ShoppingCartOutlined />}
                        loading={loadingPos === item.id}
                        disabled={!canPos}
                        onClick={() => void sendToPos(item)}
                      >
                        Vào POS
                      </Button>
                    </>
                  )}
                </div>
                {!canPos && !blockedPosIds[item.id] ? (
                  <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8, fontSize: 11 }}>
                    Cần xác nhận trước khi đưa vào POS.
                  </Typography.Text>
                ) : null}
                {blockedPosIds[item.id] ? (
                  <Alert
                    type="warning"
                    showIcon
                    className="reservation-card__block"
                    message="SP trên đơn đã ngưng bán"
                    description="Dùng nút bên trên để bán lại trên quầy hoặc từ chối đơn."
                    style={{ marginTop: 10 }}
                  />
                ) : null}
              </article>
            );
          })
        )}
      </main>

      <Modal
        title={notesTarget ? `Ghi chú · ${notesTarget.reservationNumber}` : 'Ghi chú'}
        open={notesOpen}
        onCancel={() => setNotesOpen(false)}
        onOk={() => void saveNotes()}
        confirmLoading={savingNotes}
        okText="Lưu"
      >
        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8, fontSize: 12 }}>
          Ghi chú nội bộ — khách không thấy trên app.
        </Typography.Text>
        <Input.TextArea
          rows={3}
          value={staffNotes}
          onChange={(e) => setStaffNotes(e.target.value)}
          placeholder="VD: khách đến sau 17h, để ở quầy 1…"
        />
      </Modal>

      <Modal
        className="reservation-pos-block-modal"
        title={posBlock ? `Không nạp ${posBlock.item.reservationNumber}` : 'Không nạp giữ hàng'}
        open={Boolean(posBlock)}
        onCancel={() => setPosBlock(null)}
        footer={
          <div className="reservation-pos-block-modal__footer">
            <Button
              type="primary"
              size="large"
              block
              icon={<ShoppingCartOutlined />}
              loading={openingPosCustomer}
              onClick={() => void openPosWithCustomerOnly()}
            >
              Mở POS với khách
            </Button>
            <Button
              danger
              size="large"
              block
              loading={rejectingId === posBlock?.item.id}
              onClick={() => void rejectBlockedAndClose()}
            >
              Từ chối / hủy đơn
            </Button>
            <Button size="large" block onClick={() => setPosBlock(null)}>
              Đóng
            </Button>
          </div>
        }
      >
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          message="Sản phẩm trên đơn giữ đã ngưng bán"
          description={posBlock?.reason}
        />
        <Typography.Text type="secondary" style={{ fontSize: 13 }}>
          Khách: <strong>{posBlock?.item.customerName}</strong>
          {posBlock?.item.customerPhone ? ` · ${posBlock.item.customerPhone}` : ''}. Bán lại trên
          quầy hoặc hủy đơn nếu khách không lấy.
        </Typography.Text>
      </Modal>
    </div>
  );
}
