# CKAN Linux Changelog

This file tracks the Linux-first shell work added in this repository.
It is intentionally separate from upstream CKAN release notes in `CHANGELOG.md`.

## 2026-04-19

### Stage 0: Foundations
- Added the new `App/` application layer to isolate the Linux UI from legacy WinForms state.
- Added the new `LinuxGUI/` Avalonia desktop shell targeting `net8.0`.
- Added `LinuxGUI.VisualTests/` for deterministic headless screenshot regression testing.
- Wired the new projects into `CKAN.sln` and the build pipeline.
- Added Linux-shell-specific logging/config support so the new GUI does not collide with legacy GUI output.
- Added the `AvaloniaUser` bridge for progress, messages, and synchronous `IUser` dialogs.

### Stage 1: Instance Startup Flow
- Added startup stages for loading, empty state, selection required, ready, and error handling.
- Added instance selection and instance switching through `IGameInstanceService`.
- Replaced the placeholder main window with a Linux shell that centers instance management first.

### Stage 2: Browser and Visual Verification
- Added a real mod catalog service over CKAN Core with search and visible common filters.
- Replaced the ready-state placeholder with a real browser surface, searchable mod list, and details pane.
- Improved the details pane to show status badges, compatibility, package type, license, release date, download size, and relationship counts.
- Added a no-results state for filtered catalog views.
- Added deterministic visual test coverage for startup states and browser filtering, with committed baselines.

### Notes
- The new shell deliberately does not reuse `GUIMod` or other WinForms-era presentation models.
- The legacy `Newly compatible` filter has not been ported yet because the new shell does not yet track repository-update deltas.
