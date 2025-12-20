# ADR-005: Product Image Storage in Web Root with On-the-Fly WebP Variants

Status: Accepted  
Date: 2025-12-20

## Context

Recent changes added the Products module with seller product management, including image upload and rendering. The implementation currently:
1. Uses `ProductImageService` (Web App) to accept uploads and save images under `wwwroot/uploads/products/{productId}`.
2. Generates three WebP variants per upload (large 1600px max, medium 1000px max, thumbnail 320px max) using ImageSharp.
3. Enforces a 5 MB file size limit and restricts extensions to JPG/JPEG/PNG/WebP.
4. Keeps the main image as the first entry in a multi-line image list stored with the product.

This is a deployment-impacting decision (stateful file storage) that was not previously captured in ADRs.

## Decision

For the MVP, product images will be stored on the web application's local web root:
- Save uploaded files to `wwwroot/uploads/products/{productId}`.
- Normalize filenames (GUID suffix) and emit WebP variants for consistent delivery.
- Serve images directly from static files; no external object storage or CDN is used in MVP.

## Consequences

Positive:
1. No additional cloud dependencies; simple to run locally and in CI.
2. Consistent image sizing/format (WebP variants) improves UX and page weight.
3. Straightforward debugging and cleanup during early iterations.

Negative / risks:
1. Not cloud-native: local disk is not shared across multiple instances; requires sticky storage or single instance.
2. No CDN, antivirus scanning, or signed uploads; elevated risk for production.
3. Backup/retention of uploads must be handled separately; rolling deployments could orphan files.
4. Scaling horizontally will need shared storage (e.g., Azure Blob/File Share) or a migration plan.

## Alternatives Considered

1. **Azure Blob Storage + CDN (recommended for production)**  
   Pros: shared storage, cache control, signed URLs, AV scanning integration.  
   Cons: more setup time for MVP.

2. **Allow only remote image URLs (no upload)**  
   Pros: stateless, no storage management.  
   Cons: poor control over availability, security, and transformations.

3. **Keep raw uploads without variants**  
   Pros: simpler pipeline.  
   Cons: heavier pages and inconsistent sizing.

## Notes / Implementation Guidance

1. Before production or multi-instance deployment, move uploads to Azure Blob Storage (or equivalent) behind CDN and enable AV scanning.
2. Add background cleanup for deleted products and enforce lifecycle/retention policies.
3. Ensure static file caching headers are set appropriately when served from storage/CDN.
4. Revisit size/format limits if product photography requirements change.
