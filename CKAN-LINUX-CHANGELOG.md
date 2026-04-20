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
- Added a real queued-actions panel for install, update, and remove intents.
- Added a derived preview showing dependency installs, auto-removals, recommendations, suggestions, and conflicts.
- Replaced the stubbed apply button with the first real execution path over CKAN Core installer transactions.
- Apply now runs uninstall/install/upgrade work through `ModuleInstaller`, handles virtual-provider selection prompts, clears the queue on success, and refreshes the browser state afterward.
- Rebalanced the ready-state layout so the mod list and details pane stay primary, while pending changes now live in a full-width bottom section instead of crowding the details rail.
- Removed most development-stage copy from the ready state, including the footer and in-band diagnostics, leaving diagnostics only in error handling.

### Stage 3: Structured Search Improvements
- Added structured advanced filters for author text, compatibility text, and replacement-only browsing.
- Added clear-filter actions so the browser can be reset quickly without manually undoing each toggle.
- Started using the app-level search service as the source for current browser filter state instead of treating filter UI as view-model-only state.
- Added deterministic visual coverage for the advanced-filter browser state.
- Added first-class browser sorting for name, author, installed-first, and updates-first.
- Kept sorting in the same visible toolbar as search and filters instead of burying it in menus.
- Reworked mod rows to use a primary state badge, accent rail, and dedicated version/compatibility column for faster scanning.
- Compacted the filter/sort toolbar so common filters stay visible without visually outweighing the mod list.
- Renamed the queue section to `Actions to Apply` and the preview panel to `Preview of Changes`.
- Grouped the details pane into clearer overview, install state, package details, and relationship sections.
- Added a preflight summary card plus impact counts to the preview pane so queued changes read more like a real apply checklist.
- Added structured post-apply result reporting inside `Actions to Apply`, so success, blocked, canceled, and failure states no longer live only in the global status line.
- Slimmed the ready-state instance rail into a compact install switcher, moved useful install context into the persistent header, and reduced duplicated instance metadata once the browser is active.
- Added Linux-shell session persistence for the last selected instance, browser filter/sort state, advanced-filter visibility, and window geometry using a separate `linuxgui.settings.json` file under CKAN app data.
- Hardened the preview/apply flow to surface downloads required, provider-choice/setup prompts, and rate-limit follow-up actions as first-class preflight state instead of generic errors.
- Added a Linux packaging target for the Avalonia shell with a `ckan-linux` launcher, desktop entry, icon install layout, and LinuxGUI README so publish output can be staged as a real Fedora-style app tree.
- Added `scripts/run-linuxgui-dev.sh` to launch the Avalonia shell with isolated XDG data/config/cache roots so development runs do not collide with the user’s normal CKAN setup.
- Tightened the ready-state layout for narrower windows by giving the browser more width, letting the ready surface scroll vertically instead of collapsing, and replacing rigid one-line toolbars with wrapping controls.
- Added a dedicated `.gemini-review/work/` implementation bundle plus `scripts/gemini_ui_work.py`, so Gemini can work from curated screenshots and focused Avalonia source snapshots instead of only returning critique.
- Applied the first Gemini-driven polish pass to the browser UI: calmer filter/sort strip, cleaner mod row hierarchy, and a more clearly separated `Actions to Apply` / `Impact Preview` section.
- Added deterministic visual coverage for a completed apply state in the browser.
- Added a persisted `Display Scale` setting with a restart-to-apply flow, so the Linux shell can launch smaller or larger on the next run without sharing that preference with the legacy GUI.
- Applied the saved display scale at the shell root on startup and added deterministic visual coverage for the expanded display-settings panel.

### Stage 2 Tooling Subplan: External UX Review Loop
- Added a gitignored `.gemini-review/` bundle generated by the visual-test run.
- The bundle refreshes current screenshots, a CKAN-Linux context summary, and a ready-to-paste Gemini review prompt.
- This keeps external UI/UX feedback tied to deterministic visual states instead of ad hoc screenshots.
- Added an optional Gemini API review step after visual-test screenshot generation.
- The automated Gemini review defaults to `gemini-2.5-flash-lite`, loads `GEMINI_API_KEY` from `.env` when needed, and writes feedback into `.gemini-review/feedback/`.
- Added input-hash and cooldown checks so the advisory review step respects free-tier limits and skips unchanged bundles by default.

### Notes
- The new shell deliberately does not reuse `GUIMod` or other WinForms-era presentation models.
- The legacy `Newly compatible` filter has not been ported yet because the new shell does not yet track repository-update deltas.
