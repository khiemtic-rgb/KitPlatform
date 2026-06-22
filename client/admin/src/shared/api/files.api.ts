import { http } from '@/shared/api/http';

export async function uploadImage(file: File): Promise<string> {
  const formData = new FormData();
  formData.append('file', file);
  const { data } = await http.post<{ url: string } & Record<string, unknown>>('/files/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return String(data.url ?? data.Url ?? '');
}
