# Large File Handling

> Whole-slide images (WSIs) are very large, multi-resolution files. Viewers load only the visible region at the appropriate resolution; when the view changes, different parts/resolutions are loaded as small compressed chunks. ([LazySlide intro](https://lazyslide.readthedocs.io/en/latest/tutorials/intro_wsi.html))

---

## Context

WSIs typically contain multiple resolution levels (pyramid). A viewer shows only what the user is looking at, at the right zoom. Panning or zooming triggers fetches of new tiles (small compressed chunks) from the image.

This model creates specific performance and bottleneck considerations for both the cloud platform and the user-side viewer.

---

## Performance and Bottlenecks: User-Side (Image Viewing)

| Consideration | Impact |
|---------------|--------|
| **Bandwidth and latency** | Each tile fetch adds round-trip time; slow networks cause visible lag when panning/zooming. |
| **Concurrent request limits** | Browsers limit connections per origin; many tiles in view can hit this limit and serialize fetches. |
| **Client memory** | Decoded tiles consume RAM; large viewports or many cached tiles can exhaust memory. |
| **Prefetching** | Without prefetch, panning feels sluggish; with aggressive prefetch, wasted bandwidth and memory. |
| **Tile decode** | Decoding (e.g. JPEG) on the main thread can block the UI. |

**Mitigations:**

- **Client-side LRU tile cache** – Reuse recently viewed tiles; limit cache size to control memory.
- **Prefetch adjacent tiles** – Load tiles around the viewport for smoother panning.
- **WebGL acceleration** – Use GPU for rendering (e.g. OpenSeadragon).
- **Lazy loading** – Only fetch tiles for the visible viewport and zoom level.

---

## Performance and Bottlenecks: Cloud Platform

| Consideration | Impact |
|---------------|--------|
| **S3 I/O for range requests** | Each tile = range request to S3; high tile volume increases S3 read load and cost. |
| **CloudFront cache hit rate** | Low hit rate means more origin (S3) requests; higher latency and egress cost. |
| **Origin load on cache miss** | S3 becomes bottleneck when many users view uncached regions. |
| **Egress cost** | Large tile volumes drive S3/CloudFront egress charges. |
| **Tile pyramid storage** | Pre-generated pyramids increase storage; on-demand generation adds compute. |

**Mitigations:**

- **HTTP range requests** – Fetch only needed byte ranges; avoid full-file transfer.
- **CloudFront CDN** – Cache tiles at the edge; reduce origin load and latency.
- **Tile pyramid** – Pre-build multi-resolution levels; serve tiles directly.
- **Tiered storage** – S3 Standard for hot data; Glacier for archives.

---

## End-to-End Flow

```
User pans/zooms → Viewer requests tiles (range/URL) → CloudFront (cache?) → S3 (if miss)
                                                          ↓
                                              Compressed chunks → Browser
                                                          ↓
                                              Decode, cache, render
```

---

## References

- [high_level_platform.md](high_level_platform.md) – Section 2.3 Large File Handling; Section 2.2 GPU inference
- [LazySlide WSI intro](https://lazyslide.readthedocs.io/en/latest/tutorials/intro_wsi.html) – WSI structure and tile loading
