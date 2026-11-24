# Image support implementation

20 November 2026. Working-tree implementation; version and release artifacts are
unchanged. The installed v1.6.0 application and its note data were not restarted,
replaced, or used as test fixtures.

## Implemented

- Immutable per-note image assets under full GUID folders. Clipboard images use
  PNG; file imports preserve validated original PNG/JPEG/BMP/GIF bytes. GIF display
  uses its first frame. Imports enforce 25 MiB file and 40 MP pixel limits.
- Atomic temporary-file publication, flushed writes, RTF/index backups, and
  generation-ordered saves. A committed RTF contains the full note ID, allowing
  recovery when metadata publication was interrupted. Corrupt RTFs with a valid
  backup are retained separately in Recovery before recovery saves proceed.
- Detached FlowDocument serialization, preserving paragraphs, runs, spans,
  hyperlinks, lists, sections and tables. Unsupported elements fail visibly.
  Saving does not temporarily replace live image controls.
- Versioned image occurrences, invariant JSON numbers and stable asset filenames.
  Literal brackets/marker-shaped text are escaped independently. Unknown image
  versions remain opaque; missing assets retain complete references.
- Legacy image markers and decimal-comma ratios remain readable. Legacy short-ID
  asset directories are read through without destructive conversion. New assets
  use full IDs; old assets are not re-encoded just to change their folder layout.
- Shared 2 MiB lazy thumbnail LRU and one heavy import/decode worker. Small note
  thumbnails decode at roughly 80% of rendered pixel width, accounting for DPI.
  Note previews are capped at 512 pixels wide and 1024 pixels on either edge.
  The viewer starts with a preview and uses original pixels for high zoom, copy,
  and export. Fit releases the original bitmap if it was loaded. Hide All releases
  the note windows and clears the cache while preserving Undo history in memory.
- One history for typing, formatting, images, resize, cut, paste and deletion.
  Native WPF undo does not record child image property changes, as verified by
  the feasibility check. Session history uses detached document snapshots with
  no bitmap copies: at most 50 checkpoints per note and a shared 16 MiB estimated
  string budget. Current checkpoints cannot be evicted, so exceptionally large
  current documents can exceed that history budget.
- Debounced document capture and explicit flush boundaries. Image insertion and
  multi-file drops are grouped edits. Import completion checks note lifetime,
  locking, document identity, storage root, and intervening text changes.
- Cross-note clipboard fragments import their own assets and receive new
  occurrence IDs. External applications receive readable image placeholders and
  rich text; the image action card copies the original-resolution image itself.
- Read-only policy guards image edits, Undo/Redo, and formatting handlers while
  preserving view/copy/export. Missing images offer retry and relink/replace.
- Unified PNG/JPEG conversion with white flattening for JPEG transparency, plus
  an original-byte export option. The viewer has asynchronous loading, fit,
  Ctrl+wheel zoom, ordinary scrolling, drag-to-pan, and reference release on close.
- Metadata-only streamed JSON index, dirty-content write avoidance, lazy closed-
  note RTF loading, bounded search previews, and reuse of Search/Manager windows.
  Closed note windows are removed from the application's strong-reference list.
- Periodic forced GC and working-set eviction were removed. This can increase
  the idle working-set number; allocation/commitment and responsiveness are the
  useful comparison, rather than artificially trimmed resident pages.
- Copy/verify/switch storage migration, resumable destination journal, source
  retention, and settings publication after copy verification. Occupied unrelated
  destinations, nested roots and linked/reparse storage trees are rejected.
  Migration is blocked while imports are pending.
- Conservative cold-start-only asset collection. A complete scan includes
  persisted documents, backups, retired RTFs and metadata. Unreferenced assets
  older than seven days are quarantined; quarantined assets receive another
  seven-day grace period. Unknown/corrupt recovery content aborts collection.
  Retired note RTFs are retained conservatively; their assets are consequently
  retained too. No image-folder deletion occurs during an ordinary note save.

