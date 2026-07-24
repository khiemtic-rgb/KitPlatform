import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, App, Button, Drawer, Form, InputNumber, Select, Space, Tree, Typography } from 'antd';
import type { TreeProps } from 'antd';
import {
  fetchPlatformModules,
  fetchPlatformTenantEntitlement,
  updatePlatformTenantEntitlement,
} from '@/shared/api/platform.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { PlatformModuleRegistryItem, PlatformTenantListItem } from '@/shared/api/platform.types';
import {
  PLATFORM_PACK_TEMPLATES,
  buildModuleTreeData,
  leafKeysFromChecked,
  pruneModulesToVertical,
  resolvePackTemplateModules,
} from '@/modules/platform/platform-module-tree';

const VERTICAL_OPTIONS = [
  'pharmacy',
  'pharmacy_chain',
  'supplement_store',
  'medical_equipment_store',
  'clinic',
  'lab',
  'medical_spa',
  'hybrid',
  'family',
] as const;

type EntitlementFormValues = {
  vertical: string;
  allowedModules: string[];
  packTemplate?: string;
  /** null = unlimited */
  maxBranches?: number | null;
};

type Props = {
  open: boolean;
  tenant: PlatformTenantListItem | null;
  platformKey?: string;
  onClose: () => void;
  onSaved: () => void;
};

