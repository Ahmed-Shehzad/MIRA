import { useEffect, useRef } from 'react';

interface WsiViewerProps {
  /** Tile source URL (IIIF, DZI, or image URL). When absent, shows placeholder. */
  tileSourceUrl?: string | null;
  className?: string;
}

const PLACEHOLDER_TILE_SOURCE = 'https://openseadragon.github.io/example-images/highsmith/highsmith.dzi';

/**
 * OpenSeadragon WSI viewer. Per DOCUMENTATION.md Phase 1 MVP – out-of-the-box.
 * Requires tile source (IIIF/DZI) or image URL. Without tile server, shows placeholder.
 */
export function WsiViewer({ tileSourceUrl, className = '' }: Readonly<WsiViewerProps>) {
  const containerRef = useRef<HTMLDivElement>(null);
  const viewerRef = useRef<{ destroy: () => void } | null>(null);

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    const tileSource = tileSourceUrl ?? PLACEHOLDER_TILE_SOURCE;
    let cancelled = false;

    void (async () => {
      const OSD = (await import('openseadragon')).default;
      if (cancelled) return;
      const viewer = OSD({
        element: el,
        tileSources: tileSource,
        prefixUrl: 'https://cdn.jsdelivr.net/npm/openseadragon@4.1/build/openseadragon/images/',
      });
      if (cancelled) {
        viewer?.destroy?.();
        return;
      }
      viewerRef.current = viewer;
    })();

    return () => {
      cancelled = true;
      viewerRef.current?.destroy?.();
      viewerRef.current = null;
    };
  }, [tileSourceUrl]);

  return (
    <div
      ref={containerRef}
      className={`min-h-[400px] bg-gray-100 ${className}`}
      style={{ width: '100%', height: '500px' }}
    />
  );
}