## Source layout

Models/NoteImageReference.cs owns marker identity and validation.
Services/NoteDocumentCodec.cs owns detached document conversion and fragments.
Services/ImageAssetStore.cs owns imports, asset resolution and duplication.
Services/ImageDecodeService.cs owns bounded decoding and thumbnail caching.
Services/ImageExportService.cs owns original and converted image exports.
Services/NoteEditorHistory.cs owns shared-budget document history.
Services/NoteStorageService.cs owns ordered file persistence and lazy content.
Services/StorageMigrationService.cs and ImageAssetMaintenance.cs own migration
and conservative collection. NoteWindow.Images.cs coordinates the editor session
and UI commands; it does not perform direct asset deletion.

The session remains a partial of NoteWindow instead of introducing an application-
wide MVVM rewrite. File and conversion responsibilities have moved to services.
Markers use a versioned base64 JSON payload instead of the originally proposed
semicolon grammar; JSON provides invariant numbers and extensible fields.

## Verification

The executable regression harness is tests/ImageSupport.Tests.csproj. It needs
the Windows .NET 8 SDK and existing application dependencies, but adds no testing
package dependency. All fixtures are generated below its own build output.

Final verification: 79 automated checks passed. The Release build completed with
zero warnings and zero errors. The off-screen note preview was visually inspected.

    dotnet run --project Tests/ImageSupport.Tests.csproj -p:NuGetAudit=false

Coverage includes culture round trips; split and literal markers; rich formatting
and tables; selection fragments; original-byte import and export; real JPEG
encoding; invalid content/path rejection; interrupted atomic writes; stale saves;
metadata identity recovery; corrupt RTF backup recovery; lazy closed-note loading;
mixed image/text Undo/Redo; resize/delete Undo; asynchronous cross-note paste;
locking; migration retry; conservative collection; and a rendered note preview.

The preview is rendered off screen and was visually inspected. This is not a
replacement for live mixed-DPI, IME, accessibility, shell, and clipboard testing.

## Measured image-memory comparison

Two fresh console/WPF test processes decode five copies of a generated 4000 x
3000 PNG. Image generation runs outside the measured child process. Both warm
the decoder, collect unreachable setup objects, then retain all five results.
Neither test empties its working set. These measurements isolate decoding, not
the complete application's RAM footprint.

| Mode | Pixel buffers | Approximate private-memory increase | Observed total working set |
|---|---:|---:|---:|
| Original-size decode | 228.88 MiB | 228–229 MiB | 268 MiB |
| 300-pixel thumbnails | 1.29 MiB | 0.5–1 MiB | 40–41 MiB |

Pixel-buffer storage is approximately 99.4% smaller for this fixture. Thumbnail
decoding took longer for this generated PNG because of scaling; lower memory
does not guarantee lower decoding latency. Real photos, DPI and graphics drivers
will change process measurements. Full-size viewing still requires larger memory.

## Remaining release qualification

- Live Windows 10/11 checks: mixed-DPI monitors, Explorer restart, screen-share
  capture behavior, viewer window resizing, keyboard navigation and accessibility.
- IME composition and real cross-application clipboard formats. Automated tests
  exercise clipboard payload import without replacing the user's OS clipboard.
- Sustained whole-app memory/latency and repeated window/viewer-cycle profiling
  under representative workloads. The decoder benchmark is not a leak proof.
- Actual disk-full/access-denied/process-termination testing across the migration
  switch. Atomic-write failure is injected, and recovery paths are tested, but
  actual machine shutdown was not initiated.
- Agree a release version, then update packaging and perform installer/MSIX
  upgrade qualification. No package was published or installed by this work.

Older binaries do not understand the new image markers. Retain the original
storage copy/backups when testing a downgrade. The current implementation reads
legacy notes and assets; backward writes by an older binary are not controlled.
