import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

const USER_STATUS_IDS = [0, 1] as const;
const BRANCH_STATUS_IDS = [0, 1] as const;

export function useSystemEnums() {
  const { t } = useTranslation('system');

  const userStatusLabel = (status: number) =>
    t(`enums.userStatus.${status}`, { defaultValue: String(status) });

  const branchStatusLabel = (status: number) =>
    t(`enums.branchStatus.${status}`, { defaultValue: String(status) });

  const auditActionLabel = (action: string) =>
    t(`enums.auditAction.${action}`, { defaultValue: action });

  const auditEntityLabel = (entity: string) =>
    t(`enums.auditEntity.${entity}`, { defaultValue: entity });

  const userStatusOptions = useMemo(
    () => USER_STATUS_IDS.map((value) => ({ value, label: userStatusLabel(value) })),
    [t],
  );

  const branchStatusOptions = useMemo(
    () => BRANCH_STATUS_IDS.map((value) => ({ value, label: branchStatusLabel(value) })),
    [t],
  );

  return {
    userStatusLabel,
    userStatusOptions,
    branchStatusLabel,
    branchStatusOptions,
    auditActionLabel,
    auditEntityLabel,
  };
}
