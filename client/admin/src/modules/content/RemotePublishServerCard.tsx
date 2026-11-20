import { useState } from 'react';
import { Alert, App, Button, Input, Select, Space, Tag, Typography } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { AuthWorkspace } from '@/shared/api/types';
import { remoteLogin, remoteSelectWorkspace, useRemoteSession, type RemoteStatus } from '@/shared/api/content-remote.api';

/**
 * Kết nối Studio trên máy này với máy chủ đăng bài. Người dùng tự gõ tài khoản máy chủ;
 * mật khẩu không lưu ở đâu, chỉ giữ token máy chủ trong trình duyệt này.
 */
export function RemotePublishServerCard({ status }: { status: RemoteStatus }) {
  const { message } = App.useApp();
  const session = useRemoteSession();
  const [tenantCode, setTenantCode] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [choice, setChoice] = useState<{ token: string; workspaces: AuthWorkspace[] } | null>(null);
  const [tenantId, setTenantId] = useState<string>();

  const baseUrl = status.baseUrl || '';
  const loggedIn = Boolean(session.accessToken && session.baseUrl === baseUrl);

  const login = async () => {
    setBusy(true);
    setError('');
    try {
      const result = await remoteLogin(baseUrl, { username: username.trim(), password, tenantCode: tenantCode.trim() || undefined });
      setPassword('');
      if (result.ok) {
        message.success('Đã kết nối máy chủ đăng bài.');
      } else {
        setChoice({ token: result.selectionToken, workspaces: result.workspaces });
        setTenantId(result.workspaces.find((w) => w.isDefault)?.tenantId ?? result.workspaces[0]?.tenantId);
      }
    } catch (reason) {
      setError(apiErrorMessage(reason, 'Không đăng nhập được máy chủ đăng bài.'));
    } finally {
      setBusy(false);
    }
  };

  const pick = async () => {
    if (!choice || !tenantId) return;
    setBusy(true);
    setError('');
    try {
      await remoteSelectWorkspace(baseUrl, choice.token, tenantId);
      setChoice(null);
      message.success('Đã kết nối máy chủ đăng bài.');
    } catch (reason) {
      setError(apiErrorMessage(reason, 'Không chọn được workspace.'));
    } finally {
      setBusy(false);
    }
  };

  if (loggedIn) {
    return (
      <Alert
        type="success"
        showIcon
        message={
          <Space wrap>
            <span>Đồng bộ lên máy chủ đăng bài</span>
            <Tag>{baseUrl.replace(/^https?:\/\//, '')}</Tag>
            <Typography.Text type="secondary">{session.user?.username}</Typography.Text>
          </Space>
        }
        description="Duyệt ở bước này sẽ gửi video thành phẩm và nội dung đăng lên máy chủ. Máy chủ xếp lịch và tự đăng, kể cả khi máy này tắt."
        action={<Button size="small" onClick={() => session.clear()}>Ngắt kết nối</Button>}
      />
    );
  }

  return (
    <Alert
      type="warning"
      showIcon
      message={`Chưa kết nối máy chủ đăng bài (${baseUrl.replace(/^https?:\/\//, '')})`}
      description={
        <Space direction="vertical" size={8} style={{ width: '100%' }}>
          <Typography.Text type="secondary">
            Đăng nhập bằng tài khoản admin của máy chủ một lần. Mật khẩu không được lưu; chỉ giữ phiên đăng nhập trong trình duyệt này.
          </Typography.Text>
          {choice ? (
            <Space wrap>
              <Select
                style={{ minWidth: 260 }}
                value={tenantId}
                onChange={setTenantId}
                options={choice.workspaces.map((w) => ({ value: w.tenantId, label: `${w.tenantName} (${w.tenantCode})` }))}
              />
              <Button type="primary" loading={busy} onClick={() => void pick()}>Chọn workspace</Button>
            </Space>
          ) : (
            <Space wrap>
              <Input placeholder="Mã workspace (nếu có)" style={{ width: 170 }} value={tenantCode} onChange={(e) => setTenantCode(e.target.value)} />
              <Input placeholder="Tài khoản" style={{ width: 170 }} value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" />
              <Input.Password
                placeholder="Mật khẩu"
                style={{ width: 170 }}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
                onPressEnter={() => void login()}
              />
              <Button type="primary" loading={busy} disabled={!username.trim() || !password} onClick={() => void login()}>
                Kết nối
              </Button>
            </Space>
          )}
          {error ? <Typography.Text type="danger">{error}</Typography.Text> : null}
        </Space>
      }
    />
  );
}
