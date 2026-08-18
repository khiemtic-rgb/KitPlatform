import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button, Input, Typography, message } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchLocalOsListings,
  setLocalOsListingStatus,
  updateLocalOsListing,
  type LocalListing,
} from '@/shared/api/local-os.api';

const KIND: Record<string, string> = { job: 'Việc', event: 'Sự kiện', room: 'Trọ' };

function hasPhone(row: Pick<LocalListing, 'contactPhone'>): boolean {
  const d = (row.contactPhone ?? '').replace(/\D/g, '');
  return d.length >= 9 && d.length <= 12;
}

export function LocalOsPhoneReviewPage() {
  const [queue, setQueue] = useState<LocalListing[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [phone, setPhone] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const rows = await fetchLocalOsListings({ status: 'NEEDS_REVIEW' });
      setQueue(rows);
      setPhone(rows[0]?.contactPhone ?? '');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được hàng chờ.'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const row = queue[0];

  const decide = async (status: 'ACTIVE' | 'HIDDEN') => {
    if (!row) return;
    const nextPhone = phone.trim() || row.contactPhone || '';
    if (status === 'ACTIVE' && row.kind !== 'event' && !hasPhone({ contactPhone: nextPhone })) {
      message.error('Ghi số điện thoại trước khi đăng.');
      return;
    }
    setBusy(true);
    try {
      if (status === 'ACTIVE' && nextPhone && nextPhone !== (row.contactPhone ?? '')) {
        await updateLocalOsListing(row.id, {
          ...row,
          kind: row.kind,
          title: row.title,
          contactPhone: nextPhone,
        });
      }
      await setLocalOsListingStatus(row.id, status);
      message.success(status === 'ACTIVE' ? 'Đã lên trang chủ.' : 'Đã ẩn.');
      const rest = queue.slice(1);
      setQueue(rest);
      setPhone(rest[0]?.contactPhone ?? '');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Chưa gửi được.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="los-phone">
      <header className="los-phone-head">
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            Duyệt tin
          </Typography.Title>
          <p>{loading ? 'Đang tải…' : queue.length ? `${queue.length} tin chờ` : 'Hàng chờ trống'}</p>
        </div>
        <Link to="/local-os/listings">Bảng đầy đủ</Link>
      </header>

      {!loading && !row ? (
        <div className="los-phone-card">
          <p>Hết tin chờ duyệt.</p>
        </div>
      ) : null}

      {row ? (
        <div className="los-phone-card">
          <span className="los-phone-badge">{KIND[row.kind] ?? row.kind}</span>
          <h2>{row.title}</h2>
          <p>{row.placeText || 'Chưa ghi địa điểm'}</p>
          {row.kind === 'job' && (row.salaryText || row.workingTime) ? (
            <p>{row.salaryText || row.workingTime}</p>
          ) : null}
          {row.kind === 'room' ? <p>Giá thuê: liên hệ</p> : null}
          {hasPhone(row) ? <p className="los-phone-num">Số điện thoại: {row.contactPhone}</p> : null}
          {row.kind !== 'event' && !hasPhone({ contactPhone: phone || row.contactPhone }) ? (
            <>
              <p className="los-phone-warn">Việc / phòng không có số thì không lên site.</p>
              <Input
                inputMode="tel"
                placeholder="09xxxxxxxx"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
              />
            </>
          ) : null}
          <div className="los-phone-actions">
            <Button size="large" disabled={busy} onClick={() => void decide('HIDDEN')}>
              Ẩn
            </Button>
            <Button type="primary" size="large" disabled={busy} onClick={() => void decide('ACTIVE')}>
              Đăng
            </Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
