# CKAN Linux Changelog

This file tracks the Linux-first shell work added in this repository.
It is intentionally separate from upstream CKAN release notes in `CHANGELOG.md`.

## 2026-07-17

### Catalog Freshness and Upstream CKAN Integration
- Ignored Rust catalog sidecar files older than CKAN's repository cache so stale sidecar data cannot hide newly indexed mods or versions.
- Added Rust catalog schema v2 support so sidecar rows honor CKAN's instance-wide and per-mod stable, testing, and development release tolerances while remaining compatible with schema v1 indexes.
- Ported upstream CKAN fixes for stability-aware compatibility sorting, duplicate relationship-resolver inputs, defensive stability snapshots, shared-disk free-space checks, and BOM-free UTF-8 registry/repository writes.
- Merged official CKAN through `0daeea8d` (the v1.36.5 development branch tip from 2026-07-02) into the Linux `dev` line while preserving the .NET 8 Linux targets and fork-specific instance and URL-launch behavior.
- Updated Linux-only callers and test fakes for the merged core APIs, and aligned the catalog benchmark harness with upstream's SharpZipLib 1.4.2 dependency.

## 2026-07-03

### LinuxGUI First-Run and Dialog Polish
- Replaced the first-run instance selection page with a modal picker over the normal browser shell, using the current header, skeleton browser background, table-style instance list, explicit selection state, and inline invalid-instance feedback.
- Added development-only fake instance rows behind `CKAN_LINUX_DEV_FAKE_INSTANCES=1` so first-run list selection, scrolling, invalid paths, and boundary styling can be tested without changing real CKAN instances.
- Updated top-level menu dropdown chrome and placement so File, Settings, and Help stay highlighted while open, align closer to the header buttons, and avoid stale popup positioning after opening owned dialogs.
- Cleaned up first-run and no-instance menus by disabling unavailable instance-specific actions, removing the obsolete user-guide entry, and pointing CKAN Linux issue reporting at the CKAN Linux repository.
- Removed the generic `Open ship directory` File menu action because ship folders are save-specific rather than instance-wide.
- Disabled `Settings > Game command lines` while LinuxGUI launch handling remains unavailable.
- Refreshed Settings, Compatible Game Versions, Installation Filters, Installation History, Download Statistics, Play Time, and related popup button hover states to use the newer two-tone dialog palette with darker content panels inside lighter dialog shells.
- Standardized utility tables in Manage Game Instances, Play Time, Download Statistics, and Installation History with darker table bodies, header bands, aligned separators, rounded table corners, and scrollbar gutters where scrollbars would otherwise overlap text.
- Made top-level menu popups recover their correct button-relative placement after the main window is moved, and clear the browser search focus when clicking outside the search field.
- Shortened the Installation Filters editor height and added scrollbar gutter spacing so the warning and action buttons remain reachable without oversized editor panes.
- Removed duplicate helper copy from Compatible Game Versions and Settings where controls or footer text already explained the action.

### LinuxGUI Browser and Settings Behavior
- Added an `External` filter for catalogued mods detected locally outside CKAN-managed installs, and moved `Replaceable` into the filter footer so it only appears when replacements exist.
- Fixed cached-state detection so a mod still shows as cached when any available archive for that identifier remains in the download cache, even after the mod is uninstalled.
- Reworked the Settings update panel for CKAN Linux packaging: CKAN Linux is shown as not versioned until release metadata exists, Linux GUI update checks are disabled, bundled/latest core versions are shown separately, and the releases page can be opened directly.
- Added an opt-in Settings toggle to prune installation-history snapshots older than 30 days; history pruning is disabled by default.
- Removed Settings controls that do not apply to the current LinuxGUI build, including language selection, tray behavior, unavailable auto-sort behavior, and dev-build update toggles.
- Clarified Manage Game Instances copy and action labels now that instance-management changes save immediately and switching uses `Open Install`.

## 2026-06-28

