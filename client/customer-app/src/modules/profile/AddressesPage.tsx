import { useCallback, useEffect, useState } from 'react';
import { Form, Input, Popconfirm, Spin, Switch, message } from 'antd';
import {
  ArrowLeftOutlined,
  DeleteOutlined,
  EditOutlined,
  EnvironmentOutlined,
  HomeOutlined,
  InboxOutlined,
  PhoneOutlined,
  PlusOutlined,
  StarOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  createAddress,
  deleteAddress,
  fetchAddresses,
  getApiErrorMessage,
  updateAddress,
} from '@/shared/api/customer-app.api';
import type { CustomerAddress } from '@/shared/api/customer-app.types';
import { useApiHealth, useRetryWhenApiOnline } from '@/shared/api/useApiHealth';
import { shouldHidePageErrorForOfflineApi } from '@/shared/components/ApiHealthBanner';
import {
  CustomerFormModal,
  FormModalFooter,
  FormModalLabel,
  FormModalTip,
} from '@/shared/components/CustomerFormModal';
import '@/shared/components/EntryPage.css';
import { useAuthStore } from '@/shared/auth/auth.store';

type AddressFormValues = {
  label: string;
  recipientName?: string;
  phone?: string;
  addressLine: string;
  ward?: string;
  district?: string;
  province?: string;
  isDefault?: boolean;
};

function formatAddressLine(address: CustomerAddress) {
  return [address.addressLine, address.ward, address.district, address.province].filter(Boolean).join(', ');
}

