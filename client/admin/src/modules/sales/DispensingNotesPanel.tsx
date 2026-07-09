import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { App, Button, Card, Input, Select, Space, Typography } from 'antd';
import { SaveOutlined } from '@ant-design/icons';
import {
  createDispensingNote,
  DISPENSING_NOTE_TYPES,
  fetchDispensingNotes,
  type DispensingNote,
} from '@/shared/api/pharmacy.api';
import { apiErrorMessage } from '@/shared/api/api-error';

type DispensingNotesPanelProps = {
  salesOrderId: string;
  customerId?: string;
  readOnly?: boolean;
};

export function DispensingNotesPanel({
  salesOrderId,
  customerId,
  readOnly = false,
}: DispensingNotesPanelProps) {
  const { t } = useTranslation('sales', { keyPrefix: 'dispensingNotes' });
  const { message } = App.useApp();
  const [notes, setNotes] = useState<DispensingNote[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [noteType, setNoteType] = useState('counseling');
  const [noteText, setNoteText] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setNotes(await fetchDispensingNotes(salesOrderId));
    } catch (error) {
      message.error(apiErrorMessage(error, t('loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [message, salesOrderId, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    const text = noteText.trim();
    if (!text) {
      message.warning(t('textRequired'));
      return;
    }
    setSaving(true);
    try {
      const created = await createDispensingNote({
        salesOrderId,
        customerId,
        noteType,
        noteText: text,
      });
      setNotes((prev) => [created, ...prev]);
      setNoteText('');
      message.success(t('saved'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card size="small" title={t('title')} loading={loading} style={{ marginTop: 16 }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12, fontSize: 12 }}>
        {t('intro')}
      </Typography.Paragraph>
      {!readOnly ? (
        <Space direction="vertical" style={{ width: '100%', marginBottom: 12 }} size="small">
          <Select
            value={noteType}
            onChange={setNoteType}
            style={{ width: '100%' }}
            options={DISPENSING_NOTE_TYPES.map((o) => ({
              value: o.value,
              label: t(`types.${o.labelKey}`),
            }))}
          />
          <Input.TextArea
            rows={3}
            value={noteText}
            onChange={(e) => setNoteText(e.target.value)}
            placeholder={t('placeholder')}
            maxLength={2000}
            showCount
          />
          <Button icon={<SaveOutlined />} loading={saving} onClick={() => void save()}>
            {t('save')}
          </Button>
        </Space>
      ) : null}
      {notes.length === 0 ? (
        <Typography.Text type="secondary">{t('empty')}</Typography.Text>
      ) : (
        notes.map((n) => (
          <div key={n.id} style={{ marginBottom: 8, fontSize: 12 }}>
            <Typography.Text type="secondary">
              {t(`types.${n.noteType}`, { defaultValue: n.noteType })}
              {' · '}
              {new Date(n.createdAt).toLocaleString('vi-VN')}
            </Typography.Text>
            <div>{n.noteText}</div>
          </div>
        ))
      )}
    </Card>
  );
}