### LinuxGUI Status and Dialog Fixes
- Routed the `Clean up missing installed mods` no-op notice through the existing ready-header status surface instead of a separate overlay band, fixing repeat notices that went blank or rendered behind the workspace background.
- Let the ready-header status surface wrap and size closer to its current message while keeping transient notices out of the progress-bar state.
- Wrapped and padded prompt/detail and message-dialog bodies so vertical scrollbars no longer sit on top of long text.
- Restored saved main-window placement earlier in startup so the LinuxGUI window reopens closer to its last closed position.
- Fixed preview removal analysis to surface dependent removals separately from auto-removals, with a dedicated red preview section and Browse-scoped `View` action for impacted mods.

## 2026-06-25

### LinuxGUI Queue and Cleanup Polish
- Added a `View` action to the Review Queue panel header so queued mods can be opened directly in the scoped Browse view.
- Fixed preview-scoped Browse views so clearing the queue also clears stale dependency/recommendation/suggestion scoped views instead of leaving a dead `Close` return path behind.
- Preserved browse-list position more reliably across catalog reloads triggered by removal/apply flows.
- Stopped imported `.ckan` installs from queueing mods that are already represented in the current install state.
- Added a short transient no-op cleanup notice when `Clean up missing installed mods` finds nothing to repair, with repeat notices refreshing reliably.

## 2026-06-21

### LinuxGUI Reload and Prompt Fixes
- Fixed the header reload flow to reopen the current CKAN registry from disk before rebuilding the browser instead of reusing a stale in-memory registry snapshot.
- Restored unmanaged `GameData` DLL detection during LinuxGUI reloads, so manually installed DLL mods such as `scatterer` reappear after a reload without restarting the app.
- Added LinuxGUI regression coverage for refresh-triggered registry reloads and service coverage for rereading registry state from disk and rescanning unmanaged DLLs.
- Changed generic LinuxGUI overwrite/detail prompts to split large bodies into a scrollable detail block so long file lists no longer push the confirm and cancel buttons offscreen.
- Added a visual regression baseline for the long overwrite prompt layout.

## 2026-05-14

### LinuxGUI Startup and Loading Polish
- Cleaned up the pre-browser instance selection screen by removing duplicate open actions, hiding idle progress chrome, using the current dark-blue selection styling, and showing invalid-instance errors as an inline callout instead of a full red page.
- Changed the header reload button to show `Loading...` during cold-start catalog loads and reserve `Reloading...` for explicit refreshes.

## 2026-05-13

### LinuxGUI Catalog and Utility Fixes
- Fixed LinuxGUI update detection so the `Updatable` filter, selected-mod update badge, and queue-update action use CKAN registry update resolution instead of trusting the sidecar browse row alone.
- Fixed grouped/bulk update applies so rows queued without an explicit version resolve to the live CKAN registry latest at apply time instead of reusing stale browse-row version text.
- Added post-apply registry verification so LinuxGUI no longer reports a successful apply if requested install/update/remove results are not reflected in the installed registry.
- Hid the details-pane file-manager cache action when the selected version does not have an actual cached archive, instead of showing a disabled action inherited from another version.
- Removed duplicate execution-dialog body text when the status message repeats the dialog title.
- Shortened queued unpinned update version text to `Latest` so the queue table does not truncate `latest compatible version`.
- Changed installed browse rows to show the installed version as the primary table value and show a secondary latest value only when the registry has a newer version.
- Kept startup and reload installed-mod snapshots sorted by name from A-Z before the full catalog finishes loading.
- Made the header reload button switch to `Reloading...` while a refresh or catalog reload is active.
- Restored full skeleton replacement during explicit catalog reloads so stale rows are hidden until the refreshed catalog is ready.
- Hid unused tag/category filter chips from the advanced filter popout while preserving label filters.
- Tightened and clipped skeleton version placeholders so reload skeleton pills stay inside the row background.
- Added explicit top and bottom edge strokes to top-level dropdown menus so they match submenu containment.
- Made duplicate latest rows in the optional catalog sidecar choose the highest module version for browse-list display, with regression coverage for the older-installed/newer-available case.
- Stopped advanced-filter dismiss clicks from passing through to the mod list and accidentally opening the details pane.
- Changed `Mods > Installation History` to open as a non-modal owned utility window so the mod browser remains usable for cross-referencing.
- Updated the catalog loading and execution-overlay skeleton table to use the current browser header and column layout.
- Clamped internal browser column dividers so they only resize their adjacent columns instead of shrinking unrelated columns or moving the table metadata block.
- Cleaned up LinuxGUI dropdown menu chrome by removing per-item border lines, hiding menu separator strokes, strengthening the outer dropdown border, and offsetting submenus from their parent arrow lane.
- Surfaced whether the last LinuxGUI catalog load used the Rust catalog-index sidecar or CKAN registry fallback, and taught the dev launcher to link host app-data catalog-index files into the isolated dev data home.
- Added `scripts/benchmark-linuxgui-catalog.sh` to compare installed snapshot, CKAN registry-cache, and Rust sidecar catalog load timings against the local CKAN cache.
- Fixed the LinuxGUI dev launcher to rebuild and launch the same Debug output path instead of rebuilding Debug while checking the older VSCodeIDE output.
- Fixed the LinuxGUI dev launcher so missing optional host catalog-index files do not trip `set -e` before the app starts.
- Fixed LinuxGUI dev session log rotation so it keeps the newest logs instead of the oldest logs.
- Added targeted catalog-index regression coverage for duplicate latest sidecar rows.
- Added LinuxGUI regression coverage for internal browser-column divider resize behavior.

