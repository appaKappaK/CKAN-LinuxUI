# Visual Baselines

This folder stores baseline screenshots for CKAN Linux visual regression tests.

Commands from repo root:

- `CKAN_LINUX_UPDATE_BASELINES=1 dotnet test LinuxGUI.VisualTests/CKAN-LinuxGUI.VisualTests.csproj -c NoGUI`
- `dotnet test LinuxGUI.VisualTests/CKAN-LinuxGUI.VisualTests.csproj -c NoGUI`

Notes:

- Actual captures are written to `_build/visual-tests/actual/`.
- Tests fail when any rendered screenshot differs from its baseline.
- Update baselines only after intentional UI changes are reviewed.
