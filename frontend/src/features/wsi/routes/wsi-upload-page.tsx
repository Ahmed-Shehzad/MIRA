import { useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { uploadWsiFile, registerWsiUpload } from '@/lib/wsi';

export function WsiUploadPage() {
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setError(null);
    setLoading(true);
    try {
      const key = await uploadWsiFile(file);
      const upload = await registerWsiUpload({
        s3Key: key,
        fileName: file.name,
        contentType: file.type || undefined,
        fileSizeBytes: file.size,
      });
      navigate(`/wsi/${upload.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Upload failed');
    } finally {
      setLoading(false);
      e.target.value = '';
    }
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-8">
      <h1 className="mb-6 text-2xl font-bold text-gray-900">Upload WSI</h1>
      <p className="mb-4 text-gray-600">
        Select a Whole Slide Image file to upload. Supported formats: DZI, TIFF, SVS.
      </p>
      <input
        ref={inputRef}
        type="file"
        accept=".dzi,.tif,.tiff,.svs,.ndpi,.scn"
        onChange={handleChange}
        className="hidden"
        disabled={loading}
      />
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        disabled={loading}
        className="rounded-md bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700 disabled:opacity-50"
      >
        {loading ? 'Uploading...' : 'Select file'}
      </button>
      {error && <p className="mt-4 text-sm text-red-600">{error}</p>}
    </div>
  );
}
