# Lumen — Product / Functional Specification v1.0

**Application baseline:** Lumen v1.0.4  
**Specification revision:** 1.0.4  
**Platform:** Windows 11 x64  
**Implementation:** C# / .NET Framework 4.8 / WPF, code-only UI  
**Distribution:** Portable

---

# 1. Product definition

Lumen is a fast Windows image viewer and photo-culling tool whose design target is the **Gwenview browsing experience**, not a Gwenview source-code fork.

The core job is to let a photographer move through this workflow without switching applications:

1. show relatively large thumbnails and discard obvious failures;
2. inspect remaining photos one at a time and discard failures;
3. move rapidly forward/back through bursts and keep the best frame(s);
4. apply simple exposure adjustment only;
5. select the remaining set and export resized copies with a long edge of 3840 px by default.

The product therefore optimizes **selection speed, context preservation, and low-friction deletion** over general-purpose image editing.

## 1.1 Guiding UX principles

- Browse and View are two presentations of the **same folder collection and same current selection**, not separate workflows.
- Switching mode must not lose the current photo or force the user to relocate it in the grid.
- Repeated `←` / `→` comparison must be visually immediate and contain no decorative transition animation.
- Routine successful culling actions should be quiet; errors are surfaced only when action is needed.
- Original photographs are authoritative and are not rewritten by exposure adjustment.
- Lumen should remain intentionally smaller than a DAM, RAW developer, or Photoshop-class editor.

---

# 2. Scope classification

Requirement class:

- **A** — Lumen-owned application behavior.
- **B** — delegated to Windows/.NET image or Shell infrastructure.
- **A+B** — Lumen UX with Windows/.NET delegated mechanism.
- **C** — explicit exclusion / boundary.

Priority:

- **Must** — required for v1.0 baseline.
- **Should** — valuable but may degrade gracefully where Windows/runtime support differs.

---

# 3. Functional requirements

## F01 — Folder opening / collection

| ID | Requirement | Class | Priority |
|---|---|---:|---:|
| FR-0101 | Lumen shall open a user-selected local folder and show supported images in that folder. | A+B | Must |
| FR-0102 | Folder traversal in v1.0 is non-recursive. | C | — |
| FR-0103 | Supported v1.0 input formats are JPEG/JPG, PNG, BMP, TIFF/TIF and GIF still/first-frame behavior. | A+B | Must |
| FR-0104 | HEIC/HEIF, WebP, RAW and video are outside v1.0. | C | — |
| FR-0105 | Images shall be sorted by Windows natural filename order. | A+B | Must |
| FR-0106 | Dragging a folder onto Lumen shall open that folder. | A | Must |
| FR-0107 | Dragging/opening a supported image path shall open its parent folder, select the image, and enter View. | A | Must |
| FR-0108 | The last opened folder shall be persisted as a portable preference. | A | Should |

## F02 — Browse / thumbnail grid

| ID | Requirement | Class | Priority |
|---|---|---:|---:|
| FR-0201 | Browse shall present a large thumbnail grid. | A | Must |
| FR-0202 | Thumbnail tile size shall be adjustable from the toolbar. | A | Must |
| FR-0203 | Thumbnail size shall persist across launches. | A | Should |
| FR-0204 | File names shall be visible below thumbnails. | A | Must |
| FR-0205 | Thumbnail generation shall be asynchronous and bounded to four workers. | A+B | Must |
| FR-0206 | The UI shall appear without waiting for every thumbnail to finish. | A | Must |
| FR-0207 | Browse shall support Ctrl/Shift multi-selection. | A | Must |
| FR-0208 | `Ctrl+A` shall select all photos in Browse. | A | Must |
| FR-0209 | `Enter` and thumbnail double-click shall enter View at the selected photo. | A | Must |

## F03 — Single-photo View / burst comparison

| ID | Requirement | Class | Priority |
|---|---|---:|---:|
| FR-0301 | View shall show the current image in a large central viewer. | A | Must |
| FR-0302 | View shall show a horizontal thumbnail filmstrip at the bottom. | A | Must |
| FR-0303 | Selecting a filmstrip item shall change the current image. | A | Must |
| FR-0304 | `←` and `→` shall move to previous/next image. | A | Must |
| FR-0305 | Lumen shall not use fade/slide transition animation between images. | A | Must |
| FR-0306 | Lumen shall preload nearby images in both directions and use a bounded decoded-image cache. | A | Must |
| FR-0307 | The decoded-view cache shall have a fixed small upper bound rather than grow with folder size. | A | Must |
| FR-0308 | `Esc` shall return to Browse at the current image and scroll that image into view. | A | Must |
| FR-0309 | `Space` shall toggle Browse and View. | A | Should |
| FR-0310 | Fit-to-window shall be the default display state after image change. | A | Must |
| FR-0311 | 100% view and Ctrl+mouse-wheel zoom shall be available. | A | Must |
| FR-0312 | F11 shall provide full-screen viewing. | A | Must |

