import { api } from '@/lib/api';

export interface WsiPresignedUrlResponse {
  url: string;
  key: string;
}

export interface WsiUploadResponse {
  id: string;
  s3Key: string;
  fileName: string;
  contentType: string | null;
  fileSizeBytes: number;
  widthPx: number | null;
  heightPx: number | null;
  createdAt: string;
}

export interface WsiJobResponse {
  id: string;
  wsiUploadId: string;
  status: string;
  resultS3Key: string | null;
  errorMessage: string | null;
  createdAt: string;
  completedAt: string | null;
}

export async function getWsiUploadUrl(
  fileName: string,
  contentType?: string
): Promise<WsiPresignedUrlResponse> {
  const { data } = await api.post<WsiPresignedUrlResponse>('/wsi/upload-url', {
    fileName,
    contentType: contentType ?? 'application/octet-stream',
  });
  return data;
}

export async function uploadWsiFile(file: File): Promise<string> {
  const { url, key } = await getWsiUploadUrl(file.name, file.type || 'application/octet-stream');

  await fetch(url, {
    method: 'PUT',
    body: file,
    headers: {
      'Content-Type': file.type || 'application/octet-stream',
    },
  });

  return key;
}

export async function registerWsiUpload(request: {
  s3Key: string;
  fileName: string;
  contentType?: string;
  fileSizeBytes: number;
  widthPx?: number;
  heightPx?: number;
}): Promise<WsiUploadResponse> {
  const { data } = await api.post<WsiUploadResponse>('/wsi/uploads', request);
  return data;
}

export async function getWsiUploads(): Promise<WsiUploadResponse[]> {
  const { data } = await api.get<WsiUploadResponse[]>('/wsi/uploads');
  return data;
}

export async function getWsiUpload(id: string): Promise<WsiUploadResponse | null> {
  try {
    const { data } = await api.get<WsiUploadResponse>(`/wsi/uploads/${id}`);
    return data;
  } catch (e) {
    if (e && typeof e === 'object' && 'response' in e && (e as { response?: { status?: number } }).response?.status === 404) {
      return null;
    }
    throw e;
  }
}

export async function triggerWsiAnalysis(uploadId: string): Promise<WsiJobResponse> {
  const { data } = await api.post<WsiJobResponse>(`/wsi/uploads/${uploadId}/analyze`);
  return data;
}

export async function getWsiJob(jobId: string): Promise<WsiJobResponse | null> {
  try {
    const { data } = await api.get<WsiJobResponse>(`/wsi/jobs/${jobId}`);
    return data;
  } catch (e) {
    if (e && typeof e === 'object' && 'response' in e && (e as { response?: { status?: number } }).response?.status === 404) {
      return null;
    }
    throw e;
  }
}
