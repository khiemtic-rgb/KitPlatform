import { useCallback, useEffect, useState } from 'react';
import { Outlet, useMatch, useNavigate } from 'react-router-dom';
import {
  Alert,
  App,
  Button,
  Card,
  Col,
  Drawer,
  Empty,
  Form,
  Input,
  Row,
  Tabs,
  Tag,
  Typography,
} from 'antd';
import {
  CheckCircleOutlined,
  EditOutlined,
  ExclamationCircleOutlined,
  PlusOutlined,
  ReloadOutlined,
  SaveOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { isAxiosError } from 'axios';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchProductionAiCatalog,
  fetchProductionCredentialBackupGate,
  rotateProductionCredential,
  submitModelAiCredential,
  type ProductionAiCatalog,
  type ProductionAiCatalogModel,
  type ProductionCredentialBackupGate,
} from '@/shared/api/production-ai-catalog.api';
import { registerProductionCredential } from '@/shared/api/content.api';
import { TenantAiConfigurationCard } from './TenantAiConfigurationCard';
import { readActionableRecovery, recoveryForCode } from '@/shared/errors/actionable-error';
import { ProductionCredentialSecurityPanel } from './ProductionCredentialSecurityPanel';
import {
  BACKUP_BLOCKED_BODY,
  BACKUP_BLOCKED_TITLE,
  PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED,
  backupConditionLabel,
  type BackupGateLoadError,
} from './production-credential-backup-notice';

type CredentialStatus = 'ACTIVE' | 'NOT_CONFIGURED' | 'AMBIGUOUS';

const STATUS_LABELS: Record<CredentialStatus, string> = {
  ACTIVE: 'Đang hoạt động',
  NOT_CONFIGURED: 'Chưa cấu hình',
  AMBIGUOUS: 'Cần kiểm tra',
};

const STATUS_COLORS: Record<CredentialStatus, 'success' | 'warning' | 'error'> = {
  ACTIVE: 'success',
  NOT_CONFIGURED: 'warning',
  AMBIGUOUS: 'error',
};

const STATUS_ICONS: Record<CredentialStatus, React.ReactElement> = {
  ACTIVE: <CheckCircleOutlined />,
  NOT_CONFIGURED: <ExclamationCircleOutlined />,
  AMBIGUOUS: <WarningOutlined />,
};