## F04 — Delete / culling

| ID | Requirement | Class | Priority |
|---|---|---:|---:|
| FR-0401 | `Delete` in View shall act on the current image. | A | Must |
| FR-0402 | `Delete` in Browse shall act on selected images, or the current image if nothing is selected. | A | Must |
| FR-0403 | Normal deletion shall be delegated to the Windows Recycle Bin, not permanent deletion. | A+B | Must |
| FR-0404 | Successful deletion shall not show a modal success/Undo dialog. | A | Must |
| FR-0405 | After deletion, Lumen shall keep the user in the flow by selecting the nearest surviving image. | A | Must |
| FR-0406 | `Ctrl+Z` shall attempt to restore the last Lumen deletion batch from Windows Recycle Bin using original-folder metadata. | A+B | Should |
| FR-0407 | Failure of automatic restore shall never convert a Recycle Bin deletion into permanent loss; Windows Recycle Bin remains authoritative. | A+B | Must |
| FR-0408 | Permanent deletion UI is outside v1.0. | C | — |

## F05 — Left Information / EXIF pane

| ID | Requirement | Class | Priority |
|---|---|---:|---:|
| FR-0501 | Information shall be displayed in a **left pane**. | A | Must |
| FR-0502 | File name, size, modified time and pixel dimensions shall be shown where available. | A+B | Must |
| FR-0503 | Pixel dimensions shall be available for the selected image in Browse mode as well as View mode; full-resolution viewer decoding shall not be required merely to show dimensions. | A+B | Must |
| FR-0504 | EXIF fields shall be read from source metadata without modifying the source. | A+B | Must |
| FR-0505 | User shall choose which supported EXIF fields are visible. | A | Must |
| FR-0506 | EXIF field visibility selection shall persist. | A | Must |
| FR-0507 | Selectable fields shall include Camera, Lens, Date taken, Shutter, Aperture, ISO, Focal length, Exposure bias, Color space, White balance, Saturation, Dynamic range, Quality, Sharpness, Film mode, Flash, and Software. | A+B | Must |
| FR-0508 | Standard camera metadata shall have a secondary EXIF-reading path where practical so common JPEGs are not dependent on one WPF metadata representation. | A+B | Must |
| FR-0509 | For Fujifilm JPEG metadata, Lumen shall decode supported `FUJIFILM` MakerNote fields in-process for Quality, Sharpness, White balance, Saturation, Dynamic range and Film mode when those tags are present. | A+B | Must |
| FR-0510 | Proprietary MakerNote fields are best-effort and camera/file dependent; absence or an unknown value shall not prevent viewing or other metadata display. | A+B | Must |
| FR-0511 | EXIF orientation shall be respected for display/export where available. | A+B | Must |
| FR-0512 | Information pane visibility and width shall persist where practical. | A | Should |

## F06 — Non-destructive exposure adjustment

| ID | Requirement | Class | Priority |
|---|---|---:|---:|
| FR-0601 | Exposure adjustment range shall be -3.0 to +3.0 EV in 0.1 EV steps. | A | Must |
| FR-0602 | Moving the exposure control shall update the viewer preview. | A | Must |
| FR-0603 | Exposure adjustment shall not rewrite or touch the original image file. | A | Must |
| FR-0604 | Exposure values shall be stored separately in Lumen portable state and associated with the source path. | A | Must |
| FR-0605 | Exposure adjustments shall persist across launches. | A | Must |
| FR-0606 | Reset exposure shall set the current photo back to 0.0 EV. | A | Must |
| FR-0607 | `[` / `]` shall decrease/increase exposure by 0.1 EV in View. | A | Should |
| FR-0608 | Exposure is an RGB-image adjustment, not RAW recovery; clipped source highlights cannot be reconstructed. | C | — |
| FR-0609 | Lumen shall approximate exposure in linear-light sRGB using an EV multiplier before final output encoding. | A | Must |

## F07 — Resize / Export

