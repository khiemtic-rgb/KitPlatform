import { useCallback, useEffect, useMemo, useState, type CSSProperties } from 'react';
import { useTranslation } from 'react-i18next';
import {
  App,
  AutoComplete,
  Button,
  Col,
  DatePicker,
  Drawer,
  InputNumber,
  Row,
  Select,
  Space,
  Switch,
  Table,
  Tag,
} from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import { TeamOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import {
  issueVoucherBulkAdmin,
  searchVoucherIssueCandidates,
  type VoucherAdmin,
  type VoucherIssueCandidate,
} from '@/shared/api/vouchers.api';
import { fetchLoyaltySettings } from '@/shared/api/loyalty.api';
import { searchCustomers } from '@/shared/api/sales.api';
import type { LoyaltyTierAdmin } from '@/shared/api/loyalty.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { TabularMoney } from '@/modules/sales/sales-ui-styles';
import { formatDisplayMoney, moneyInputNumberPropsAllowZeroSuffix, moneyInputNumberStyle } from '@/shared/utils/money';

const MONTH_OPTIONS = Array.from({ length: 12 }, (_, i) => ({
  value: i + 1,
  label: String(i + 1),
}));

const filterLabelStyle: CSSProperties = { fontSize: 12, whiteSpace: 'nowrap' };
const FILTER_LABEL_W = 108;
function dayOptionsForMonth(month: number) {
  const days = month === 2 ? 29 : [4, 6, 9, 11].includes(month) ? 30 : 31;
  return Array.from({ length: days }, (_, i) => ({ value: i + 1, label: String(i + 1) }));
}

function formatBirthday(value?: string | null) {
  if (!value) return '—';
  const d = dayjs(value);
  return d.isValid() ? d.format('DD/MM') : '—';
}

export function VoucherBulkIssueDrawer({
  open,
  voucher,
  onClose,
  onIssued,
}: {
  open: boolean;
  voucher: VoucherAdmin | null;
  onClose: () => void;
  onIssued: () => void | Promise<void>;
}) {
  const { t } = useTranslation('sales', { keyPrefix: 'vouchers.bulkIssue' });
  const { message } = App.useApp();
  const [tiers, setTiers] = useState<LoyaltyTierAdmin[]>([]);
  const [search, setSearch] = useState('');
  const [customerOptions, setCustomerOptions] = useState<{ value: string; label: string }[]>([]);
  const [revenueEnabled, setRevenueEnabled] = useState(false);
  const [revenueRange, setRevenueRange] = useState<[Dayjs, Dayjs] | null>(null);
  const [minRevenue, setMinRevenue] = useState(500_000);
  const [birthdayEnabled, setBirthdayEnabled] = useState(false);
  const [birthdayFromMonth, setBirthdayFromMonth] = useState(1);
  const [birthdayFromDay, setBirthdayFromDay] = useState(1);
  const [birthdayToMonth, setBirthdayToMonth] = useState(31);
  const [birthdayToDay, setBirthdayToDay] = useState(12);
  const [tierEnabled, setTierEnabled] = useState(false);
  const [tierIds, setTierIds] = useState<string[]>([]);
  const [excludeAlreadyIssued, setExcludeAlreadyIssued] = useState(true);
  const [items, setItems] = useState<VoucherIssueCandidate[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [loading, setLoading] = useState(false);
  const [issuing, setIssuing] = useState(false);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);

  useEffect(() => {
    if (!open) return;
    fetchLoyaltySettings()
      .then((settings) => setTiers(settings.program?.tiers ?? []))
      .catch(() => setTiers([]));
  }, [open]);

  useEffect(() => {
    if (!open) {
      setSearch('');
      setCustomerOptions([]);
      setRevenueEnabled(false);
      setRevenueRange([dayjs().startOf('month'), dayjs().endOf('month')]);
      setMinRevenue(500_000);
      setBirthdayEnabled(false);
      setBirthdayFromMonth(1);
      setBirthdayFromDay(1);
      setBirthdayToMonth(31);
      setBirthdayToDay(12);
      setTierEnabled(false);
      setTierIds([]);
      setExcludeAlreadyIssued(true);
      setItems([]);
      setTotal(0);
      setPage(1);
      setSelectedIds([]);
    } else {
      setRevenueRange([dayjs().startOf('month'), dayjs().endOf('month')]);
    }
  }, [open]);

  const tierOptions = useMemo(
    () =>
      tiers
        .filter((t) => t.id)
        .map((t) => ({ value: t.id!, label: t.tierName || t.tierCode })),
    [tiers],
  );

  const loadCustomerOptions = async (text: string) => {
    setSearch(text);
    if (!text.trim()) {
      setCustomerOptions([]);
      return;
    }
    try {
      const rows = await searchCustomers(text.trim());
      setCustomerOptions(
        rows.map((c) => {
          const label = `${c.fullName}${c.phone ? ` · ${c.phone}` : ''}${c.customerCode ? ` · ${c.customerCode}` : ''}`;
          return { value: label, label };
        }),
      );
    } catch {
      setCustomerOptions([]);
    }
  };

  const buildPayload = useCallback(
    (pageNum: number, size: number) => ({
      search: search.trim() || undefined,
      revenueEnabled,
      revenueFrom: revenueEnabled && revenueRange?.[0]
        ? revenueRange[0].startOf('day').toISOString()
        : undefined,
      revenueTo: revenueEnabled && revenueRange?.[1]
        ? revenueRange[1].endOf('day').toISOString()
        : undefined,
      minRevenue: revenueEnabled ? minRevenue : undefined,
      birthdayEnabled,
      birthdayFromMonth: birthdayEnabled ? birthdayFromMonth : undefined,
      birthdayFromDay: birthdayEnabled ? birthdayFromDay : undefined,
      birthdayToMonth: birthdayEnabled ? birthdayToMonth : undefined,
      birthdayToDay: birthdayEnabled ? birthdayToDay : undefined,
      tierEnabled,
      tierIds: tierEnabled ? tierIds : undefined,
      excludeAlreadyIssued,
      page: pageNum,
      pageSize: size,
    }),
    [
      search,
      revenueEnabled,
      revenueRange,
      minRevenue,
      birthdayEnabled,
      birthdayFromMonth,
      birthdayFromDay,
      birthdayToMonth,
      birthdayToDay,
      tierEnabled,
      tierIds,
      excludeAlreadyIssued,
    ],
  );

  const runSearch = useCallback(
    async (pageNum = page, size = pageSize) => {
      if (!voucher) return;
      setLoading(true);
      try {
        const result = await searchVoucherIssueCandidates(voucher.id, buildPayload(pageNum, size));
        setItems(result.items);
        setTotal(result.total);
        setPage(result.page);
        setPageSize(result.pageSize);
        setSelectedIds((prev) => prev.filter((id) => result.items.some((row) => row.id === id)));
      } catch (error) {
        message.error(apiErrorMessage(error, t('messages.searchFailed')));
      } finally {
        setLoading(false);
      }
    },
    [voucher, page, pageSize, buildPayload, message, t],
  );

  const handleIssue = async () => {
    if (!voucher || selectedIds.length === 0) {
      message.warning(t('messages.selectAtLeastOne'));
      return;
    }
    setIssuing(true);
    try {
      const result = await issueVoucherBulkAdmin(voucher.id, selectedIds);
      message.success(
        result.skippedAlreadyHad > 0
          ? t('messages.issueSuccessSkipped', {
              issued: result.issuedCount,
              skipped: result.skippedAlreadyHad,
            })
          : t('messages.issueSuccess', { issued: result.issuedCount }),
      );
      setSelectedIds([]);
      await runSearch(1, pageSize);
      await onIssued();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.issueFailed')));
    } finally {
      setIssuing(false);
    }
  };

  const columns: ColumnsType<VoucherIssueCandidate> = useMemo(
    () => [
      { title: t('columns.code'), dataIndex: 'customerCode', width: 72, ellipsis: true },
      { title: t('columns.customer'), dataIndex: 'fullName', ellipsis: true },
      { title: t('columns.phone'), dataIndex: 'phone', width: 96, ellipsis: true },
      {
        title: t('columns.tier'),
        dataIndex: 'tierName',
        width: 64,
        ellipsis: true,
        render: (v?: string | null) => v ?? '—',
      },
      {
        title: t('columns.revenue'),
        dataIndex: 'periodRevenue',
        width: 96,
        align: 'right',
        render: (v?: number | null) =>
          v != null && v > 0 ? <TabularMoney>{formatDisplayMoney(v)}</TabularMoney> : '—',
      },
      {
        title: t('columns.birthday'),
        dataIndex: 'dateOfBirth',
        width: 52,
        render: (v?: string | null) => formatBirthday(v),
      },
      {
        title: t('columns.voucher'),
        key: 'issued',
        width: 72,
        render: (_, row) =>
          row.alreadyIssued ? (
            <Tag style={{ margin: 0, fontSize: 11, lineHeight: '18px' }}>{t('hasVoucher')}</Tag>
          ) : (
            <Tag color="green" style={{ margin: 0, fontSize: 11, lineHeight: '18px' }}>
              {t('noVoucher')}
            </Tag>
          ),
      },
    ],
    [t],
  );

  const handleTableChange = (pagination: TablePaginationConfig) => {
    const nextPage = pagination.current ?? 1;
    const nextSize = pagination.pageSize ?? pageSize;
    void runSearch(nextPage, nextSize);
  };

  return (
    <Drawer
      title={voucher ? t('titleWithCode', { code: voucher.voucherCode }) : t('title')}
      open={open}
      onClose={onClose}
      width={920}
      destroyOnClose
      styles={{
        header: { padding: '10px 16px' },
        body: { padding: '8px 12px 12px' },
      }}
      extra={
        <Button
          type="primary"
          size="small"
          icon={<TeamOutlined />}
          loading={issuing}
          disabled={selectedIds.length === 0}
          onClick={() => void handleIssue()}
        >
          {t('issueButton', { count: selectedIds.length })}
        </Button>
      }
    >
      <Space direction="vertical" style={{ width: '100%' }} size={4}>
        <Row gutter={8} align="middle" wrap={false}>
          <Col flex="auto">
            <AutoComplete
              size="small"
              style={{ width: '100%' }}
              options={customerOptions}
              value={search}
              onChange={(v) => setSearch(String(v ?? ''))}
              onSearch={(v) => void loadCustomerOptions(v)}
              onSelect={(value) => setSearch(String(value))}
              placeholder={t('searchPlaceholder')}
              allowClear
              onClear={() => {
                setSearch('');
                setCustomerOptions([]);
              }}
            />
          </Col>
          <Col flex="none">
            <Button type="primary" size="small" loading={loading} onClick={() => void runSearch(1, pageSize)}>
              {t('searchCustomers')}
            </Button>
          </Col>
        </Row>

        <Row gutter={[8, 4]} align="middle" wrap={false}>
          <Col style={{ width: FILTER_LABEL_W, flex: `0 0 ${FILTER_LABEL_W}px` }}>
            <Space size={6}>
              <Switch size="small" checked={revenueEnabled} onChange={setRevenueEnabled} />
              <span style={filterLabelStyle}>{t('revenue')}</span>
            </Space>
          </Col>
          <Col flex="auto" style={{ minWidth: 0 }}>
            <div style={{ display: 'flex', gap: 8, width: '100%', alignItems: 'center' }}>
              <div style={{ flex: 1, minWidth: 0 }}>
                <DatePicker.RangePicker
                  size="small"
                  style={{ width: '100%' }}
                  format="DD/MM/YY"
                  value={revenueRange}
                  disabled={!revenueEnabled}
                  onChange={(range) => setRevenueRange(range as [Dayjs, Dayjs] | null)}
                />
              </div>
              <InputNumber
                size="small"
                {...moneyInputNumberPropsAllowZeroSuffix}
                style={{ ...moneyInputNumberStyle, width: 108, flexShrink: 0 }}
                min={0}
                disabled={!revenueEnabled}
                value={minRevenue}
                onChange={(v) => setMinRevenue(Number(v ?? 0))}
                placeholder={t('minRevenue')}
              />
              <Button
                size="small"
                style={{ flexShrink: 0 }}
                disabled={!revenueEnabled}
                onClick={() => setRevenueRange([dayjs().startOf('month'), dayjs().endOf('month')])}
              >
                {t('thisMonth')}
              </Button>
            </div>
          </Col>
        </Row>

        <Row gutter={[8, 4]} align="middle" wrap={false}>
          <Col style={{ width: FILTER_LABEL_W, flex: `0 0 ${FILTER_LABEL_W}px` }}>
            <Space size={6}>
              <Switch size="small" checked={birthdayEnabled} onChange={setBirthdayEnabled} />
              <span style={filterLabelStyle}>{t('birthday')}</span>
            </Space>
          </Col>
          <Col flex="auto" style={{ minWidth: 0 }}>
            <Space size={4} wrap={false} style={{ width: '100%' }}>
              <Select
                size="small"
                style={{ width: 56 }}
                disabled={!birthdayEnabled}
                value={birthdayFromMonth}
                options={MONTH_OPTIONS}
                onChange={setBirthdayFromMonth}
              />
              <Select
                size="small"
                style={{ width: 52 }}
                disabled={!birthdayEnabled}
                value={birthdayFromDay}
                options={dayOptionsForMonth(birthdayFromMonth)}
                onChange={setBirthdayFromDay}
              />
              <span style={{ fontSize: 12 }}>→</span>
              <Select
                size="small"
                style={{ width: 56 }}
                disabled={!birthdayEnabled}
                value={birthdayToMonth}
                options={MONTH_OPTIONS}
                onChange={setBirthdayToMonth}
              />
              <Select
                size="small"
                style={{ width: 52 }}
                disabled={!birthdayEnabled}
                value={birthdayToDay}
                options={dayOptionsForMonth(birthdayToMonth)}
                onChange={setBirthdayToDay}
              />
            </Space>
          </Col>
        </Row>

        <Row gutter={[8, 4]} align="middle" wrap={false}>
          <Col style={{ width: FILTER_LABEL_W, flex: `0 0 ${FILTER_LABEL_W}px` }}>
            <Space size={6}>
              <Switch size="small" checked={tierEnabled} onChange={setTierEnabled} />
              <span style={filterLabelStyle}>{t('tier')}</span>
            </Space>
          </Col>
          <Col flex="auto" style={{ minWidth: 0 }}>
            <Select
              size="small"
              mode="multiple"
              maxTagCount="responsive"
              style={{ width: '100%' }}
              placeholder={t('selectTiers')}
              disabled={!tierEnabled}
              value={tierIds}
              onChange={setTierIds}
              options={tierOptions}
              allowClear
            />
          </Col>
          <Col flex="none">
            <Space size={6}>
              <Switch size="small" checked={excludeAlreadyIssued} onChange={setExcludeAlreadyIssued} />
              <span style={filterLabelStyle}>{t('hideAlreadyIssued')}</span>
            </Space>
          </Col>
        </Row>
      </Space>

      <Table
        style={{ marginTop: 8 }}
        size="small"
        rowKey="id"
        loading={loading}
        columns={columns}
        dataSource={items}
        rowSelection={{
          selectedRowKeys: selectedIds,
          onChange: (keys) => setSelectedIds(keys as string[]),
          getCheckboxProps: (row) => ({
            disabled: row.alreadyIssued,
          }),
        }}
        pagination={{
          current: page,
          pageSize,
          total,
          size: 'small',
          showSizeChanger: true,
          pageSizeOptions: [20, 50, 100, 200],
          showTotal: (total) => t('paginationTotal', { count: total }),
        }}
        onChange={handleTableChange}
        scroll={{ y: 'calc(100vh - 248px)' }}
      />
    </Drawer>
  );
}
