import { http } from '@/shared/api/http';
import {
  classifyResearchError,
  type ResearchExperiment,
  type ResearchLoadResult,
  type ResearchPaperReport,
  type ResearchPaperStatus,
  type ResearchPublication,
} from './research-view';

const base = '/personal-intelligence/research';

export const researchApi = {
  publication: () => http.get<ResearchPublication>(`${base}/publication`).then((r) => r.data),
  experiments: () => http.get<{ availability: string; experiments: ResearchExperiment[] }>(`${base}/experiments`).then((r) => r.data),
  experiment: (id: string) => http.get<{ availability: string; experiment: ResearchExperiment | null }>(`${base}/experiments/${encodeURIComponent(id)}`).then((r) => r.data),
  paperStatus: () => http.get<{ availability: string; status: ResearchPaperStatus | null }>(`${base}/paper/status`).then((r) => r.data),
  paperReport: () => http.get<{ availability: string; report: ResearchPaperReport | null }>(`${base}/paper/report`).then((r) => r.data),
};

const rank: Record<string, number> = {
  UNSUPPORTED_VERSION: 6,
  UNREADABLE: 5,
  INCONSISTENT: 4,
  MISSING: 3,
  STALE: 2,
  FRESH: 1,
};

function stricter(values: string[]): string {
  return values.reduce((worst, next) => ((rank[next] ?? 5) > (rank[worst] ?? 5) ? next : worst), 'FRESH');
}

export async function loadResearch(): Promise<ResearchLoadResult> {
  try {
    const publication = await researchApi.publication();
    if (publication.availability !== 'FRESH' && publication.availability !== 'STALE') {
      return { kind: 'ready', availability: publication.availability, publication, experiments: [], status: null, report: null };
    }
    const [experiments, status, report] = await Promise.all([
      researchApi.experiments(),
      researchApi.paperStatus(),
      researchApi.paperReport(),
    ]);
    const availability = stricter([
      publication.availability,
      experiments.availability,
      status.availability,
      report.availability,
    ]);
    const showBodies = availability === 'FRESH' || availability === 'STALE';
    return {
      kind: 'ready',
      availability,
      publication,
      experiments: showBodies ? experiments.experiments : [],
      status: showBodies ? status.status : null,
      report: showBodies ? report.report : null,
    };
  } catch (error) {
    return classifyResearchError(error);
  }
}
