import { useParams } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getWsiUpload, triggerWsiAnalysis } from '@/lib/wsi';
import { WsiViewer } from '@/features/wsi/components/wsi-viewer';

export function WsiViewerPage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const { data: upload, isLoading, error } = useQuery({
    queryKey: ['wsi-upload', id],
    queryFn: () => getWsiUpload(id!),
    enabled: !!id,
  });

  const triggerMutation = useMutation({
    mutationFn: () => triggerWsiAnalysis(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['wsi-upload', id] });
    },
  });

  if (isLoading || !id) return <div className="p-4">Loading...</div>;
  if (error || !upload) return <div className="p-4 text-red-600">Upload not found</div>;

  return (
    <div className="p-4">
      <h1 className="text-xl font-semibold mb-2">{upload.fileName}</h1>
      <p className="text-sm text-gray-500 mb-4">
        {(upload.fileSizeBytes / 1024 / 1024).toFixed(2)} MB
        {upload.widthPx && upload.heightPx && (
          <> · {upload.widthPx}×{upload.heightPx} px</>
        )}
        {upload.status && (
          <> · Status: {upload.status}</>
        )}
      </p>
      <div className="mb-4">
        <button
          type="button"
          onClick={() => triggerMutation.mutate()}
          disabled={triggerMutation.isPending || upload.status !== 'Ready'}
          className="rounded-md bg-green-600 px-4 py-2 font-medium text-white hover:bg-green-700 disabled:opacity-50"
        >
          {triggerMutation.isPending ? 'Triggering...' : 'Trigger Analysis'}
        </button>
        {triggerMutation.data && (
          <span className="ml-2 text-sm text-green-600">
            Job {triggerMutation.data.id.slice(0, 8)}… created
          </span>
        )}
      </div>
      <WsiViewer tileSourceUrl={null} />
    </div>
  );
}
