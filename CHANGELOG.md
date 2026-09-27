# Changelog

All notable public changes to Lumen are recorded here.

## [1.0.4] - 2026-09-27

Initial public release.

### Added

- Large adjustable thumbnail Browse mode for fast first-pass culling.
- Single-photo View mode with a bottom thumbnail filmstrip.
- Fast Browse ↔ View switching while retaining the current selection and browsing context.
- Nearby-image preloading for rapid previous/next burst comparison with no transition animation.
- Configurable left-side EXIF / information pane.
- Standard EXIF metadata including aperture, focal length, image dimensions, color space, white balance, saturation, sharpness and other exposure information when available.
- Direct Fujifilm MakerNote parsing for Quality, Sharpness, White Balance, Saturation, Dynamic Range, and Film Mode / Film Simulation when present.
- Non-destructive exposure adjustment from -3.0 EV to +3.0 EV.
- Windows Recycle Bin deletion and best-effort undo of the last Lumen deletion batch.
- Multi-selection and Select All in Browse mode.
- Batch export with long-edge resize, default 3840 px, aspect-ratio preservation, no-upscale default, and one final encode from the original source.
- Portable application state under `Portable/Data` with no photo-sidecar files.

### Compatibility

- Windows 11
- .NET Framework 4.8 / WPF
- x64

### Notes

Versions 1.0.0 through 1.0.3 were pre-publication development/build-fix iterations. v1.0.4 is the first public GitHub release.
