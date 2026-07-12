import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import {
  App,
  Alert,
  AutoComplete,
  Badge,
  Button,
  Card,
  Drawer,
  Dropdown,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { MenuProps } from 'antd';
import {
  PlusOutlined,
  ReloadOutlined,
  EyeOutlined,
  SaveOutlined,
  EditOutlined,
  CheckCircleOutlined,
  FileAddOutlined,
  SendOutlined,
  SafetyCertificateOutlined,
  FilePdfOutlined,
  CloseOutlined,
  DeleteOutlined,
  MoreOutlined,
  StopOutlined,
  ClockCircleOutlined,
  UserOutlined,
  MedicineBoxOutlined,
  FlagOutlined,
  ApartmentOutlined,
  FileTextOutlined,
  SolutionOutlined,
  PhoneOutlined,
  FilterOutlined,
  SearchOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchProducts } from '@/shared/api/catalog.api';
import { fetchCustomers } from '@/shared/api/customer-admin.api';
import { searchNationalDrugs } from '@/shared/api/national-drug.api';
import {
  addClinicVisitNote,
  cancelClinicPrescription,
  createClinicPrescription,
  createClinicVisit,
  downloadClinicPrescriptionPdf,
  fetchClinicPrescriptions,
  fetchClinicProviders,
  fetchClinicVisit,
  fetchClinicVisitNotes,
  fetchClinicVisits,
  finalizeClinicPrescription,
  sendClinicPrescriptionToPharmacy,
  signClinicPrescription,
  updateClinicPrescription,
  updateClinicVisit,
  type ClinicPrescription,
  type ClinicProvider,
  type ClinicVisit,
  type ClinicVisitNote,
} from '@/shared/api/clinic.api';
import {
  fetchClinicMemberships,
  fetchConnectOrgLinks,
  fetchPartnerPharmacyProducts,
  type ConnectDoctorMembership,
  type ConnectOrgLink,
} from '@/shared/api/connect.api';
import {
  connectBadgeForProvider,
  suggestProviderIdFromConnect,
} from '@/modules/clinic/provider-connect-match';

const STATUS_COLOR: Record<string, string> = {
  open: 'orange',
  closed: 'green',
  cancelled: 'default',
};

const RX_STATUS_COLOR: Record<string, string> = {
  draft: 'blue',
  finalized: 'green',
  signed: 'cyan',
  cancelled: 'default',
};

type WalkInForm = {
  customerId: string;
  providerId?: string;
  chiefComplaint?: string;
  encounterModality: 'in_person' | 'remote_async';
};

type RxLineForm = {
  drugName: string;
  strength?: string;
  quantity: number;
  unit?: string;
  dosageInstruction?: string;
};

type RxForm = {
  providerId?: string;
  diagnosisText?: string;
  notes?: string;
  lines: RxLineForm[];
};

type VisitQueue = 'at_clinic' | 'remote' | 'closed';

function isRemoteModality(modality?: string) {
  return modality === 'remote_async' || modality === 'remote_video';
}

function colTitle(icon: ReactNode, label: string) {
  return (
    <Space size={6}>
      <span style={{ color: 'rgba(0,0,0,0.45)', display: 'inline-flex' }}>{icon}</span>
      <span>{label}</span>
    </Space>
  );
}

export function ClinicVisitsPage() {
  const { t } = useTranslation('clinic');
  const { message, modal } = App.useApp();
  const [searchParams, setSearchParams] = useSearchParams();
  const [items, setItems] = useState<ClinicVisit[]>([]);
  const [providers, setProviders] = useState<ClinicProvider[]>([]);
  const [connectMemberships, setConnectMemberships] = useState<ConnectDoctorMembership[]>([]);
  const [customerOptions, setCustomerOptions] = useState<{ value: string; label: string }[]>([]);
  const [listQueue, setListQueue] = useState<VisitQueue>('at_clinic');
  const [search, setSearch] = useState('');
  const [providerFilter, setProviderFilter] = useState<string>();
  const [statusFilter, setStatusFilter] = useState<string>();
  const [loading, setLoading] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<WalkInForm>();
  const [active, setActive] = useState<ClinicVisit | null>(null);
  const [notes, setNotes] = useState<ClinicVisitNote[]>([]);
  const [prescriptions, setPrescriptions] = useState<ClinicPrescription[]>([]);
  const [noteBody, setNoteBody] = useState('');
  const [detailSaving, setDetailSaving] = useState(false);
  const [detailForm] = Form.useForm<{
    chiefComplaint?: string;
    diagnosisSummary?: string;
    providerId?: string;
  }>();
  const [pharmacyPartners, setPharmacyPartners] = useState<ConnectOrgLink[]>([]);
  const [sendOpen, setSendOpen] = useState(false);
  const [sendRxId, setSendRxId] = useState<string | null>(null);
  const [sendSaving, setSendSaving] = useState(false);
  const [sendForm] = Form.useForm<{ pharmacyTenantId: string }>();
  const [rxOpen, setRxOpen] = useState(false);
  /** null = tạo mới; có id = sửa đơn nháp hiện có */
  const [editingRxId, setEditingRxId] = useState<string | null>(null);
  const [rxSaving, setRxSaving] = useState(false);
  const [rxForm] = Form.useForm<RxForm>();
  const [drugOptions, setDrugOptions] = useState<
    { value: string; label: string; strength?: string; unit?: string }[]
  >([]);
  const [drugSearching, setDrugSearching] = useState(false);

  const searchDrugs = useCallback(
    async (q: string) => {
      const query = q.trim();
      if (query.length < 2) {
        setDrugOptions([]);
        return;
      }
      setDrugSearching(true);
      try {
        const pharmacyId = active?.preferredPharmacyTenantId;
        // Bệnh nhân từ NT Connect → chỉ hiện SKU của NT đó (để NT bán được).
        if (pharmacyId) {
          const products = await fetchPartnerPharmacyProducts(pharmacyId, query).catch(
            () => [] as Awaited<ReturnType<typeof fetchPartnerPharmacyProducts>>,
          );
          setDrugOptions(
            products.map((p) => {
              const stock =
                p.stockAvailableQty > 0
                  ? ` · tồn ${Number(p.stockAvailableQty).toLocaleString('vi-VN')}`
                  : ' · hết hàng';
              const meta = [p.genericName, p.productCode].filter(Boolean).join(' · ');
              return {
                value: p.productName,
                label: `${p.productName}${meta ? ` — ${meta}` : ''}${stock}`,
                unit: p.defaultUnitName || undefined,
              };
            }),
          );
          return;
        }

        const [national, catalog] = await Promise.all([
          searchNationalDrugs({ search: query, page: 1, pageSize: 12 }).catch(() => ({
            items: [] as Awaited<ReturnType<typeof searchNationalDrugs>>['items'],
          })),
          fetchProducts({ search: query, page: 1, pageSize: 12, status: 1 }).catch(() => ({
            items: [] as Awaited<ReturnType<typeof fetchProducts>>['items'],
          })),
        ]);

        const seen = new Set<string>();
        const options: { value: string; label: string; strength?: string; unit?: string }[] = [];

        for (const p of catalog.items) {
          const name = p.productName?.trim();
          if (!name) continue;
          const key = name.toLowerCase();
          if (seen.has(key)) continue;
          seen.add(key);
          options.push({
            value: name,
            label: `${name}${p.genericName ? ` · ${p.genericName}` : ''}`,
            unit: p.saleUnitName || undefined,
          });
        }

        for (const d of national.items) {
          const name = d.productName?.trim();
          if (!name) continue;
          const key = name.toLowerCase();
          if (seen.has(key)) continue;
          seen.add(key);
          const meta = [d.activeIngredient, d.strength, d.drugCategoryLabel]
            .filter(Boolean)
            .join(' · ');
          options.push({
            value: name,
            label: meta ? `${name} — ${meta}` : name,
            strength: d.strength || undefined,
            unit: d.unitName || undefined,
          });
        }

        setDrugOptions(options);
      } finally {
        setDrugSearching(false);
      }
    },
    [active?.preferredPharmacyTenantId],
  );

  const loadProviders = useCallback(async () => {
    try {
      const [prov, mem] = await Promise.all([
        fetchClinicProviders(false),
        fetchClinicMemberships('active').catch(() => [] as ConnectDoctorMembership[]),
      ]);
      setProviders(prov);
      setConnectMemberships(mem);
      return { providers: prov, memberships: mem };
    } catch {
      setProviders([]);
      setConnectMemberships([]);
      return {
        providers: [] as ClinicProvider[],
        memberships: [] as ConnectDoctorMembership[],
      };
    }
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [open, closed, prov, mem] = await Promise.all([
        fetchClinicVisits({ status: 'open' }),
        fetchClinicVisits({ status: 'closed' }),
        fetchClinicProviders(false).catch(() => [] as ClinicProvider[]),
        fetchClinicMemberships('active').catch(() => [] as ConnectDoctorMembership[]),
      ]);
      const list = [...open, ...closed].sort(
        (a, b) => dayjs(b.startedAt).valueOf() - dayjs(a.startedAt).valueOf(),
      );
      setItems(list);
      setProviders(prov);
      setConnectMemberships(mem);
      try {
        const links = await fetchConnectOrgLinks('active');
        setPharmacyPartners(links.filter((l) => l.partnerOrgRole === 'pharmacy'));
      } catch {
        setPharmacyPartners([]);
      }
    } catch (error) {
      message.error(apiErrorMessage(error, t('visits.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const openWalkIn = async () => {
    form.resetFields();
    form.setFieldsValue({ encounterModality: 'in_person' });
    const { providers: prov, memberships: mem } = await loadProviders();
    const suggested = suggestProviderIdFromConnect(prov, mem);
    if (suggested) {
      form.setFieldsValue({ providerId: suggested });
    } else if (prov.length === 1) {
      form.setFieldsValue({ providerId: prov[0].id });
    }
    setCreateOpen(true);
  };

  const openVisit = useCallback(
    async (visit: ClinicVisit) => {
      setActive(visit);
      detailForm.setFieldsValue({
        chiefComplaint: visit.chiefComplaint,
        diagnosisSummary: visit.diagnosisSummary,
        providerId: visit.providerId,
      });
      try {
        const fresh = await fetchClinicVisit(visit.id);
        setActive(fresh);
        detailForm.setFieldsValue({
          chiefComplaint: fresh.chiefComplaint,
          diagnosisSummary: fresh.diagnosisSummary,
          providerId: fresh.providerId,
        });
        const [n, rx] = await Promise.all([
          fetchClinicVisitNotes(visit.id),
          fetchClinicPrescriptions(visit.id),
        ]);
        setNotes(n);
        setPrescriptions(rx);
      } catch {
        setNotes([]);
        setPrescriptions([]);
      }
    },
    [detailForm],
  );

  // Deep-link ?open=visitId (from appointments check-in)
  const openVisitId = searchParams.get('open');
  useEffect(() => {
    if (!openVisitId) return;
    let cancelled = false;
    void (async () => {
      try {
        const visit = await fetchClinicVisit(openVisitId);
        if (cancelled) return;
        setListQueue(
          isRemoteModality(visit.encounterModality)
            ? 'remote'
            : visit.visitStatus === 'closed'
              ? 'closed'
              : 'at_clinic',
        );
        await openVisit(visit);
        setSearchParams(
          (prev) => {
            const next = new URLSearchParams(prev);
            next.delete('open');
            return next;
          },
          { replace: true },
        );
      } catch {
        /* ignore */
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [openVisitId, openVisit, setSearchParams]);

  const closeRemoteVisitAfterRx = async (visit: ClinicVisit) => {
    if (!isRemoteModality(visit.encounterModality) || visit.visitStatus !== 'open') {
      return visit;
    }
    const updated = await updateClinicVisit(visit.id, { visitStatus: 'closed' });
    setActive(updated);
    setListQueue('remote');
    message.success(t('visits.remoteClosedAfterRx'));
    await load();
    return updated;
  };

  const searchCustomers = async (q: string) => {
    try {
      const result = await fetchCustomers({ search: q || undefined, page: 1, pageSize: 20 });
      setCustomerOptions(
        result.items.map((c) => ({
          value: c.id,
          label: `${c.fullName}${c.phone ? ` · ${c.phone}` : ''}`,
        })),
      );
    } catch {
      setCustomerOptions([]);
    }
  };

  const onWalkIn = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      const visit = await createClinicVisit({
        customerId: values.customerId,
        providerId: values.providerId,
        chiefComplaint: values.chiefComplaint,
        encounterModality: values.encounterModality || 'in_person',
      });
      message.success(t('visits.createSuccess'));
      setCreateOpen(false);
      form.resetFields();
      setListQueue(
        values.encounterModality === 'remote_async' ? 'remote' : 'at_clinic',
      );
      await load();
      await openVisit(visit);
    } catch (error) {
      message.error(apiErrorMessage(error, t('visits.createFailed')));
    } finally {
      setSaving(false);
    }
  };

  const onSaveDetail = async () => {
    if (!active) return;
    try {
      const values = await detailForm.validateFields();
      setDetailSaving(true);
      const updated = await updateClinicVisit(active.id, {
        chiefComplaint: values.chiefComplaint?.trim() ?? '',
        diagnosisSummary: values.diagnosisSummary?.trim() ?? '',
        ...(values.providerId ? { providerId: values.providerId } : {}),
      });
      setActive(updated);
      detailForm.setFieldsValue({
        chiefComplaint: updated.chiefComplaint,
        diagnosisSummary: updated.diagnosisSummary,
        providerId: updated.providerId,
      });
      setItems((prev) => prev.map((v) => (v.id === updated.id ? updated : v)));
      const bits = [
        updated.providerDisplayName || null,
        updated.chiefComplaint || null,
        updated.diagnosisSummary || null,
      ].filter(Boolean);
      message.success(
        bits.length > 0
          ? `${t('visits.saveSuccess')}: ${bits.join(' · ')}`
          : t('visits.saveSuccess'),
      );
      if (isRemoteModality(updated.encounterModality)) {
        setListQueue('remote');
      }
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) {
        message.warning(t('visits.saveInvalid'));
        return;
      }
      message.error(apiErrorMessage(error, t('visits.actionFailed')));
    } finally {
      setDetailSaving(false);
    }
  };

  const onCloseVisit = async () => {
    if (!active) return;

    const drafts = prescriptions.filter((p) => p.prescriptionStatus === 'draft');
    if (drafts.length > 0) {
      const ok = await new Promise<boolean>((resolve) => {
        modal.confirm({
          title: t('visits.closeDraftTitle'),
          content: t('visits.closeDraftContent', { count: drafts.length }),
          okText: t('visits.closeDraftOk'),
          cancelText: t('visits.closeDraftCancel'),
          onOk: () => resolve(true),
          onCancel: () => resolve(false),
        });
      });
      if (!ok) return;

      setDetailSaving(true);
      try {
        for (const d of drafts) {
          await finalizeClinicPrescription(d.id);
        }
        await reloadRx(active.id);
        const updated = await updateClinicVisit(active.id, { visitStatus: 'closed' });
        setActive(updated);
        message.success(t('visits.closeAfterFinalizeSuccess'));
        await load();
      } catch (error) {
        message.error(apiErrorMessage(error, t('visits.actionFailed')));
      } finally {
        setDetailSaving(false);
      }
      return;
    }

    setDetailSaving(true);
    try {
      const updated = await updateClinicVisit(active.id, { visitStatus: 'closed' });
      setActive(updated);
      message.success(t('visits.closeSuccess'));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('visits.actionFailed')));
    } finally {
      setDetailSaving(false);
    }
  };

  const onAddNote = async () => {
    if (!active || !noteBody.trim()) return;
    try {
      await addClinicVisitNote(active.id, { noteBody: noteBody.trim(), noteType: 'clinical' });
      setNoteBody('');
      setNotes(await fetchClinicVisitNotes(active.id));
      message.success(t('visits.noteSuccess'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('visits.actionFailed')));
    }
  };

  const reloadRx = async (visitId: string) => {
    setPrescriptions(await fetchClinicPrescriptions(visitId));
  };

  const openRxEditor = (rx?: ClinicPrescription) => {
    if (active?.visitStatus !== 'open') {
      message.warning(t('rx.editClosedBlocked'));
      return;
    }
    setDrugOptions([]);
    if (rx && rx.prescriptionStatus === 'draft') {
      setEditingRxId(rx.id);
      rxForm.setFieldsValue({
        providerId:
          rx.providerId ||
          active?.providerId ||
          (detailForm.getFieldValue('providerId') as string | undefined),
        diagnosisText: rx.diagnosisText || active?.diagnosisSummary,
        notes: rx.notes,
        lines:
          rx.lines.length > 0
            ? rx.lines.map((l) => ({
                drugName: l.drugName,
                strength: l.strength,
                quantity: l.quantity || 1,
                unit: l.unit,
                dosageInstruction: l.dosageInstruction,
              }))
            : [{ drugName: '', quantity: 1 }],
      });
    } else {
      setEditingRxId(null);
      rxForm.setFieldsValue({
        providerId:
          active?.providerId ||
          (detailForm.getFieldValue('providerId') as string | undefined),
        diagnosisText: active?.diagnosisSummary,
        notes: undefined,
        lines: [{ drugName: '', quantity: 1 }],
      });
    }
    setRxOpen(true);
  };

  const openPrescribeClick = () => {
    const draft = prescriptions.find((p) => p.prescriptionStatus === 'draft');
    openRxEditor(draft);
  };

  const onSaveRx = async () => {
    if (!active) return;
    const values = await rxForm.validateFields();
    const providerId = values.providerId as string | undefined;
    if (!providerId) {
      message.warning(t('rx.providerRequired'));
      return;
    }
    setRxSaving(true);
    try {
      if (active.providerId !== providerId) {
        await updateClinicVisit(active.id, { providerId });
        detailForm.setFieldsValue({ providerId });
      }
      if (editingRxId) {
        await updateClinicPrescription(editingRxId, {
          providerId,
          diagnosisText: values.diagnosisText || active.diagnosisSummary,
          notes: values.notes,
          lines: values.lines,
        });
        message.success(t('rx.updateSuccess'));
      } else {
        await createClinicPrescription({
          visitId: active.id,
          providerId,
          diagnosisText: values.diagnosisText || active.diagnosisSummary,
          notes: values.notes,
          lines: values.lines,
        });
        message.success(t('rx.createSuccess'));
      }
      setRxOpen(false);
      setEditingRxId(null);
      rxForm.resetFields();
      const fresh = await fetchClinicVisit(active.id);
      setActive(fresh);
      await reloadRx(active.id);
      if (isRemoteModality(active.encounterModality)) {
        setListQueue('remote');
      }
    } catch (error) {
      message.error(
        apiErrorMessage(error, editingRxId ? t('rx.updateFailed') : t('rx.createFailed')),
      );
    } finally {
      setRxSaving(false);
    }
  };

  const onFinalizeRx = async (id: string) => {
    const rx = prescriptions.find((p) => p.id === id);
    if (rx && (!rx.providerId || !rx.providerDisplayName?.trim())) {
      message.warning(t('rx.providerRequired'));
      return;
    }
    try {
      await finalizeClinicPrescription(id);
      message.success(t('rx.finalizeSuccess'));
      if (active) {
        await reloadRx(active.id);
        await closeRemoteVisitAfterRx(active);
      }
    } catch (error) {
      message.error(apiErrorMessage(error, t('rx.actionFailed')));
    }
  };

  const onSignRx = async (id: string) => {
    try {
      await signClinicPrescription(id);
      message.success(t('rx.signSuccess'));
      if (active) await reloadRx(active.id);
    } catch (error) {
      message.error(apiErrorMessage(error, t('rx.actionFailed')));
    }
  };

  const onCancelRx = async (id: string) => {
    try {
      await cancelClinicPrescription(id);
      message.success(t('rx.cancelSuccess'));
      if (active) await reloadRx(active.id);
    } catch (error) {
      message.error(apiErrorMessage(error, t('rx.actionFailed')));
    }
  };

  const onPdf = async (rx: ClinicPrescription) => {
    try {
      await downloadClinicPrescriptionPdf(rx.id, `${rx.prescriptionCode}.pdf`);
    } catch (error) {
      message.error(apiErrorMessage(error, t('rx.pdfFailed')));
    }
  };

  const openSend = (rx: ClinicPrescription) => {
    if (!rx.providerId || !rx.providerDisplayName?.trim()) {
      message.warning(t('rx.providerRequiredForSend'));
      return;
    }
    setSendRxId(rx.id);
    sendForm.resetFields();
    const lockedId = active?.preferredPharmacyTenantId;
    if (lockedId) {
      sendForm.setFieldsValue({ pharmacyTenantId: lockedId });
    } else if (pharmacyPartners.length === 1) {
      sendForm.setFieldsValue({ pharmacyTenantId: pharmacyPartners[0].partnerTenantId });
    }
    setSendOpen(true);
  };

  const onSendToPharmacy = async () => {
    if (!sendRxId) return;
    const rx = prescriptions.find((p) => p.id === sendRxId);
    if (rx && (!rx.providerId || !rx.providerDisplayName?.trim())) {
      message.warning(t('rx.providerRequiredForSend'));
      return;
    }
    const values = await sendForm.validateFields();
    setSendSaving(true);
    try {
      await sendClinicPrescriptionToPharmacy(sendRxId, values.pharmacyTenantId);
      message.success(t('rx.sendSuccess'));
      setSendOpen(false);
      setSendRxId(null);
      if (active) {
        await reloadRx(active.id);
        await closeRemoteVisitAfterRx(active);
      }
    } catch (error) {
      message.error(apiErrorMessage(error, t('rx.sendFailed')));
    } finally {
      setSendSaving(false);
    }
  };

  const providerSelectOptions = useMemo(
    () =>
      providers.map((p) => {
        const badge = connectBadgeForProvider(p, connectMemberships);
        return {
          value: p.id,
          label: badge
            ? `${p.displayName}${p.specialty ? ` · ${p.specialty}` : ''} (${badge})`
            : p.specialty
              ? `${p.displayName} · ${p.specialty}`
              : p.displayName,
        };
      }),
    [providers, connectMemberships],
  );
  const atClinicBase = useMemo(
    () =>
      items.filter((v) => v.visitStatus === 'open' && !isRemoteModality(v.encounterModality)),
    [items],
  );
  const remoteBase = useMemo(
    () => items.filter((v) => isRemoteModality(v.encounterModality)),
    [items],
  );
  const closedBase = useMemo(
    () =>
      items.filter((v) => v.visitStatus === 'closed' && !isRemoteModality(v.encounterModality)),
    [items],
  );

  const applyFilters = useCallback(
    (list: ClinicVisit[]) => {
      const q = search.trim().toLowerCase();
      return list.filter((row) => {
        if (providerFilter && row.providerId !== providerFilter) return false;
        if (statusFilter && row.visitStatus !== statusFilter) return false;
        if (!q) return true;
        const hay = [
          row.customerName,
          row.customerPhone,
          row.providerDisplayName,
          row.chiefComplaint,
          row.diagnosisSummary,
        ]
          .filter(Boolean)
          .join(' ')
          .toLowerCase();
        return hay.includes(q);
      });
    },
    [search, providerFilter, statusFilter],
  );

  const atClinicItems = useMemo(() => applyFilters(atClinicBase), [applyFilters, atClinicBase]);
  const remoteItems = useMemo(() => applyFilters(remoteBase), [applyFilters, remoteBase]);
  const closedItems = useMemo(() => applyFilters(closedBase), [applyFilters, closedBase]);
  const tableItems =
    listQueue === 'at_clinic'
      ? atClinicItems
      : listQueue === 'remote'
        ? remoteItems
        : closedItems;

  const hasFilters =
    search.trim().length > 0 || !!providerFilter || !!statusFilter;

  const clearFilters = () => {
    setSearch('');
    setProviderFilter(undefined);
    setStatusFilter(undefined);
  };

  const onTabChange = (key: string) => {
    setListQueue(key as VisitQueue);
    setStatusFilter(undefined);
  };

  const columns: ColumnsType<ClinicVisit> = [
    {
      title: colTitle(<ClockCircleOutlined />, t('visits.colWhen')),
      dataIndex: 'startedAt',
      width: '10%',
      align: 'center',
      render: (v: string) =>
        v ? (
          <div>
            <Typography.Text strong style={{ display: 'block' }}>
              {dayjs(v).format('HH:mm')}
            </Typography.Text>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {dayjs(v).format('DD/MM')}
            </Typography.Text>
          </div>
        ) : (
          '—'
        ),
    },
    {
      title: colTitle(<UserOutlined />, t('visits.colPatient')),
      key: 'patient',
      width: '18%',
      ellipsis: true,
      render: (_, row) => {
        const name = row.customerName || row.customerId.slice(0, 8);
        const phone = row.customerPhone?.trim();
        return (
          <div style={{ minWidth: 0 }}>
            <Typography.Text strong ellipsis style={{ display: 'block', maxWidth: '100%' }}>
              {name}
            </Typography.Text>
            {phone ? (
              <Typography.Text type="secondary" style={{ fontSize: 12 }} ellipsis>
                {phone}
              </Typography.Text>
            ) : null}
          </div>
        );
      },
    },
    {
      title: colTitle(<MedicineBoxOutlined />, t('visits.colProvider')),
      key: 'provider',
      width: '14%',
      ellipsis: true,
      render: (_, row) =>
        row.providerDisplayName ? (
          <Typography.Text ellipsis style={{ display: 'block', maxWidth: '100%' }}>
            {row.providerDisplayName}
          </Typography.Text>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
    {
      title: colTitle(<FlagOutlined />, t('visits.colStatus')),
      dataIndex: 'visitStatus',
      width: '10%',
      align: 'center',
      render: (s: string) => (
        <Tag color={STATUS_COLOR[s] ?? 'default'} style={{ marginInlineEnd: 0 }}>
          {t(`visits.status.${s}`, { defaultValue: s })}
        </Tag>
      ),
    },
    {
      title: colTitle(<ApartmentOutlined />, t('visits.colModality')),
      key: 'modality',
      width: '12%',
      align: 'center',
      render: (_, row) => (
        <Tag
          color={isRemoteModality(row.encounterModality) ? 'purple' : 'default'}
          style={{ marginInlineEnd: 0 }}
        >
          {t(`visits.modality.${row.encounterModality}`, {
            defaultValue: row.encounterModality || 'in_person',
          })}
        </Tag>
      ),
    },
    {
      title: colTitle(<FileTextOutlined />, t('visits.colComplaint')),
      dataIndex: 'chiefComplaint',
      width: '14%',
      ellipsis: true,
      render: (v?: string) => v || '—',
    },
    {
      title: colTitle(<SolutionOutlined />, t('visits.colDiagnosis')),
      dataIndex: 'diagnosisSummary',
      width: '14%',
      ellipsis: true,
      render: (v?: string) => v || '—',
    },
    {
      title: t('visits.colActions'),
      key: 'actions',
      width: '8%',
      align: 'right',
      render: (_, row) => (
        <Tooltip title={t('visits.open')}>
          <Button
            size="small"
            type="primary"
            icon={<EyeOutlined />}
            onClick={() => void openVisit(row)}
          >
            {t('visits.open')}
          </Button>
        </Tooltip>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            <Space size={8}>
              <SolutionOutlined />
              {t('visits.title')}
            </Space>
          </Typography.Title>
          <Typography.Text type="secondary">{t('visits.subtitle')}</Typography.Text>
        </div>
        <Space wrap>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {t('visits.refresh')}
          </Button>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => {
              void searchCustomers('');
              void openWalkIn();
            }}
          >
            {t('visits.walkIn')}
          </Button>
        </Space>
      </div>

      <Card styles={{ body: { paddingTop: 12 } }}>
        <Tabs
          type="card"
          activeKey={listQueue}
          onChange={onTabChange}
          items={[
            {
              key: 'at_clinic',
              label: (
                <Space size={8}>
                  <MedicineBoxOutlined />
                  <span>{t('visits.queueAtClinic')}</span>
                  <Badge
                    count={atClinicItems.length}
                    showZero
                    color={listQueue === 'at_clinic' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
            },
            {
              key: 'remote',
              label: (
                <Space size={8}>
                  <PhoneOutlined />
                  <span>{t('visits.queueRemote')}</span>
                  <Badge
                    count={remoteItems.length}
                    showZero
                    color={listQueue === 'remote' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
            },
            {
              key: 'closed',
              label: (
                <Space size={8}>
                  <CheckCircleOutlined />
                  <span>{t('visits.queueClosed')}</span>
                  <Badge
                    count={closedItems.length}
                    showZero
                    color={listQueue === 'closed' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
            },
          ]}
        />

        <Typography.Paragraph type="secondary" style={{ marginTop: 0, marginBottom: 12 }}>
          {listQueue === 'remote'
            ? t('visits.queueRemoteHint')
            : listQueue === 'at_clinic'
              ? t('visits.queueAtClinicHint')
              : t('visits.queueClosedHint')}
        </Typography.Paragraph>

        <Space wrap style={{ marginBottom: 12, width: '100%' }} size={[8, 8]}>
          <Input
            allowClear
            prefix={<SearchOutlined style={{ color: 'rgba(0,0,0,0.25)' }} />}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t('visits.searchPlaceholder')}
            style={{ width: 240 }}
          />
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            value={providerFilter}
            onChange={(v) => setProviderFilter(v)}
            options={providers.map((p) => ({
              value: p.id,
              label: p.specialty ? `${p.displayName} · ${p.specialty}` : p.displayName,
            }))}
            placeholder={t('visits.filterProvider')}
            style={{ minWidth: 160 }}
            suffixIcon={<FilterOutlined />}
          />
          {listQueue === 'remote' ? (
            <Select
              allowClear
              value={statusFilter}
              onChange={(v) => setStatusFilter(v)}
              options={[
                { value: 'open', label: t('visits.status.open') },
                { value: 'closed', label: t('visits.status.closed') },
              ]}
              placeholder={t('visits.filterStatus')}
              style={{ minWidth: 140 }}
            />
          ) : null}
          {hasFilters ? (
            <Button type="link" onClick={clearFilters}>
              {t('visits.filterClear')}
            </Button>
          ) : null}
        </Space>

        <Table
          rowKey="id"
          loading={loading}
          columns={columns}
          dataSource={tableItems}
          tableLayout="fixed"
          pagination={{ pageSize: 20 }}
          locale={{
            emptyText: hasFilters
              ? t('visits.filterEmpty')
              : listQueue === 'remote'
                ? t('visits.emptyRemote')
                : t('visits.empty'),
          }}
        />
      </Card>

      <Modal
        title={t('visits.walkInTitle')}
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        onOk={() => void onWalkIn()}
        confirmLoading={saving}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="customerId"
            label={t('visits.patient')}
            rules={[{ required: true, message: t('visits.patientRequired') }]}
          >
            <Select
              showSearch
              filterOption={false}
              onSearch={(q) => void searchCustomers(q)}
              options={customerOptions}
            />
          </Form.Item>
          <Form.Item name="providerId" label={t('visits.provider')}>
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={
                providers.length === 0 ? t('visits.providerEmpty') : t('visits.providerPlaceholder')
              }
              notFoundContent={t('visits.providerEmpty')}
              getPopupContainer={(node) => node.parentElement ?? document.body}
              options={providerSelectOptions}
            />
          </Form.Item>
          <Form.Item
            name="encounterModality"
            label={t('visits.modalityLabel')}
            initialValue="in_person"
            rules={[{ required: true }]}
          >
            <Select
              options={[
                { value: 'in_person', label: t('visits.modality.in_person') },
                { value: 'remote_async', label: t('visits.modality.remote_async') },
              ]}
            />
          </Form.Item>
          <Form.Item name="chiefComplaint" label={t('visits.complaint')}>
            <Input />
          </Form.Item>
        </Form>
      </Modal>

      <Drawer
        title={
          active
            ? `${t('visits.drawerTitle')} — ${active.customerName || active.customerId.slice(0, 8)}`
            : t('visits.drawerTitle')
        }
        open={!!active}
        onClose={() => setActive(null)}
        width={560}
        destroyOnClose
        footer={
          active?.visitStatus === 'open' ? (
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
              <Button
                icon={<StopOutlined />}
                loading={detailSaving}
                onClick={() => void onCloseVisit()}
              >
                {t('visits.close')}
              </Button>
              <Button
                type="primary"
                icon={<SaveOutlined />}
                loading={detailSaving}
                onClick={() => void onSaveDetail()}
              >
                {t('visits.save')}
              </Button>
            </div>
          ) : null
        }
      >
        {active ? (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Space wrap>
              <Tag color={STATUS_COLOR[active.visitStatus]}>
                {t(`visits.status.${active.visitStatus}`, { defaultValue: active.visitStatus })}
              </Tag>
              <Tag color={active.encounterModality === 'remote_async' ? 'purple' : 'default'}>
                {t(`visits.modality.${active.encounterModality}`, {
                  defaultValue: active.encounterModality,
                })}
              </Tag>
              {active.preferredPharmacyTenantId ? (
                <Tag color="geekblue" icon={<ApartmentOutlined />}>
                  {t('visits.fromPharmacy', {
                    pharmacy:
                      active.preferredPharmacyName ||
                      active.preferredPharmacyCode ||
                      active.preferredPharmacyTenantId.slice(0, 8),
                  })}
                </Tag>
              ) : (
                <Tag>{t('visits.walkInSource')}</Tag>
              )}
            </Space>
            {active.encounterModality === 'remote_async' ? (
              <Typography.Text type="secondary">{t('visits.remoteHint')}</Typography.Text>
            ) : null}
            <Form form={detailForm} layout="vertical">
              {active.visitStatus === 'open' ? (
                <Form.Item name="providerId" label={t('visits.provider')}>
                  <Select
                    allowClear
                    showSearch
                    optionFilterProp="label"
                    placeholder={
                      providers.length === 0
                        ? t('visits.providerEmpty')
                        : t('visits.providerPlaceholder')
                    }
                    notFoundContent={t('visits.providerEmpty')}
                    getPopupContainer={(node) => node.parentElement ?? document.body}
                    options={providerSelectOptions}
                  />
                </Form.Item>
              ) : (
                <div style={{ marginBottom: 16 }}>
                  <Typography.Text type="secondary">{t('visits.provider')}</Typography.Text>
                  <div>
                    {active.providerDisplayName || t('visits.providerNone')}
                  </div>
                </div>
              )}
              <Form.Item name="chiefComplaint" label={t('visits.complaint')}>
                <Input disabled={active.visitStatus !== 'open'} />
              </Form.Item>
              <Form.Item name="diagnosisSummary" label={t('visits.diagnosis')}>
                <Input.TextArea rows={3} disabled={active.visitStatus !== 'open'} />
              </Form.Item>
            </Form>

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <Typography.Title level={5} style={{ margin: 0 }}>
                {t('rx.title')}
              </Typography.Title>
              {active.visitStatus === 'open' ? (
                <Button
                  size="small"
                  type="primary"
                  icon={
                    prescriptions.some((p) => p.prescriptionStatus === 'draft') ? (
                      <EditOutlined />
                    ) : (
                      <FileAddOutlined />
                    )
                  }
                  onClick={openPrescribeClick}
                >
                  {prescriptions.some((p) => p.prescriptionStatus === 'draft')
                    ? t('rx.editDraft')
                    : t('rx.create')}
                </Button>
              ) : null}
            </div>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t('rx.hint')}
            </Typography.Text>
            {active.visitStatus !== 'open' &&
            prescriptions.some((p) => p.prescriptionStatus === 'draft') ? (
              <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 8 }}
                message={t('rx.closedDraftHint')}
              />
            ) : null}
            <Space direction="vertical" style={{ width: '100%' }}>
              {prescriptions.map((rx) => {
                const visitOpen = active.visitStatus === 'open';
                const canSend =
                  Boolean(rx.providerId && rx.providerDisplayName?.trim()) && !rx.sentAt;
                const primaryAction =
                  rx.prescriptionStatus === 'draft' ? (
                    visitOpen ? (
                      <Button
                        size="small"
                        type="primary"
                        icon={<EditOutlined />}
                        onClick={() => openRxEditor(rx)}
                      >
                        {t('rx.edit')}
                      </Button>
                    ) : (
                      <Button
                        size="small"
                        type="primary"
                        icon={<CheckCircleOutlined />}
                        onClick={() => void onFinalizeRx(rx.id)}
                      >
                        {t('rx.finalize')}
                      </Button>
                    )
                  ) : canSend &&
                    (rx.prescriptionStatus === 'finalized' ||
                      rx.prescriptionStatus === 'signed') ? (
                    <Button
                      size="small"
                      type="primary"
                      icon={<SendOutlined />}
                      onClick={() => openSend(rx)}
                    >
                      {t('rx.send')}
                    </Button>
                  ) : null;

                const moreItems: MenuProps['items'] = [];
                if (rx.prescriptionStatus === 'draft') {
                  if (visitOpen) {
                    moreItems.push({
                      key: 'finalize',
                      icon: <CheckCircleOutlined />,
                      label: t('rx.finalize'),
                      onClick: () => void onFinalizeRx(rx.id),
                    });
                    moreItems.push({
                      key: 'cancel',
                      icon: <CloseOutlined />,
                      label: t('rx.cancel'),
                      danger: true,
                      onClick: () => void onCancelRx(rx.id),
                    });
                  } else {
                    moreItems.push({
                      key: 'cancel',
                      icon: <CloseOutlined />,
                      label: t('rx.cancel'),
                      danger: true,
                      onClick: () => void onCancelRx(rx.id),
                    });
                  }
                }
                if (rx.prescriptionStatus === 'finalized' && !rx.sentAt) {
                  moreItems.push({
                    key: 'sign',
                    icon: <SafetyCertificateOutlined />,
                    label: t('rx.sign'),
                    disabled: !rx.providerId || !rx.providerDisplayName?.trim(),
                    onClick: () => void onSignRx(rx.id),
                  });
                }
                if (rx.prescriptionStatus !== 'cancelled') {
                  moreItems.push({
                    key: 'pdf',
                    icon: <FilePdfOutlined />,
                    label: t('rx.pdf'),
                    onClick: () => void onPdf(rx),
                  });
                }

                return (
                  <Card key={rx.id} size="small">
                    <Space direction="vertical" size={8} style={{ width: '100%' }}>
                      <Space wrap>
                        <Typography.Text strong>{rx.prescriptionCode}</Typography.Text>
                        <Tag color={RX_STATUS_COLOR[rx.prescriptionStatus] ?? 'default'}>
                          {t(`rx.status.${rx.prescriptionStatus}`, {
                            defaultValue: rx.prescriptionStatus,
                          })}
                        </Tag>
                        {rx.providerDisplayName ? (
                          <Tag color="blue">
                            {t('rx.providerTag', { name: rx.providerDisplayName })}
                          </Tag>
                        ) : (
                          <Tag color="warning">{t('rx.providerMissing')}</Tag>
                        )}
                        {rx.signatureProvider === 'mock' ? (
                          <Tag color="cyan">{t('rx.signedMock')}</Tag>
                        ) : null}
                        {rx.sentAt ? <Tag color="purple">{t('rx.sent')}</Tag> : null}
                      </Space>
                      {rx.lines.map((line, idx) => (
                        <div key={line.id || idx}>
                          {idx + 1}. {line.drugName}
                          {line.strength ? ` (${line.strength})` : ''} — {line.quantity}
                          {line.unit ? ` ${line.unit}` : ''}
                          {line.dosageInstruction ? (
                            <Typography.Text type="secondary">
                              {' '}
                              · {line.dosageInstruction}
                            </Typography.Text>
                          ) : null}
                        </div>
                      ))}
                      {primaryAction || moreItems.length > 0 ? (
                        <Space wrap>
                          {primaryAction}
                          {moreItems.length > 0 ? (
                            <Dropdown menu={{ items: moreItems }} trigger={['click']}>
                              <Button size="small" icon={<MoreOutlined />}>
                                {t('rx.moreActions')}
                              </Button>
                            </Dropdown>
                          ) : null}
                        </Space>
                      ) : null}
                    </Space>
                  </Card>
                );
              })}
              {prescriptions.length === 0 ? (
                <Typography.Text type="secondary">{t('rx.empty')}</Typography.Text>
              ) : null}
            </Space>

            <Typography.Title level={5}>{t('visits.notes')}</Typography.Title>
            <Space direction="vertical" style={{ width: '100%' }}>
              {notes.map((n) => (
                <Card key={n.id} size="small">
                  <Typography.Text type="secondary">
                    {dayjs(n.createdAt).format('DD/MM HH:mm')} · {n.noteType}
                  </Typography.Text>
                  <div>{n.noteBody}</div>
                </Card>
              ))}
              {notes.length === 0 ? (
                <Typography.Text type="secondary">{t('visits.notesEmpty')}</Typography.Text>
              ) : null}
            </Space>
            {active.visitStatus === 'open' ? (
              <Space.Compact style={{ width: '100%' }}>
                <Input
                  value={noteBody}
                  onChange={(e) => setNoteBody(e.target.value)}
                  placeholder={t('visits.notePlaceholder')}
                />
                <Button type="primary" onClick={() => void onAddNote()}>
                  {t('visits.addNote')}
                </Button>
              </Space.Compact>
            ) : null}
          </Space>
        ) : null}
      </Drawer>

      <Modal
        title={editingRxId ? t('rx.editTitle') : t('rx.createTitle')}
        open={rxOpen}
        onCancel={() => {
          setRxOpen(false);
          setEditingRxId(null);
        }}
        confirmLoading={rxSaving}
        width={960}
        destroyOnClose
        footer={[
          <Button
            key="cancel"
            icon={<CloseOutlined />}
            onClick={() => {
              setRxOpen(false);
              setEditingRxId(null);
            }}
          >
            {t('rx.createCancel')}
          </Button>,
          <Button
            key="ok"
            type="primary"
            icon={editingRxId ? <SaveOutlined /> : <FileAddOutlined />}
            loading={rxSaving}
            onClick={() => void onSaveRx()}
          >
            {editingRxId ? t('rx.editOk') : t('rx.createOk')}
          </Button>,
        ]}
      >
        <Form form={rxForm} layout="vertical">
          {active?.preferredPharmacyTenantId ? (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 16 }}
              message={t('rx.drugCatalogPartner', {
                pharmacy:
                  active.preferredPharmacyName ||
                  active.preferredPharmacyCode ||
                  active.preferredPharmacyTenantId.slice(0, 8),
              })}
              description={t('rx.drugSearchPartnerHint', {
                pharmacy:
                  active.preferredPharmacyName ||
                  active.preferredPharmacyCode ||
                  active.preferredPharmacyTenantId.slice(0, 8),
              })}
            />
          ) : null}
          <Form.Item
            name="providerId"
            label={t('visits.provider')}
            rules={[{ required: true, message: t('rx.providerRequired') }]}
            extra={t('rx.providerOnRxHint')}
          >
            <Select
              showSearch
              optionFilterProp="label"
              placeholder={
                providers.length === 0
                  ? t('visits.providerEmpty')
                  : t('visits.providerPlaceholder')
              }
              notFoundContent={t('visits.providerEmpty')}
              options={providerSelectOptions}
            />
          </Form.Item>
          <Form.Item name="diagnosisText" label={t('rx.diagnosis')}>
            <Input />
          </Form.Item>
          <Form.Item name="notes" label={t('rx.notes')}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.List
            name="lines"
            rules={[
              {
                validator: async (_, lines) => {
                  if (!lines || lines.length < 1) {
                    return Promise.reject(new Error(t('rx.linesRequired')));
                  }
                },
              },
            ]}
          >
            {(fields, { add, remove }) => (
              <>
                {fields.map((field) => (
                  <div
                    key={field.key}
                    style={{
                      display: 'flex',
                      flexWrap: 'nowrap',
                      gap: 8,
                      alignItems: 'flex-start',
                      marginBottom: 8,
                    }}
                  >
                    <Form.Item
                      {...field}
                      name={[field.name, 'drugName']}
                      rules={[{ required: true, message: t('rx.drugRequired') }]}
                      style={{ flex: '1 1 240px', marginBottom: 0, minWidth: 0 }}
                    >
                      <AutoComplete
                        options={drugOptions}
                        onSearch={(q) => void searchDrugs(q)}
                        onSelect={(_value, option) => {
                          const opt = option as {
                            value: string;
                            strength?: string;
                            unit?: string;
                          };
                          if (opt.strength) {
                            rxForm.setFieldValue(['lines', field.name, 'strength'], opt.strength);
                          }
                          if (opt.unit) {
                            rxForm.setFieldValue(['lines', field.name, 'unit'], opt.unit);
                          }
                        }}
                        placeholder={
                          active?.preferredPharmacyTenantId
                            ? t('rx.drugSearchPartner')
                            : t('rx.drugSearch')
                        }
                        notFoundContent={
                          drugSearching
                            ? '…'
                            : active?.preferredPharmacyTenantId
                              ? t('rx.drugSearchPartnerEmpty')
                              : t('rx.drugSearchHint')
                        }
                        style={{ width: '100%' }}
                        allowClear
                      />
                    </Form.Item>
                    <Form.Item
                      {...field}
                      name={[field.name, 'strength']}
                      style={{ flex: '0 0 110px', marginBottom: 0 }}
                    >
                      <Input placeholder={t('rx.strength')} style={{ width: '100%' }} />
                    </Form.Item>
                    <Form.Item
                      {...field}
                      name={[field.name, 'quantity']}
                      rules={[{ required: true }]}
                      initialValue={1}
                      style={{ flex: '0 0 88px', marginBottom: 0 }}
                    >
                      <InputNumber min={0.001} style={{ width: '100%' }} />
                    </Form.Item>
                    <Form.Item
                      {...field}
                      name={[field.name, 'unit']}
                      style={{ flex: '0 0 72px', marginBottom: 0 }}
                    >
                      <Input placeholder={t('rx.unit')} style={{ width: '100%' }} />
                    </Form.Item>
                    <Form.Item
                      {...field}
                      name={[field.name, 'dosageInstruction']}
                      style={{ flex: '1 1 180px', marginBottom: 0, minWidth: 0 }}
                    >
                      <Input placeholder={t('rx.dosage')} style={{ width: '100%' }} />
                    </Form.Item>
                    {fields.length > 1 ? (
                      <Button
                        type="text"
                        danger
                        icon={<DeleteOutlined />}
                        onClick={() => remove(field.name)}
                        aria-label={t('rx.removeLine')}
                        style={{ flex: '0 0 auto', marginTop: 4 }}
                      />
                    ) : null}
                  </div>
                ))}
                <Button
                  type="dashed"
                  icon={<PlusOutlined />}
                  onClick={() => add({ quantity: 1 })}
                  block
                >
                  {t('rx.addLine')}
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      </Modal>

      <Modal
        title={t('rx.sendTitle')}
        open={sendOpen}
        onCancel={() => {
          setSendOpen(false);
          setSendRxId(null);
        }}
        onOk={() => void onSendToPharmacy()}
        confirmLoading={sendSaving}
        okButtonProps={{
          disabled:
            pharmacyPartners.length === 0 &&
            !active?.preferredPharmacyTenantId,
        }}
        destroyOnClose
      >
        {(() => {
          const lockedId = active?.preferredPharmacyTenantId;
          const lockedLabel =
            active?.preferredPharmacyName && active.preferredPharmacyCode
              ? `${active.preferredPharmacyName} (${active.preferredPharmacyCode})`
              : active?.preferredPharmacyName ||
                pharmacyPartners.find((p) => p.partnerTenantId === lockedId)
                  ?.partnerTenantName;

          if (lockedId) {
            const stillLinked = pharmacyPartners.some((p) => p.partnerTenantId === lockedId);
            return (
              <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <Typography.Paragraph style={{ marginBottom: 0 }}>
                  {t('rx.sendLockedHint', {
                    pharmacy: lockedLabel || lockedId.slice(0, 8),
                  })}
                </Typography.Paragraph>
                {!stillLinked ? (
                  <Typography.Text type="warning">{t('rx.sendLockedUnlinked')}</Typography.Text>
                ) : null}
                <Form form={sendForm} layout="vertical">
                  <Form.Item name="pharmacyTenantId" hidden rules={[{ required: true }]}>
                    <Input />
                  </Form.Item>
                  <Form.Item label={t('rx.pharmacy')}>
                    <Input value={lockedLabel || lockedId} disabled />
                  </Form.Item>
                </Form>
              </Space>
            );
          }

          if (pharmacyPartners.length === 0) {
            return <Typography.Text type="warning">{t('rx.noPharmacy')}</Typography.Text>;
          }

          return (
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('rx.sendWalkInHint')}
              </Typography.Paragraph>
              <Form form={sendForm} layout="vertical">
                <Form.Item
                  name="pharmacyTenantId"
                  label={t('rx.pharmacy')}
                  rules={[{ required: true, message: t('rx.pharmacyRequired') }]}
                >
                  <Select
                    options={pharmacyPartners.map((p) => ({
                      value: p.partnerTenantId,
                      label: `${p.partnerTenantName} (${p.partnerTenantCode})`,
                    }))}
                    placeholder={t('rx.pharmacyPlaceholder')}
                    showSearch
                    optionFilterProp="label"
                  />
                </Form.Item>
              </Form>
            </Space>
          );
        })()}
      </Modal>
    </Space>
  );
}
