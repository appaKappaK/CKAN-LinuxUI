# CKAN Linux Ready-State Redesign

## Summary
Redesign the ready state into a dense, list-first Linux utility UI. Remove the ready-state sidebar and the separate `Active Install` strip, move install context and shell settings into the header, make the mod list the dominant surface, keep a fixed right details pane with a header toggle, and replace the oversized bottom area with a true pending-changes drawer.

This pass changes shell layout and interaction only. It does not change CKAN Core behavior, installer logic, search semantics, or add new provider/source filters.

## Implementation Changes
### Shell and header
- Ready state becomes: slim header, primary browser/list column, fixed right details pane, bottom pending-changes drawer.
- Remove the ready-state left rail and the separate ready-state `Active Install` band entirely.
- Keep the current install-picker rail only for startup, empty, selection-required, and error states.
- Enforce a minimum ready-state window size of `1040x700`.
- Single instance shows passive install context in the header only; no switcher chrome.
- Multiple instances use a compact header dropdown button with immediate switch on selection.
- Inaccessible instances show a warning indicator in the dropdown.
- If the user tries to switch installs with a non-empty queue, show a confirm dialog that explicitly states the queued-change count and that pending changes for the current instance will be discarded.
- Confirm action label is `Discard Queue and Switch`; cancel action label is `Cancel`.
- Confirming clears the current-instance queue and switches installs; cancel keeps the current install and queue intact.
- Header priority at minimum width is: shell actions, status/progress, instance label.
- No header action icon disappears at the supported minimum width; the instance label truncates first, and the status text ellipsizes inside its container.
- Remove the redundant ready-state `State Ready` pill.

### Header status and shell settings
- Keep progress compact and header-safe: use the existing `StatusMessage` and `ProgressPercent`, plus an internal shell state for indeterminate progress when work is active without meaningful percent.
- Status/progress lives in a fixed-width compact block, target `260px`, floor `220px`, with a thin progress bar and one-line status text.
- If `StatusMessage` contains richer transfer details from `ByteRateCounter.Summary`, clamp it to one line in the header and expose the full string via tooltip rather than expanding the header.
- Determinate progress uses the compact bar normally; indeterminate work uses the same bar in indeterminate mode rather than leaving an inert `0%` bar.
- Move shell-only settings into a header settings popover.
- Keep the persisted display-scale slider and restart-to-apply behavior.
- Settings popover contains: scale slider, current vs next-launch summary, reset to 100%, restart now.
- When saved scale differs from applied scale, show a persistent browser-area restart strip outside the popover.
- The restart strip includes current scale, next-launch scale, `Restart Now`, and `Dismiss`.
- `Dismiss` hides the restart strip only for the current session and current pending scale value.
- If the user changes the scale again in the same session, the restart strip reappears.
- After a successful restart applies the pending scale, the restart strip does not reappear.
- Restart-strip dismissal state is independent of queue/apply state.
- Add a dedicated header icon button for toggling the details pane and persist that visibility.
- The details-toggle icon must show clear on/off state and tooltip text so a hidden pane remains discoverable.

### Browser, filters, and list
- The mod list is the hero surface. Remove the current dashboard/card feel.
- Use a two-column ready-state layout: list on the left, details pane on the right.
- Keep search primary and always visible.
- Keep common filter chips visible in the main toolbar.
- Filter chips are multi-select toggles.
- Inactive chips use low-contrast neutral styling; active chips use a filled accent treatment distinct from the selected-row accent line.
- Keep sort visible but visually lighter than search and primary filters.
- Replace the current advanced-filter block with a compact toolbar trigger labeled `More Filters ▾`.
- `More Filters ▾` opens an anchored overlay attached to the button itself, opening below it over the list area rather than reflowing the toolbar or list.
- The overlay contains the current advanced controls only: `Author contains`, `Compatibility contains`, `Has Replacement`, current advanced-filter summary, and `Clear Advanced`.
- Advanced filters apply live as controls change; there is no separate confirm/apply button.
- The `More Filters` trigger reflects hidden active state by showing the active advanced-filter count in its label when nonzero.
- The overlay has a clear border/shadow, traps focus while open, and closes on `Escape` or outside click.
- Outside click behavior is single-click dismiss-and-forward: if the click target is an interactive browser/list element (for example a row or toolbar control), dismiss the overlay and let that same click activate the target.
- Clicking non-interactive browser/list area dismisses the overlay only.
- The mod list and the details pane scroll independently.
- Mod rows become compact scan rows: one-line ellipsized name, tooltip for truncated full name, one-line clamped summary, compact secondary text, right-side version/compatibility column, one primary state badge.
- Row column sizing is fixed enough to prevent layout drift:
  - left accent rail stays narrow and constant
  - main text column is flexible
  - right metadata column targets `132px` width with a hard floor of `120px`
  - if width pressure occurs, right-column secondary text truncates before the main name/summary column is squeezed below usability
