# CKAN Linux Changelog

This file tracks the Linux-first shell work added in this repository.
It is intentionally separate from upstream CKAN release notes in `CHANGELOG.md`.

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