## 2026-05-09

### LinuxGUI Startup and Preview Polish
- Reduced the preview relationship scroll areas for dependencies, recommendations, suggestions, and supported mods so each section shows about four entries before scrolling.
- Warm-started the ready browser state when an active, remembered, or default install is already known, avoiding a brief legacy loading shell flash before the mod browser appears.
- Stopped startup from recreating a deleted saved game-instance folder just to initialize `CKAN/`; stale instances remain listed for explicit forgetting instead.
- Added regression coverage for immediate ready-state startup.

## 2026-05-07

### LinuxGUI Review Fix Pass
- Captured the 50-item design review checklist in `LINUXGUI_FINDINGS.tmp.md` before editing so the remaining findings stay visible.
- Improved interaction safety for destructive actions, including authentication-token deletion, download-cache purge actions, cached-archive purge, and forgotten game instances.
- Tightened LinuxGUI feedback loops for ready-status progress, add-repository failures, invalid cache-size and refresh-interval settings, download-statistics empty states, and play-time edits.
- Hardened plugin import so replacing a plugin DLL stages through a temporary file before overwriting the destination.
- Clarified several UI affordances: masked authentication tokens, explicit preferred-host button tooltips, recommendation-audit context text, distinct selected-mod resource labels, and consistent release-date formatting.
- Updated generic prompt behavior so yes/no style confirmations use action buttons directly, while provider/dependency choices require an explicit selection before confirmation.
- Added follow-up fixes for overflow and silent-failure cases: scroll-contained generic message dialogs and advanced filters, visible auto-update errors, compatible-version empty input validation, command-line load/duplicate warnings, installation-filter path validation, file-manager/help-link launch failures, and plugin operation status.
- Reworded plugin unloading to a session-level forget action, trimmed long plugin rows, and cleared repaired plugin load failures when a replacement assembly loads cleanly.
- Re-enabled the Play submenu parent when a game instance is active so its launch and command-line actions are reachable.
- Added bounds checks for restored main-window position/size, delayed launch update prompts while another owned dialog is active, and constrained execution-result overlays so long apply messages stay reachable.
- Changed manual game-instance registration to select the game folder instead of an anchor file, clarified that instance-management edits save immediately, and changed close-style controls there and in display scale to read as completed actions.
- Made play-time editing format and parse hours with the current UI culture while still accepting invariant decimal input.
- Reworded unavailable Settings controls so they explain that the feature is not available, and surfaced a fallback message when the launch update prompt cannot open the releases page.
- Restored visible keyboard focus indicators by removing LinuxGUI focus-adorn suppression, kept the Browse/Preview switcher visible instead of hover-hidden, and moved remaining purple preview/default accents into the established blue/amber status palette.
- Split catalog-load failures from normal empty filter results so failed loads show the diagnostic text and a retry action instead of a misleading no-results message.
- Renamed the plugin Reload action to Restart so the label reflects deactivate/activate behavior without implying the assembly can be unloaded and re-read from disk.
- Replaced the search clear text glyph with a small vector clear icon and marked the temporary findings checklist with fixed/mitigated status for follow-up tracking.
- Moved the ready-state loading/progress surface out of the centered header lane so it no longer renders underneath the Browse/Preview switcher.
- Replaced the floating Browse/Preview switcher with a workspace tab strip above the catalog content; Browse stays visible, while Review Queue appears with a queued item count only when there is something to review.
- Gave workspace tabs a resting outline and surface fill so they remain legible without requiring hover.
- Rebalanced the ready-workspace background with a darker app canvas, bordered workspace slab, and active tab surface that visually connects to the catalog content.
- Matched the workspace slab background to the tab surface so the protruding Browse tab reads as connected to the page rather than separated by a different band color.
- Moved ready-state loading/progress into the left header gap between Help and the centered logo, and changed the workspace tab band so the darker background stays above the divider while brighter tabs stand out from it.
- Let the dark workspace tab band span the full ready panel width by moving panel padding down into the Browse and Review content areas.
- Moved the workspace divider behind the tab controls and removed the tab button's own bottom stroke so no extra border hugs the active tab underside.
- Removed the outer ready-workspace border and tab-band divider so borders are limited to the Browse content controls and the tab outline.
- Aligned the workspace tab with the content surface edge and matched the Browse/Review content background to the tab color so the tab reads as attached to the lighter panel.
- Matched the workspace tab-band left inset to the content surface inset so the Browse tab edge lines up with the attached panel.
- Removed the remaining tab-band left padding so the rendered Browse tab edge lines up with the attached blue workspace surface.
- Removed the remaining left inset from the Browse and Review workspace surfaces so the blue panel starts directly under the tab edge.
- Extended the active workspace frame from the selected tab around the Browse/Review content panel, while inactive workspace tabs use a darker surface to read as separate tabs.
- Matched the active workspace tab's root and template backgrounds so no darker strip appears inside the selected Browse tab.
- Replaced native workspace-tab chrome with an explicit tab shell so the selected tab fills cleanly into its left corner without template background bleed.
- Removed the workspace panel side/bottom frame so the right-side light strip is gone and containment stays with the search and mod-list controls.
- Removed the Browse workspace right/bottom padding so the actual search/list frame reaches the panel edge instead of leaving a light strip beside it.
- Extended the blue Browse/Review workspace surface to the row edges and restored equal internal padding on all sides so the left and right blue borders around the list match.

