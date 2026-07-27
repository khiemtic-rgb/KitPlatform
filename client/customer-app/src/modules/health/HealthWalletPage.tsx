import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  Button,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Spin,
  Upload,
  message,
} from 'antd';
import {
  BellOutlined,
  CalendarOutlined,
  ColumnHeightOutlined,
  DeleteOutlined,
  EditOutlined,
  FileTextOutlined,
  LineChartOutlined,
  LinkOutlined,
  MedicineBoxOutlined,
  PlusOutlined,
  SafetyCertificateOutlined,
  UploadOutlined,
  UserOutlined,
} from '@ant-design/icons';
import type { UploadFile } from 'antd/es/upload/interface';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import {
  createCareReminder,
  createHealthRecord,
  deleteHealthRecord,
  fetchCareReminders,
  fetchFamilyMembers,
  fetchHealthRecords,
  getApiErrorMessage,
  markCareReminderDone,
  updateHealthRecord,
  uploadHealthRecordAttachment,
} from '@/shared/api/customer-app.api';
import {
  CARE_REMINDER_TYPE_LABELS,
  HEALTH_RECORD_TYPE_LABELS,
  VITAL_RECORD_TYPES,
  type CareReminder,
  type FamilyMember,
  type HealthRecord,
  type HealthRecordAttachment,
} from '@/shared/api/customer-app.types';
import {
  CustomerFormModal,
  FormModalFooter,
  FormModalLabel,
} from '@/shared/components/CustomerFormModal';
import i18n from '@/shared/i18n';
import { useCustomerLabels } from '@/shared/i18n/useCustomerLabels';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import { withCustomerUploadAuth } from '@/shared/utils/upload-url';
import './HealthWalletPage.css';

type HealthTab = 'vitals' | 'records' | 'care';

const MAX_ATTACHMENT_BYTES = 5 * 1024 * 1024;

function parseMeta(record: HealthRecord): Record<string, unknown> {
  try {
    return JSON.parse(record.metadataJson || '{}') as Record<string, unknown>;
  } catch {
    return {};
  }
}

async function filesToAttachments(files: UploadFile[]): Promise<HealthRecordAttachment[]> {
  const attachments: HealthRecordAttachment[] = [];
  for (const file of files) {
    const raw = file.originFileObj;
    if (!raw) continue;
    if (raw.size > MAX_ATTACHMENT_BYTES) {
      throw new Error(i18n.t('health.fileTooLarge', { name: raw.name }));
    }
    const uploaded = await uploadHealthRecordAttachment(raw);
    attachments.push({
      fileName: uploaded.fileName,
      mimeType: uploaded.mimeType,
      url: uploaded.url,
    });
  }
  return attachments;
}

