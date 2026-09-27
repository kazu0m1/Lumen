# Lumen v1.0.4

> 日本語: [README.ja.md](README.ja.md)

Lumen is a portable Windows photo viewer/culling application designed to reproduce the parts of the Gwenview user experience that matter most for photo selection: a large thumbnail overview, immediate Browse ↔ View switching, fast previous/next comparison, and a configurable EXIF information pane.

Lumen is an independent original implementation. It is not a Gwenview fork and is not affiliated with KDE.

## Download / start

For normal use, download `Lumen-v1.0.4-win-portable.zip` from the GitHub Releases page, extract it, and run `Lumen.exe`. The release ZIP is built and verified on Windows by GitHub Actions.

For source-tree use, `Portable\Run-Lumen.cmd` builds `Portable\Lumen.exe` on first launch using the .NET Framework compiler already present on Windows. No NuGet package or internet connection is required. You can also run `Build-Lumen.cmd` manually.

## Primary workflow

1. **Browse** — open a photo folder and cull obvious failures from large thumbnails.
2. **View** — `Enter` or double-click a thumbnail to inspect one photo.
3. **Burst comparison** — use `←` / `→` repeatedly; Lumen preloads nearby images and uses no transition animation.
4. **Delete** — `Delete` sends the current/selected photo(s) to the Windows Recycle Bin without a success dialog. `Ctrl+Z` attempts to restore the last Lumen deletion batch through the Windows Shell.
5. **Exposure** — use the left Information pane's Exposure slider (`-3.0` to `+3.0 EV`, 0.1 EV steps). The source file is not changed.
6. **Resize/export** — return to Browse, `Ctrl+A`, then `Ctrl+E`. Default long edge is **3840 px**; aspect ratio is preserved and smaller images are not enlarged by default.

## Keyboard

| Key | Action |
|---|---|
| `Enter` | Browse → View |
| `Esc` | View → Browse; in Browse clears selection |
| `Space` | Toggle Browse / View |
| `←` / `→` | Previous / next photo in View |
| `Delete` | Move current/selected photo(s) to Recycle Bin |
| `Ctrl+Z` | Restore last Lumen deletion batch (Windows Shell dependent) |
| `Ctrl+A` | Select all in Browse |
| `Ctrl+E` | Export current selection/current photo |
| `[` / `]` | Exposure -0.1 / +0.1 EV in View |
| `Ctrl+mouse wheel` | Zoom in/out in View |
| `F11` | Full screen |

## EXIF pane

Use **Choose EXIF fields...** in the left pane to select which metadata Lumen displays. v1.0.4 includes:

- Camera
- Lens
- Date taken
- Shutter speed
- Aperture
- ISO
- Focal length
- Exposure bias
- Color space
- White balance
- Saturation
- Dynamic range
- Quality
- Sharpness
- Film mode / film simulation
- Flash
- Software

**Dimensions** are shown in the Information section independently of the EXIF-field checkboxes. They are loaded for the selected image in Browse mode as well as View mode.

For standard metadata, Lumen uses WPF metadata first and a GDI+ EXIF fallback for camera JPEG compatibility. Fujifilm-specific rendering settings such as Quality, Sharpness, detailed White Balance, Saturation, Dynamic Range, and Film Mode are decoded directly from a `FUJIFILM` MakerNote when present. No external ExifTool installation is required. Missing or unsupported vendor fields are simply omitted.

The selection is saved in `Portable\Data\LumenSettings.ini`.

## Exposure and image quality

Exposure adjustments are stored non-destructively in `Portable\Data\LumenEdits.tsv`, keyed by the original path. Lumen does not rewrite a JPEG when the slider is moved.

At export, Lumen reads the original, applies orientation, resizes with high-quality bicubic interpolation, applies the exposure adjustment, then encodes the final output once. JPEG quality defaults to **100** and can be set from 70–100.

**Important:** reducing pixel dimensions cannot be mathematically lossless because pixels are discarded. JPEG encoding is also lossy. Lumen's design therefore means *original-preserving and single-generation*: the original stays untouched and Lumen avoids an intermediate exposure-save followed by a second resize-save.

PNG output remains PNG; TIFF remains TIFF; BMP remains BMP. GIF input is exported as a still PNG.

## Supported image types in v1.0

JPEG/JPG, PNG, BMP, TIFF/TIF, GIF (first/still frame behavior).

HEIC/HEIF, WebP, RAW camera formats, video, and animated-image editing are intentionally outside v1.0.

## Portable state

Lumen writes only small application state under `Portable\Data`. It does not create sidecar files next to photographs and does not maintain a whole-library database or index.

## Source / build

The implementation is original C#/.NET Framework WPF code. It does not contain Gwenview/KDE source code. The UI is built in C# rather than XAML so the package can be compiled directly with `csc.exe` without an SDK project pipeline.

See:

- `Lumen_SPEC_v1.0.md`
- `CHANGELOG.md`
- `RELEASE_NOTES_v1.0.4.md`
- `Docs\TEST_CHECKLIST_v1.0.md`
- `Docs\RELEASE_NOTES_v1.0.md` (pre-publication development history)
- `Source\`

## License

MIT License. See `LICENSE.txt`.
