import { useEffect, useState } from 'react';
import { Alert, Button, Drawer, Select, Spin, Typography } from 'antd';
import { PosSupportSale } from './PosSupportSale';
import { fetchPosCustomerContext } from '@/shared/api/pos-customer-context.api';
import type { CustomerKnowledgeEntryDto, CustomerPosContextDto } from '@/shared/api/pos-customer-context.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import './pos-customer-context.css';

const { Text } = Typography;

interface PosCustomerContextPanelProps {
  open: boolean;
  onClose: () => void;
  customerId?: string;
  warehouseId?: string;
  onEditProfile?: () => void;
  onAddProduct: (lookupCode: string) => Promise<void>;
  onConsultationSaved: (sessionId: string, safetyLevel: string) => void;
  customers: { id: string; label: string }[];
  customerSearchLoading?: boolean;
  onCustomerSearch: (query: string) => void;
  onCustomerChange: (customerId?: string) => void;
}

const KNOWLEDGE_LABEL: Record<string, string> = {
  CHRONIC: 'Bệnh nền',
  ALLERGY_DRUG: 'Dị ứng thuốc',
  ALLERGY_OTHER: 'Dị ứng khác',
  ADVERSE_REACTION: 'Phản ứng bất lợi',
  MEDICATION_USE: 'Đã xác nhận sử dụng',
  FOLLOW_UP: 'Cần hỏi lại',
};

export function PosCustomerContextPanel({
  open,
  onClose,
  customerId,
  warehouseId,
  onEditProfile,
  onAddProduct,
  onConsultationSaved,
  customers,
  customerSearchLoading,
  onCustomerSearch,
  onCustomerChange,
}: PosCustomerContextPanelProps) {
  const [context, setContext] = useState<CustomerPosContextDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!customerId || !warehouseId) {
      setContext(null);
      return;
    }

    let cancelled = false;

    setLoading(true);
    setError(null);

    fetchPosCustomerContext(customerId, warehouseId)
      .then((data) => {
        if (!cancelled) {
          setContext(data);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          console.error('Failed to fetch customer context:', err);
          setError(apiErrorMessage(err, 'Không thể tải thông tin khách hàng'));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [customerId, warehouseId]);

  const bought = (context?.recentOrders ?? [])
    .flatMap((order) => order.topProducts.map((product) => product.productName))
    .filter((name, index, all) => name && all.indexOf(name) === index)
    .slice(0, 6);
  const frequent = (context?.frequentPurchases ?? []).slice(0, 4);
  const prescriptions = (context?.recentPrescriptions ?? []).slice(0, 3);
  const lastConsult = context?.recentConsultations[0];
  const notes = (context?.knowledgeEntries ?? []).slice(0, 4);

  return (
    <Drawer title="Hỗ trợ tư vấn" open={open} width="min(1180px, calc(100vw - 16px))" onClose={onClose}>
      <div className="pos-support-grid">
        <div className="pos-support-side">
          <div className="pos-customer-context-disclaimer">
            <Text type="secondary" style={{ fontSize: 11 }}>
              Thông tin tham khảo — quyết định cuối cùng thuộc về nhân viên/dược sĩ
            </Text>
          </div>
          {!customerId ? (
            <Select
              showSearch
              filterOption={false}
              allowClear
              placeholder="Chọn khách"
              style={{ width: '100%', marginTop: 12 }}
              loading={customerSearchLoading}
              onSearch={onCustomerSearch}
              onDropdownVisibleChange={(visible) => { if (visible) onCustomerSearch(''); }}
              options={customers.map((customer) => ({ value: customer.id, label: customer.label }))}
              onChange={(value) => onCustomerChange(value)}
              notFoundContent={customerSearchLoading ? <Spin size="small" /> : 'Không thấy khách'}
            />
          ) : (
            <CustomerBrief
              customerId={customerId}
              context={context}
              loading={loading}
              error={error}
              bought={bought}
              frequent={frequent}
              prescriptions={prescriptions}
              notes={notes}
              lastConsult={lastConsult}
              onEditProfile={onEditProfile}
            />
          )}
        </div>
        <PosSupportSale
          customerId={customerId}
          warehouseId={warehouseId}
          onAddProduct={onAddProduct}
          onSaved={(session) => onConsultationSaved(session.id, session.safetyLevel)}
        />
      </div>
    </Drawer>
  );
}

function CustomerBrief({
  customerId,
  context,
  loading,
  error,
  bought,
  frequent,
  prescriptions,
  notes,
  lastConsult,
  onEditProfile,
}: {
  customerId?: string;
  context: CustomerPosContextDto | null;
  loading: boolean;
  error: string | null;
  bought: string[];
  frequent: CustomerPosContextDto['frequentPurchases'];
  prescriptions: NonNullable<CustomerPosContextDto['recentPrescriptions']>;
  notes: CustomerKnowledgeEntryDto[];
  lastConsult?: CustomerPosContextDto['recentConsultations'][number];
  onEditProfile?: () => void;
}) {
  if (!customerId) {
    return (
      <Text type="secondary">
        Chọn khách ở quầy để xem hồ sơ, đơn đã mua và lần tư vấn trước. Tra triệu chứng vẫn dùng được bên dưới.
      </Text>
    );
  }
  if (loading) {
    return <div style={{ textAlign: 'center', padding: 12 }}><Spin /></div>;
  }
  if (error) {
    return <Alert type="error" showIcon message="Không thể tải thông tin khách hàng" description={error} />;
  }
  if (!context) return null;
  const profile = context.profile;
  const profileMeta = [profile.customerCode, profile.ageYears != null ? `${profile.ageYears} tuổi` : null, profile.gender]
    .filter(Boolean)
    .join(' · ');
  return (
    <div className="pos-support-person">
      <div className="pos-support-person-name">{profile.fullName}</div>
      <Text type="secondary">{profileMeta}</Text>
      {onEditProfile ? (
        <Button type="link" size="small" style={{ paddingLeft: 0 }} onClick={onEditProfile}>Bổ sung hồ sơ</Button>
      ) : null}
      {profile.clinicalNotes ? <div className="pos-support-note">{profile.clinicalNotes}</div> : null}
      {notes.map((entry) => (
        <div key={entry.id} className="pos-support-note">
          {KNOWLEDGE_LABEL[entry.kind] ?? entry.kind}: {entry.body}
        </div>
      ))}
      <div className="pos-support-kicker" style={{ marginTop: 8 }}>Lịch sử</div>
      <div className="pos-support-history">
        <div className="pos-support-history-row">
          <span>Đã mua</span>
          <span>{bought.length > 0 ? bought.join(', ') : 'Chưa có trong 90 ngày'}</span>
        </div>
        {(frequent ?? []).length > 0 ? (
          <div className="pos-support-history-row">
            <span>Hay mua</span>
            <span>{(frequent ?? []).map((item) => item.productName).join(', ')}</span>
          </div>
        ) : null}
        <div className="pos-support-history-row">
          <span>Tư vấn</span>
          <span>
            {lastConsult
              ? `${new Date(lastConsult.consultationDate).toLocaleDateString('vi-VN')}${lastConsult.symptoms.length > 0 ? ` · ${lastConsult.symptoms.slice(0, 3).join(', ')}` : ''}`
              : 'Chưa lưu lần nào'}
          </span>
        </div>
        <div className="pos-support-history-row">
          <span>Đơn đã ghi</span>
          <span>{prescriptions.length > 0 ? prescriptions.map((item) => item.productNames || item.prescriptionCode).join('; ') : 'Chưa có'}</span>
        </div>
      </div>
    </div>
  );
}