export function AddressesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const profile = useAuthStore((s) => s.profile);
  const { online } = useApiHealth();
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [items, setItems] = useState<CustomerAddress[]>([]);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<CustomerAddress | null>(null);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<AddressFormValues>();

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setItems(await fetchAddresses());
    } catch (error) {
      setItems([]);
      setLoadError(getApiErrorMessage(error, t('addresses.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  useRetryWhenApiOnline(() => load());

  const openCreate = () => {
    setEditing(null);
    form.setFieldsValue({
      label: t('addresses.defaultLabel'),
      recipientName: profile?.fullName ?? '',
      phone: profile?.phone ?? '',
      addressLine: '',
      ward: '',
      district: '',
      province: '',
      isDefault: items.length === 0,
    });
    setModalOpen(true);
  };

  const openEdit = (address: CustomerAddress) => {
    setEditing(address);
    form.setFieldsValue({
      label: address.label,
      recipientName: address.recipientName ?? '',
      phone: address.phone ?? '',
      addressLine: address.addressLine,
      ward: address.ward ?? '',
      district: address.district ?? '',
      province: address.province ?? '',
      isDefault: address.isDefault,
    });
    setModalOpen(true);
  };

  const onSave = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);
      if (editing) {
        await updateAddress(editing.id, {
          label: values.label.trim(),
          recipientName: values.recipientName?.trim() || undefined,
          phone: values.phone?.trim() || undefined,
          addressLine: values.addressLine.trim(),
          ward: values.ward?.trim() || undefined,
          district: values.district?.trim() || undefined,
          province: values.province?.trim() || undefined,
          isDefault: Boolean(values.isDefault),
        });
        message.success(t('addresses.updated'));
      } else {
        await createAddress({
          label: values.label.trim(),
          recipientName: values.recipientName?.trim() || undefined,
          phone: values.phone?.trim() || undefined,
          addressLine: values.addressLine.trim(),
          ward: values.ward?.trim() || undefined,
          district: values.district?.trim() || undefined,
          province: values.province?.trim() || undefined,
          isDefault: Boolean(values.isDefault),
        });
        message.success(t('addresses.added'));
      }
      setModalOpen(false);
      await load();
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) return;
      message.error(getApiErrorMessage(error, t('addresses.saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  const onDelete = async (id: string) => {
    try {
      await deleteAddress(id);
      message.success(t('addresses.deleted'));
      await load();
    } catch (error) {
      message.error(getApiErrorMessage(error, t('addresses.deleteFailed')));
    }
  };

  if (loading) {
    return (
      <div className="entry-page">
        <div className="entry-page-loading">
          <Spin />
        </div>
      </div>
    );
  }

  return (
    <div className="entry-page">
      <button type="button" className="entry-page-home" onClick={() => navigate('/')}>
        <ArrowLeftOutlined />
        {t('common.backHome')}
      </button>

      <h1 className="entry-page-title">{t('addresses.title')}</h1>
      <p className="entry-page-intro">{t('addresses.intro')}</p>

      {loadError && !shouldHidePageErrorForOfflineApi(loadError, online) ? (
        <div className="entry-card">
          <div className="entry-page-error">{loadError}</div>
          <div className="entry-actions" style={{ marginTop: 10 }}>
            <button type="button" className="entry-btn entry-btn--ghost" onClick={() => void load()}>
              {t('common.retry')}
            </button>
          </div>
        </div>
      ) : null}

      <button type="button" className="entry-btn entry-btn--primary entry-open-create" onClick={openCreate}>
        <PlusOutlined />
        {t('addresses.add')}
      </button>

      {items.length === 0 ? (
        <div className="entry-empty" style={{ marginTop: 14 }}>
          <InboxOutlined className="entry-empty-icon" />
          <span>{t('addresses.empty')}</span>
        </div>
      ) : (
        <div className="entry-list">
          {items.map((address) => (
            <article
              key={address.id}
              className={`entry-list-card${address.isDefault ? ' entry-list-card--active' : ''}`}
              style={{ cursor: 'default' }}
            >
              <div className="entry-list-card-top">
                <HomeOutlined style={{ color: '#0f766e' }} />
                <span className="entry-list-card-title">{address.label}</span>
                {address.isDefault ? (
                  <span className="entry-status entry-status--success">{t('addresses.default')}</span>
                ) : null}
              </div>
              {address.recipientName ? (
                <div className="entry-list-card-sub">{address.recipientName}</div>
              ) : null}
              {address.phone ? <div className="entry-list-card-sub">{address.phone}</div> : null}
              <div className="entry-detail-notes">{formatAddressLine(address)}</div>
              <div className="entry-actions" style={{ marginTop: 12, flexDirection: 'row' }}>
                <button
                  type="button"
                  className="entry-btn entry-btn--ghost"
                  style={{ flex: 1 }}
                  onClick={() => openEdit(address)}
                >
                  <EditOutlined />
                  {t('common.edit')}
                </button>
                <Popconfirm
                  title={t('addresses.confirmDelete')}
                  okText={t('common.delete')}
                  cancelText={t('common.cancel')}
                  onConfirm={() => void onDelete(address.id)}
                >
                  <button type="button" className="entry-btn entry-btn--danger" style={{ flex: 1 }}>
                    <DeleteOutlined />
                    {t('common.delete')}
                  </button>
                </Popconfirm>
              </div>
            </article>
          ))}
        </div>
      )}

      <div style={{ marginTop: 14 }}>
        <Link className="entry-page-link" to="/profile">
          {t('addresses.backToProfile')}
        </Link>
      </div>

      <CustomerFormModal
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        icon={<EnvironmentOutlined />}
        title={editing ? t('addresses.modalEdit') : t('addresses.modalAdd')}
        subtitle={t('addresses.modalSub')}
        footer={
          <FormModalFooter
            onCancel={() => setModalOpen(false)}
            onOk={() => void onSave()}
            confirmLoading={saving}
          />
        }
      >
        <Form form={form} layout="vertical" className="cfm-form" requiredMark={false}>
          <Form.Item
            name="label"
            label={
              <FormModalLabel icon={<HomeOutlined />} required>
                {t('addresses.label')}
              </FormModalLabel>
            }
            rules={[{ required: true, message: t('addresses.labelRequired') }]}
          >
            <Input size="large" placeholder={t('addresses.labelPlaceholder')} />
          </Form.Item>
          <Form.Item
            name="recipientName"
            label={<FormModalLabel icon={<UserOutlined />}>{t('addresses.recipient')}</FormModalLabel>}
          >
            <Input size="large" />
          </Form.Item>
          <Form.Item
            name="phone"
            label={<FormModalLabel icon={<PhoneOutlined />}>{t('addresses.phone')}</FormModalLabel>}
          >
            <Input size="large" inputMode="tel" />
          </Form.Item>
          <Form.Item
            name="addressLine"
            label={
              <FormModalLabel icon={<EnvironmentOutlined />} required>
                {t('addresses.address')}
              </FormModalLabel>
            }
            rules={[{ required: true, message: t('addresses.addressRequired') }]}
          >
            <Input.TextArea rows={2} placeholder={t('addresses.addressPlaceholder')} />
          </Form.Item>
          <Form.Item
            name="ward"
            label={<FormModalLabel icon={<EnvironmentOutlined />}>{t('addresses.ward')}</FormModalLabel>}
          >
            <Input size="large" />
          </Form.Item>
          <Form.Item
            name="district"
            label={<FormModalLabel icon={<EnvironmentOutlined />}>{t('addresses.district')}</FormModalLabel>}
          >
            <Input size="large" />
          </Form.Item>
          <Form.Item
            name="province"
            label={<FormModalLabel icon={<EnvironmentOutlined />}>{t('addresses.province')}</FormModalLabel>}
          >
            <Input size="large" />
          </Form.Item>
          <FormModalTip
            icon={<StarOutlined />}
            title={t('addresses.setDefault')}
            subtitle={t('addresses.setDefaultHint')}
            action={
              <Form.Item name="isDefault" valuePropName="checked" noStyle>
                <Switch checkedChildren={t('addresses.yes')} unCheckedChildren={t('addresses.no')} />
              </Form.Item>
            }
          />
        </Form>
      </CustomerFormModal>
    </div>
  );
}
