# Image support: architecture and implementation plan

Status: implementation in progress, 19 November 2026. Core storage, codec,
history, image commands, migration, recovery, and memory changes are implemented.
See [implementation results](image-support-implementation.md) for verification,
specific design decisions, and remaining release qualification.

## Objective and scope

Make pasted screenshots and imported images reliable parts of a note: editable,
undoable, recoverable after a failed save, and responsive on ordinary Windows PCs.
Retain WPF, FlowDocument, local storage, RTF, the existing image card, and the viewer.
Do not require a database, cloud service, full MVVM conversion, or a new editor.

The current implementation mixes asset I/O, document conversion, and interaction in
NoteWindow.Images.cs. The most urgent problems are irreversible reference loss,
early file deletion, locale-sensitive tokens, and partially successful migration.
This design separates those responsibilities without rewriting unrelated features.

## Rules the implementation must preserve

1. A save never changes the live FlowDocument, selection, caret, or undo history.
2. Each persisted image reference resolves to an immutable local asset, or remains
   an identifiable missing-image reference. Loading never discards references.
3. Inserting an image commits its file before committing a document that uses it.
4. Removing an image changes the document; it does not immediately delete bytes.
5. Locked notes allow view/copy/export, but reject insert/remove/resize operations.
6. A failed operation leaves the previous committed state available and reports
   the failure. A request to save is not a successful save acknowledgement.
7. An image-only note is nonempty. File existence is not the definition of content.
8. Background work cannot apply an obsolete result to a closed or newer session.

## Component boundaries

| Component | Responsibility |
|---|---|
| NoteImageReference | Image occurrence ID, asset ID, normalized width ratio, optional accessible description |
| ImageAssetStore | Import, validate, atomically publish, resolve, copy, and eventually collect assets |
| NoteDocumentCodec | Convert a live document to detached RTF and restore RTF into editor elements; derive references and searchable text |
| NoteEditorSession | Current document revision, lock policy, image commands, unified history, selection, dirty state, and pending operations |
| NoteSaveCoordinator | Debounce dirty notes, order writes, reject stale saves, flush, and acknowledge committed revisions |
| NoteStorageService | Atomic RTF persistence, metadata persistence, loading, and legacy compatibility |
| ImageDecodeService | Bounded background decoding, thumbnail cache, cancellation, and frozen bitmap delivery |
| ImageExportService | Explicit original-file export versus encoded PNG/JPEG export |
| StorageMigrationService | Copy, verify, switch storage root, and retain rollback data |
| NoteImageControl / ImageViewerWindow | Render state and raise user intents; no file deletion or note persistence |

Use constructor injection from App; a DI framework is unnecessary. The active
storage root belongs to one storage context, not repeated AppSettings.Load calls.
Commands capture that context and its generation so migration cannot redirect
half of an operation to a different folder.

## Data and identity

Use full GUIDs for note asset folders and asset IDs. A document occurrence has a
separate ID: inserting the same asset twice must permit independent resizing and
deletion. Cloning a note gives its occurrences new IDs and copies required assets
to the destination note. Avoid cross-note asset sharing in this release.

Suggested asset path:

    Images/{full-note-id}/{full-asset-id}.{validated-extension}

Persist image occurrences inside the canonical RTF using a versioned marker:

    [[PNIMG:v1;occ=<guid>;asset=<guid>;width=0.3000]]

The codec, not the UI, owns this grammar. Format and parse numbers with invariant
culture; enforce finite widths in the supported 0.15–1.00 range. Validate IDs and
map extensions using trusted asset metadata or a controlled directory lookup.
Never accept a marker as an arbitrary path. Unsupported versions remain intact
as unresolved elements. Escape literal marker-shaped text during serialization.

Note.Images becomes a derived cache, not an independent source of truth. Asset
references and width ratios must travel together in the same committed RTF.
An asset manifest may store original filename, validated format, byte length,
dimensions, and checksum; retain recoverable file discovery if it is damaged.
Search text is derived from document structure with a readable [Image] placeholder.

Keep existing note RTF filenames initially to preserve title/Explorer behavior.
Separate the correctness of image persistence from a future filename redesign.
Metadata/index updates are not a multi-file transaction: if interrupted, recover
image references from committed RTF rather than trusting a stale Images list.

## Document serialization and editing

Build a detached FlowDocument through an explicit structural copier. Copy supported
block/inline types and formatting; replace image UI containers with markers only
in the copy, then save that copy as RTF. The source document is never modified.
Capture WPF document state on its owning dispatcher; send only immutable bytes,
strings, metadata, and frozen bitmaps to workers.

Inventory the structures produced by typing, Markdown, hyperlinks, and rich paste.
Handle paragraphs, runs, spans, hyperlinks, lists, sections, tables, and existing
non-image UI containers deliberately. Preserve an existing non-image element via
an explicit adapter, or fail the save visibly while retaining the prior revision;
never silently omit an unsupported element. Parse markers across adjacent runs,
since RTF formatting may split their text. Test mixed formatting around markers.

