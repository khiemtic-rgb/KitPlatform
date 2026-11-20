import { http } from '@/shared/api/http';

const base = '/content/san-gach-do';

export type SanGachDoCharacter = {
  id: string;
  name: string;
  kind: 'person' | 'prop';
  sheets: string[];
  status: string;
};

export type SanGachDoDesk = {
  film: string;
  root: string;
  folderReady: boolean;
  blenderReady: boolean;
  matched: boolean;
  motionReady: boolean;
  previewReady: boolean;
  characters: SanGachDoCharacter[];
};

export function fetchSanGachDoDesk() {
  return http.get<SanGachDoDesk>(`${base}/characters`).then((r) => r.data);
}

export function fetchSanGachDoPreview() {
  return http.get(`${base}/preview/bo`, { responseType: 'blob' }).then((res) => URL.createObjectURL(res.data as Blob));
}

export async function fetchSanGachDoSheet(id: string, kind: string) {
  const res = await http.get(`${base}/characters/${encodeURIComponent(id)}/sheets/${encodeURIComponent(kind)}`, {
    responseType: 'blob',
  });
  return URL.createObjectURL(res.data as Blob);
}