## 2026-05-06

### Browser and Details Usability
- Added an inline search clear button that appears only when the browser search box has text.
- Added a details-pane "Mods Require This" relationship section with a capped inline preview and full browser-scoped view.
- Scoped installed-mod dependent counts to installed dependents so removal impact is easier to judge.
- Tightened resource-link labels and alignment in the details pane.

### File Menu Convenience
- Added `File > Open ship directory` to open the current instance's `Ships` folder and create `Ships/VAB` and `Ships/SPH` when needed.

## 2026-04-28

### Status Chrome
- Simplified the ready-state status pill so transient messages have more usable width and no longer compete with an embedded progress bar.

### Preview and Relationship Browser Fixes
- Fixed preview dependency/recommendation/suggestion `View` actions so Browse opens the relationship target mods instead of matching the source mod from text such as `recommended by`.
- Kept already-planned installs out of optional recommendation/suggestion results so self-recommending or already queued mods do not block Apply.
- Changed recommendation-analysis failures during preview into notices instead of conflicts when Apply can otherwise proceed.
- Improved preview conflict issue text for entries that do not have a second conflict target.

### Browser Layout Stability
- Reserved the mod browser vertical scrollbar lane even for short filtered lists so column headers stay aligned with rows in scoped relationship views.
- Temporarily disabled the Play menu entries in both header variants while launch handling remains unavailable in the Linux shell.
- Added regression coverage for preview relationship browsing and already-planned recommendation handling.

### Catalog Performance
- Added optional Rust sidecar catalog-index support for fast LinuxGUI catalog list builds without per-row CKAN registry metadata resolution.
- Expanded catalog timing diagnostics for sidecar index loading, installed-row handling, filter application, details loading, and preview generation.
- Removed duplicate startup catalog loads by making explicit startup/switch flows own catalog loading while instance-change notifications only refresh instance UI state.

### Rust Sidecar Publishing
- Published `ckan-meta-rs` as the public Rust catalog sidecar generator at `https://github.com/appaKappaK/ckan-meta-rs`.
- Updated the root and LinuxGUI README files to point at the public `ckan-meta-rs` source while documenting that CKAN-Linux includes the optional sidecar reader.

