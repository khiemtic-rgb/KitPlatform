import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, Col, List, Row, Select, Space, Tag, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchKitSalesAssist,
  fetchKitSalesJourneyDue,
  fetchKitSalesLeads,
  fetchKitSalesPipelineSummary,
  type KitSalesAssist,
  type KitSalesJourneyPlan,
  type KitSalesLead,
  type KitSalesPipelineSummary,
} from '@/shared/api/kit-sales.api';
import { KitSalesFacebookChatCard } from './KitSalesFacebookChatCard';
import { KitSalesJourneyCard } from './KitSalesJourneyCard';
import { classifyKitSalesToday, kitSalesTemperatureColor } from './kit-sales.helpers';

export function KitSalesWorkspacePage() {
  const { t } = useTranslation('kitSales');
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [leads, setLeads] = useState<KitSalesLead[]>([]);
  const [dueJourneys, setDueJourneys] = useState<KitSalesJourneyPlan[]>([]);
  const [summary, setSummary] = useState<KitSalesPipelineSummary | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [assist, setAssist] = useState<KitSalesAssist | null>(null);
  const [query, setQuery] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [rows, pipeline] = await Promise.all([
        fetchKitSalesLeads({ limit: 200, province: 'Thái Nguyên' }),
        fetchKitSalesPipelineSummary(),
      ]);
      setLeads(rows);
      setSummary(pipeline);
      try {
        setDueJourneys(await fetchKitSalesJourneyDue(20));
      } catch {
        setDueJourneys([]);
      }
    } catch (error) {
      message.error(apiErrorMessage(error, t('create.failed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const today = useMemo(() => classifyKitSalesToday(leads, dueJourneys), [dueJourneys, leads]);
  const selected = leads.find((row) => row.id === selectedId) ?? null;
  const demoCount =
    summary?.byStatus.find((row) => row.status === 'demo')?.count ?? today.demo.length;

  useEffect(() => {
    if (selectedId) return;
    const first = today.due[0] ?? today.followup[0] ?? today.phc[0] ?? today.recent[0];
    if (first) setSelectedId(first.id);
  }, [selectedId, today]);

  useEffect(() => {
    if (!selectedId) {
      setAssist(null);
      return;
    }
    void fetchKitSalesAssist(selectedId)
      .then(setAssist)
      .catch(() => setAssist(null));
  }, [selectedId]);

  const statusLabel = (code: string) => t(`status.${code}`, { defaultValue: code });
  const temperatureLabel = (code: string) => t(`temperature.${code}`, { defaultValue: code });

  const queue = [
    { key: 'due' as const, title: t('workspace.needAction'), rows: today.due, color: 'red' },
    { key: 'followup' as const, title: t('workspace.followup'), rows: today.followup, color: 'gold' },
    { key: 'phc' as const, title: t('workspace.phc'), rows: today.phc, color: 'blue' },
  ];

  const searchOptions = leads
    .filter((row) => row.businessName.toLowerCase().includes(query.trim().toLowerCase()))
    .slice(0, 12)
    .map((row) => ({ value: row.id, label: row.businessName }));

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Row justify="space-between" align="middle" gutter={[12, 12]}>
        <Col>
          <Typography.Title level={3} style={{ marginBottom: 0 }}>
            {t('title')}
          </Typography.Title>
        </Col>
        <Col>
          <Space wrap>
            <Select
              showSearch
              allowClear
              placeholder={t('workspace.search')}
              style={{ minWidth: 240 }}
              filterOption={false}
              onSearch={setQuery}
              options={searchOptions}
              onSelect={(id: string) => navigate(`/kit-sales/leads/${id}`)}
            />
            <Button icon={<PlusOutlined />} onClick={() => navigate('/kit-sales/leads')}>
              {t('workspace.add')}
            </Button>
          </Space>
        </Col>
      </Row>

      <Typography.Text type="secondary">
        {t('workspace.strip', {
          due: today.due.length,
          followup: today.followup.length,
          phc: today.phc.length,
          demo: demoCount,
        })}
      </Typography.Text>

      <Row gutter={[16, 16]}>
        <Col xs={24} lg={8}>
          <Card size="small" title={t('workspace.title')} loading={loading && leads.length === 0}>
            {queue.every((group) => group.rows.length === 0) && (
              <Typography.Paragraph type="secondary">{t('workspace.emptyDue')}</Typography.Paragraph>
            )}
            {queue.map((group) =>
              group.rows.length === 0 ? null : (
                <div key={group.key} style={{ marginBottom: 12 }}>
                  <Typography.Text type="secondary">
                    {group.title} · {group.rows.length}
                  </Typography.Text>
                  <List
                    size="small"
                    dataSource={group.rows}
                    renderItem={(row) => (
                      <List.Item
                        onClick={() => setSelectedId(row.id)}
                        style={{
                          cursor: 'pointer',
                          background: selectedId === row.id ? '#e6f4ff' : undefined,
                          paddingInline: 8,
                          borderRadius: 6,
                        }}
                      >
                        <Space direction="vertical" size={0} style={{ width: '100%' }}>
                          <Typography.Text strong>{row.businessName}</Typography.Text>
                          <Space size={4} wrap>
                            <Tag color={group.color}>{group.title}</Tag>
                            <Tag color={kitSalesTemperatureColor(row.leadTemperature)}>
                              {temperatureLabel(row.leadTemperature)}
                            </Tag>
                          </Space>
                        </Space>
                      </List.Item>
                    )}
                  />
                </div>
              ),
            )}
          </Card>
        </Col>

        <Col xs={24} lg={16}>
          <Card size="small" loading={loading && !selected}>
            {!selected ? (
              <Typography.Paragraph type="secondary">{t('workspace.pick')}</Typography.Paragraph>
            ) : (
              <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <div>
                  <Typography.Title level={4} style={{ marginBottom: 4 }}>
                    {selected.businessName}
                  </Typography.Title>
                  <Space wrap>
                    <Typography.Text type="secondary">{selected.province || '—'}</Typography.Text>
                    <Tag color={kitSalesTemperatureColor(selected.leadTemperature)}>
                      {temperatureLabel(selected.leadTemperature)}
                    </Tag>
                    <Tag>{statusLabel(selected.leadStatus)}</Tag>
                  </Space>
                </div>
                {assist && (
                  <div>
                    <Typography.Text type="secondary">{t('workspace.aiSuggest')}</Typography.Text>
                    <div>
                      <Typography.Text>{assist.goal || assist.nextHint}</Typography.Text>
                    </div>
                  </div>
                )}
                <KitSalesJourneyCard
                  leadId={selected.id}
                  title={t('workspace.greeting')}
                  onChanged={() => void load()}
                />
                <KitSalesFacebookChatCard
                  compact
                  leadId={selected.id}
                  onLogged={() => void load()}
                />
                <Button type="primary" onClick={() => navigate(`/kit-sales/leads/${selected.id}`)}>
                  {t('workspace.openLead')}
                </Button>
              </Space>
            )}
          </Card>
        </Col>
      </Row>

      <Card size="small" title={t('workspace.recent')}>
        <Space wrap>
          {today.recent.map((row) => (
            <Button
              key={row.id}
              type={selectedId === row.id ? 'primary' : 'default'}
              onClick={() => setSelectedId(row.id)}
            >
              {row.businessName}
            </Button>
          ))}
        </Space>
      </Card>
    </Space>
  );
}
