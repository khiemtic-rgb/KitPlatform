import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, Input, Space, Tag, Typography, message } from 'antd';
import {
  CopyOutlined,
  EditOutlined,
  MessageOutlined,
  ReloadOutlined,
  SendOutlined,
  SettingOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  composeKitSalesChat,
  createKitSalesTask,
  fetchKitSalesAssist,
  fetchKitSalesChatChannel,
  sendKitSalesDraft,
  type KitSalesAssist,
  type KitSalesChatChannel,
  type KitSalesChatCompose,
  type KitSalesInteraction,
} from '@/shared/api/kit-sales.api';

interface KitSalesFacebookChatCardProps {
  leadId?: string | null;
  compact?: boolean;
  onLogged?: () => void;
}

function reviewColor(status: string) {
  if (status === 'sent') return 'green';
  if (status === 'approved') return 'blue';
  return 'gold';
}

export function KitSalesFacebookChatCard({
  leadId,
  compact,
  onLogged,
}: KitSalesFacebookChatCardProps) {
  const { t } = useTranslation('kitSales');
  const navigate = useNavigate();
  const [channel, setChannel] = useState<KitSalesChatChannel | null>(null);
  const [assist, setAssist] = useState<KitSalesAssist | null>(null);
  const [text, setText] = useState('');
  const [compose, setCompose] = useState<KitSalesChatCompose | null>(null);
  const [draft, setDraft] = useState('');
  const [editing, setEditing] = useState(false);
  const [sent, setSent] = useState<KitSalesInteraction | null>(null);
  const [loading, setLoading] = useState(false);
  const [sending, setSending] = useState(false);
  const [savingTask, setSavingTask] = useState(false);
  const lastAutoKey = useRef('');

  const loadAssist = async (id: string) => {
    try {
      setAssist(await fetchKitSalesAssist(id));
    } catch {
      setAssist(null);
    }
  };

  useEffect(() => {
    void fetchKitSalesChatChannel()
      .then(setChannel)
      .catch(() => undefined);
  }, []);

  useEffect(() => {
    lastAutoKey.current = '';
    setText('');
    setCompose(null);
    setDraft('');
    setSent(null);
    setEditing(false);
    if (!leadId) {
      setAssist(null);
      return;
    }
    void loadAssist(leadId);
  }, [leadId]);

  const runAutoReply = async (inbound: string) => {
    const trimmed = inbound.trim();
    if (!trimmed || !leadId) return;
    const key = `${leadId}:${trimmed}`;
    if (lastAutoKey.current === key) return;
    lastAutoKey.current = key;
    setLoading(true);
    try {
      const next = await composeKitSalesChat(trimmed, leadId, true);
      setCompose(next);
      setDraft(next.reply);
      setEditing(false);
      if (next.reviewStatus === 'sent') {
        setSent({
          id: next.draftId ?? key,
          leadId,
          channel: 'facebook',
          direction: 'outbound',
          interactionType: 'message',
          content: next.reply,
          occurredAt: new Date().toISOString(),
          reviewStatus: 'sent',
        });
      } else {
        setSent(null);
      }
      if (next.assist) setAssist(next.assist);
      if (next.autoReplied && next.reply) {
        try {
          await navigator.clipboard.writeText(next.reply);
          message.success(next.facebookSendReady ? t('chat.autoReplied') : t('chat.autoCopied'));
        } catch {
          message.success(t('chat.autoReplied'));
        }
      }
      onLogged?.();
    } catch (error) {
      lastAutoKey.current = '';
      message.error(apiErrorMessage(error, t('chat.failed')));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!assist?.needsReply || !assist.lastInboundContent || !leadId) return;
    setText(assist.lastInboundContent);
    void runAutoReply(assist.lastInboundContent);
  }, [assist?.needsReply, assist?.lastInboundContent, leadId]);

  useEffect(() => {
    const trimmed = text.trim();
    if (trimmed.length < 2 || !leadId) return;
    const timer = window.setTimeout(() => void runAutoReply(trimmed), 800);
    return () => window.clearTimeout(timer);
  }, [text, leadId]);

  const onCompose = async () => {
    lastAutoKey.current = '';
    await runAutoReply(text);
  };

  const onSend = async () => {
    if (!compose?.draftId || !leadId) return;
    setSending(true);
    try {
      const row = await sendKitSalesDraft(compose.draftId, draft.trim() || compose.reply);
      setSent(row);
      setCompose({ ...compose, reviewStatus: 'sent', reply: row.content ?? draft });
      try {
        await navigator.clipboard.writeText(row.content ?? draft);
        message.success(t('chat.sentCopied'));
      } catch {
        message.success(t('chat.sent'));
      }
      onLogged?.();
      await loadAssist(leadId);
    } catch (error) {
      message.error(apiErrorMessage(error, t('chat.failed')));
    } finally {
      setSending(false);
    }
  };

  const onAcceptAction = async () => {
    if (!leadId || !assist) return;
    setSavingTask(true);
    try {
      await createKitSalesTask(leadId, {
        actionCode: assist.nextActionCode,
        title: assist.nextActionLabel,
        dueAt: assist.suggestedAt,
      });
      message.success(t('task.success'));
      onLogged?.();
    } catch (error) {
      message.error(apiErrorMessage(error, t('task.failed')));
    } finally {
      setSavingTask(false);
    }
  };

  const copy = async (value: string) => {
    try {
      await navigator.clipboard.writeText(value);
      message.success(t('chat.copied'));
    } catch {
      message.error(t('chat.failed'));
    }
  };

  const reviewStatus = sent?.reviewStatus ?? compose?.reviewStatus ?? 'draft';

  return (
    <Card
      size={compact ? 'small' : 'default'}
      title={
        <Space>
          <MessageOutlined />
          {t('chat.title')}
        </Space>
      }
    >
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        {t('chat.blurb')} {t('chat.voiceHint')}
      </Typography.Paragraph>
      {assist && (
        <>
          <Typography.Text strong>{t('chat.talkingTo', { name: assist.businessName })}</Typography.Text>
          <div style={{ marginTop: 8, marginBottom: 12 }}>
            <Typography.Text type="secondary">{t('chat.goal')}</Typography.Text>
            <div>{assist.goal}</div>
            <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8 }}>
              {t('chat.context')}
            </Typography.Text>
            <Typography.Text>{assist.context}</Typography.Text>
          </div>
        </>
      )}
      <Space wrap style={{ marginBottom: 12 }}>
        {channel && (
          <Tag color={channel.facebookSendReady ? 'green' : 'default'}>
            {channel.facebookSendReady ? t('chat.live') : t('chat.previewOnly')}
          </Tag>
        )}
        <Button
          size="small"
          icon={<CopyOutlined />}
          onClick={() => void copy(channel?.meLink ?? 'https://m.me/novixa68')}
        >
          {t('chat.copyMe')}
        </Button>
        <Button
          size="small"
          icon={<CopyOutlined />}
          onClick={() => void copy(channel?.phcUrl ?? 'https://novixa.vn/vi/health-check/')}
        >
          {t('chat.copyPhc')}
        </Button>
        <Button size="small" icon={<SettingOutlined />} onClick={() => navigate('/kit-sales/settings')}>
          {t('nav.settings')}
        </Button>
      </Space>

      <Typography.Text type="secondary">{t('chat.inboundLabel')}</Typography.Text>
      <Input.TextArea
        rows={compact ? 2 : 3}
        value={text}
        onChange={(event) => setText(event.target.value)}
        placeholder={t('chat.placeholder')}
        style={{ marginTop: 4 }}
      />
      <Button
        type="primary"
        loading={loading}
        onClick={() => void onCompose()}
        style={{ marginTop: 8 }}
      >
        {t('chat.preview')}
      </Button>

      {compose && (
        <div style={{ marginTop: 12 }}>
          <Typography.Text type="secondary">{t('chat.analysis')}</Typography.Text>
          <div>
            <Tag color={compose.escalate ? 'orange' : 'gold'}>
              {compose.escalate ? t('chat.escalate') : compose.classificationLabel}
            </Tag>
            {compose.geminiUsed && <Tag color="purple">{t('chat.voiceTag')}</Tag>}
          </div>
          <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 8 }}>
            {t('chat.nextHint')}: {compose.nextHint}
          </Typography.Paragraph>

          <Card size="small" style={{ background: '#fafafa' }}>
            <Space wrap style={{ marginBottom: 8 }}>
              <Tag color={reviewColor(reviewStatus)}>
                {reviewStatus === 'sent'
                  ? t('chat.statusSent')
                  : reviewStatus === 'approved'
                    ? t('chat.statusApproved')
                    : t('chat.statusDraft')}
              </Tag>
            </Space>
            {sent && (
              <Typography.Paragraph type="secondary">
                {t('chat.sentAt', {
                  when: dayjs(sent.sentAt ?? sent.occurredAt).format('HH:mm'),
                  who: sent.approvedByName || sent.sentByName || t('chat.you'),
                })}
              </Typography.Paragraph>
            )}
            {editing && reviewStatus !== 'sent' ? (
              <Input.TextArea
                rows={4}
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
              />
            ) : (
              <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 8 }}>
                {draft}
              </Typography.Paragraph>
            )}
            <Space wrap>
              {reviewStatus !== 'sent' && (
                <Button
                  icon={editing ? <ReloadOutlined /> : <EditOutlined />}
                  onClick={() => setEditing((value) => !value)}
                >
                  {editing ? t('chat.doneEdit') : t('chat.edit')}
                </Button>
              )}
              <Button icon={<CopyOutlined />} onClick={() => void copy(draft)}>
                {t('chat.copyReply')}
              </Button>
              {leadId && compose.draftId && reviewStatus !== 'sent' && (
                <Button
                  type="primary"
                  icon={<SendOutlined />}
                  loading={sending}
                  onClick={() => void onSend()}
                >
                  {t('chat.send')}
                </Button>
              )}
            </Space>
            <div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('chat.sources')}: {compose.citations.join(' · ')}
              </Typography.Text>
            </div>
          </Card>
        </div>
      )}

      {assist && leadId && (
        <Card size="small" style={{ marginTop: 12 }}>
          <Typography.Text type="secondary">{t('chat.nba')}</Typography.Text>
          <div style={{ marginTop: 4 }}>
            <Typography.Text strong>{assist.nextActionLabel}</Typography.Text>
          </div>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
            {assist.windowLabel}
          </Typography.Paragraph>
          <Button loading={savingTask} onClick={() => void onAcceptAction()}>
            {t('chat.acceptNba')}
          </Button>
        </Card>
      )}
    </Card>
  );
}