## 2026-04-26

### Packaging and Entry Points
- Added `scripts/install-linuxgui-local.sh` for installing `ckan-linux` under `~/.local` by default.
- Updated root and LinuxGUI README files around the desktop app workflow, launch paths, and package layout.
- Routed Debian graphical no-argument `ckan` launches to the Linux GUI while leaving argument-driven and headless flows on existing CKAN paths.
- Fixed LinuxGUI taskbar/window icon metadata and desktop entry behavior.

### Mod Cleanup and Queue Management
- Defaulted the browser to the `Installed` list when no saved filter state is active, making cleanup and updates the startup focus instead of the full catalog.
- Added an empty-installed fallback so instances with no installed mods open on `All` instead of an empty `Installed` list.
- Added `Queue remove all installed mods` to the `Mods` menu as a confirmed bulk cleanup action for CKAN-managed installed mods.
- Added `Clean up missing installed mods` as a confirmed direct cleanup action for stale CKAN registry entries and stale autodetected DLL records left behind by manually deleted `GameData` folders, followed by a browser reload.
- Improved removal previews to include auto-removable dependencies that are no longer required by mods remaining installed.
- Added tests covering remove-all queue replacement, direct missing-installed cleanup, remove-only dependency auto-removals, saved advanced filter restoration, and default installed-list behavior.

### Preview and Apply Flow
- Added scroll containment for long apply follow-up lists so large cleanup runs do not create oversized warning dialogs.
- Changed optional recommendations, suggestions, and supported integration mods from a blocking chooser flow to an informational preview notice.
- Added a `Supported` optional-extras preview section and per-section `View` actions so users can inspect optional extras in Browse before applying.
- Kept required dependencies automatic, while provider choices for required virtual dependencies still prompt during apply.
- Reformatted the provider-choice dialog with a capped scrollable list, LinuxGUI dark surface, clearer required-dependency prompt text, and provider rows that include version, cache, download, and summary hints when CKAN metadata provides them.

### Documentation and Housekeeping
- Updated `LinuxGUI/README.md` with the current desktop workflow, cleanup menu actions, optional-extras behavior, and provider-choice behavior.
- Moved `PROJECT_PLAN.md` into a clearer local-planning ignore section and untracked it from git.

## 2026-04-25

### Preview and Queue Persistence
- Added persisted queued-action snapshots so queued work can survive view-model/app recreation.
- Added configurable mod browser column layout and persistence for browser columns.
- Improved queue drawer behavior, collapsed queue/apply-result stubs, and post-apply result acknowledgement.
- Added richer preview conflict choice models and browser-scoped conflict review flows.

### Relationship Browsing
- Added relationship browser scoping so dependencies, recommendations, suggestions, and conflicts can open Browse filtered to relevant mods.
- Added relationship details models for selected mods and preview entries.
- Improved recommendation audit grouping and optional-extras selection behavior before the later non-blocking preview notice change.

### Preview Polish
- Added preflight summary cards, impact metrics, dependency guidance, download guidance, and footer notes.
- Tightened preview empty/loading/ready/blocked states and refreshed preview visual baselines.

## 2026-04-24

### Visual Polish Pass
- Applied the shared `utility-list` visual language across dialogs and list-heavy utility windows.
- Added Add Repository, Add Auth Token, and message dialog windows.
- Polished plugin, recommendation audit, play-time, settings, download-statistics, command-line, preferred-host, and unmanaged-file dialogs.
- Refined instance summaries and catalog row presentation for a calmer, denser browser.

### Recommendation Audit
- Expanded recommendation audit rows with clearer kind badges, details, download counts, and selection affordances.
- Improved optional recommendation/suggestion/supporter queue source text so queued extras retain context.

### Visual Baselines
- Refreshed deterministic visual baselines for browser, preview, startup, display scale, and dialog-adjacent states after the polish pass.

## 2026-04-23

### Search and Filters
- Added structured advanced filters for author, identifier, summary, description, license, language, relationships, tags, labels, compatibility, cache state, replacement state, and install/update state.
- Added tag and label filter pickers plus clear-filter actions so the browser can be reset quickly.
- Started using the app-level search service as the source for current browser filter state instead of treating filter UI as view-model-only state.
- Added first-class browser sorting for name, author, popularity, compatibility, release date, install date, installed-first, and updates-first.

