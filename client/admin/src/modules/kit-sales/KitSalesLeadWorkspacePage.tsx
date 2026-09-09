import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Button,
  Card,
  Col,
  DatePicker,
  Form,
  Input,
  List,
  Row,
  Select,
  Space,
  Tag,
  Timeline,
  Typography,
  message,
} from 'antd';
import { ArrowLeftOutlined, CheckOutlined, LeftOutlined, RightOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  completeKitSalesTask,
  createKitSalesInteraction,
  createKitSalesTask,
  fetchKitSalesAssist,
  fetchKitSalesLeadDetail,
  updateKitSalesLead,
  type KitSalesAssist,
  type KitSalesInteraction,
  type KitSalesLeadDetail,
  type KitSalesTask,
} from '@/shared/api/kit-sales.api';
import { KitSalesFacebookChatCard } from './KitSalesFacebookChatCard';
import { KitSalesJourneyCard } from './KitSalesJourneyCard';
import { KitSalesPainCard } from './KitSalesPainCard';
import { KitSalesPsidCard } from './KitSalesPsidCard';
import {
  KIT_SALES_ACTION_CODES,
  KIT_SALES_CHANNELS,
  KIT_SALES_INTERACTION_TYPES,
  KIT_SALES_PIPELINE_STATUSES,
  KIT_SALES_TEMPERATURES,
  kitSalesActionI18nKey,
  nextPipelineStatus,
  prevPipelineStatus,
} from './kit-sales.constants';
import { formatKitSalesDate, kitSalesTemperatureColor } from './kit-sales.helpers';