export function ContentAiSettingsPage() {
  const { message } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [catalog, setCatalog] = useState<ProductionAiCatalog>({ video: [], voice: [], lipSync: [], image: [] });
  const [tab, setTab] = useState<'video' | 'image' | 'voice' | 'lipsync'>('video');
  const [configureModel, setConfigureModel] = useState<ProductionAiCatalogModel | null>(null);
  const [saving, setSaving] = useState(false);
  const [tenantConfigKey, setTenantConfigKey] = useState(0);
  const [gate, setGate] = useState<ProductionCredentialBackupGate | null>(null);
  const [gateError, setGateError] = useState<BackupGateLoadError | null>(null);
  const [backupBlocked, setBackupBlocked] = useState(false);
  const [imageHint, setImageHint] = useState<string | null>(null);
  const navigate = useNavigate();
  const securityOpen = useMatch('/content/ai/credential-security') !== null;
  const backupRecovery = recoveryForCode(PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED);
  const [form] = Form.useForm<{ secret: string; displayName?: string }>();

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    const [catalogResult, gateResult] = await Promise.allSettled([
      fetchProductionAiCatalog(),
      fetchProductionCredentialBackupGate(),
    ]);
    if (catalogResult.status === 'fulfilled') {
      setCatalog(catalogResult.value);
      setTenantConfigKey((k) => k + 1);
    } else {
      setError(apiErrorMessage(catalogResult.reason, 'Không thể tải cấu hình Model AI'));
    }
    if (gateResult.status === 'fulfilled') {
      setGate(gateResult.value);
      setGateError(null);
    } else {
      setGate(null);
      const status = isAxiosError(gateResult.reason) ? gateResult.reason.response?.status : 0;
      setGateError(status === 401 || status === 403 ? 'forbidden' : 'unavailable');
    }
    setLoading(false);
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const onConfigure = async () => {
    if (!configureModel || configureModel.credentialStatus === 'AMBIGUOUS') return;
    const imageModel =
      configureModel.capability === 'Picture' || configureModel.capability === 'IMAGE'
        ? configureModel.displayName
        : null;
    try {
      const values = await form.validateFields();
      setSaving(true);
      const outcome = await submitModelAiCredential(configureModel, values, {
        register: registerProductionCredential,
        rotate: rotateProductionCredential,
        reload: async () => {
          form.resetFields();
          setConfigureModel(null);
          setBackupBlocked(false);
          await load();
        },
      });
      if (imageModel) setImageHint(imageModel);
      if (outcome.kind === 'rotated') {
        message.success(
          `Đã cập nhật ${configureModel.displayName} · ${STATUS_LABELS[outcome.status as CredentialStatus] ?? outcome.status} · phiên bản ${outcome.version}. Chưa bật capability cho đến khi lưu Tenant AI Config.`,
        );
      } else {
        message.success(
          imageModel
            ? `Đã lưu credential ${configureModel.displayName}. Chọn credential ở dòng Hình ảnh, bật cấu hình, rồi bấm Lưu.`
            : `Đã cấu hình ${configureModel.displayName}`,
        );
      }
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      if (readActionableRecovery(e)?.code === PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED) {
        setBackupBlocked(true);
        return;
      }
      setBackupBlocked(false);
      message.error(apiErrorMessage(e, 'Không lưu được cấu hình'));
    } finally {
      setSaving(false);
    }
  };

  const renderModelCard = (model: ProductionAiCatalogModel) => {
    const statusLabel = STATUS_LABELS[model.credentialStatus];
    const statusColor = STATUS_COLORS[model.credentialStatus];
    const statusIcon = STATUS_ICONS[model.credentialStatus];

    return (
      <Card
        key={`${model.providerId}-${model.modelId}`}
        size="small"
        style={{
          marginBottom: 16,
          borderRadius: 8,
          borderColor: model.credentialStatus === 'ACTIVE' ? '#52c41a' : undefined,
        }}
      >
        <div style={{ marginBottom: 12 }}>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {model.providerId}
          </Typography.Text>
          <Typography.Title level={5} style={{ margin: '4px 0 0 0' }}>
            {model.displayName}
          </Typography.Title>
          <Tag style={{ marginTop: 4 }}>{model.capability}</Tag>
        </div>

        <div style={{ marginBottom: 12 }}>
          <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
            Trạng thái
          </Typography.Text>
          <Tag icon={statusIcon} color={statusColor}>
            {statusLabel}
          </Tag>
        </div>

        {model.credentialStatus === 'NOT_CONFIGURED' && (
          <Button
            type="primary"
            size="small"
            icon={<PlusOutlined />}
            onClick={() => {
              form.resetFields();
              setBackupBlocked(false);
              setConfigureModel(model);
            }}
          >
            Cấu hình
          </Button>
        )}

        {model.credentialStatus === 'ACTIVE' && (
          <Button
            size="small"
            icon={<EditOutlined />}
            onClick={() => {
              form.resetFields();
              setBackupBlocked(false);
              setConfigureModel(model);
            }}
          >
            Chỉnh sửa
          </Button>
        )}

        {model.credentialStatus === 'AMBIGUOUS' && (
          <Typography.Text type="danger" style={{ fontSize: 12 }}>
            <WarningOutlined /> Có nhiều credential đang hoạt động cho model này. Cần kiểm tra trước khi chỉnh sửa.
          </Typography.Text>
        )}
      </Card>
    );
  };

  const tabItems = [
    {
      key: 'video',
      label: 'Video',
      children: (
        <>
          {catalog.video.length === 0 && !loading ? (
            <Empty description="Chưa có mô hình AI khả dụng." />
          ) : (
            <Row gutter={16}>
              {catalog.video.map((model) => (
                <Col xs={24} sm={12} md={8} lg={6} key={`${model.providerId}-${model.modelId}`}>
                  {renderModelCard(model)}
                </Col>
              ))}
            </Row>
          )}
        </>
      ),
    },
    {
      key: 'image',
      label: 'Hình ảnh',
      children: (
        <>
          {catalog.image.length === 0 && !loading ? (
            <Empty description="Chưa có mô hình AI khả dụng." />
          ) : (
            <Row gutter={16}>
              {catalog.image.map((model) => (
                <Col xs={24} sm={12} md={8} lg={6} key={`${model.providerId}-${model.modelId}`}>
                  {renderModelCard(model)}
                </Col>
              ))}
            </Row>
          )}
        </>
      ),
    },
    {
      key: 'voice',
      label: 'Giọng nói',
      children: (
        <>
          {catalog.voice.length === 0 && !loading ? (
            <Empty description="Chưa có mô hình AI khả dụng." />
          ) : (
            <Row gutter={16}>
              {catalog.voice.map((model) => (
                <Col xs={24} sm={12} md={8} lg={6} key={`${model.providerId}-${model.modelId}`}>
                  {renderModelCard(model)}
                </Col>
              ))}
            </Row>
          )}
        </>
      ),
    },
    {
      key: 'lipsync',
      label: 'Lip Sync',
      children: (
        <>
          {catalog.lipSync.length === 0 && !loading ? (
            <Empty description="Chưa có mô hình AI khả dụng." />
          ) : (
            <Row gutter={16}>
              {catalog.lipSync.map((model) => (
                <Col xs={24} sm={12} md={8} lg={6} key={`${model.providerId}-${model.modelId}`}>
                  {renderModelCard(model)}
                </Col>
              ))}
            </Row>
          )}
        </>
      ),
    },
  ];

  const conditionLabel = backupConditionLabel(gate, gateError);
  const openSecurity = () => {
    if (backupRecovery) navigate(backupRecovery.route);
  };

  if (loading && configureModel === null && !securityOpen) {
    return (
      <Card>
        <div style={{ textAlign: 'center', padding: '40px 0' }}>
          <Typography.Text type="secondary">Đang tải các mô hình AI...</Typography.Text>
        </div>
      </Card>
    );
  }

  if (error && configureModel === null && !securityOpen) {
    return (
      <Card>
        <Alert
          type="error"
          showIcon
          message="Không thể tải cấu hình Model AI"
          description={error}
          action={
            <Button size="small" type="primary" onClick={() => void load()}>
              Thử lại
            </Button>
          }
        />
      </Card>
    );
  }

  return (
    <>
      <TenantAiConfigurationCard reloadKey={tenantConfigKey} />
      {imageHint && (
        <Alert
          type="success"
          showIcon
          style={{ marginBottom: 16 }}
          message="Credential đã lưu. Capability Hình ảnh chưa được bật."
          description={`Chọn credential của ${imageHint} ở dòng Hình ảnh, bật cấu hình, rồi bấm Lưu. Tải lại trang và đối chiếu trạng thái với dữ liệu máy chủ.`}
        />
      )}
      {conditionLabel && (
        <Alert
          type={conditionLabel === 'Đạt' ? 'success' : conditionLabel === 'Chưa đạt' ? 'warning' : 'info'}
          showIcon
          style={{ marginBottom: 16 }}
          message={`Điều kiện sao lưu khóa mã hóa: ${conditionLabel}`}
          description={
            gateError === 'forbidden'
              ? 'Cần quản trị viên kiểm tra điều kiện này.'
              : gateError === 'unavailable'
                ? 'Không đọc được trạng thái từ máy chủ. Credential chưa được ghi nhận là đã lưu.'
                : conditionLabel === 'Đạt'
                  ? 'Khóa mã hóa đang dùng đã được niêm và mở lại thành công trên máy này. Có thể lưu thông tin xác thực.'
                : conditionLabel === 'Chưa đạt'
                  ? 'Chưa lưu được thông tin xác thực. Mở điều kiện bảo mật để xem trạng thái và bước tiếp theo.'
                  : 'Máy chủ có thể đang cho phép ghi. Bằng chứng khôi phục vẫn chưa có.'
          }
          action={
            conditionLabel === 'Chưa đạt' && backupRecovery ? (
              <Button size="small" type="primary" onClick={openSecurity}>
                {backupRecovery.actionLabel}
              </Button>
            ) : undefined
          }
        />
      )}
      {securityOpen ? (
        <ProductionCredentialSecurityPanel
          gate={gate}
          gateError={gateError}
          checking={loading}
          secretHeld={Boolean(form.getFieldValue('secret'))}
          onRecheck={() => void load()}
          onBack={() => navigate('/content/ai')}
        />
      ) : (
      <Card>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <Typography.Text type="secondary">
            Mô hình AI cho video, hình ảnh, giọng nói và khớp môi dùng khi dựng phim.
          </Typography.Text>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
        </div>

        <Tabs
          activeKey={tab}
          onChange={(key) => setTab(key as typeof tab)}
          items={tabItems}
        />
      </Card>
      )}

      <Outlet />
      <Drawer
        title={`${configureModel?.credentialStatus === 'ACTIVE' ? 'Chỉnh sửa' : 'Cấu hình'} ${configureModel?.displayName || 'Model'}`}
        width={480}
        open={configureModel !== null}
        onClose={() => setConfigureModel(null)}
        destroyOnClose={false}
        extra={
          <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={() => void onConfigure()}>
            Lưu cấu hình
          </Button>
        }
      >
        {configureModel && (
          <>
            <div style={{ marginBottom: 24 }}>
              <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
                Model
              </Typography.Text>
              <Typography.Title level={5} style={{ margin: '4px 0 0 0' }}>
                {configureModel.displayName}
              </Typography.Title>
            </div>

            <div style={{ marginBottom: 24 }}>
              <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
                Provider
              </Typography.Text>
              <Typography.Text>{configureModel.providerId}</Typography.Text>
            </div>

            <div style={{ marginBottom: 24 }}>
              <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
                Capability
              </Typography.Text>
              <Tag>{configureModel.capability}</Tag>
            </div>

            {backupBlocked && (
              <Alert
                type="error"
                showIcon
                style={{ marginBottom: 16 }}
                message={BACKUP_BLOCKED_TITLE}
                description={
                  <>
                    <div>{BACKUP_BLOCKED_BODY}</div>
                    <div style={{ marginTop: 8 }}>
                      Mã lỗi: {PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED}
                    </div>
                    <div style={{ marginTop: 12 }}>
                      <Button size="small" type="primary" onClick={openSecurity}>
                        {backupRecovery?.actionLabel ?? 'Mở điều kiện bảo mật'}
                      </Button>
                    </div>
                  </>
                }
              />
            )}

            <Form form={form} layout="vertical" onFinish={() => void onConfigure()}>
              {configureModel.credentialStatus === 'NOT_CONFIGURED' && (
                <Form.Item name="displayName" label="Tên hiển thị (tuỳ chọn)">
                  <Input placeholder={configureModel.displayName} autoComplete="off" />
                </Form.Item>
              )}

              <Form.Item
                name="secret"
                label="API Key / Secret"
                rules={[{ required: true, message: 'Nhập API key hoặc secret' }]}
                extra="Secret chỉ ghi một chiều và không hiện lại sau khi lưu."
              >
                <Input.Password placeholder="Nhập API key" autoComplete="new-password" visibilityToggle={false} />
              </Form.Item>

              <Alert
                type="info"
                showIcon
                message="Bảo mật"
                description="Secret không được hiển thị lại và được mã hóa an toàn. Chỉ tenant của bạn có thể sử dụng credential này."
                style={{ marginTop: 16 }}
              />
            </Form>
          </>
        )}
      </Drawer>
    </>
  );
}