| ID | Requirement | Class | Priority |
|---|---|---:|---:|
| FR-0701 | Export shall operate on Browse selection; if no Browse selection is active, it may operate on current photo. | A | Must |
| FR-0702 | `Ctrl+E` shall open Export. | A | Must |
| FR-0703 | Default resize target shall be **long edge = 3840 px**. | A | Must |
| FR-0704 | Aspect ratio shall be preserved; Lumen shall not crop to 16:9. | A | Must |
| FR-0705 | Default behavior shall not upscale source images smaller than target long edge. | A | Must |
| FR-0706 | Long-edge target shall be editable. | A | Must |
| FR-0707 | JPEG quality shall be editable from 70 to 100; factory default shall be 100. | A+B | Must |
| FR-0708 | JPEG originals shall export as JPEG; PNG as PNG; TIFF as TIFF; BMP as BMP; GIF still frames as PNG. | A+B | Must |
| FR-0709 | Lumen shall never overwrite an existing export by default; suffix `_1`, `_2`, ... shall be used. | A | Must |
| FR-0710 | Original photos shall remain unchanged after export. | A | Must |
| FR-0711 | Exposure and resize shall be rendered from the original and followed by **one final encode**, with no intermediate exposure-saved JPEG. | A+B | Must |
| FR-0712 | Resampling shall use high-quality bicubic interpolation. | A+B | Must |
| FR-0713 | Metadata shall be copied where encoder/runtime support permits; orientation metadata shall be normalized after physical rotation. | A+B | Should |
| FR-0714 | Export shall run off the UI thread and report progress in the status area. | A | Must |

### 3.7.1 Meaning of “lossless” in this product

A reduction in pixel dimensions cannot be mathematically lossless: output contains fewer samples than the original. JPEG encoding is also inherently lossy.

Therefore Lumen v1.0 defines the image-quality requirement as:

> **original-preserving, single-generation export**

The original remains untouched. Lumen avoids this undesirable chain:

`original JPEG → exposure JPEG save → reopen → resize → second JPEG save`

and instead performs:

`original → orientation/render → resize/exposure → one output encode`

This minimizes avoidable generation loss while keeping the original as the lossless fallback/authority.

## F08 — Settings / portable state

| ID | Requirement | Class | Priority |
|---|---|---:|---:|
| FR-0801 | Primary distribution shall be Portable. | A | Must |
| FR-0802 | Preferences shall live under `Portable\Data` and not require registry settings. | A | Must |
| FR-0803 | Preferences shall be small human-readable text. | A | Must |
| FR-0804 | Exposure edit state shall live under `Portable\Data`, not as sidecars beside photographs. | A | Must |
| FR-0805 | Window size and maximized state shall persist. | A | Should |
| FR-0806 | Automatic cloud synchronization is outside v1.0. | C | — |
| FR-0807 | Telemetry, crash upload, advertising and automatic updater are outside v1.0. | C | — |
| FR-0808 | v1.0 UI language is English only. | A | Must |

## F09 — Build / distribution

| ID | Requirement | Class | Priority |
|---|---|---:|---:|
| FR-0901 | Source code shall be included in the deliverable ZIP. | A | Must |
| FR-0902 | Specification, release notes, field-test checklist and README shall be included. | A | Must |
| FR-0903 | Lumen shall not require NuGet packages or an internet connection to build/run. | A+B | Must |
| FR-0904 | First-run Portable launcher may build `Lumen.exe` using the installed .NET Framework C# compiler if no executable is present. | A+B | Must |
| FR-0905 | `Lumen.exe` shall be the executable name. | A | Must |
| FR-0906 | A conventional installer and shell-wide file-association takeover are outside v1.0. | C | — |

---

# 4. Non-functional requirements — performance / reliability

| ID | Requirement | Class | Priority |
|---|---|---:|---:|
| NFR-1001 | Folder shell and file list shall become usable without waiting for all thumbnails/EXIF. | A | Must |
| NFR-1002 | Thumbnail work shall use bounded concurrency, not one thread/task per photo. | A | Must |
| NFR-1003 | Full/large decoded-view image cache shall have a fixed small upper bound. | A | Must |
| NFR-1004 | Obsolete image decode results shall not replace a newer current image after rapid navigation. | A | Must |
| NFR-1005 | Corrupt/unreadable individual images shall not terminate the application. | A+B | Must |
| NFR-1006 | Missing EXIF shall not terminate the application. | A+B | Must |
| NFR-1007 | Missing/corrupt preference files shall fall back to safe defaults. | A | Must |
| NFR-1008 | Preference/edit writes should use temporary/replace behavior where practical. | A | Should |
| NFR-1009 | Lumen shall preserve Windows Unicode paths/filenames including Japanese characters. | A+B | Must |
| NFR-1010 | UI shall be DPI-aware on common Windows 11 display scaling. | A+B | Must |
| NFR-1011 | The application shall not maintain a photo-library index/database. | C | — |
| NFR-1012 | Long-running normal use shall not intentionally accumulate unbounded decoded image caches. | A | Must |
| NFR-1013 | Routine success events shall use status text rather than modal completion dialogs. | A | Must |

### Performance philosophy

No artificial hard millisecond number is fixed before real Windows field measurements. The behavioral targets are:

- show the application/folder state immediately;
- let thumbnails populate progressively;
- prioritize current/nearby images for comparison;
- drop stale async results after the user moves on;
- spend bounded memory to gain responsive burst comparison;
- never create a persistent private photo cache/index merely for marginal speed.

