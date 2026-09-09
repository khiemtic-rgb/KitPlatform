import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, Select, Space, Tag, Typography, message } from 'antd';
import { ClockCircleOutlined, CopyOutlined, HighlightOutlined, SendOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchKitSalesJourneyPains,
  fetchKitSalesLeadJourney,
  rewriteKitSalesJourney,
  scheduleKitSalesJourney,
  sendKitSalesJourney,
  type KitSalesJourneyPain,
  type KitSalesJourneyPlan,
} from '@/shared/api/kit-sales.api';

interface KitSalesJourneyCardProps {
  leadId: string;
  onChanged?: () => void;
  title?: string;
}

const STEP_KEYS: Record<string, string> = {
  hook: 'journey.stepHook',
  phc_ask: 'journey.stepPhc',
  report_ask: 'journey.stepReport',
  pilot_ask: 'journey.stepPilot',
};

export function KitSalesJourneyCard({ leadId, onChanged, title }: KitSalesJourneyCardProps) {
  const { t } = useTranslation('kitSales');
  const [pains, setPains] = useState<KitSalesJourneyPain[]>([]);
  const [plan, setPlan] = useState<KitSalesJourneyPlan | null>(null);
  const [pain, setPain] = useState<string>('after_sale');
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [rewriting, setRewriting] = useState(false);

  const load = async (painCode?: string) => {
    setLoading(true);
    try {
      const next = await fetchKitSalesLeadJourney(leadId, painCode);
      setPlan(next);
      setPain(next.painCode);
    } catch (error) {
      message.error(apiErrorMessage(error, t('journey.failed')));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void fetchKitSalesJourneyPains().then(setPains).catch(() => undefined);
    void load();
  }, [leadId]);

  const onPain = (code: string) => {
    setPain(code);
    void load(code);
  };

  const onSchedule = async () => {
    setSaving(true);
    try {
      setPlan(await scheduleKitSalesJourney(leadId, { painCode: pain, step: plan?.step }));
      message.success(t('journey.scheduled'));
      onChanged?.();
    } catch (error) {
      message.error(apiErrorMessage(error, t('journey.failed')));
    } finally {
      setSaving(false);
    }
  };

  const onRewrite = async () => {
    setRewriting(true);
    try {
      const next = await rewriteKitSalesJourney(leadId, { painCode: pain, step: plan?.step });
      setPlan(next);
      setPain(next.painCode);
      message.success(t('journey.rewritten'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('journey.failed')));
    } finally {
      setRewriting(false);
    }
  };

  const onSend = async () => {
    if (!plan?.draft) return;
    setSaving(true);
    try {
      const next = await sendKitSalesJourney(leadId, {
        painCode: pain,
        step: plan.step,
        content: plan.draft,
      });
      setPlan(next);
      try {
        await navigator.clipboard.writeText(plan.draft);
      } catch {
        /* staff can still paste from the card */
      }
      window.open('https://www.facebook.com/messages/', '_blank', 'noopener,noreferrer');
      message.success(t('journey.sentStaffInbox'));
      onChanged?.();
    } catch (error) {
      message.error(apiErrorMessage(error, t('journey.failed')));
    } finally {
      setSaving(false);
    }
  };

  const openPharmacyFacebook = () => {
    const target = plan?.facebookUrl;
    if (!target) {
      message.warning(t('journey.sentOutboundNoLink'));
      return;
    }
    window.open(target, '_blank', 'noopener,noreferrer');
  };

  const copy = async () => {
    if (!plan?.draft) return;
    try {
      await navigator.clipboard.writeText(plan.draft);
      message.success(t('chat.copied'));
    } catch {
      message.error(t('chat.failed'));
    }
  };

  return (
    <Card
      size="small"
      loading={loading && !plan}
      title={
        <Space>
          <ClockCircleOutlined />
          {title ?? t('journey.title')}
        </Space>
      }
    >
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        {t('journey.blurb')}
      </Typography.Paragraph>
      <Select
        style={{ width: '100%', marginBottom: 8 }}
        value={pain}
        showSearch
        optionFilterProp="label"
        options={Object.entries(
          pains.reduce<Record<string, { value: string; label: string }[]>>((acc, row) => {
            const group = row.categoryLabel || row.category || t('pain.library');
            acc[group] = acc[group] ?? [];
            acc[group].push({ value: row.code, label: row.label });
            return acc;
          }, {}),
        ).map(([label, options]) => ({ label, options }))}
        onChange={onPain}
      />
      {plan && (
        <>
          <Space wrap style={{ marginBottom: 8 }}>
            <Tag color={plan.isDue ? 'red' : 'blue'}>
              {t(STEP_KEYS[plan.step] ?? plan.step)}
            </Tag>
            <Tag>{plan.painLabel}</Tag>
            {plan.discoveryMode && (
              <Tag color={plan.discoveryMode === 'exploit' ? 'green' : 'blue'}>
                {t(`pain.mode.${plan.discoveryMode}`, { defaultValue: plan.discoveryMode })}
              </Tag>
            )}
            {plan.geminiUsed && <Tag color="purple">{t('chat.voiceTag')}</Tag>}
            {plan.isDue && <Tag color="orange">{t('journey.due')}</Tag>}
          </Space>
          {plan.hook && plan.step === 'hook' && (
            <Typography.Paragraph style={{ marginBottom: 4 }}>
              <Typography.Text type="secondary">{t('pain.hook')}: </Typography.Text>
              {plan.hook}
            </Typography.Paragraph>
          )}
          {plan.whyThisPain && (
            <Typography.Paragraph type="secondary" style={{ fontSize: 12 }}>
              {plan.whyThisPain}
            </Typography.Paragraph>
          )}
          {plan.solutionDirection && (
            <Typography.Paragraph>
              <Typography.Text type="secondary">{t('pain.solutionAfter')}: </Typography.Text>
              {plan.solutionDirection}
            </Typography.Paragraph>
          )}
          <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 8 }}>
            {plan.draft}
          </Typography.Paragraph>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 4 }}>
            {plan.windowLabel}
          </Typography.Paragraph>
          <Typography.Paragraph type="secondary" style={{ fontSize: 12 }}>
            {plan.whyNow}
          </Typography.Paragraph>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('chat.sources')}: {plan.citations.join(' · ')}
          </Typography.Text>
          <Space wrap style={{ marginTop: 8 }}>
            <Button icon={<CopyOutlined />} onClick={() => void copy()}>
              {t('chat.copyReply')}
            </Button>
            <Button
              icon={<HighlightOutlined />}
              loading={rewriting}
              onClick={() => void onRewrite()}
            >
              {t('journey.rewrite')}
            </Button>
            <Button loading={saving} onClick={() => void onSchedule()}>
              {t('journey.schedule')}
            </Button>
            <Button
              type="primary"
              icon={<SendOutlined />}
              loading={saving}
              onClick={() => void onSend()}
            >
              {t('journey.send')}
            </Button>
            <Button onClick={openPharmacyFacebook} disabled={!plan.facebookUrl}>
              {t('journey.openPharmacy')}
            </Button>
          </Space>
        </>
      )}
    </Card>
  );
}