Restore unresolved assets as MissingImageControl carrying the complete reference.
Offer retry and relink; relink imports a replacement asset through a normal edit.
Saving the placeholder preserves the reference, including unknown-version data.

All mutations go through the session: InsertImages, RemoveImage, ResizeImage,
ReplaceMissingImage. UI enabled states mirror command policy, but the command
checks the lock again. An import completing after the note is locked or closed
must not insert content. Capture a tracked insertion anchor, not a numeric offset
that becomes stale while decoding. Batch file drops preserve input order and
produce one undoable edit; report failures per file.

## Undo and asset retention

Use one history for text, formatting, and image edits. Do not add an independent
image undo stack beside WPF undo: action ordering would be incorrect.

First run a feasibility spike against the actual RichTextBox. Prove whether native
undo can cover image insertion/removal and persistent resize metadata as one user
action, without save-time document edits or unsupported WPF internals. Do not
assume changing a child control property participates in native document undo.

If native history cannot satisfy the contract, implement session-owned document
snapshot history and disable native undo. Route keyboard, toolbar, context-menu,
and command Undo/Redo to that history. Group contiguous typing, keep formatting
and image commands as separate edits, and treat one drag as one resize action.
Snapshots contain detached RTF, image references, selection offsets/direction,
and occurrence IDs. Rehydration suppresses edit recording. Preserve IME behavior.
Prototype this before wiring every command: it is the largest integration risk.

Bound retained history by count and bytes; initial targets are 100 actions and
32 MiB of document snapshots per active note, measured before finalizing. Define
behavior for one oversized snapshot and show undo availability accurately.
Prefer native history if the spike proves every requirement; record the decision.

Asset liveness includes current documents, retained undo/redo states, pending
imports, active viewers, backup revisions, and recovery data. Do not collect
files during an ordinary debounced note save. Initially retain unused assets for
the session and defer collection until a clean startup with no old undo roots.
Move confirmed garbage to a dated trash area, then purge after a retention period
(proposed default: seven days). If recovery is incomplete, skip collection.
Deleting a note uses the same retention policy. Retain an interrupted import as
temporary data until recovery can safely classify it.

## Save and failure protocol

1. Import to a temporary file under the active storage volume; validate format,
   dimensions, limits, and checksum. Atomically publish an immutable asset file.
2. On the dispatcher, recheck note existence, lock, insertion anchor, and storage
   generation. Apply the edit and increment the session document revision.
3. Debounce capture of a detached document snapshot; do not rebuild all notes on
   every keypress. Flush capture on close, duplicate, migration, and orderly exit.
4. Serialize per-note disk writes. Write a sibling temporary RTF, flush it, and
   atomically replace the old RTF, retaining one known-good backup. Define and
   test first-save handling separately. Write derived metadata atomically too.
5. Acknowledge the committed revision on the dispatcher. Show Saved only when the
   acknowledgement still covers the current revision; otherwise retain Saving.
   On failure show Unsaved with retry, keeping the document dirty.

Crash guarantees: interrupted import leaves an unused file; interrupted RTF write
leaves the previous committed RTF or a recoverable backup; interrupted metadata
write does not erase image references. Do not claim all note files are committed
atomically as a group. Recovery selects complete revisions and never runs cleanup
from a partial scan. Older queued snapshots cannot overwrite newer revisions.

## Decode, format support, and viewer

Validate content through a decoder, not extension alone. Publish a tested format
allowlist: PNG/JPEG/BMP first; add GIF as a static first-frame image only if that
behavior is documented and tested. Enable WebP only when the chosen decoder is
available on every supported installation; otherwise show an unsupported-format
message. Do not advertise animation support.

Use bounded workers for imports and decoding. Proposed initial limits: 25 MiB
per file, 40 megapixels decoded, two concurrent decodes, and a 64 MiB thumbnail
cache. Treat these as tunable product defaults and validate header dimensions
before full decode. Reject oversized clipboard bitmaps before encoding them.
Keep original assets unchanged; decode thumbnails near rendered size with DPI
awareness. Cache by asset identity and decode size, not control instance. Viewer
decoding is lazy and cancellable; release its bitmap and asset lease on close.

Share one export service. Offer Save Original (byte copy, matching extension) or
Export PNG/JPEG (actual encoding). Define JPEG transparency flattening explicitly.
The viewer supports fit, zoom, scrolling, and genuine drag-to-pan. Fit uses WPF
layout dimensions consistently; very large images may need below 10% zoom. Clamp
popup placement using monitor work area and DPI-correct coordinates. Locking a
note keeps view/copy/export available and removes resize/delete controls.

## Duplication, migration, and compatibility

Duplicate from a flushed current document snapshot, not possibly stale model RTF.
Copy only referenced assets, including unresolved references as unresolved state.
Publish the new note only after required available assets are copied; report a
copy failure without presenting an apparently successful broken duplicate.

