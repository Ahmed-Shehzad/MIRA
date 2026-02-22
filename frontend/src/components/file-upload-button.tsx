import { uploadFile } from "@/lib/storage";
import { useRef, useState } from "react";

interface FileUploadButtonProps {
  onUploaded: (key: string) => void;
  accept?: string;
  className?: string;
  children?: React.ReactNode;
}

/**
 * Button that opens a file picker and uploads the selected file via S3 presigned URL.
 * Requires S3 to be configured on the backend.
 */
export function FileUploadButton({
  onUploaded,
  accept,
  className = "rounded-md bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700",
  children = "Upload file",
}: Readonly<FileUploadButtonProps>) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setError(null);
    setLoading(true);
    try {
      const key = await uploadFile(file);
      onUploaded(key);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Upload failed");
    } finally {
      setLoading(false);
      e.target.value = "";
    }
  }

  return (
    <span>
      <input
        ref={inputRef}
        type="file"
        accept={accept}
        onChange={handleChange}
        className="hidden"
        disabled={loading}
      />
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        disabled={loading}
        className={className}
      >
        {loading ? "Uploading..." : children}
      </button>
      {error && <span className="ml-2 text-sm text-red-600">{error}</span>}
    </span>
  );
}