export function HealthWalletPage() {
  const { t } = useTranslation();
  const { branding } = useCustomerBranding();
  const { healthRecordType, careReminderType } = useCustomerLabels();
  const [records, setRecords] = useState<HealthRecord[]>([]);
  const [careReminders, setCareReminders] = useState<CareReminder[]>([]);
  const [family, setFamily] = useState<FamilyMember[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<HealthTab>('vitals');
  const [recordModal, setRecordModal] = useState(false);
  const [vitalsModal, setVitalsModal] = useState(false);
  const [careModal, setCareModal] = useState(false);
  const [editingRecord, setEditingRecord] = useState<HealthRecord | null>(null);
  const [recordFiles, setRecordFiles] = useState<UploadFile[]>([]);
  const [recordForm] = Form.useForm();
  const [vitalsForm] = Form.useForm();
  const [careForm] = Form.useForm();

  const recordOptions = useMemo(
    () =>
      Object.keys(HEALTH_RECORD_TYPE_LABELS)
        .filter((value) => !VITAL_RECORD_TYPES.includes(value as (typeof VITAL_RECORD_TYPES)[number]))
        .map((value) => ({ value, label: healthRecordType(value) })),
    [healthRecordType],
  );

  const vitalOptions = useMemo(
    () =>
      VITAL_RECORD_TYPES.map((value) => ({
        value,
        label: healthRecordType(value),
      })),
    [healthRecordType],
  );

  const careReminderOptions = useMemo(
    () =>
      Object.keys(CARE_REMINDER_TYPE_LABELS).map((value) => ({
        value,
        label: careReminderType(value),
      })),
    [careReminderType],
  );

  const vitalsRecords = useMemo(
    () => records.filter((r) => VITAL_RECORD_TYPES.includes(r.recordType as (typeof VITAL_RECORD_TYPES)[number])),
    [records],
  );

  const documentRecords = useMemo(
    () => records.filter((r) => !VITAL_RECORD_TYPES.includes(r.recordType as (typeof VITAL_RECORD_TYPES)[number])),
    [records],
  );

  const familyName = (id: string | null) => {
    if (!id) return t('health.self');
    return family.find((f) => f.id === id)?.fullName ?? t('health.familyMember');
  };

  const openCareFromRecord = (record: HealthRecord) => {
    careForm.setFieldsValue({
      familyMemberId: record.familyMemberId ?? undefined,
      healthRecordId: record.id,
      reminderType: 'visit',
      title: t('health.followUpTitle', { title: record.title }),
      remindAt: dayjs().add(7, 'day'),
      note: record.providerName ? t('health.facilityNote', { name: record.providerName }) : undefined,
    });
    setCareModal(true);
  };

  const openEditRecord = (record: HealthRecord) => {
    setEditingRecord(record);
    const isVital = VITAL_RECORD_TYPES.includes(record.recordType as (typeof VITAL_RECORD_TYPES)[number]);
    if (isVital) {
      let meta: Record<string, unknown> = {};
      try {
        meta = JSON.parse(record.metadataJson || '{}') as Record<string, unknown>;
      } catch {
        meta = {};
      }
      vitalsForm.setFieldsValue({
        familyMemberId: record.familyMemberId ?? undefined,
        recordType: record.recordType,
        recordedAt: dayjs(record.recordedAt),
        weightKg: meta.weightKg,
        heightCm: meta.heightCm,
        systolic: meta.systolic,
        diastolic: meta.diastolic,
        glucoseValue: meta.value,
        glucoseUnit: meta.unit ?? 'mmol/L',
      });
      setVitalsModal(true);
      return;
    }

    recordForm.setFieldsValue({
      familyMemberId: record.familyMemberId ?? undefined,
      recordType: record.recordType,
      title: record.title,
      summary: record.summary,
      providerName: record.providerName,
      recordedAt: dayjs(record.recordedAt),
    });
    setRecordFiles([]);
    setRecordModal(true);
  };

  const closeRecordModal = () => {
    setRecordModal(false);
    setEditingRecord(null);
    setRecordFiles([]);
    recordForm.resetFields();
  };

  const closeVitalsModal = () => {
    setVitalsModal(false);
    setEditingRecord(null);
    vitalsForm.resetFields();
  };

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [recordsResult, careResult, familyResult] = await Promise.allSettled([
        fetchHealthRecords(),
        fetchCareReminders(true),
        fetchFamilyMembers(),
      ]);

      if (recordsResult.status === 'fulfilled') {
        setRecords(recordsResult.value);
      } else {
        message.error(getApiErrorMessage(recordsResult.reason, t('health.loadRecordsFailed')));
      }

      if (careResult.status === 'fulfilled') {
        setCareReminders(careResult.value.filter((c) => !c.isDone));
      } else {
        message.error(getApiErrorMessage(careResult.reason, t('health.loadCareFailed')));
      }

      if (familyResult.status === 'fulfilled') {
        setFamily(familyResult.value.filter((f) => f.status === 1));
      }
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const familyOptions = useMemo(
    () => [
      { value: undefined, label: t('health.self') },
      ...family.map((f) => ({ value: f.id, label: f.fullName })),
    ],
    [family, t],
  );

  const onCreateRecord = async () => {
    const values = await recordForm.validateFields();
    try {
      const attachments = editingRecord
        ? editingRecord.attachments
        : await filesToAttachments(recordFiles);
      const payload = {
        familyMemberId: values.familyMemberId,
        recordType: values.recordType,
        title: values.title,
        summary: values.summary,
        providerName: values.providerName,
        recordedAt: (values.recordedAt as dayjs.Dayjs).toISOString(),
        attachmentsJson: JSON.stringify(attachments),
      };

      if (editingRecord) {
        await updateHealthRecord(editingRecord.id, payload);
        message.success(t('health.recordUpdated'));
      } else {
        await createHealthRecord(payload);
        message.success(t('health.recordAdded'));
      }

      closeRecordModal();
      await load();
    } catch (error) {
      message.error(error instanceof Error ? error.message : getApiErrorMessage(error, t('health.saveRecordFailed')));
    }
  };

  const onCreateVital = async () => {
    const values = await vitalsForm.validateFields();
    const recordedAt = (values.recordedAt as dayjs.Dayjs).toISOString();
    let title = '';
    let summary = '';
    let metadata: Record<string, unknown> = {};

    if (values.recordType === 'bmi') {
      const heightCm = Number(values.heightCm);
      const weightKg = Number(values.weightKg);
      const heightM = heightCm / 100;
      const bmi = heightM > 0 ? Math.round((weightKg / (heightM * heightM)) * 10) / 10 : 0;
      title = `BMI ${bmi}`;
      summary = `${weightKg} kg · ${heightCm} cm`;
      metadata = { weightKg, heightCm, bmi };
    } else if (values.recordType === 'blood_pressure') {
      const systolic = Number(values.systolic);
      const diastolic = Number(values.diastolic);
      title = `HA ${systolic}/${diastolic}`;
      summary = `${systolic}/${diastolic} mmHg`;
      metadata = { systolic, diastolic, unit: 'mmHg' };
    } else {
      const value = Number(values.glucoseValue);
      const unit = values.glucoseUnit ?? 'mmol/L';
      title = `${healthRecordType('blood_glucose')} ${value}`;
      summary = `${value} ${unit}`;
      metadata = { value, unit };
    }

    try {
      const payload = {
        familyMemberId: values.familyMemberId,
        recordType: values.recordType,
        title,
        summary,
        recordedAt,
        metadataJson: JSON.stringify(metadata),
      };

      if (editingRecord) {
        await updateHealthRecord(editingRecord.id, payload);
        message.success(t('health.recordUpdated'));
      } else {
        await createHealthRecord(payload);
        message.success(t('health.vitalSaved'));
      }

      closeVitalsModal();
      await load();
    } catch (error) {
      message.error(getApiErrorMessage(error, t('health.saveVitalFailed')));
    }
  };

  const onCreateCare = async () => {
    const values = await careForm.validateFields();
    try {
      await createCareReminder({
        familyMemberId: values.familyMemberId,
        healthRecordId: values.healthRecordId,
        reminderType: values.reminderType,
        title: values.title,
        note: values.note,
        remindAt: (values.remindAt as dayjs.Dayjs).toISOString(),
      });
      message.success(t('health.careAdded'));
      setCareModal(false);
      careForm.resetFields();
      await load();
    } catch (error) {
      message.error(getApiErrorMessage(error, t('health.saveCareFailed')));
    }
  };

  const onDeleteRecord = async (id: string) => {
    try {
      await deleteHealthRecord(id);
      setRecords((prev) => prev.filter((r) => r.id !== id));
      message.success(t('health.recordDeleted'));
    } catch (error) {
      message.error(getApiErrorMessage(error, t('health.deleteFailed')));
    }
  };

  const onDoneCare = async (item: CareReminder) => {
    try {
      await markCareReminderDone(item);
      setCareReminders((prev) => prev.filter((r) => r.id !== item.id));
      message.success(t('health.careDone'));
    } catch (error) {
      message.error(getApiErrorMessage(error, t('health.updateFailed')));
    }
  };

  const renderVitalCard = (item: HealthRecord) => {
    const meta = parseMeta(item);
    const typeLabel = healthRecordType(item.recordType);
    const bmi = meta.bmi != null ? String(meta.bmi) : null;
    const weight = meta.weightKg != null ? `${meta.weightKg} kg` : null;
    const height = meta.heightCm != null ? `${meta.heightCm} cm` : null;
    const bp =
      meta.systolic != null && meta.diastolic != null
        ? `${meta.systolic}/${meta.diastolic} mmHg`
        : null;
    const glucose = meta.value != null ? `${meta.value} ${meta.unit ?? 'mmol/L'}` : null;

    return (
      <div key={item.id} className="health-hub-card">
        <div className="health-hub-card-top">
          <span className="health-hub-card-icon">
            <LineChartOutlined />
          </span>
          <div className="health-hub-card-main">
            <div className="health-hub-card-row">
              <span className="health-hub-type">{typeLabel}</span>
              <span className="health-hub-card-title">{familyName(item.familyMemberId)}</span>
              {bmi ? <span className="health-hub-card-bmi">BMI {bmi}</span> : null}
              {!bmi && (bp || glucose) ? (
                <span className="health-hub-card-bmi">{bp || glucose}</span>
              ) : null}
            </div>
            <div className="health-hub-card-date">
              <CalendarOutlined />
              {dayjs(item.recordedAt).format('DD/MM/YYYY')}
            </div>
          </div>
        </div>
        {(weight || height || bp || glucose) && item.recordType === 'bmi' ? (
          <div className="health-hub-stats">
            {weight ? (
              <span className="health-hub-stat">
                <SafetyCertificateOutlined />
                {weight}
              </span>
            ) : null}
            {weight && height ? <span className="health-hub-stat-divider" /> : null}
            {height ? (
              <span className="health-hub-stat">
                <ColumnHeightOutlined />
                {height}
              </span>
            ) : null}
          </div>
        ) : null}
        {item.recordType !== 'bmi' && (bp || glucose || item.summary) ? (
          <div className="health-hub-stats">
            <span className="health-hub-stat">{bp || glucose || item.summary}</span>
          </div>
        ) : null}
        <div className="health-hub-actions">
          <button type="button" className="health-hub-btn health-hub-btn--edit" onClick={() => openEditRecord(item)}>
            <EditOutlined />
            {t('health.editRecord')}
          </button>
          <button
            type="button"
            className="health-hub-btn health-hub-btn--danger"
            onClick={() => void onDeleteRecord(item.id)}
          >
            <DeleteOutlined />
            {t('common.delete')}
          </button>
        </div>
      </div>
    );
  };

  const renderDocCard = (item: HealthRecord) => (
    <div key={item.id} className="health-hub-card">
      <div className="health-hub-card-top">
        <span className="health-hub-card-icon">
          <UserOutlined />
        </span>
        <div className="health-hub-card-main">
          <div className="health-hub-card-row">
            <span className="health-hub-type">{healthRecordType(item.recordType)}</span>
            <span className="health-hub-card-title">{item.title}</span>
          </div>
          <div className="health-hub-card-date">
            <CalendarOutlined />
            {dayjs(item.recordedAt).format('DD/MM/YYYY')}
            {item.providerName ? ` · ${item.providerName}` : ''}
            {` · ${familyName(item.familyMemberId)}`}
          </div>
          {item.summary ? <div className="health-hub-card-date">{item.summary}</div> : null}
          {item.attachments.length > 0 ? (
            <Space direction="vertical" size={2} style={{ marginTop: 8 }}>
              {item.attachments.map((att, index) => {
                const href = att.url ? withCustomerUploadAuth(att.url) : att.dataUrl;
                return href ? (
                  <a key={`${item.id}-att-${index}`} href={href} target="_blank" rel="noopener noreferrer" style={{ fontSize: 13 }}>
                    📎 {att.fileName}
                  </a>
                ) : (
                  <span key={`${item.id}-att-${index}`} style={{ fontSize: 12, color: '#64748b' }}>
                    {att.fileName}
                  </span>
                );
              })}
            </Space>
          ) : null}
        </div>
      </div>
      <div className="health-hub-actions">
        <button type="button" className="health-hub-btn health-hub-btn--edit" onClick={() => openCareFromRecord(item)}>
          <BellOutlined />
          {t('health.followUpBtn')}
        </button>
        <button type="button" className="health-hub-btn health-hub-btn--edit" onClick={() => openEditRecord(item)}>
          <EditOutlined />
          {t('health.editRecord')}
        </button>
        <button
          type="button"
          className="health-hub-btn health-hub-btn--danger"
          onClick={() => void onDeleteRecord(item.id)}
        >
          <DeleteOutlined />
          {t('common.delete')}
        </button>
      </div>
    </div>
  );

  const vitalType = Form.useWatch('recordType', vitalsForm) ?? 'bmi';
  const headerStyle = {
    background: `linear-gradient(135deg, ${branding.primaryColor}, ${branding.secondaryColor})`,
  };

  const tabs: Array<{ key: HealthTab; label: string; icon: ReactNode }> = [
    { key: 'vitals', label: t('health.tabVitals'), icon: <LineChartOutlined /> },
    { key: 'records', label: t('health.tabRecords'), icon: <UserOutlined /> },
    { key: 'care', label: t('health.tabCare'), icon: <BellOutlined /> },
  ];

  const openAddPrimary = () => {
    if (activeTab === 'vitals') {
      setEditingRecord(null);
      vitalsForm.resetFields();
      setVitalsModal(true);
      return;
    }
    if (activeTab === 'records') {
      setEditingRecord(null);
      recordForm.resetFields();
      setRecordFiles([]);
      setRecordModal(true);
      return;
    }
    setCareModal(true);
  };

  return (
    <div className="health-hub">
      <header className="health-hub-header" style={headerStyle}>
        <div className="health-hub-header-inner">
          <div className="health-hub-brand">
            <BrandingLogo logoUrl={branding.logoUrl} />
            <div>
              <div className="health-hub-brand-title">{branding.appName}</div>
              <div className="health-hub-tagline">{branding.tagline || t('health.hubTagline')}</div>
            </div>
          </div>
        </div>
      </header>

      <div className="health-hub-sheet">
        <div className="health-hub-hero">
          <div className="health-hub-hero-copy">
            <h1 className="health-hub-hero-title">
              {t('health.title')}
              <SafetyCertificateOutlined />
            </h1>
            <p className="health-hub-hero-intro">{t('health.heroIntro')}</p>
          </div>
          <div className="health-hub-art" aria-hidden>
            <div className="health-hub-folder">
              <div className="health-hub-folder-paper" />
              <span className="health-hub-folder-badge">
                <SafetyCertificateOutlined />
              </span>
            </div>
            <span className="health-hub-folder-plus">
              <PlusOutlined />
            </span>
          </div>
        </div>

        <div className="health-hub-tabs" role="tablist">
          {tabs.map((tab) => (
            <button
              key={tab.key}
              type="button"
              role="tab"
              aria-selected={activeTab === tab.key}
              className={`health-hub-tab${activeTab === tab.key ? ' health-hub-tab--active' : ''}`}
              onClick={() => setActiveTab(tab.key)}
            >
              {tab.icon}
              {tab.label}
            </button>
          ))}
        </div>

        <div className="health-hub-cta">
          <div className="health-hub-cta-icon">
            {activeTab === 'care' ? <BellOutlined /> : activeTab === 'records' ? <UserOutlined /> : <LineChartOutlined />}
          </div>
          <div className="health-hub-cta-copy">
            <div className="health-hub-cta-title">
              {activeTab === 'vitals'
                ? t('health.trackVitalsTitle')
                : activeTab === 'records'
                  ? t('health.trackRecordsTitle')
                  : t('health.trackCareTitle')}
            </div>
            <div className="health-hub-cta-sub">
              {activeTab === 'vitals'
                ? t('health.trackVitalsSub')
                : activeTab === 'records'
                  ? t('health.trackRecordsSub')
                  : t('health.trackCareSub')}
            </div>
          </div>
          <button type="button" className="health-hub-cta-btn" onClick={openAddPrimary}>
            <PlusOutlined />
            {activeTab === 'vitals'
              ? t('health.addVital')
              : activeTab === 'records'
                ? t('health.addRecord')
                : t('health.addCare')}
          </button>
        </div>

        <div className="health-hub-list-head">
          <h2 className="health-hub-list-title">
            {activeTab === 'vitals'
              ? t('health.listVitals')
              : activeTab === 'records'
                ? t('health.listRecords')
                : t('health.listCare')}
          </h2>
          <span className="health-hub-sort">{t('health.sortNewest')} ▾</span>
        </div>

        {loading ? (
          <div style={{ textAlign: 'center', padding: 48 }}>
            <Spin />
          </div>
        ) : activeTab === 'vitals' ? (
          vitalsRecords.length === 0 ? (
            <div className="health-hub-empty">{t('health.emptyVitals')}</div>
          ) : (
            <div className="health-hub-list">{vitalsRecords.map(renderVitalCard)}</div>
          )
        ) : activeTab === 'records' ? (
          documentRecords.length === 0 ? (
            <div className="health-hub-empty">{t('health.emptyRecords')}</div>
          ) : (
            <div className="health-hub-list">{documentRecords.map(renderDocCard)}</div>
          )
        ) : careReminders.length === 0 ? (
          <div className="health-hub-empty">{t('health.emptyCare')}</div>
        ) : (
          <div className="health-hub-list">
            {careReminders.map((item) => (
              <div key={item.id} className="health-hub-card">
                <div className="health-hub-card-top">
                  <span className="health-hub-card-icon">
                    <BellOutlined />
                  </span>
                  <div className="health-hub-card-main">
                    <div className="health-hub-card-row">
                      <span className="health-hub-type">{careReminderType(item.reminderType)}</span>
                      <span className="health-hub-card-title">{item.title}</span>
                    </div>
                    <div className="health-hub-card-date">
                      <CalendarOutlined />
                      {dayjs(item.remindAt).format('DD/MM/YYYY HH:mm')} · {familyName(item.familyMemberId)}
                    </div>
                    {item.note ? <div className="health-hub-card-date">{item.note}</div> : null}
                  </div>
                </div>
                <div className="health-hub-actions">
                  <button
                    type="button"
                    className="health-hub-btn health-hub-btn--edit"
                    onClick={() => void onDoneCare(item)}
                  >
                    {t('health.careDoneBtn')}
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <CustomerFormModal
        open={recordModal}
        onCancel={closeRecordModal}
        icon={<FileTextOutlined />}
        title={editingRecord ? t('health.modalEditRecord') : t('health.modalAddRecord')}
        subtitle={t('health.modalRecordSub')}
        footer={
          <FormModalFooter onCancel={closeRecordModal} onOk={() => void onCreateRecord()} />
        }
      >
        <Form
          form={recordForm}
          layout="vertical"
          className="cfm-form"
          requiredMark={false}
          initialValues={{ recordType: 'prescription', recordedAt: dayjs() }}
        >
          <Form.Item
            name="familyMemberId"
            label={<FormModalLabel icon={<UserOutlined />}>{t('health.forWho')}</FormModalLabel>}
          >
            <Select size="large" allowClear options={familyOptions} placeholder={t('health.self')} />
          </Form.Item>
          <Form.Item
            name="recordType"
            label={
              <FormModalLabel icon={<MedicineBoxOutlined />} required>
                {t('health.type')}
              </FormModalLabel>
            }
            rules={[{ required: true }]}
          >
            <Select size="large" options={recordOptions} />
          </Form.Item>
          <Form.Item
            name="title"
            label={
              <FormModalLabel icon={<FileTextOutlined />} required>
                {t('health.titleLabel')}
              </FormModalLabel>
            }
            rules={[{ required: true }]}
          >
            <Input size="large" placeholder={t('health.titlePlaceholder')} />
          </Form.Item>
          <Form.Item
            name="providerName"
            label={<FormModalLabel icon={<SafetyCertificateOutlined />}>{t('health.provider')}</FormModalLabel>}
          >
            <Input size="large" placeholder={t('health.providerPlaceholder')} />
          </Form.Item>
          <Form.Item
            name="recordedAt"
            label={
              <FormModalLabel icon={<CalendarOutlined />} required>
                {t('health.date')}
              </FormModalLabel>
            }
            rules={[{ required: true }]}
          >
            <DatePicker size="large" style={{ width: '100%' }} format="DD/MM/YYYY" />
          </Form.Item>
          <Form.Item
            name="summary"
            label={<FormModalLabel icon={<FileTextOutlined />}>{t('health.note')}</FormModalLabel>}
          >
            <Input.TextArea rows={3} />
          </Form.Item>
          <Form.Item
            label={<FormModalLabel icon={<UploadOutlined />}>{t('health.attachments')}</FormModalLabel>}
          >
            <Upload
              fileList={recordFiles}
              beforeUpload={() => false}
              onChange={({ fileList }) => setRecordFiles(fileList.slice(-3))}
              maxCount={3}
              accept="image/*,.pdf"
              disabled={Boolean(editingRecord)}
            >
              <Button icon={<UploadOutlined />} disabled={Boolean(editingRecord)}>
                {t('health.chooseFile')}
              </Button>
            </Upload>
          </Form.Item>
        </Form>
      </CustomerFormModal>

      <CustomerFormModal
        open={vitalsModal}
        onCancel={closeVitalsModal}
        icon={<LineChartOutlined />}
        title={t('health.modalAddVital')}
        subtitle={t('health.modalVitalSub')}
        footer={
          <FormModalFooter onCancel={closeVitalsModal} onOk={() => void onCreateVital()} />
        }
      >
        <Form
          form={vitalsForm}
          layout="vertical"
          className="cfm-form"
          requiredMark={false}
          initialValues={{ recordType: 'bmi', recordedAt: dayjs(), glucoseUnit: 'mmol/L' }}
        >
          <Form.Item
            name="familyMemberId"
            label={<FormModalLabel icon={<UserOutlined />}>{t('health.forWho')}</FormModalLabel>}
          >
            <Select size="large" allowClear options={familyOptions} placeholder={t('health.self')} />
          </Form.Item>
          <Form.Item
            name="recordType"
            label={
              <FormModalLabel icon={<LineChartOutlined />} required>
                {t('health.vitalType')}
              </FormModalLabel>
            }
            rules={[{ required: true }]}
          >
            <Select size="large" options={vitalOptions} />
          </Form.Item>
          <Form.Item
            name="recordedAt"
            label={
              <FormModalLabel icon={<CalendarOutlined />} required>
                {t('health.measureDate')}
              </FormModalLabel>
            }
            rules={[{ required: true }]}
          >
            <DatePicker size="large" style={{ width: '100%' }} format="DD/MM/YYYY" />
          </Form.Item>
          {vitalType === 'bmi' ? (
            <>
              <Form.Item
                name="weightKg"
                label={
                  <FormModalLabel icon={<ColumnHeightOutlined />} required>
                    {t('health.weight')}
                  </FormModalLabel>
                }
                rules={[{ required: true }]}
              >
                <InputNumber size="large" min={1} max={300} style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item
                name="heightCm"
                label={
                  <FormModalLabel icon={<ColumnHeightOutlined />} required>
                    {t('health.height')}
                  </FormModalLabel>
                }
                rules={[{ required: true }]}
              >
                <InputNumber size="large" min={50} max={250} style={{ width: '100%' }} />
              </Form.Item>
            </>
          ) : null}
          {vitalType === 'blood_pressure' ? (
            <>
              <Form.Item
                name="systolic"
                label={
                  <FormModalLabel icon={<LineChartOutlined />} required>
                    {t('health.systolic')}
                  </FormModalLabel>
                }
                rules={[{ required: true }]}
              >
                <InputNumber size="large" min={60} max={250} style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item
                name="diastolic"
                label={
                  <FormModalLabel icon={<LineChartOutlined />} required>
                    {t('health.diastolic')}
                  </FormModalLabel>
                }
                rules={[{ required: true }]}
              >
                <InputNumber size="large" min={40} max={150} style={{ width: '100%' }} />
              </Form.Item>
            </>
          ) : null}
          {vitalType === 'blood_glucose' ? (
            <>
              <Form.Item
                name="glucoseValue"
                label={
                  <FormModalLabel icon={<LineChartOutlined />} required>
                    {t('health.glucoseValue')}
                  </FormModalLabel>
                }
                rules={[{ required: true }]}
              >
                <InputNumber size="large" min={1} max={40} step={0.1} style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item
                name="glucoseUnit"
                label={<FormModalLabel icon={<MedicineBoxOutlined />}>{t('health.unit')}</FormModalLabel>}
              >
                <Select
                  size="large"
                  options={[
                    { value: 'mmol/L', label: 'mmol/L' },
                    { value: 'mg/dL', label: 'mg/dL' },
                  ]}
                />
              </Form.Item>
            </>
          ) : null}
        </Form>
      </CustomerFormModal>

      <CustomerFormModal
        open={careModal}
        onCancel={() => setCareModal(false)}
        icon={<BellOutlined />}
        title={t('health.modalCare')}
        subtitle={t('health.modalCareSub')}
        footer={
          <FormModalFooter
            onCancel={() => setCareModal(false)}
            onOk={() => void onCreateCare()}
          />
        }
      >
        <Form
          form={careForm}
          layout="vertical"
          className="cfm-form"
          requiredMark={false}
          initialValues={{ reminderType: 'visit', remindAt: dayjs().add(7, 'day') }}
        >
          <Form.Item
            name="familyMemberId"
            label={<FormModalLabel icon={<UserOutlined />}>{t('health.forWho')}</FormModalLabel>}
          >
            <Select size="large" allowClear options={familyOptions} placeholder={t('health.self')} />
          </Form.Item>
          <Form.Item
            name="reminderType"
            label={
              <FormModalLabel icon={<BellOutlined />} required>
                {t('health.careReminderType')}
              </FormModalLabel>
            }
            rules={[{ required: true }]}
          >
            <Select size="large" options={careReminderOptions} />
          </Form.Item>
          <Form.Item
            name="healthRecordId"
            label={<FormModalLabel icon={<LinkOutlined />}>{t('health.linkRecord')}</FormModalLabel>}
          >
            <Select
              size="large"
              allowClear
              placeholder={t('health.linkRecordPlaceholder')}
              options={documentRecords.map((r) => ({ value: r.id, label: r.title }))}
            />
          </Form.Item>
          <Form.Item
            name="title"
            label={
              <FormModalLabel icon={<FileTextOutlined />} required>
                {t('health.titleLabel')}
              </FormModalLabel>
            }
            rules={[{ required: true }]}
          >
            <Input size="large" placeholder={t('health.careTitlePlaceholder')} />
          </Form.Item>
          <Form.Item
            name="remindAt"
            label={
              <FormModalLabel icon={<CalendarOutlined />} required>
                {t('health.remindAt')}
              </FormModalLabel>
            }
            rules={[{ required: true }]}
          >
            <DatePicker showTime size="large" style={{ width: '100%' }} format="DD/MM/YYYY HH:mm" />
          </Form.Item>
          <Form.Item
            name="note"
            label={<FormModalLabel icon={<FileTextOutlined />}>{t('health.note')}</FormModalLabel>}
          >
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </CustomerFormModal>
    </div>
  );
}