Migration is exclusive with storage writes and imports. Stop accepting mutations,
flush pending saves, copy to staging at the destination, verify all references
and checksums, then publish destination data and switch the configured root last.
Use a recovery journal for interruption before/after the switch. Do not overwrite
an existing unrelated destination store: reject it for this release. Retain the
source until a verified reopen succeeds and keep it as a rollback backup. On
failure keep the old root active and report the error. Update SettingsWindow so
it does not persist CustomStoragePath before migration succeeds.

Read existing ⟦IMG:filename|r=ratio⟧ tokens and old short-ID asset folders. Accept
legacy decimal commas only within the legacy grammar. Back up originals, assign
new IDs, copy assets first, and rewrite each note atomically. Preserve unresolved
legacy references rather than substituting plain text. Migration is resumable
and idempotent. An older binary may display new markers as text: document that
downgrading needs the pre-migration backup; avoid silently reopening unsupported
new-format notes in an older writable schema path where enforceable.

Fix CloseNoteWindow to use structural HasContent, including unresolved images.
Plain text export uses placeholders; normal selection copy/cut/paste must define
image transfer explicitly. Within the app transfer asset payloads plus references
and mint new occurrence IDs; external copy offers bitmap/original file as useful.
Never persist a new note with an unresolvable reference copied from another note.

## Implementation sequence and acceptance gates

| Phase | Work | Completion gate |
|---|---|---|
| 0. Reproduce and spike | Add a Windows test project and fixtures; reproduce locale/missing-file/export issues; prove detached copy and unified undo strategy | Record working document-copy coverage and history decision before implementation expands |
| 1. Asset lifecycle | Add identities, storage context, validated atomic import, result/error types, retention and path checks | Interrupted import creates no broken committed reference; invalid input is visible and contained |
| 2. Codec and compatibility | Add detached copier, versioned/legacy parsers, missing placeholders, derived search text, migration fixtures | Repeated save/load preserves formatting, image order, ratios, and unresolved references without changing the live editor |
| 3. Editing and history | Add session commands, lock enforcement, selected-range replacement, batched insertion, resize history, internal clipboard transfer | Mixed text/image undo/redo works; locked notes reject every image mutation; image-only notes survive close |
| 4. Durable integration | Add revision-ordered saves and acknowledgements; update duplicate/close/exit; remove early deletion and timer-driven folder cleanup | Fault injection at each write boundary preserves a recoverable document and correct dirty state |
| 5. Migration and recovery | Journaled copy/verify/switch; legacy upgrade; trash collection from complete roots | Cancel, denied access, full disk, and process interruption preserve the source and allow deterministic recovery |
| 6. Performance and UI | Thumbnail decoder/cache, viewer/export consolidation, popup placement, progress/cancellation and accessible names | Large-image fixtures meet agreed memory/latency budgets; exports decode as their selected formats |
| 7. Release qualification | Run regression matrix; refresh help/README; align version and packaging; build installer and MSIX | All gates pass on supported Windows configurations; clean install and upgrade preserve existing notes |

Each phase should be reviewable independently. Keep legacy reads until migration
tests pass. Remove the old live-document swapping serializer and direct file I/O
from views only after the replacement path is integrated. Do not run both writers.

## Required verification

- Round trips: multiple adjacent images, same asset twice, rich formatting,
  hyperlinks, checklists, lists, tables from paste, split tokens, missing/corrupt
  files, unknown marker versions, and en-US/de-DE/fr-FR cultures.
- Editing: selected-text replacement, text/image cut and paste, same-note and
  cross-note paste, duplicate, resize, lock during import, note close during
  decode, undo/redo after saves, and IME text entry.
- Durability: file-system fault injection before/after asset publish, RTF replace,
  metadata replace, duplicate, migration switch, and garbage collection. Include
  an obsolete queued save and a failed root scan; neither may delete live assets.
- Compatibility: real v1.6 text-only notes, current uncommitted image notes,
  missing legacy assets, old short-ID folders, interrupted upgrade, and rollback.
- Performance: many thumbnails, a large valid photo, oversized input rejection,
  repeated viewer open/close, memory after closing notes, and typing/resize
  responsiveness. Record measurements on a named machine before setting budgets.
- UI: 100/150/200% DPI, mixed-DPI monitors, dark/light notes, screen edges,
  keyboard-only commands, accessible names, view while locked, correct Saved /
  Saving / Unsaved states, and original versus converted image exports.

## Risk and effort

The asset store and export fixes are straightforward. The codec, mixed undo,
clipboard interoperability, and crash recovery are the substantial parts.
Do not attach a reliable time estimate until phase 0 establishes history and
document-copy feasibility. Ship only when durable save/load, reference retention,
and mixed undo gates pass; viewer polish can be reduced without weakening those
guarantees. This is a focused subsystem refactor, not a quick patch.
