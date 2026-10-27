import { http } from '@/shared/api/http';

export type TaskStep = {
  key: string;
  kind: string;
  dependsOn: string[];
  status: string;
  note?: string | null;
};

export type PaperTask = {
  taskId: string;
  taskKey: string;
  status: string;
  goal: string;
  decisionId: string;
  operationKey: string;
  decisionStatus: string;
  paperExecutionCount: number;
  verificationNote?: string | null;
  steps?: TaskStep[];
};

export const taskApi = {
  list: () => http.get<PaperTask[]>('/personal-intelligence/paper-tasks').then((response) => response.data),
  get: (taskId: string) => http.get<PaperTask>(`/personal-intelligence/paper-tasks/${taskId}`).then((response) => response.data),
};