- Badge precedence remains: `Incompatible`, `Update Available`, `Installed`, `Has Replacement`, `Cached`, `Available`.
- Existing repo logic already avoids the installed-plus-incompatible edge case when a compatible update exists, so no extra precedence rule is added.
- Incompatible mods remain visible by default.
- Use strong hover and selection states, including a clear left-edge selection accent and visible keyboard focus treatment.
- Hover is visual only in this pass; hover does not reveal extra row actions.
- Empty search/filter results show a dedicated empty-results panel in the list area with `No mods match the current search and filters.` and a `Clear Filters` action.
- Catalog loading uses skeleton rows in the list area rather than a centered spinner to preserve row rhythm and avoid layout jump.

### Details pane
- Keep the details pane docked on the right.
- Fixed width `320px`, hard floor `300px`, no drag-resize in this pass.
- Visible by default, toggleable from the header icon, and persisted in shell settings.
- Hiding details is allowed at any window width.
- No modal or overlay details behavior in this pass.
- When no mod is selected, the details pane shows a compact placeholder state instead of blank space.
- When a mod is selected and details are loading, show a compact loading state/skeleton rather than flashing blank content.
- Selected-mod layout is grouped into: title and authors, primary actions, install/compatibility summary, metadata grid, relationships summary, description.
- Primary action hierarchy is explicit:
  - show only one primary queue action for the selected mod at a time
  - `Queue Install` for available mods
  - `Queue Update` for installed mods with updates
  - `Queue Remove` for installed mods without updates
  - if the mod already has a queued action, the primary button changes to its queued state and acts as dequeue/cancel for that queued action
- Metadata grid reflows to a stacked single-column label/value layout at narrow pane widths instead of forcing cramped two-column wrapping.
- Relationship summary is a compact vertical list of labeled rows (`Dependencies`, `Recommendations`, `Suggestions`) with readable counts, not tiny stat tiles.
- Labels must remain readable and not clip awkwardly.
- Long technical strings and long author lists must wrap safely.
- Dates and short status values stay on one line where possible.
- Description rendering stays plain-text/unstyled text in this pass; markdown/rendered rich text is deferred.

### Pending changes drawer
- Empty state collapses to a full-width stub bar with `No pending changes`.
- The entire empty stub is the click target.
- First empty-to-non-empty transition auto-expands the drawer.
- If the user manually collapses a populated drawer, keep it collapsed while the queue remains non-empty.
- Collapsed populated stub shows queued count, preview status, and a compact `Apply Changes` action on the right.
- Clicking the non-button area of a collapsed populated stub expands the drawer.
- Clicking `Apply Changes` from the collapsed populated stub auto-expands the drawer first, then runs apply so progress/result feedback is visible.
- Expanded drawer layout is queued actions on the left and preview/apply result/conflicts/follow-up on the right.
- If an error or apply result remains after the queue clears, show a severity-colored collapsed summary stub instead of silently disappearing.
- Success-result stub uses a subdued success treatment and persists until dismissed or replaced by a new queue/apply result.
- Error/block/warning-result stub uses a stronger warning/error treatment and persists until dismissed or replaced.
- On blocked, canceled, warning, or error apply results, the queue is preserved; the user can correct the issue and retry without re-queueing.
- The drawer is not part of the `Escape` cascade; only overlays and selection participate there.

