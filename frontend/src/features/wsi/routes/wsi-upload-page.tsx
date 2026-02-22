import { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  uploadWsiFileWithProgress,
  mapUploadError,
  type UploadProgress,
} from '@/lib/wsi';

export function WsiUploadPage() {
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement>(null);
  const progressFillRef = useRef<HTMLDivElement>(null);
  const [loading, setLoading] = useState(false);
  const [progress, setProgress] = useState<UploadProgress | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setError(null);
    setProgress(null);
    setLoading(true);
    try {
      const upload = await uploadWsiFileWithProgress(file, (p) => setProgress(p));
      navigate(`/wsi/${upload.id}`);
    } catch (err) {
      setError(mapUploadError(err));
    } finally {
      setLoading(false);
      setProgress(null);
      e.target.value = '';
    }
  }

  useEffect(() => {
    if (progressFillRef.current && progress) {
      progressFillRef.current.style.setProperty('--progress-percent', `${progress.percent}%`);
    }
  }, [progress]);

  return (
    <div className="mx-auto max-w-3xl px-6 py-8">
      <h1 className="mb-6 text-2xl font-bold text-gray-900">Upload WSI</h1>
      <p className="mb-4 text-gray-600">
        Select a Whole Slide Image file to upload. Supported formats: DZI, TIFF, SVS. Maximum size: 5 GB.
      </p>
      <input
        ref={inputRef}
        type="file"
        accept=".dzi,.tif,.tiff,.svs,.ndpi,.scn"
        onChange={handleChange}
        className="hidden"
        disabled={loading}
        aria-label="Select Whole Slide Image file to upload"
      />
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        disabled={loading}
        className="rounded-md bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700 disabled:opacity-50"
      >
        {loading ? 'Uploading...' : 'Select file'}
      </button>
      {progress && (
        <div className="mt-4">
          <div className="h-2 w-full rounded-full bg-gray-200">
            <div ref={progressFillRef} className="wsi-progress-fill" />
          </div>
          <p className="mt-1 text-sm text-gray-500">
            {progress.percent}% ({Math.round(progress.loaded / 1024 / 1024)} / {Math.round(progress.total / 1024 / 1024)} MB)
          </p>
        </div>
      )}
      {error && <p className="mt-4 text-sm text-red-600">{error}</p>}
    </div>
  );
}
