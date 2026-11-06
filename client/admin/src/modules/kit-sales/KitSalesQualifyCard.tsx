import { useState } from 'react';
import { Button, Card, Space, Typography, message } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  acceptKitSalesAgentTask,
  dismissKitSalesAgentTask,
  qualifyKitSalesLead,
  type KitSalesQualification,
} from '@/shared/api/kit-sales.api';

export function KitSalesQualifyCard({
  leadId,
  onChanged,
}: {
  leadId: string;
  onChanged?: () => void;
}) {
  const [loading, setLoading] = useState(false);
  const [proposal, setProposal] = useState<KitSalesQualification | null>(null);

  const propose = async () => {
    setLoading(true);
    try {
      setProposal(await qualifyKitSalesLead(leadId));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tạo được đề xuất.'));
    } finally {
      setLoading(false);
    }
  };

  const accept = async () => {
    if (!proposal?.agentTaskId) return;
    setLoading(true);
    try {
      await acceptKitSalesAgentTask(proposal.agentTaskId);
      message.success('Đã nhận đề xuất. Tin nhắn nếu có vẫn là bản nháp.');
      setProposal(null);
      onChanged?.();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không nhận được đề xuất.'));
    } finally {
      setLoading(false);
    }
  };

  const dismiss = async () => {
    if (!proposal?.agentTaskId) return;
    setLoading(true);
    try {
      await dismissKitSalesAgentTask(proposal.agentTaskId);
      setProposal(null);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không gạt được đề xuất.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Card size="small" title="Qualification Copilot">
      <Space direction="vertical" style={{ width: '100%' }}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Novixa được chấm bằng công thức cố định. Famixa và KEMS không được chào. Copilot không gửi tin và không đổi trạng thái.
        </Typography.Paragraph>
        <Button onClick={() => void propose()} loading={loading}>
          Đề xuất
        </Button>
        {proposal && (
          <div>
            <Typography.Text strong>
              {proposal.decision}
              {proposal.scored && proposal.total != null ? ` · ${proposal.total}` : ''}
            </Typography.Text>
            <div>{proposal.formula}</div>
            {proposal.missing.length > 0 && (
              <Typography.Text type="secondary">Thiếu: {proposal.missing.join(', ')}</Typography.Text>
            )}
            {proposal.draft && <Typography.Paragraph>{proposal.draft}</Typography.Paragraph>}
            {proposal.agentTaskId && proposal.taskStatus === 'pending' && (
              <Space>
                <Button type="primary" onClick={() => void accept()} loading={loading}>
                  Nhận
                </Button>
                <Button onClick={() => void dismiss()} loading={loading}>
                  Gạt
                </Button>
              </Space>
            )}
          </div>
        )}
      </Space>
    </Card>
  );
}