### Interaction, selection, and motion
- Essential keyboard scope only: arrow-key list navigation, `Home`/`End` in the list, `Ctrl+F` focuses search, `Ctrl+Shift+B` toggles the drawer, `Escape` closes the most recently opened overlay first, then clears mod selection if no overlay is open.
- Keyboard shortcut scope rules:
  - `Ctrl+F` is handled by the shell only when focus is not in an editable text control; if focus is already in search, preserve default text-edit behavior.
  - `Ctrl+Shift+B` is handled by the shell only when focus is not in an editable text control.
  - `Escape` closes overlay/popover layers first; if no overlay is open and focus is in an editable text control, it does not clear mod selection.
- Keep interaction scope tight: single selection only, no context menus, no multi-select.
- On filter/search/sort refresh, preserve the selected mod by identifier if it still exists; otherwise select the first visible result; if no results remain, clear selection and show the details placeholder.
- On filter/search/sort refresh, preserve current list scroll offset when the preserved selection remains visible; if selection changes because the previous selection no longer exists, scroll to the new selection.
- On instance switch, clear selection, reset the details placeholder, and reset list scroll to the top.
- After apply, reload the list and preserve selection by identifier if it still exists; otherwise fall back to the first visible result.
- After apply-driven reload, scroll to the preserved/new selection if one exists; if no results remain, reset list scroll to top.
- Tooltips on truncated text should appear quickly enough to support dense browsing; they should not feel delayed.
- All state transitions are instant or extremely short, maximum `100ms` to `150ms`, with no decorative bounce or slow easing.
- Accessibility beyond the essential keyboard behavior is explicitly deferred to a later pass.

## Test Plan
- Add/update headless visual tests for: single-instance ready state with no ready-state sidebar or install strip, multi-instance header dropdown open, details visible, details hidden, empty-results state, collapsed empty queue stub, collapsed populated queue stub with `Apply Changes` visible, expanded drawer with queued actions, restart strip visible, settings popover open, `More Filters` overlay open, narrow ready state at `1040x700`, row with multiple simultaneous states, details pane rendering long technical strings and long author lists without bad clipping, metadata grid reflow at narrow pane width.
- Add viewmodel tests for: single-instance ready state hides switcher chrome, multi-instance dropdown switching, non-empty-queue instance switch confirmation includes queued-count + discard wording, details toggle persistence, drawer auto-expands only on empty-to-non-empty transition, manual collapse stays sticky while queue remains non-empty, empty queue returns to empty stub, collapsed-stub apply expands before apply feedback, saved scale change triggers restart strip, dismissed restart strip reappears on a new scale change, restart strip stays gone after successful restart, advanced-filter count label updates when hidden filters are active.
- Add interaction tests for: `More Filters` outside-click dismiss-and-forward on row click, non-interactive outside-click dismiss-only behavior, `Ctrl+F` and `Ctrl+Shift+B` shortcut suppression while editable controls are focused, and `Escape` behavior priority (overlay close before selection clear).
- Manual acceptance criteria: ready state is materially denser at `100%` and `90%`, single-instance use no longer wastes a full sidebar column, the mod list is clearly the dominant surface, header remains stable at `1040px` with actions, compact status block, and truncated instance label, details text does not clip awkwardly at the `300px` floor, empty queue does not reserve large blank space, collapsed populated queue still supports quick apply, overlays feel instant and do not leave stale focus behind.

## Assumptions and Defaults
- Dark theme remains the only theme in this pass.
- Ready-state redesign is the only major scope; startup/empty/error states get consistency cleanup only as needed.
- Multiple-instance switching uses a compact header dropdown button.
- Details pane is fixed-width `320px`, floor `300px`, no resize handle, toggleable, and persisted.
- `More Filters ▾` opens an anchored overlay, not an inline expanding row.
- Row names truncate in-list and expose full text via tooltip.
- Incompatible mods remain visible by default.
- Context menus, multi-select, provider/source filters, markdown description rendering, and dedicated accessibility work are explicitly deferred.
- Gemini/DeepSeek feedback is advisory only; acceptance is based on the criteria above.
