# RAM assessment — 19 November 2026

Implementation follow-up: thumbnail decoding, shared cache/history budgets,
lazy closed-note loading, streamed metadata, capture debouncing and window
lifetime changes are now implemented. See [implementation results](image-support-implementation.md)
for isolated decoding measurements. The baseline below describes the unchanged
installed v1.6.0 release and is not a measurement of the new whole application.

## Scope and method

Read-only process sampling and source review. No restart, forced collection,
working-set trimming, editing of user notes, or application-code changes were
performed by this assessment. The application continues its own scheduled trims.

Sampled PID 17520 with Get-Process every two seconds: 21 samples across 40 seconds.
The executable is the installed WindowsApps v1.6.0 release, product version
1.6.0+1557dcdf235978b27303dbb7bab54446ec5ba28c. It started on 17 November.
It is not a running build of the uncommitted image implementation.
The current note/window workload was not controlled or counted.

## Measured baseline

| Metric | Result |
|---|---|
| Working set during sampling | 0.56–10.38 MiB |
| Private committed memory during sampling | 181.14–181.41 MiB |
| Peak working set since process start, read separately | 269.27 MiB |
| Peak paged-memory counter since process start, read separately | 210.95 MiB |
| CPU time accumulated during the sample | Approximately 0.062 seconds |
| Handle count during sampling | 691–698 |

MiB means 1,048,576 bytes. Private committed memory is not a measurement of
currently resident RAM or of live managed objects. Working set includes resident
private and shareable pages; it is not directly additive across processes.
Lifetime peaks do not identify the operation that caused them.
The huge virtual-address-space reservation shown by Get-Process is not RAM usage.

The installed tag and working source both force two aggressive full collections,
wait for finalizers, and call EmptyWorkingSet and SetProcessWorkingSetSize every
30 seconds. Source: App.xaml.cs, TrimMemory and startup timer initialization.
The sample shows drops in resident pages while private commitment stays almost
constant. This is consistent with trimming, not proof of a small allocation
footprint. Windows can fault those pages back in when the user interacts.

There is no demonstrated leak in this short observation. Equally, this is not a
leak test, a startup benchmark, or a measurement of image-feature overhead.

## Image implementation: estimated incremental cost

NoteImageControl.LoadImage uses BitmapImage without DecodePixelWidth/Height.
Reducing control Width does not request a smaller decoded image. The viewer
also loads a bitmap from the file; whether resources are shared is runtime
dependent, so do not assume a guaranteed second full copy or guaranteed sharing.

Approximate cost of one 32-bit pixel buffer:

| Image | Pixels | Buffer size |
|---|---|---|
| Full-HD screenshot | 1920 × 1080 | 7.91 MiB |
| 4K screenshot | 3840 × 2160 | 31.64 MiB |
| 12 MP photo | 4000 × 3000 | 45.78 MiB |
| Small thumbnail | 300 × 225 | 0.257 MiB |

Ten distinct 12 MP photos can require approximately 457.8 MiB for one buffer per
photo alone. Ten 300 × 225 thumbnails require approximately 2.57 MiB of pixel
buffers. These are arithmetic estimates, not process measurements. Decoder work,
clipboard contents, WPF objects, rendering resources, and temporary copies add
cost; some resources may be shared. Compression size does not predict decode RAM.
Displayed pixel dimensions should account for monitor DPI.

Saving creates temporary RTF streams, byte arrays, and strings. Current saves
also swap live document elements, which may interact with retained editor history.
These paths warrant allocation profiling; source inspection cannot quantify them.
DeepCloneNotes copies note objects and image lists but shares immutable string
references; it does not immediately duplicate every RTF string. JSON serialization
does allocate another representation containing those RTF strings.

## Changes to the proposed memory policy

- Use display-sized decoding and a shared, lazy thumbnail cache. Start with a
  16 MiB decoded-pixel budget across the app, not 64 MiB. Active controls and
  in-flight decoding also retain memory outside the cache and must be measured.
- Start with one concurrent heavy decode. Limit decoded dimensions and estimate
  pixel-buffer bytes before full decode. Release viewer references on close;
  actual process memory release may be delayed by runtime/graphics caches.
- Prefer native undo if the feasibility spike passes. If custom snapshot history
  is required, start with a 16 MiB app-wide serialized-history budget, not 32 MiB
  per note. Include redo and use a defined policy for oversized actions. Strings
  and rehydrated objects add overhead beyond serialized bytes.
- Keep bitmap bytes out of history snapshots; reference immutable disk assets.
- Debounce document capture as well as disk writes; save only dirty notes.
- Benchmark with periodic forced GC/working-set trimming disabled in an isolated
  development build. Choose whether to remove that policy using latency, page
  fault, CPU, and commitment results; do not optimize solely for idle working set.

Cache and history budgets allocate on demand and are not a total-process cap.
Keep the limits configurable internally until measured on representative machines.

## Required controlled benchmark

Use a DEV build with a temporary isolated storage root and generated images.
Verify every notes/settings/log path is isolated before launch; the current DEV
conditional does not by itself establish isolation for all storage paths.
Do not stop the user's installed instance. Give the development instance a
separate mutex identity if needed. Compare release and feature code with the
same build configuration, workload, and trim policy.

Measure fresh startup; one and ten text notes; ten Full-HD screenshots; ten
12 MP photos; opening/closing a photo viewer repeatedly; editing and undoing
mixed content; and closing all image notes. Track working set, private bytes,
managed heap, allocation rate, collections, UI pauses, and available graphics
memory counters. Sample before interaction, during operations, and after settling.
Check that repeated open/close cycles plateau rather than grow indefinitely.

Provisional acceptance targets: text-only steady private memory within 10% or
10 MiB of the matched baseline, whichever is larger; no retained full-size bitmap
per thumbnail; bounded history/cache use; and no sustained growth across repeated
viewer cycles. Set a viewer peak target from image dimensions and actual decoder
measurements rather than promising a fixed RAM ceiling.

## Conclusion

The observed release has very low idle resident memory but approximately 181 MiB
of private commitment. That does not substantiate a single-digit-MiB allocation
footprint. The present image code can add tens of MiB per photo. Thumbnail decoding
is the highest-impact improvement; a bounded cache and history policy are needed
to make the proposed architecture appropriate for a lightweight desktop utility.

References:
- https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-emptyworkingset
- https://learn.microsoft.com/en-us/windows/win32/memory/working-set
- https://learn.microsoft.com/en-us/dotnet/framework/wpf/graphics-multimedia/imaging-overview
