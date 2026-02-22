import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { getWsiUploads } from '@/lib/wsi';

export function WsiUploadsPage() {
  const { data: uploads, isLoading, error } = useQuery({
    queryKey: ['wsi-uploads'],
    queryFn: getWsiUploads,
  });

  if (isLoading) return <div className="p-4">Loading...</div>;
  if (error) return <div className="p-4 text-red-600">Failed to load uploads</div>;

  return (
    <div className="p-4">
      <h1 className="text-xl font-semibold mb-4">WSI Uploads</h1>
      <p className="text-gray-600 mb-4">
        Whole Slide Images. Upload via the viewer page, then trigger analysis.
      </p>
      {uploads?.length === 0 ? (
        <p className="text-gray-500">No uploads yet. Go to the viewer to upload.</p>
      ) : (
        <ul className="space-y-2">
          {uploads?.map((u) => (
            <li key={u.id} className="flex items-center gap-2">
              <Link
                to={`/wsi/${u.id}`}
                className="text-blue-600 hover:underline"
              >
                {u.fileName}
              </Link>
              <span className="text-sm text-gray-500">
                {(u.fileSizeBytes / 1024 / 1024).toFixed(2)} MB
              </span>
            </li>
          ))}
        </ul>
      )}
      <Link
        to="/wsi/new"
        className="mt-4 inline-block rounded-md bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700"
      >
        Upload WSI
      </Link>
    </div>
  );
}