export function PlatformTenantEntitlementDrawer({ open, tenant, platformKey, onClose, onSaved }: Props) {
  const { t } = useTranslation('auth', { keyPrefix: 'setup' });
  const { message } = App.useApp();
  const [form] = Form.useForm<EntitlementFormValues>();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [registry, setRegistry] = useState<PlatformModuleRegistryItem[]>([]);

  const vertical = Form.useWatch('vertical', form);
  const allowedModules = Form.useWatch('allowedModules', form) ?? [];

  useEffect(() => {
    if (!open || !tenant) return;

    let cancelled = false;
    void (async () => {
      setLoading(true);
      try {
        const [entitlement, modules] = await Promise.all([
          fetchPlatformTenantEntitlement(tenant.id, platformKey),
          fetchPlatformModules(platformKey),
        ]);
        if (cancelled) return;
        setRegistry(modules);
        const pruned = pruneModulesToVertical(
          entitlement.allowedModules,
          modules,
          entitlement.vertical,
        );
        form.setFieldsValue({
          vertical: entitlement.vertical,
          allowedModules: pruned.length > 0 ? pruned : entitlement.allowedModules,
          packTemplate: undefined,
          maxBranches: entitlement.maxBranches,
        });
      } catch (error) {
        if (!cancelled) {
          message.error(apiErrorMessage(error, t('entitlement.loadFailed')));
          onClose();
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [open, tenant, platformKey, form, message, t, onClose]);

  const treeData = useMemo(
    () =>
      buildModuleTreeData(registry, vertical, (labelKey) =>
        t(labelKey, { defaultValue: labelKey }),
      ),
    [registry, vertical, t],
  );

  const onVerticalChange = (nextVertical: string) => {
    const current = form.getFieldValue('allowedModules') as string[] | undefined;
    const pruned = pruneModulesToVertical(current ?? [], registry, nextVertical);
    form.setFieldsValue({
      vertical: nextVertical,
      allowedModules: pruned,
      packTemplate: undefined,
    });
  };

  const onApplyTemplate = (templateId: string) => {
    const resolved = resolvePackTemplateModules(templateId, registry);
    if (!resolved) {
      message.error(t('entitlement.templateUnknown'));
      return;
    }
    form.setFieldsValue({
      packTemplate: templateId,
      vertical: resolved.vertical,
      allowedModules: resolved.moduleCodes,
    });
    message.success(
      t('entitlement.templateApplied', {
        count: resolved.moduleCodes.length,
        vertical: t(`verticals.${resolved.vertical}`, { defaultValue: resolved.vertical }),
      }),
    );
  };

  const onTreeCheck: TreeProps['onCheck'] = (checked) => {
    form.setFieldsValue({
      allowedModules: leafKeysFromChecked(checked),
      packTemplate: undefined,
    });
  };

  const onFinish = async (values: EntitlementFormValues) => {
    if (!tenant) return;
    const modules = pruneModulesToVertical(values.allowedModules ?? [], registry, values.vertical);
    if (modules.length === 0) {
      message.error(t('entitlement.modulesRequired'));
      return;
    }

    const maxBranches =
      values.maxBranches == null || Number.isNaN(Number(values.maxBranches))
        ? null
        : Math.floor(Number(values.maxBranches));

    setSaving(true);
    try {
      await updatePlatformTenantEntitlement(
        tenant.id,
        {
          vertical: values.vertical,
          allowedModules: modules,
          syncEnabledModules: true,
          maxBranches,
        },
        platformKey,
      );
      message.success(t('entitlement.saveSuccess'));
      onSaved();
      onClose();
    } catch (error) {
      message.error(apiErrorMessage(error, t('entitlement.saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Drawer
      title={tenant ? t('entitlement.title', { code: tenant.tenantCode }) : t('entitlement.configure')}
      width={560}
      open={open}
      onClose={onClose}
      destroyOnClose
    >
      <Form form={form} layout="vertical" onFinish={(v) => void onFinish(v)} disabled={loading}>
        <Alert type="info" showIcon style={{ marginBottom: 16 }} message={t('entitlement.intro')} />

        <Form.Item
          name="packTemplate"
          label={t('entitlement.packTemplate')}
          extra={t('entitlement.packTemplateHint')}
        >
          <Select
            allowClear
            placeholder={t('entitlement.packTemplatePlaceholder')}
            options={PLATFORM_PACK_TEMPLATES.map((tpl) => ({
              value: tpl.id,
              label: t(tpl.labelKey, { defaultValue: tpl.id }),
            }))}
            onChange={(id) => {
              if (id) onApplyTemplate(String(id));
            }}
          />
        </Form.Item>

        <Form.Item name="vertical" label={t('entitlement.vertical')} rules={[{ required: true }]}>
          <Select
            options={VERTICAL_OPTIONS.map((code) => ({
              value: code,
              label: t(`verticals.${code}`, { defaultValue: code }),
            }))}
            onChange={onVerticalChange}
          />
        </Form.Item>

        <Form.Item
          name="maxBranches"
          label={t('entitlement.maxBranches')}
          extra={t('entitlement.maxBranchesHint')}
        >
          <InputNumber
            min={1}
            max={999}
            precision={0}
            style={{ width: '100%' }}
            placeholder={t('entitlement.maxBranchesUnlimited')}
          />
        </Form.Item>

        <Form.Item
          name="allowedModules"
          label={t('entitlement.allowedModules')}
          rules={[{ required: true, message: t('entitlement.modulesRequired') }]}
          extra={t('entitlement.modulesTreeHint')}
        >
          <input type="hidden" />
        </Form.Item>

        {treeData.length === 0 ? (
          <Alert type="warning" showIcon message={t('entitlement.noModulesForVertical')} />
        ) : (
          <Tree
            checkable
            defaultExpandAll
            selectable={false}
            treeData={treeData}
            checkedKeys={allowedModules}
            onCheck={onTreeCheck}
            style={{ marginBottom: 16, maxHeight: '50vh', overflow: 'auto' }}
          />
        )}

        <Space direction="vertical" size={4} style={{ width: '100%', marginBottom: 16 }}>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('entitlement.selectedCount', { count: allowedModules.length })}
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('entitlement.skuNote')}
          </Typography.Text>
        </Space>

        <Button type="primary" htmlType="submit" loading={saving} block>
          {t('entitlement.save')}
        </Button>
      </Form>
    </Drawer>
  );
}
