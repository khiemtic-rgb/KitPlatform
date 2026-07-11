import { useState } from 'react';
import {
  Button,
  Card,
  Descriptions,
  Drawer,
  Empty,
  Input,
  List,
  Modal,
  Space,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import {
  CloseCircleOutlined,
  CopyOutlined,
  EditOutlined,
  EyeOutlined,
  PlusOutlined,
  SendOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router-dom';
import {
  cancelPortalPrescription,
  fetchPortalPrescription,
  fetchPortalPrescriptions,
  fetchPrescriptionShare,
  getApiErrorMessage,
} from '@/shared/api/prescriber-portal.api';
import {
  canAmendPortalPrescription,
  type PortalPrescriptionDetail,
} from '@/shared/api/prescriber-portal.types';
import { rxStatusColor, rxStatusLabel } from '@/shared/ui/status-labels';

async function copyText(text: string): Promise<boolean> {
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch {
    /* fall through */
  }
  try {
    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.left = '-9999px';
    document.body.appendChild(area);
    area.select();
    const ok = document.execCommand('copy');
    document.body.removeChild(area);
    return ok;
  } catch {
    return false;
  }
}

export function PrescriptionsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [detailId, setDetailId] = useState<string | null>(null);
  const [shareOpen, setShareOpen] = useState(false);
  const [shareLink, setShareLink] = useState('');
  const [shareCode, setShareCode] = useState('');
  const [shareLoadingId, setShareLoadingId] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ['prescriber', 'prescriptions'],
    queryFn: () => fetchPortalPrescriptions(),
  });

  const detailQuery = useQuery({
    queryKey: ['prescriber', 'prescription', detailId],
    queryFn: () => fetchPortalPrescription(detailId!),
    enabled: Boolean(detailId),
  });

  const cancelMutation = useMutation({
    mutationFn: (id: string) => cancelPortalPrescription(id, t('prescriptions.cancelReasonDefault')),
    onSuccess: () => {
      message.success(t('prescriptions.cancelSuccess'));
      void queryClient.invalidateQueries({ queryKey: ['prescriber', 'prescriptions'] });
      setDetailId(null);
    },
    onError: (error) => message.error(getApiErrorMessage(error, t('prescriptions.cancelFailed'))),
  });

  const openShare = async (prescriptionId: string) => {
    setShareLoadingId(prescriptionId);
    try {
      const share = await fetchPrescriptionShare(prescriptionId);
      setShareLink(share.posDeepLink);
      setShareCode(share.prescriptionCode);
      setShareOpen(true);
    } catch (error) {
      message.error(getApiErrorMessage(error, t('prescriptions.linkCopyFailed')));
    } finally {
      setShareLoadingId(null);
    }
  };

  const copyShareLink = async () => {
    if (!shareLink) return;
    const ok = await copyText(shareLink);
    if (ok) message.success(t('prescriptions.linkCopied'));
    else message.warning(t('prescriptions.linkCopyFailed'));
  };

  const detail = detailQuery.data;
  const canAmend = detail ? canAmendPortalPrescription(detail) : false;

  const confirmCancel = (rx: PortalPrescriptionDetail) => {
    Modal.confirm({
      title: t('prescriptions.cancelConfirmTitle'),
      content: t('prescriptions.cancelConfirmBody', { code: rx.prescriptionCode }),
      okText: t('prescriptions.cancelAction'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () => cancelMutation.mutateAsync(rx.id),
    });
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <Typography.Title level={4} style={{ margin: 0 }}>
          {t('prescriptions.title')}
        </Typography.Title>
        <Link to="/prescriptions/new">
          <Button type="primary" icon={<PlusOutlined />}>
            {t('prescriptions.new')}
          </Button>
        </Link>
      </div>
      <Card loading={query.isLoading}>
        {!query.data?.length ? (
          <Empty description={t('prescriptions.empty')} />
        ) : (
          <List
            dataSource={query.data}
            renderItem={(item) => (
              <List.Item
                actions={[
                  <Button
                    key="view"
                    type="link"
                    icon={<EyeOutlined />}
                    onClick={() => setDetailId(item.id)}
                  >
                    {t('prescriptions.view')}
                  </Button>,
                  item.status === 'signed' ? (
                    <Button
                      key="edit"
                      type="link"
                      icon={<EditOutlined />}
                      onClick={() => navigate(`/prescriptions/${item.id}/edit`)}
                    >
                      {t('prescriptions.edit')}
                    </Button>
                  ) : null,
                  item.status !== 'cancelled' ? (
                    <Button
                      key="share"
                      type="link"
                      icon={<SendOutlined />}
                      loading={shareLoadingId === item.id}
                      onClick={() => void openShare(item.id)}
                    >
                      {t('prescriptions.copyPosLink')}
                    </Button>
                  ) : null,
                ].filter(Boolean)}
              >
                <List.Item.Meta
                  title={
                    <Space>
                      <Typography.Link onClick={() => setDetailId(item.id)}>
                        {item.prescriptionCode}
                      </Typography.Link>
                      <Tag color={rxStatusColor(item.status)}>{rxStatusLabel(t, item.status)}</Tag>
                    </Space>
                  }
                  description={
                    <>
                      {item.tenantName} · {item.patientName ?? item.patientPhone ?? '—'} · {item.lineCount}{' '}
                      {t('prescriptions.lineCount')}
                    </>
                  }
                />
              </List.Item>
            )}
          />
        )}
      </Card>

      <Drawer
        title={detail?.prescriptionCode ?? t('prescriptions.view')}
        width={560}
        open={Boolean(detailId)}
        onClose={() => setDetailId(null)}
        destroyOnClose
        extra={
          detail ? (
            <Space>
              {canAmend ? (
                <Button
                  type="primary"
                  icon={<EditOutlined />}
                  onClick={() => {
                    setDetailId(null);
                    navigate(`/prescriptions/${detail.id}/edit`);
                  }}
                >
                  {t('prescriptions.edit')}
                </Button>
              ) : null}
              {canAmend ? (
                <Button
                  danger
                  icon={<CloseCircleOutlined />}
                  loading={cancelMutation.isPending}
                  onClick={() => confirmCancel(detail)}
                >
                  {t('prescriptions.cancelAction')}
                </Button>
              ) : null}
            </Space>
          ) : null
        }
      >
        {detailQuery.isLoading ? (
          <Typography.Text type="secondary">{t('common.loading')}</Typography.Text>
        ) : detailQuery.isError ? (
          <Typography.Text type="danger">
            {getApiErrorMessage(detailQuery.error, t('prescriptions.loadFailed'))}
          </Typography.Text>
        ) : detail ? (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label={t('prescriptions.status')}>
                <Tag color={rxStatusColor(detail.status)}>{rxStatusLabel(t, detail.status)}</Tag>
              </Descriptions.Item>
              <Descriptions.Item label={t('prescriptions.pharmacy')}>{detail.tenantName}</Descriptions.Item>
              <Descriptions.Item label={t('prescriptions.customer')}>
                {detail.patientName ?? '—'} {detail.patientPhone ? `· ${detail.patientPhone}` : ''}
              </Descriptions.Item>
              <Descriptions.Item label={t('prescriptions.notes')}>{detail.notes || '—'}</Descriptions.Item>
            </Descriptions>
            {!canAmend && detail.status === 'signed' ? (
              <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
                {t('prescriptions.editBlockedDispensed')}
              </Typography.Paragraph>
            ) : null}
            <Table
              size="small"
              pagination={false}
              rowKey="id"
              dataSource={detail.lines}
              columns={[
                {
                  title: t('prescriptions.product'),
                  render: (_, row) => (
                    <span>
                      {row.productName}
                      <Typography.Text type="secondary"> ({row.productCode})</Typography.Text>
                    </span>
                  ),
                },
                {
                  title: t('prescriptions.qty'),
                  dataIndex: 'qtyPrescribed',
                  width: 80,
                },
                {
                  title: t('prescriptions.unit'),
                  dataIndex: 'unitName',
                  width: 90,
                  render: (v) => v || '—',
                },
                {
                  title: t('prescriptions.dosage'),
                  dataIndex: 'dosageInstruction',
                  render: (v) => v || '—',
                },
              ]}
            />
            {detail.status !== 'cancelled' ? (
              <Button
                icon={<SendOutlined />}
                loading={shareLoadingId === detail.id}
                onClick={() => void openShare(detail.id)}
              >
                {t('prescriptions.copyPosLink')}
              </Button>
            ) : null}
          </Space>
        ) : null}
      </Drawer>

      <Modal
        title={t('prescriptions.shareTitle')}
        open={shareOpen}
        onCancel={() => setShareOpen(false)}
        footer={[
          <Button key="close" onClick={() => setShareOpen(false)}>
            {t('prescriptions.shareClose')}
          </Button>,
          <Button key="copy" type="primary" icon={<CopyOutlined />} onClick={() => void copyShareLink()}>
            {t('prescriptions.shareCopy')}
          </Button>,
        ]}
      >
        <Typography.Paragraph type="secondary">{t('prescriptions.shareHint')}</Typography.Paragraph>
        {shareCode ? (
          <Typography.Paragraph>
            <Tag color="green">{shareCode}</Tag>
          </Typography.Paragraph>
        ) : null}
        <Input.TextArea value={shareLink} readOnly autoSize={{ minRows: 2, maxRows: 4 }} />
      </Modal>
    </div>
  );
}