export function KitSalesLeadWorkspacePage() {
  const { leadId } = useParams<{ leadId: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation('kitSales');
  const [loading, setLoading] = useState(false);
  const [advancing, setAdvancing] = useState(false);
  const [logging, setLogging] = useState(false);
  const [savingTask, setSavingTask] = useState(false);
  const [completingId, setCompletingId] = useState<string | null>(null);
  const [detail, setDetail] = useState<KitSalesLeadDetail | null>(null);
  const [assist, setAssist] = useState<KitSalesAssist | null>(null);
  const [logForm] = Form.useForm();
  const [taskForm] = Form.useForm();

  const statusLabel = (code: string) => t(`status.${code}`, { defaultValue: code });
  const temperatureLabel = (code: string) => t(`temperature.${code}`, { defaultValue: code });
  const actionLabel = (code: string) => t(kitSalesActionI18nKey(code), { defaultValue: code });
  const channelLabel = (code: string) => t(`channel.${code}`, { defaultValue: code });
  const typeLabel = (code: string) => t(`interactionType.${code}`, { defaultValue: code });

  const loadDetail = async (id: string) => {
    setLoading(true);
    try {
      const [next, brief] = await Promise.all([
        fetchKitSalesLeadDetail(id),
        fetchKitSalesAssist(id).catch(() => null),
      ]);
      setDetail(next);
      setAssist(brief);
    } catch (error) {
      message.error(apiErrorMessage(error, t('detail.failed')));
      navigate('/kit-sales/leads', { replace: true });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!leadId) return;
    void loadDetail(leadId);
  }, [leadId]);

  const refresh = async () => {
    if (!leadId) return;
    await loadDetail(leadId);
  };

  const lead = detail?.lead ?? null;
  const nextStatus = lead ? nextPipelineStatus(lead.leadStatus) : null;
  const prevStatus = lead ? prevPipelineStatus(lead.leadStatus) : null;

  const setStatus = async (status: string) => {
    if (!lead) return;
    setAdvancing(true);
    try {
      await updateKitSalesLead(lead.id, { leadStatus: status });
      message.success(t('detail.stageSuccess'));
      await refresh();
    } catch (error) {
      message.error(apiErrorMessage(error, t('detail.stageFailed')));
    } finally {
      setAdvancing(false);
    }
  };

  const setTemperature = async (code: string) => {
    if (!lead) return;
    try {
      await updateKitSalesLead(lead.id, { leadTemperature: code });
      await refresh();
    } catch (error) {
      message.error(apiErrorMessage(error, t('leads.editFailed')));
    }
  };

  const onLog = async () => {
    if (!lead) return;
    const values = await logForm.validateFields();
    setLogging(true);
    try {
      await createKitSalesInteraction(lead.id, {
        interactionType: values.interactionType,
        channel: values.channel,
        content: values.content?.trim() || undefined,
      });
      message.success(t('log.success'));
      logForm.resetFields();
      logForm.setFieldsValue({ interactionType: 'note', channel: 'phone' });
      await refresh();
    } catch (error) {
      message.error(apiErrorMessage(error, t('log.failed')));
    } finally {
      setLogging(false);
    }
  };

  const onCreateTask = async () => {
    if (!lead) return;
    const values = await taskForm.validateFields();
    setSavingTask(true);
    try {
      const due = values.dueAt as Dayjs | undefined;
      await createKitSalesTask(lead.id, {
        actionCode: values.actionCode,
        title: actionLabel(values.actionCode),
        dueAt: due?.isValid() ? due.toISOString() : undefined,
      });
      message.success(t('task.success'));
      taskForm.resetFields();
      taskForm.setFieldsValue({ actionCode: 'call' });
      await refresh();
    } catch (error) {
      message.error(apiErrorMessage(error, t('task.failed')));
    } finally {
      setSavingTask(false);
    }
  };

  const onCompleteTask = async (task: KitSalesTask) => {
    setCompletingId(task.id);
    try {
      await completeKitSalesTask(task.id);
      message.success(t('task.completeSuccess'));
      await refresh();
    } catch (error) {
      message.error(apiErrorMessage(error, t('task.completeFailed')));
    } finally {
      setCompletingId(null);
    }
  };

  const openTasks = (detail?.tasks ?? []).filter((row) => row.status === 'open');
  const interactions = detail?.interactions ?? [];

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Space wrap>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/kit-sales')}>
          {t('leadWorkspace.back')}
        </Button>
        <Button type="link" onClick={() => navigate('/kit-sales/leads')}>
          {t('nav.leads')}
        </Button>
      </Space>

      {lead && (
        <>
          <div>
            <Typography.Title level={3} style={{ marginBottom: 4 }}>
              {lead.businessName}
            </Typography.Title>
            <Space wrap>
              <Typography.Text type="secondary">{lead.province || '—'}</Typography.Text>
              <Tag color={kitSalesTemperatureColor(lead.leadTemperature)}>
                {temperatureLabel(lead.leadTemperature)}
              </Tag>
              <Tag>{statusLabel(lead.leadStatus)}</Tag>
              {lead.phone && <a href={`tel:${lead.phone}`}>{lead.phone}</a>}
              {lead.facebookUrl && (
                <Typography.Link href={lead.facebookUrl} target="_blank" rel="noreferrer">
                  {t('leads.openFacebook')}
                </Typography.Link>
              )}
            </Space>
          </div>

          <Row gutter={[16, 16]}>
            <Col xs={24} xl={16}>
              <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                {assist && (
                  <Card size="small" title={t('leadWorkspace.aiSummary')} loading={loading && !assist}>
                    <Typography.Text strong>{assist.headline}</Typography.Text>
                    <Typography.Paragraph style={{ marginBottom: 0, marginTop: 8 }}>
                      {assist.summary}
                    </Typography.Paragraph>
                  </Card>
                )}
                <KitSalesPainCard leadId={lead.id} onChanged={() => void refresh()} />
                <KitSalesJourneyCard leadId={lead.id} onChanged={() => void refresh()} />
                <KitSalesPsidCard leadId={lead.id} />
                <KitSalesFacebookChatCard leadId={lead.id} onLogged={() => void refresh()} />
              </Space>
            </Col>
            <Col xs={24} xl={8}>
              <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                {assist && (
                  <Card size="small" title={t('chat.nba')}>
                    <Typography.Text strong>{assist.nextActionLabel}</Typography.Text>
                    <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
                      {assist.nextHint}
                    </Typography.Paragraph>
                    <Typography.Text type="secondary">{assist.windowLabel}</Typography.Text>
                  </Card>
                )}

                <Card size="small" title={t('detail.stage')}>
                  <Space wrap>
                    <Button
                      icon={<LeftOutlined />}
                      disabled={!prevStatus}
                      loading={advancing}
                      onClick={() => prevStatus && void setStatus(prevStatus)}
                    >
                      {prevStatus ? statusLabel(prevStatus) : t('detail.noPrev')}
                    </Button>
                    <Button
                      type="primary"
                      icon={<RightOutlined />}
                      disabled={!nextStatus}
                      loading={advancing}
                      onClick={() => nextStatus && void setStatus(nextStatus)}
                    >
                      {nextStatus ? statusLabel(nextStatus) : t('detail.noNext')}
                    </Button>
                    {lead.leadStatus !== 'lost' && (
                      <Button danger loading={advancing} onClick={() => void setStatus('lost')}>
                        {statusLabel('lost')}
                      </Button>
                    )}
                  </Space>
                  <Select
                    style={{ width: '100%', marginTop: 8 }}
                    value={lead.leadStatus}
                    disabled={advancing}
                    options={[...KIT_SALES_PIPELINE_STATUSES, 'lost'].map((code) => ({
                      value: code,
                      label: statusLabel(code),
                    }))}
                    onChange={(value) => void setStatus(value)}
                  />
                  <Select
                    style={{ width: '100%', marginTop: 8 }}
                    value={lead.leadTemperature}
                    options={KIT_SALES_TEMPERATURES.map((code) => ({
                      value: code,
                      label: temperatureLabel(code),
                    }))}
                    onChange={(value) => void setTemperature(value)}
                  />
                </Card>

                <Card size="small" title={t('task.title')}>
                  {lead.nextActionCode && (
                    <Typography.Paragraph>
                      {t('task.current', {
                        action: actionLabel(lead.nextActionCode),
                        when: formatKitSalesDate(lead.nextActionAt),
                      })}
                    </Typography.Paragraph>
                  )}
                  <Form
                    form={taskForm}
                    layout="vertical"
                    initialValues={{ actionCode: 'call' }}
                    onFinish={() => void onCreateTask()}
                  >
                    <Space.Compact style={{ width: '100%' }}>
                      <Form.Item name="actionCode" style={{ flex: 1, marginBottom: 8 }}>
                        <Select
                          options={KIT_SALES_ACTION_CODES.map((code) => ({
                            value: code,
                            label: actionLabel(code),
                          }))}
                        />
                      </Form.Item>
                      <Form.Item name="dueAt" style={{ flex: 1, marginBottom: 8 }}>
                        <DatePicker showTime style={{ width: '100%' }} placeholder={t('task.dueAt')} />
                      </Form.Item>
                    </Space.Compact>
                    <Button type="primary" htmlType="submit" loading={savingTask}>
                      {t('task.submit')}
                    </Button>
                  </Form>
                  <List
                    size="small"
                    dataSource={openTasks}
                    locale={{ emptyText: t('task.empty') }}
                    renderItem={(row) => (
                      <List.Item
                        actions={[
                          <Button
                            key="done"
                            type="link"
                            size="small"
                            icon={<CheckOutlined />}
                            loading={completingId === row.id}
                            onClick={() => void onCompleteTask(row)}
                          >
                            {t('task.complete')}
                          </Button>,
                        ]}
                      >
                        <List.Item.Meta
                          title={row.title || actionLabel(row.actionCode)}
                          description={row.dueAt ? formatKitSalesDate(row.dueAt) : t('task.noDue')}
                        />
                      </List.Item>
                    )}
                  />
                </Card>
              </Space>
            </Col>
          </Row>

          <Card size="small" title={t('log.title')}>
            <Form
              form={logForm}
              layout="vertical"
              initialValues={{ interactionType: 'note', channel: 'phone' }}
              onFinish={() => void onLog()}
            >
              <Space.Compact style={{ width: '100%' }}>
                <Form.Item name="interactionType" style={{ flex: 1, marginBottom: 8 }}>
                  <Select
                    options={KIT_SALES_INTERACTION_TYPES.map((code) => ({
                      value: code,
                      label: typeLabel(code),
                    }))}
                  />
                </Form.Item>
                <Form.Item name="channel" style={{ flex: 1, marginBottom: 8 }}>
                  <Select
                    options={KIT_SALES_CHANNELS.map((code) => ({
                      value: code,
                      label: channelLabel(code),
                    }))}
                  />
                </Form.Item>
              </Space.Compact>
              <Form.Item name="content" style={{ marginBottom: 8 }}>
                <Input.TextArea rows={2} placeholder={t('log.content')} />
              </Form.Item>
              <Button type="primary" htmlType="submit" loading={logging}>
                {t('log.submit')}
              </Button>
            </Form>
            {interactions.length === 0 ? (
              <Typography.Text type="secondary">{t('log.empty')}</Typography.Text>
            ) : (
              <Timeline
                style={{ marginTop: 16 }}
                items={interactions.map((row: KitSalesInteraction) => ({
                  children: (
                    <div>
                      <Space wrap size={4}>
                        <Typography.Text strong>
                          {typeLabel(row.interactionType)} · {channelLabel(row.channel)}
                        </Typography.Text>
                        {row.reviewStatus && (
                          <Tag
                            color={
                              row.reviewStatus === 'sent'
                                ? 'green'
                                : row.reviewStatus === 'approved'
                                  ? 'blue'
                                  : 'gold'
                            }
                          >
                            {row.reviewStatus === 'sent'
                              ? t('chat.statusSent')
                              : row.reviewStatus === 'approved'
                                ? t('chat.statusApproved')
                                : t('chat.statusDraft')}
                          </Tag>
                        )}
                      </Space>
                      <div>
                        <Typography.Text type="secondary">
                          {formatKitSalesDate(row.occurredAt)}
                        </Typography.Text>
                      </div>
                      {row.content && <div>{row.content}</div>}
                    </div>
                  ),
                }))}
              />
            )}
          </Card>
        </>
      )}
    </Space>
  );
}