### Navigation and Chrome
- Added About and Settings windows and expanded the main menu surface.
- Added resource-link models and mod version choice UI improvements.
- Reworked the filter/sort toolbar and details sections so common workflows stay visible without dominating the mod list.

## 2026-04-22

### Details and Preview Depth
- Expanded mod details with richer metadata, version choices, resource links, relationship counts, and better installed/cache/update state.
- Added version-targeted queue actions and detail text for queued installs, updates, removals, and downloads.
- Added preview-specific visual states for empty, queued, applying, and applied flows.
- Improved apply result reporting so success, warning, blocked, canceled, and error states appear inside the queue/preview flow instead of only in the global status line.

### Loading and Narrow Layouts
- Added catalog skeleton rows and persisted skeleton settings so loading states stay visually stable.
- Added narrower browser/detail visual coverage and tightened details sizing for smaller windows.
- Improved `run-linuxgui-dev.sh` rebuild checks, logging, and launch behavior.

## 2026-04-21

### Shell Parity Windows
- Added Linux GUI windows for compatible game versions, download statistics, game command lines, installation filters, installation history, plugins, preferred hosts, play time, and unmanaged files.
- Added `LinuxGuiPluginController` and `PluginCompat/` bridge work so legacy GUI plugins can be exercised from the Linux shell.
- Added plugin smoke-test coverage and wired plugin compatibility projects into the solution.

### Settings and Scale
- Added a dedicated display-scale window and persisted UI scale settings.
- Applied saved display scale at shell startup with a restart-to-apply flow.
- Expanded settings persistence for window state and shell-level options.

## 2026-04-20

### Browser, Queue, and Preview Basics
- Built out app-layer models and services for settings, search filters, queued actions, apply previews, and catalog state.
- Added a real queued-actions panel for install, update, remove, and download intents.
- Added a derived preview showing dependency installs, auto-removals, downloads required, recommendations, suggestions, and conflicts.
- Replaced the stubbed apply button with the first real execution path over CKAN Core installer transactions.
- Apply now runs uninstall/install/upgrade work through `ModuleInstaller`, handles virtual-provider selection prompts, clears the queue on success, and refreshes browser state afterward.

### Layout and Visual Coverage
- Rebalanced the ready-state layout so the mod list and details pane stay primary, with pending changes in a full-width bottom section.
- Removed most development-stage copy from the ready state, leaving diagnostics for actual error handling.
- Added visual baselines for startup, browser filtering/sorting, queued state, display scale, and completed apply state.
- Added first tests around app settings persistence and visual-state rendering.

### Developer Workflow
- Added Linux packaging scaffolding for a `ckan-linux` launcher, desktop entry, icon layout, and packaged README.
- Added `scripts/run-linuxgui-dev.sh` for isolated XDG data/config/cache/run directories during development.
- Added Gemini UI review/work bundle scripts so external UI review can use deterministic screenshots and focused source snapshots.

## 2026-04-19

### Foundations
- Added the new `App/` application layer to isolate Linux UI state and services from the legacy WinForms GUI.
- Added the new `LinuxGUI/` Avalonia desktop shell targeting `net8.0`.
- Added `LinuxGUI.VisualTests/` for deterministic headless screenshot regression testing.
- Wired the Linux GUI projects into `CKAN.sln` and the build pipeline.
- Added Linux-shell-specific logging/config support so the new GUI does not collide with legacy GUI output.
- Added the `AvaloniaUser` bridge for progress, messages, and synchronous `IUser` dialogs.

### Startup and First Browser
- Added startup stages for loading, empty state, selection required, ready, and error handling.
- Added instance selection and instance switching through `IGameInstanceService`.
- Replaced the placeholder main window with a Linux shell that centers instance management first.
- Added a first real catalog browser over CKAN Core with basic search, filters, details, and visual baselines.

## Notes
- The new shell deliberately does not reuse `GUIMod` or other WinForms-era presentation models.
- The legacy `Newly compatible` filter has not been ported yet because the new shell does not yet track repository-update deltas.
