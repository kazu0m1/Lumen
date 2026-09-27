# Contributing to Lumen

Lumen is intentionally small and workflow-focused. Changes should preserve the fast photo-culling experience and avoid turning the application into a general-purpose photo editor.

## Development principles

- Keep Browse ↔ View switching immediate and predictable.
- Preserve original photo files unless the user explicitly exports a new file.
- Prefer Windows/.NET Framework facilities over large new dependencies.
- Keep background work bounded and cancellable.
- Treat deletion, export, metadata parsing, and settings persistence as data-safety-sensitive areas.
- Do not add telemetry, background services, or network requirements without an explicit design decision.

## Build

Run `Build-Lumen.cmd` on Windows with .NET Framework 4.8 / WPF available. The project intentionally has no NuGet dependencies.

## Pull requests

Please describe the user-visible problem, the intended behavior, and how the change was tested. For UI or photo-processing changes, include relevant before/after details.