---

# 5. Explicit v1.0 out of scope

Lumen v1.0 intentionally does not implement:

- Gwenview/KDE source-code reuse or fork maintenance;
- RAW development / demosaic;
- white-balance editing, saturation editing, curves, HSL, or color grading (metadata display of white balance/saturation is supported);
- local adjustments / masks;
- crop/straighten/rotate editing UI;
- denoise, sharpen, lens correction;
- HEIC/HEIF/WebP decoding;
- video management;
- rating stars, tags, albums, face recognition;
- recursive library catalog/index/database;
- duplicate-image detection;
- filesystem rename/move/copy management beyond opening/export/deleting;
- permanent-delete workflow;
- custom Recycle Bin storage;
- installer, updater, telemetry, cloud sync;
- localization infrastructure.

---

# 6. Key user flows

## 6.1 Coarse culling

Open Folder → Browse grid appears → thumbnails fill → adjust thumbnail size → multi-select obvious failures → `Delete` → Windows Recycle Bin → continue with no modal success dialog.

## 6.2 Fine culling

Select photo → `Enter` → View → inspect → `Delete` if bad → nearest surviving photo remains current → continue.

## 6.3 Burst selection

View → `→ → → →` / `← ←` → no transition animation → nearby decoded images reused → keep best frame → Delete other frames → `Esc` → Browse returns to final current frame.

## 6.4 Exposure

View → left pane Exposure → ±0.1 EV → preview updates → Lumen stores only edit value → source file remains untouched.

## 6.5 Final 4K-size export

Browse → `Ctrl+A` → `Ctrl+E` → Long edge 3840 / Never upscale / JPEG quality 100 → choose output folder → original decoded → orientation respected → high-quality resize → exposure adjustment → one final encode → original unchanged.

---

# 7. Decision log

### D-001 — Gwenview is a UX reference, not a code base

Lumen is original Windows-native code. No Gwenview/KDE source is required for the v1.0 implementation.

### D-002 — Browse/View share one selection model

The selected/current photo is preserved through mode switching. This is central to reproducing the desired Gwenview feel.

### D-003 — No image transition animation

Decorative fades/slides interfere with rapid burst-frame comparison and are intentionally omitted.

### D-004 — Windows Recycle Bin owns deleted files

Lumen does not create a private trash folder. Undo is a convenience layer over Windows Shell restore and may degrade gracefully; Windows Recycle Bin remains the source of truth.

### D-005 — Originals are immutable under editing/export setup

Exposure changes are portable Lumen state. Export reads originals and creates new outputs.

### D-006 — “Lossless resize” is specified honestly

Pixel-size reduction is inherently information-losing. Lumen minimizes avoidable loss through original preservation and one-generation export rather than claiming mathematical losslessness.

### D-007 — Exposure only

Exposure is the only v1.0 image adjustment. This prevents the viewer from turning into a general editor and keeps the culling workflow fast.

### D-008 — Long edge 3840, not a 3840×2160 crop box

Photography aspect ratios (3:2, 4:3, portrait) are preserved. A 6000×4000 image therefore becomes 3840×2560, not 3240×2160 and not a 16:9 crop.

### D-009 — Portable / no NuGet

The same distribution philosophy as Ferry is used: source + documentation + Portable launcher, relying on Windows/.NET Framework rather than package-manager dependencies.

### D-010 — Bounded caches

Nearby preloading is justified for burst comparison, but decoded-image memory is capped. Lumen does not trade unbounded memory for marginal responsiveness.

---

# 8. Specification revision history

| Revision | Scope |
|---|---|
| **1.0.0** | Initial Lumen v1.0 baseline: Browse/View UX, culling, selectable EXIF, non-destructive exposure, one-generation long-edge export, Portable distribution. |
| **1.0.1** | Build-bootstrap compatibility fix: broadened WPF assembly discovery and added per-assembly diagnostics; functional photo workflow unchanged. |
| **1.0.2** | Build-only correction: added explicit `System.Xaml.dll` discovery/reference required by WPF `Application`; functional photo workflow unchanged. |
| **1.0.3** | Build-only source compatibility fix: disambiguated GDI+ `PixelFormat` references and made the Recycle Bin restore thread delegate explicitly `ThreadStart`; functional photo workflow unchanged. |
| **1.0.4** | Metadata expansion: Browse-mode dimensions; robust standard EXIF fallback for aperture/focal length; Color space, White balance, Saturation, Dynamic range, Quality, Sharpness and Film mode; direct Fujifilm MakerNote decoding for proprietary rendering settings. |

Release notes are the chronological record of implementation changes. This specification is the authoritative description of intended **Lumen v1.0 behavior**.
