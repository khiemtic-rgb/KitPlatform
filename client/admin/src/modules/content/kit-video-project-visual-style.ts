export const PROJECT_VISUAL_STYLE_V1_ID = 'FAMIXA_PROJECT_VISUAL_STYLE_V1';
export const PROJECT_VISUAL_STYLE_SUITE = 'FAMIXA_PROJECT_VISUAL_STYLE_V1_REGRESSION';
export const PROJECT_VISUAL_STYLE_V2_ID = 'FAMIXA_PROJECT_VISUAL_STYLE_V2';
export const PROJECT_VISUAL_STYLE_V2_SUITE = 'FAMIXA_PROJECT_VISUAL_STYLE_V2_REGRESSION';

export const PROJECT_STYLE_PRESETS = [
  { styleKey: '3D_STYLIZED_REALISM', styleName: '3D Stylized Realism' },
  { styleKey: '3D_CARTOON', styleName: '3D Cartoon' },
  { styleKey: 'PHOTOREALISTIC', styleName: 'Photorealistic' },
  { styleKey: 'CINEMATIC_REALISM', styleName: 'Cinematic Realism' },
  { styleKey: 'ILLUSTRATION', styleName: 'Illustration' },
  { styleKey: 'ANIME', styleName: 'Anime' },
  { styleKey: 'CUSTOM', styleName: 'Custom' },
] as const;

export function projectStyleConfigured(status?: string | null) {
  const s = (status || '').toUpperCase();
  return s === 'ACTIVE' || s === 'LOCKED';
}

export function projectStyleReady(row?: { ready?: boolean; status?: string; sha?: string | null } | null) {
  return Boolean(row?.ready && projectStyleConfigured(row.status) && (row.sha || '').length === 64);
}

export function visualStyleRevisionMayRequest(status?: string | null) {
  const s = (status || '').toUpperCase();
  return !s || s === 'REJECTED' || s === 'LOCKED';
}

export function visualStyleRevisionMayApprove(status?: string | null) {
  return (status || '').toUpperCase() === 'PENDING_REVIEW';
}

export function visualStyleRevisionMayReject(status?: string | null) {
  return (status || '').toUpperCase() === 'PENDING_REVIEW';
}

export function visualStyleRevisionMayLock(status?: string | null) {
  return (status || '').toUpperCase() === 'APPROVED';
}
