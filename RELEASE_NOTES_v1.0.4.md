# Lumen v1.0.4

Lumen v1.0.4 is the **first public release** of Lumen, a lightweight Windows photo viewer and culling application inspired by the browsing workflow of KDE Gwenview.

The project is an original Windows implementation and does not contain Gwenview/KDE source code.

## Highlights

- **Large thumbnail Browse mode** for quickly rejecting obviously bad shots.
- **Immediate Browse ↔ View switching** while retaining the current photo and browsing context.
- **Fast burst comparison** with `←` / `→`, nearby-image preload, and no transition animation.
- **Recycle Bin culling** with `Delete` and best-effort `Ctrl+Z` restore for the last Lumen deletion batch.
- **Configurable EXIF pane** with aperture, focal length, image dimensions, color space, white balance, saturation, sharpness and other standard metadata when available.
- **Fujifilm MakerNote support** for Quality, Sharpness, White Balance, Saturation, Dynamic Range, and Film Mode / Film Simulation where the camera stores those values.
- **Non-destructive exposure adjustment** from -3.0 EV to +3.0 EV; source files are not rewritten.
- **Batch resize/export** with long-edge sizing (3840 px by default), aspect-ratio preservation, no-upscale default, and a single final encode from the original image.
- **Portable design** with no NuGet dependency, telemetry, updater, cloud service, or library database.

## Supported image types

JPEG/JPG, PNG, BMP, TIFF/TIF, and GIF still-frame behavior.

HEIC/HEIF, WebP, camera RAW files, video, and animated-image editing are outside the v1.0 scope.

## Requirements

- Windows 11 x64
- .NET Framework 4.8 / WPF

The GitHub release ZIP contains a prebuilt `Lumen.exe`; no local compilation is required for normal use.

## Download

Download `Lumen-v1.0.4-win-portable.zip`, extract it, and run `Lumen.exe`.

Lumen is currently unsigned, so Windows SmartScreen may display a warning on first launch.

## Image-quality note

Reducing pixel dimensions is inherently lossy because pixels are discarded, and JPEG encoding is also lossy. Lumen preserves the original file and avoids unnecessary generational loss by applying orientation, exposure adjustment and resize from the original source before one final encode.
