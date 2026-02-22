import { api } from '@/lib/api';

export interface PresignedUrlResponse {
  url: string;
  key: string;
}

/**
 * Get a presigned upload URL from the API, then upload the file directly to S3.
 * Returns the object key on success.
 */
export async function uploadFile(file: File): Promise<string> {
  const { data } = await api.post<PresignedUrlResponse>('/storage/upload-url', {
    fileName: file.name,
    contentType: file.type || 'application/octet-stream',
  });

  await fetch(data.url, {
    method: 'PUT',
    body: file,
    headers: {
      'Content-Type': file.type || 'application/octet-stream',
    },
  });

  return data.key;
}
