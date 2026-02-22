import { api } from '@/lib/api';

const MAX_SINGLE_PUT_BYTES = 5 * 1024 * 1024 * 1024; // 5 GB

export interface WsiPresignedUrlResponse {
  url: string;
  key: string;
  uploadId: string;
}

export interface WsiUploadResponse {
  id: string;
  s3Key: string;
  fileName: string;
  contentType: string | null;
  fileSizeBytes: number;
  widthPx: number | null;
  heightPx: number | null;
  status: string;
  createdAt: string;
}

export interface UploadProgress {
  loaded: number;
  total: number;
  percent: number;
}

export function validateFileSize(file: File): void {
  if (file.size > MAX_SINGLE_PUT_BYTES) {
    throw new Error(`File size exceeds maximum of ${MAX_SINGLE_PUT_BYTES / (1024 * 1024 * 1024)} GB for single upload.`);
  }
}

export function mapUploadError(err: unknown): string {
  if (err instanceof Error) return err.message;
  if (err && typeof err === 'object' && 'response' in err) {
    const res = (err as { response?: { data?: { message?: string }; status?: number } }).response;
    if (res?.data?.message) return res.data.message;
    if (res?.status === 403) return 'Upload link expired. Please try again.';
    if (res?.status === 413) return 'File too large.';
    if (res?.status === 503) return 'Storage unavailable. Please try again later.';
  }
  return 'Upload failed.';
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
  fileSizeBytes: number,
  contentType?: string
): Promise<WsiPresignedUrlResponse> {
  const { data } = await api.post<WsiPresignedUrlResponse>('/wsi/upload-url', {
    fileName,
    contentType: contentType ?? 'application/octet-stream',
    fileSizeBytes,
  });
  return data;
}

export async function uploadWsiFile(
  file: File,
  url: string,
  onProgress?: (progress: UploadProgress) => void
): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.upload.addEventListener('progress', (e) => {
      if (e.lengthComputable && onProgress) {
        onProgress({
          loaded: e.loaded,
          total: e.total,
          percent: Math.round((e.loaded / e.total) * 100),
        });
      }
    });
    xhr.addEventListener('load', () => {
      if (xhr.status >= 200 && xhr.status < 300) resolve();
      else reject(new Error(xhr.status === 403 ? 'Upload link expired.' : `Upload failed (${xhr.status}).`));
    });
    xhr.addEventListener('error', () => reject(new Error('Network error during upload.')));
    xhr.addEventListener('abort', () => reject(new Error('Upload cancelled.')));
    xhr.open('PUT', url);
    xhr.setRequestHeader('Content-Type', file.type || 'application/octet-stream');
    xhr.send(file);
  });
}

export async function confirmWsiUpload(uploadId: string): Promise<WsiUploadResponse> {
  const { data } = await api.post<WsiUploadResponse>(`/wsi/uploads/${uploadId}/confirm`);
  return data;
}

export async function uploadWsiFileWithProgress(
  file: File,
  onProgress?: (progress: UploadProgress) => void
): Promise<WsiUploadResponse> {
  validateFileSize(file);
  const { url, uploadId } = await getWsiUploadUrl(
    file.name,
    file.size,
    file.type || 'application/octet-stream'
  );
  await uploadWsiFile(file, url, onProgress);
  return confirmWsiUpload(uploadId);
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
