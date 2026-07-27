import { useCallback, useEffect, useMemo, useState } from 'react';
import { Form, Input, Popconfirm, Select, Spin, Switch, message } from 'antd';
import {
  ArrowLeftOutlined,
  BellOutlined,
  DeleteOutlined,
  EditOutlined,
  FileTextOutlined,
  HeartFilled,
  ManOutlined,
  PhoneOutlined,
  PlusOutlined,
  RightOutlined,
  SafetyCertificateOutlined,
  TeamOutlined,
  UserAddOutlined,
  UserOutlined,
  WomanOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import {
  createFamilyMember,
  deleteFamilyMember,
  fetchFamilyMembers,
  getApiErrorMessage,
  setFamilyNotifyCaregiver,
  updateFamilyMember,
} from '@/shared/api/customer-app.api';
import type { FamilyMember } from '@/shared/api/customer-app.types';
import { FAMILY_RELATIONSHIP_LABELS } from '@/shared/api/customer-app.types';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import {
  CustomerFormModal,
  FormModalFooter,
  FormModalLabel,
  FormModalTip,
} from '@/shared/components/CustomerFormModal';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { FAMILY_GENDER, familyRoleLabel, resolveFamilyGender } from '@/shared/i18n/family-role-label';
import { useCustomerLabels } from '@/shared/i18n/useCustomerLabels';
import './FamilyPage.css';

function memberAvatarSrc(member: FamilyMember): string {
  const g = resolveFamilyGender(member.gender, member.id) ?? FAMILY_GENDER.female;
  if (member.relationship === 'child') {
    return g === FAMILY_GENDER.female ? '/home/avatars/girl.jpg' : '/home/avatars/boy.jpg';
  }
  return g === FAMILY_GENDER.male ? '/home/avatars/adult-male.jpg' : '/home/avatars/adult-female.jpg';
}

type GenderOption = { value: number; label: string; icon: React.ReactNode };

function GenderToggle({
  value,
  onChange,
  options,
  ariaLabel,
}: {
  value?: number;
  onChange?: (next: number) => void;
  options: GenderOption[];
  ariaLabel: string;
}) {
  return (
    <div className="family-modal-gender" role="radiogroup" aria-label={ariaLabel}>
      {options.map((opt) => (
        <button
          key={opt.value}
          type="button"
          role="radio"
          aria-checked={value === opt.value}
          className={`family-modal-gender-btn${value === opt.value ? ' family-modal-gender-btn--active' : ''}`}
          onClick={() => onChange?.(opt.value)}
        >
          {opt.icon}
          {opt.label}
        </button>
      ))}
    </div>
  );
}

export function FamilyPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { branding } = useCustomerBranding();
  const { familyRelationship } = useCustomerLabels();
  const [items, setItems] = useState<FamilyMember[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<FamilyMember | null>(null);
  const [togglingNotifyId, setTogglingNotifyId] = useState<string | null>(null);
  const [form] = Form.useForm();

  const relationshipOptions = useMemo(
    () =>
      Object.keys(FAMILY_RELATIONSHIP_LABELS).map((value) => ({
        value,
        label: familyRelationship(value),
      })),
    [familyRelationship],
  );

  const genderOptions = useMemo(
    () => [
      { value: FAMILY_GENDER.male, label: t('family.genderMale'), icon: <ManOutlined /> },
      { value: FAMILY_GENDER.female, label: t('family.genderFemale'), icon: <WomanOutlined /> },
    ],
    [t],
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const rows = await fetchFamilyMembers();
      setItems(rows.filter((r) => r.status === 1));
    } catch (error) {
      message.error(getApiErrorMessage(error, t('family.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const openCreate = () => {
    setEditing(null);
    form.setFieldsValue({
      relationship: 'parent',
      gender: FAMILY_GENDER.female,
      fullName: '',
      phone: '',
      notes: '',
      notifyCaregiver: false,
    });
    setModalOpen(true);
  };

  const openEdit = (item: FamilyMember) => {
    setEditing(item);
    form.setFieldsValue({
      fullName: item.fullName,
      relationship: item.relationship,
      gender: item.gender ?? FAMILY_GENDER.female,
      phone: item.phone ?? '',
      notes: item.notes ?? '',
      notifyCaregiver: item.notifyCaregiver,
    });
    setModalOpen(true);
  };

  const onSubmit = async () => {
    const values = await form.validateFields();
    const gender = Number(values.gender) as number;
    try {
      if (editing) {
        const updated = await updateFamilyMember(editing.id, {
          fullName: values.fullName,
          relationship: values.relationship,
          gender,
          phone: values.phone || undefined,
          notes: values.notes || undefined,
          status: 1,
          notifyCaregiver: Boolean(values.notifyCaregiver),
        });
        setItems((prev) =>
          prev.map((r) =>
            r.id === updated.id
              ? { ...updated, notifyCaregiver: Boolean(values.notifyCaregiver) }
              : r,
          ),
        );
        message.success(t('family.updated'));
      } else {
        const created = await createFamilyMember({
          fullName: values.fullName,
          relationship: values.relationship,
          gender,
          phone: values.phone || undefined,
          notes: values.notes || undefined,
          notifyCaregiver: Boolean(values.notifyCaregiver),
        });
        setItems((prev) => [
          ...prev,
          { ...created, notifyCaregiver: Boolean(values.notifyCaregiver) },
        ]);
        message.success(t('family.memberAdded'));
      }
      setModalOpen(false);
    } catch (error) {
      message.error(getApiErrorMessage(error, t('family.saveFailed')));
    }
  };

  const onDelete = async (item: FamilyMember) => {
    try {
      await deleteFamilyMember(item.id);
      setItems((prev) => prev.filter((r) => r.id !== item.id));
      message.success(t('family.deleted'));
    } catch (error) {
      message.error(getApiErrorMessage(error, t('family.deleteFailed')));
    }
  };

  const toggleNotify = async (item: FamilyMember, checked: boolean) => {
    const previous = items;
    setItems((prev) =>
      prev.map((r) => (r.id === item.id ? { ...r, notifyCaregiver: checked } : r)),
    );
    setTogglingNotifyId(item.id);
    try {
      const updated = await setFamilyNotifyCaregiver(item.id, checked);
      setItems((prev) =>
        prev.map((r) => (r.id === item.id ? { ...updated, notifyCaregiver: checked } : r)),
      );
      message.success(checked ? t('family.notifyOn') : t('family.notifyOff'));
    } catch (error) {
      setItems(previous);
      message.error(getApiErrorMessage(error, t('family.updateFailed')));
    } finally {
      setTogglingNotifyId(null);
    }
  };

  const headerStyle = {
    background: `linear-gradient(135deg, ${branding.primaryColor}, ${branding.secondaryColor})`,
  };

  return (
    <div className="family-hub">
      <header className="family-hub-header" style={headerStyle}>
        <div className="family-hub-header-inner">
          <div className="family-hub-brand">
            <button
              type="button"
              className="family-hub-back"
              aria-label={t('common.back')}
              onClick={() => (window.history.length > 1 ? navigate(-1) : navigate('/'))}
            >
              <ArrowLeftOutlined />
            </button>
            <BrandingLogo logoUrl={branding.logoUrl} />
            <div>
              <div className="family-hub-brand-title">{branding.appName}</div>
              <div className="family-hub-tagline">{branding.tagline || t('family.hubTagline')}</div>
            </div>
          </div>
          <div className="family-hub-art" aria-hidden>
            <div className="family-hub-house">
              <div className="family-hub-house-people">
                <span className="family-hub-person" />
                <span className="family-hub-person family-hub-person--sm" />
                <span className="family-hub-person" />
              </div>
            </div>
            <span className="family-hub-heart">
              <HeartFilled />
            </span>
          </div>
        </div>
      </header>

      <div className="family-hub-sheet">
        <div className="family-hub-top">
          <div>
            <h1 className="family-hub-title">{t('family.title')}</h1>
            <p className="family-hub-intro">{t('family.hubIntro')}</p>
          </div>
          <button type="button" className="family-hub-add" onClick={openCreate}>
            <PlusOutlined />
            {t('family.addMember')}
          </button>
        </div>

        {loading ? (
          <div className="family-hub-loading">
            <Spin />
          </div>
        ) : items.length === 0 ? (
          <div className="family-hub-empty">{t('family.emptyHint')}</div>
        ) : (
          <div className="family-hub-list">
            {items.map((item) => (
              <article key={item.id} className="family-hub-card">
                <div className="family-hub-card-main">
                  <div className="family-hub-avatar">
                    <UserOutlined aria-hidden />
                    <img
                      src={memberAvatarSrc(item)}
                      alt=""
                      onError={(e) => {
                        e.currentTarget.style.display = 'none';
                      }}
                    />
                  </div>
                  <div className="family-hub-card-copy">
                    <div className="family-hub-card-name-row">
                      <span className="family-hub-card-name">{item.fullName}</span>
                      <span className="family-hub-role">
                        {familyRoleLabel(item.relationship, item.gender, t, familyRelationship, item.id)}
                      </span>
                    </div>
                    {item.phone ? (
                      <div className="family-hub-phone">
                        <PhoneOutlined />
                        {item.phone}
                      </div>
                    ) : null}
                  </div>
                  <RightOutlined className="family-hub-chevron" aria-hidden />
                </div>

                <div className="family-hub-notify">
                  <span className="family-hub-notify-label">
                    <BellOutlined />
                    {t('family.notifyCaregiverLabel')}
                  </span>
                  <Switch
                    checked={item.notifyCaregiver}
                    loading={togglingNotifyId === item.id}
                    onChange={(checked) => void toggleNotify(item, checked)}
                  />
                </div>

                <div className="family-hub-actions">
                  <button type="button" className="family-hub-btn family-hub-btn--edit" onClick={() => openEdit(item)}>
                    <EditOutlined />
                    {t('common.edit')}
                  </button>
                  <Popconfirm
                    title={t('family.confirmDelete')}
                    okText={t('common.delete')}
                    cancelText={t('common.cancel')}
                    okButtonProps={{ danger: true }}
                    onConfirm={() => void onDelete(item)}
                  >
                    <button type="button" className="family-hub-btn family-hub-btn--delete">
                      <DeleteOutlined />
                      {t('common.delete')}
                    </button>
                  </Popconfirm>
                </div>
              </article>
            ))}
          </div>
        )}

        <div className="family-hub-privacy">
          <span className="family-hub-privacy-icon">
            <SafetyCertificateOutlined />
          </span>
          <div className="family-hub-privacy-copy">
            <div className="family-hub-privacy-title">{t('family.privacyTitle')}</div>
            <div className="family-hub-privacy-sub">{t('family.privacySub')}</div>
          </div>
          <RightOutlined className="family-hub-chevron" aria-hidden />
        </div>
      </div>

      <CustomerFormModal
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        icon={<UserAddOutlined />}
        title={editing ? t('family.modalEdit') : t('family.modalAdd')}
        subtitle={t('family.modalSub')}
        footer={
          <FormModalFooter onCancel={() => setModalOpen(false)} onOk={() => void onSubmit()} />
        }
      >
        <Form form={form} layout="vertical" className="cfm-form" requiredMark={false}>
          <Form.Item
            name="fullName"
            label={
              <FormModalLabel icon={<UserOutlined />} required>
                {t('family.fullName')}
              </FormModalLabel>
            }
            rules={[{ required: true, message: t('family.fullNameRequired') }]}
          >
            <Input size="large" placeholder={t('family.fullNamePlaceholder')} />
          </Form.Item>

          <Form.Item
            name="relationship"
            label={
              <FormModalLabel icon={<TeamOutlined />} required>
                {t('family.relationship')}
              </FormModalLabel>
            }
            rules={[{ required: true }]}
          >
            <Select
              size="large"
              options={relationshipOptions}
              placeholder={t('family.relationshipPlaceholder')}
            />
          </Form.Item>

          <Form.Item
            name="gender"
            label={
              <FormModalLabel icon={<UserOutlined />} required>
                {t('family.gender')}
              </FormModalLabel>
            }
            rules={[{ required: true, message: t('family.genderRequired') }]}
            extra={<span className="cfm-hint">{t('family.genderHint')}</span>}
          >
            <GenderToggle options={genderOptions} ariaLabel={t('family.gender')} />
          </Form.Item>

          <Form.Item
            name="phone"
            label={<FormModalLabel icon={<PhoneOutlined />}>{t('family.phone')}</FormModalLabel>}
          >
            <Input size="large" placeholder={t('family.phonePlaceholder')} inputMode="tel" />
          </Form.Item>

          <Form.Item
            name="notes"
            label={<FormModalLabel icon={<FileTextOutlined />}>{t('family.notes')}</FormModalLabel>}
          >
            <Input.TextArea rows={3} placeholder={t('family.notesPlaceholder')} />
          </Form.Item>

          <FormModalTip
            icon={<BellOutlined />}
            title={t('family.notifyCaregiver')}
            subtitle={t('family.notifyCaregiverExtra')}
            action={
              <Form.Item name="notifyCaregiver" valuePropName="checked" noStyle>
                <Switch />
              </Form.Item>
            }
          />
        </Form>
      </CustomerFormModal>
    </div>
  );
}
