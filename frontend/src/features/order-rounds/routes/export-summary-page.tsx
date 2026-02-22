import { useParams, Link } from 'react-router-dom';
import { useOrderRoundExport } from '@/features/order-rounds/hooks/use-order-rounds';

export function ExportSummaryPage() {
  const { id } = useParams<{ id: string }>();
  const { data: round, isLoading } = useOrderRoundExport(id);

  const exportText = round
    ? [
        `HIVE Food Order - ${round.restaurantName}`,
        `Deadline: ${new Date(round.deadline).toLocaleString()}`,
        `Status: ${round.status}`,
        '',
        '--- Orders ---',
        ...round.items.map(
          (i) =>
            `- ${i.description} | €${i.price.toFixed(2)} | ${i.userEmail}${i.notes ? ' (' + i.notes + ')' : ''}`
        ),
        '',
        `Total: €${round.items.reduce((s, i) => s + i.price, 0).toFixed(2)}`,
      ].join('\n')
    : '';

  function copyToClipboard() {
    navigator.clipboard.writeText(exportText);
  }

  if (isLoading || !round) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gray-100">
        <p className="text-gray-600">Loading...</p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-8">
      <Link to={`/rounds/${id}`} className="mb-6 inline-block text-blue-600 hover:text-blue-700">
        ← Back to order
      </Link>
      <h1 className="mb-4 text-2xl font-bold text-gray-900">Export: {round.restaurantName}</h1>
      <button
        onClick={copyToClipboard}
        className="mb-4 rounded-md bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700"
      >
        Copy to clipboard
      </button>
      <pre className="whitespace-pre-wrap rounded-lg bg-gray-50 p-4 text-sm text-gray-800">
        {exportText}
      </pre>
    </div>
  );
}
