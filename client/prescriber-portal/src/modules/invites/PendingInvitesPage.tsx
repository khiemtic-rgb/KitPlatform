import { Button, Card, Empty, List, Space, Typography, message } from 'antd';
import { CheckOutlined, CloseOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  acceptPharmacyInvite,
  fetchPendingInvites,
  getApiErrorMessage,
  rejectPharmacyInvite,
} from '@/shared/api/prescriber-portal.api';

export function PendingInvitesPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const invitesQuery = useQuery({
    queryKey: ['prescriber', 'invites'],
    queryFn: fetchPendingInvites,
  });

  const acceptMutation = useMutation({
    mutationFn: acceptPharmacyInvite,
    onSuccess: async () => {
      message.success(t('invites.acceptSuccess'));
      await queryClient.invalidateQueries({ queryKey: ['prescriber'] });
    },
    onError: (error) => message.error(getApiErrorMessage(error, t('invites.acceptFailed'))),
  });

  const rejectMutation = useMutation({
    mutationFn: (linkId: string) => rejectPharmacyInvite(linkId),
    onSuccess: async () => {
      message.success(t('invites.rejectSuccess'));
      await queryClient.invalidateQueries({ queryKey: ['prescriber'] });
    },
    onError: (error) => message.error(getApiErrorMessage(error, t('invites.rejectFailed'))),
  });

  return (
    <div>
      <Typography.Title level={4}>{t('invites.title')}</Typography.Title>
      <Card loading={invitesQuery.isLoading}>
        {!invitesQuery.data?.length ? (
          <Empty description={t('invites.empty')} />
        ) : (
          <List
            dataSource={invitesQuery.data}
            renderItem={(item) => (
              <List.Item>
                <List.Item.Meta title={item.tenantName} description={item.tenantCode} />
                <Space>
                  <Button
                    type="primary"
                    icon={<CheckOutlined />}
                    loading={acceptMutation.isPending && acceptMutation.variables === item.id}
                    onClick={() => acceptMutation.mutate(item.id)}
                  >
                    {t('invites.accept')}
                  </Button>
                  <Button
                    danger
                    icon={<CloseOutlined />}
                    loading={rejectMutation.isPending && rejectMutation.variables === item.id}
                    onClick={() => rejectMutation.mutate(item.id)}
                  >
                    {t('invites.reject')}
                  </Button>
                </Space>
              </List.Item>
            )}
          />
        )}
      </Card>
    </div>
  );
}
