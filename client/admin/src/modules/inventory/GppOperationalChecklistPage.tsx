import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Button,
  Card,
  Checkbox,
  Collapse,
  Progress,
  Space,
  Typography,
  message,
} from 'antd';
import { CheckCircleOutlined, ReloadOutlined } from '@ant-design/icons';

const STORAGE_KEY = 'novixa:gpp-checklist-v1';

type ChecklistItem = {
  id: string;
  labelKey: string;
};

type ChecklistSection = {
  id: string;
  titleKey: string;
  items: ChecklistItem[];
};

const SECTIONS: ChecklistSection[] = [
  {
    id: 'daily',
    titleKey: 'sections.daily',
    items: [
      { id: 'shift-open', labelKey: 'items.shiftOpen' },
      { id: 'fefo-sale', labelKey: 'items.fefoSale' },
      { id: 'dispense-notes', labelKey: 'items.dispenseNotes' },
      { id: 'shift-close', labelKey: 'items.shiftClose' },
      { id: 'low-stock-scan', labelKey: 'items.lowStockScan' },
    ],
  },
  {
    id: 'weekly',
    titleKey: 'sections.weekly',
    items: [
      { id: 'near-expiry-report', labelKey: 'items.nearExpiryReport' },
      { id: 'inventory-count', labelKey: 'items.inventoryCount' },
      { id: 'grn-verify', labelKey: 'items.grnVerify' },
      { id: 'po-approval', labelKey: 'items.poApproval' },
    ],
  },
  {
    id: 'monthly',
    titleKey: 'sections.monthly',
    items: [
      { id: 'full-count', labelKey: 'items.fullCount' },
      { id: 'access-review', labelKey: 'items.accessReview' },
      { id: 'backup-check', labelKey: 'items.backupCheck' },
    ],
  },
];

function loadChecked(): Record<string, boolean> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as Record<string, boolean>) : {};
  } catch {
    return {};
  }
}

export function GppOperationalChecklistPage() {
  const { t } = useTranslation('inventory', { keyPrefix: 'gppChecklist' });
  const [checked, setChecked] = useState<Record<string, boolean>>(() => loadChecked());

  const allItems = useMemo(
    () => SECTIONS.flatMap((s) => s.items.map((i) => ({ ...i, sectionId: s.id }))),
    [],
  );

  const doneCount = allItems.filter((i) => checked[i.id]).length;
  const percent = allItems.length ? Math.round((doneCount / allItems.length) * 100) : 0;

  const persist = useCallback((next: Record<string, boolean>) => {
    setChecked(next);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
  }, []);

  const toggle = (id: string, value: boolean) => {
    persist({ ...checked, [id]: value });
  };

  const reset = () => {
    persist({});
    message.info(t('resetDone'));
  };

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ marginBottom: 4 }}>
          {t('title')}
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('description')}
        </Typography.Paragraph>
      </div>

      <Alert type="info" showIcon message={t('disclaimer')} />

      <Card size="small">
        <Space direction="vertical" style={{ width: '100%' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <Typography.Text>
              {t('progress', { done: doneCount, total: allItems.length })}
            </Typography.Text>
            <Button icon={<ReloadOutlined />} onClick={reset}>
              {t('reset')}
            </Button>
          </div>
          <Progress percent={percent} status={percent === 100 ? 'success' : 'active'} />
        </Space>
      </Card>

      <Collapse
        defaultActiveKey={SECTIONS.map((s) => s.id)}
        items={SECTIONS.map((section) => ({
          key: section.id,
          label: t(section.titleKey),
          children: (
            <Space direction="vertical" style={{ width: '100%' }}>
              {section.items.map((item) => (
                <Checkbox
                  key={item.id}
                  checked={Boolean(checked[item.id])}
                  onChange={(e) => toggle(item.id, e.target.checked)}
                >
                  {checked[item.id] ? (
                    <CheckCircleOutlined style={{ color: '#16a34a', marginRight: 6 }} />
                  ) : null}
                  {t(item.labelKey)}
                </Checkbox>
              ))}
            </Space>
          ),
        }))}
      />
    </Space>
  );
}
