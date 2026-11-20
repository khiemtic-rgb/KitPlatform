import { useEffect, useState } from 'react';
import { Typography } from 'antd';
import { Link } from 'react-router-dom';
import { listProductionCredentials } from '@/shared/api/content.api';

type CredentialState = 'loading' | 'active' | 'missing' | 'ambiguous';

/**
 * Video desk status for the tenant runway/gen4_turbo credential. No secret field.
 */
export function FamixaProductionCredentialStatus() {
  const [state, setState] = useState<CredentialState>('loading');

  useEffect(() => {
    let cancelled = false;
    void listProductionCredentials()
      .then((rows) => {
        if (cancelled) return;
        const active = rows.filter(
          (row) =>
            row.providerId === 'runway'
            && row.modelId === 'gen4_turbo'
            && row.status === 'ACTIVE'
            && row.identityStatus === 'ACTIVE',
        );
        if (active.length === 1) setState('active');
        else if (active.length === 0) setState('missing');
        else setState('ambiguous');
      })
      .catch(() => {
        if (!cancelled) setState('missing');
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const label =
    state === 'active'
      ? 'Active'
      : state === 'ambiguous'
        ? 'PRODUCTION_CREDENTIAL_AMBIGUOUS'
        : state === 'loading'
          ? '…'
          : 'Not configured';

  return (
    <div data-production-credential-status="1" data-credential-state={state} style={{ marginBottom: 8 }}>
      <div>
        <Typography.Text type="secondary">Provider: </Typography.Text>
        <Typography.Text>Runway</Typography.Text>
      </div>
      <div>
        <Typography.Text type="secondary">Model: </Typography.Text>
        <Typography.Text>gen4_turbo</Typography.Text>
      </div>
      <div>
        <Typography.Text type="secondary">Credential: </Typography.Text>
        <Typography.Text>{label}</Typography.Text>
        {state === 'missing' ? (
          <>
            {' '}
            <Link to="/content/ai">Cấu hình AI</Link>
          </>
        ) : null}
      </div>
    </div>
  );
}
