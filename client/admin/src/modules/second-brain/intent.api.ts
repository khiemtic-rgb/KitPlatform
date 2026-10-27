import { http } from '@/shared/api/http';
import type { IntentEvidence, IntentRuntimeStatus } from './intent-workspace';

export const intentApi = {
  submit: (utterance: string) => http.post<IntentEvidence>(
    '/personal-intelligence/intent',
    { utterance },
    { timeout: 180_000 },
  ).then((response) => response.data),
  status: () => http.get<IntentRuntimeStatus>('/personal-intelligence/intent/status').then((response) => response.data),
};
