using System;
using System.IO;
using System.Linq;
using System.Text;

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using NUnit.Framework;

namespace CKAN.LinuxGUI.VisualTests
{
    internal static class VisualTestSupport
    {
        private const string UpdateBaselinesVar = "CKAN_LINUX_UPDATE_BASELINES";
        private static readonly string[] WorkScreenshotNames =
        {
            "startup-ready.png",
            "startup-ready-narrow.png",
            "browser-filtered.png",
            "browser-advanced-filters.png",
            "browser-loading.png",
            "browser-details-loading.png",
            "browser-display-scale.png",
            "browser-queued.png",
            "browser-applied.png",
        };

        private static readonly string[] WorkCodeFiles =
        {
            "LinuxGUI/MainWindow.axaml",
            "LinuxGUI/MainWindowViewModel.cs",
            "App/Models/ModListItem.cs",
            "App/Models/ChangesetPreviewModel.cs",
        };

        public static void CaptureAndAssert(Window window, string scenarioName)
        {
            string repoRoot = FindRepoRoot();
            string actualDir = Path.Combine(repoRoot, "_build", "visual-tests", "actual");
            string baselineDir = Path.Combine(repoRoot, "LinuxGUI.VisualTests", "Baselines");
            string actualPath = Path.Combine(actualDir, $"{scenarioName}.png");
            string baselinePath = Path.Combine(baselineDir, $"{scenarioName}.png");

            Directory.CreateDirectory(actualDir);
            Directory.CreateDirectory(baselineDir);

            window.Show();
            var frame = window.CaptureRenderedFrame()
                       ?? throw new InvalidOperationException($"No rendered frame was captured for {scenarioName}.");
            frame.Save(actualPath);
            UpdateGeminiReviewBundle(repoRoot, scenarioName, actualPath);
            window.Close();

            if (ShouldUpdateBaselines() || !File.Exists(baselinePath))
            {
                frame.Save(baselinePath);
                Assert.Pass($"Baseline updated for {scenarioName}: {baselinePath}");
            }

            using var expectedStream = File.OpenRead(baselinePath);
            using var actualStream = File.OpenRead(actualPath);
            using var expected = WriteableBitmap.Decode(expectedStream);
            using var actual = WriteableBitmap.Decode(actualStream);
            Assert.That(ImageDiffRatio(expected, actual), Is.EqualTo(0D),
                $"Visual regression mismatch for {scenarioName}. Expected {baselinePath}, actual {actualPath}.");
        }

        private static bool ShouldUpdateBaselines()
            => string.Equals(Environment.GetEnvironmentVariable(UpdateBaselinesVar), "1", StringComparison.Ordinal);

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "CKAN.sln")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate repo root from test output directory.");
        }

        private static unsafe double ImageDiffRatio(WriteableBitmap expected, WriteableBitmap actual)
        {
            using var expectedLock = expected.Lock();
            using var actualLock = actual.Lock();

            if (expectedLock.Size != actualLock.Size
                || expectedLock.RowBytes != actualLock.RowBytes)
            {
                return 1D;
            }

            int byteCount = expectedLock.RowBytes * expectedLock.Size.Height;
            byte* expectedPtr = (byte*)expectedLock.Address;
            byte* actualPtr = (byte*)actualLock.Address;
            long different = 0;

            for (int i = 0; i < byteCount; i++)
            {
                if (expectedPtr[i] != actualPtr[i])
                {
                    different++;
                }
            }

            return Math.Round((double)different / byteCount, 6);
        }

        private static void UpdateGeminiReviewBundle(string repoRoot,
                                                     string scenarioName,
                                                     string actualPath)
        {
            string bundleDir = Path.Combine(repoRoot, ".gemini-review");
            string shotsDir = Path.Combine(bundleDir, "screenshots");
            string shotCopyPath = Path.Combine(shotsDir, $"{scenarioName}.png");

            Directory.CreateDirectory(bundleDir);
            Directory.CreateDirectory(shotsDir);
            File.Copy(actualPath, shotCopyPath, overwrite: true);

            var screenshotFiles = Directory.GetFiles(shotsDir, "*.png")
                                           .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                                           .ToList();

            File.WriteAllText(Path.Combine(bundleDir, "README.md"),
                              BuildReadme(repoRoot, screenshotFiles));
            File.WriteAllText(Path.Combine(bundleDir, "GEMINI-REVIEW-PROMPT.md"),
                              BuildPrompt(screenshotFiles));
            File.WriteAllText(Path.Combine(bundleDir, "CONTEXT.md"),
                              BuildContext(repoRoot, screenshotFiles));
            UpdateGeminiWorkBundle(repoRoot, screenshotFiles);
        }

        private static void UpdateGeminiWorkBundle(string repoRoot,
                                                   System.Collections.Generic.IReadOnlyList<string> screenshotFiles)
        {
            string bundleDir = Path.Combine(repoRoot, ".gemini-review", "work");
            string shotsDir = Path.Combine(bundleDir, "screenshots");
            string codeDir = Path.Combine(bundleDir, "code");

            Directory.CreateDirectory(bundleDir);
            Directory.CreateDirectory(shotsDir);
            Directory.CreateDirectory(codeDir);

            foreach (var file in Directory.GetFiles(shotsDir, "*.png"))
            {
                File.Delete(file);
            }

            foreach (var sourcePath in screenshotFiles.Where(path => WorkScreenshotNames.Contains(Path.GetFileName(path),
                                                                                                 StringComparer.OrdinalIgnoreCase)))
            {
                File.Copy(sourcePath,
                          Path.Combine(shotsDir, Path.GetFileName(sourcePath)),
                          overwrite: true);
            }

            foreach (var relativePath in WorkCodeFiles)
            {
                string sourcePath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                string destinationPath = Path.Combine(codeDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)
                                        ?? codeDir);
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }

            var bundledShots = Directory.GetFiles(shotsDir, "*.png")
                                        .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                                        .ToList();
            var bundledCodeFiles = Directory.GetFiles(codeDir, "*", SearchOption.AllDirectories)
                                            .OrderBy(path => Path.GetRelativePath(codeDir, path), StringComparer.OrdinalIgnoreCase)
                                            .ToList();

            File.WriteAllText(Path.Combine(bundleDir, "README.md"),
                              BuildWorkReadme(bundledShots, bundledCodeFiles));
            File.WriteAllText(Path.Combine(bundleDir, "GEMINI-WORK-PROMPT.md"),
                              BuildWorkPrompt(bundledShots, bundledCodeFiles));
            File.WriteAllText(Path.Combine(bundleDir, "CONTEXT.md"),
                              BuildWorkContext(repoRoot, bundledShots, bundledCodeFiles));
        }

        private static string BuildReadme(string repoRoot,
                                          System.Collections.Generic.IReadOnlyList<string> screenshotFiles)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Gemini Review Bundle");
            sb.AppendLine();
            sb.AppendLine("This folder is auto-generated by `LinuxGUI.VisualTests`.");
            sb.AppendLine("It is intended as a handoff bundle for external UI/UX review.");
            sb.AppendLine();
            sb.AppendLine("Files:");
            sb.AppendLine("- `CONTEXT.md`: current CKAN-Linux progress summary and review framing");
            sb.AppendLine("- `GEMINI-REVIEW-PROMPT.md`: ready-to-paste prompt for Gemini");
            sb.AppendLine("- `screenshots/`: latest deterministic UI captures from visual tests");
            sb.AppendLine("- `feedback/`: latest automatic Gemini feedback, if a key is configured");
            sb.AppendLine();
            sb.AppendLine($"Repo root: `{repoRoot}`");
            sb.AppendLine($"Updated: `{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}`");
            sb.AppendLine();
            sb.AppendLine("Current screenshots:");
            foreach (var file in screenshotFiles)
            {
                sb.AppendLine($"- `{Path.GetFileName(file)}`");
            }
            return sb.ToString();
        }

        private static string BuildPrompt(System.Collections.Generic.IReadOnlyList<string> screenshotFiles)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Review this Linux desktop app UI for usability, information hierarchy, clarity, and visual polish.");
            sb.AppendLine();
            sb.AppendLine("Context:");
            sb.AppendLine("- This is a Fedora/Linux-first Avalonia rewrite of CKAN for Kerbal Space Program mod management.");
            sb.AppendLine("- The app currently supports instance selection, mod browsing, visible and advanced filters, a visible sort control, a details pane, a queue of install/update/remove intents, a preflight preview with download/setup hints, a persisted display-scale setting, and a real apply path.");
            sb.AppendLine("- The main UI debt now is refinement of information hierarchy and browsing flow rather than missing shell scaffolding.");
            sb.AppendLine();
            sb.AppendLine("What to focus on:");
            sb.AppendLine("1. What feels dated, cluttered, confusing, or visually weak.");
            sb.AppendLine("2. Whether the list/details/queue layout is the right information hierarchy after moving pending changes into a bottom section.");
            sb.AppendLine("3. Whether the mod list rows are now easy to scan and whether the filter/sort strip still feels too loud.");
            sb.AppendLine("4. Whether the preview pane makes downloads, prompts, conflicts, and post-apply follow-up states understandable before the user commits.");
            sb.AppendLine("5. Specific, ranked improvements with the highest UX impact.");
            sb.AppendLine();
            sb.AppendLine("Constraints:");
            sb.AppendLine("- Desktop Linux app, not a web UI");
            sb.AppendLine("- Large mod lists must remain scannable");
            sb.AppendLine("- Common filters should remain visible, not hidden in menus");
            sb.AppendLine("- The app should feel more modern than legacy CKAN without becoming visually noisy");
            sb.AppendLine();
            sb.AppendLine("Screenshots included in this bundle:");
            foreach (var file in screenshotFiles)
            {
                sb.AppendLine($"- {Path.GetFileName(file)}");
            }
            sb.AppendLine();
            sb.AppendLine("Please respond with:");
            sb.AppendLine("- a short overall assessment");
            sb.AppendLine("- the top 5 UX issues in priority order");
            sb.AppendLine("- concrete layout/content improvements");
            sb.AppendLine("- any labels or wording that should be rewritten");
            return sb.ToString();
        }

        private static string BuildContext(string repoRoot,
                                           System.Collections.Generic.IReadOnlyList<string> screenshotFiles)
        {
            string changelogPath = Path.Combine(repoRoot, "CKAN-LINUX-CHANGELOG.md");
            string changelogSnippet = File.Exists(changelogPath)
                ? File.ReadAllText(changelogPath)
                : "CKAN Linux changelog not found.";

            var sb = new StringBuilder();
            sb.AppendLine("# CKAN Linux Review Context");
            sb.AppendLine();
            sb.AppendLine("## Current Scope");
            sb.AppendLine("- Linux-first Avalonia shell over existing CKAN Core");
            sb.AppendLine("- Stage 0: foundations complete");
            sb.AppendLine("- Stage 1: instance startup and selection complete");
            sb.AppendLine("- Stage 2: mod browser, details pane, queue preview/apply flow, and screenshot regression tests complete");
            sb.AppendLine("- Current increment: details-pane grouping, preview readability, and post-apply reporting on top of scanability and toolbar compaction");
            sb.AppendLine();
            sb.AppendLine("## What Is Implemented");
            sb.AppendLine("- instance loading and switching");
            sb.AppendLine("- searchable mod list");
            sb.AppendLine("- visible common filter toggles");
            sb.AppendLine("- advanced filters for author, compatibility, and replacement-only browsing");
            sb.AppendLine("- visible browser sorting for name, author, installed-first, and updates-first");
            sb.AppendLine("- details pane with metadata and status badges");
            sb.AppendLine("- details pane grouped into overview, install state, package details, and relationships");
            sb.AppendLine("- pending queue UI in a full-width bottom section with derived preview, preflight summary, download/setup hints, and post-apply result reporting");
            sb.AppendLine("- denser mod rows with a primary state badge and dedicated version/compatibility column");
            sb.AppendLine("- first real apply execution path for uninstall/install/upgrade");
            sb.AppendLine();
            sb.AppendLine("## Not Implemented Yet");
            sb.AppendLine("- richer post-apply result reporting beyond the status line");
            sb.AppendLine("- advanced filter UX beyond the current inline panel");
            sb.AppendLine("- richer search semantics beyond substring matching");
            sb.AppendLine("- legacy `Newly compatible` repo-delta filter");
            sb.AppendLine();
            sb.AppendLine("## Available Screenshots");
            foreach (var file in screenshotFiles)
            {
                sb.AppendLine($"- `{Path.GetFileName(file)}`");
            }
            sb.AppendLine();
            sb.AppendLine("## Changelog Snapshot");
            sb.AppendLine("```md");
            sb.AppendLine(changelogSnippet.TrimEnd());
            sb.AppendLine("```");
            return sb.ToString();
        }

        private static string BuildWorkReadme(System.Collections.Generic.IReadOnlyList<string> screenshotFiles,
                                              System.Collections.Generic.IReadOnlyList<string> codeFiles)
        {
            var sb = new StringBuilder();
            string codeRoot = Path.Combine(FindRepoRoot(), ".gemini-review", "work", "code");
            sb.AppendLine("# Gemini Work Bundle");
            sb.AppendLine();
            sb.AppendLine("This folder is auto-generated for implementation-oriented Gemini prompts.");
            sb.AppendLine("Use it when you want Gemini to propose concrete Avalonia/XAML/C# edits instead of review-only feedback.");
            sb.AppendLine();
            sb.AppendLine("Files:");
            sb.AppendLine("- `GEMINI-WORK-PROMPT.md`: the implementation prompt");
            sb.AppendLine("- `CONTEXT.md`: current UI goals and constraints");
            sb.AppendLine("- `screenshots/`: high-signal screenshots for the current browser layout");
            sb.AppendLine("- `code/`: focused source snapshots for Gemini to edit against");
            sb.AppendLine("- `latest.md`: latest Gemini implementation output");
            sb.AppendLine();
            sb.AppendLine("Suggested run:");
            sb.AppendLine("- `python3 scripts/gemini_ui_work.py --force`");
            sb.AppendLine();
            sb.AppendLine("Bundled screenshots:");
            foreach (var file in screenshotFiles)
            {
                sb.AppendLine($"- `{Path.GetFileName(file)}`");
            }
            sb.AppendLine();
            sb.AppendLine("Bundled code files:");
            foreach (var file in codeFiles)
            {
                sb.AppendLine($"- `{Path.GetRelativePath(codeRoot, file)}`");
            }
            return sb.ToString();
        }

        private static string BuildWorkPrompt(System.Collections.Generic.IReadOnlyList<string> screenshotFiles,
                                              System.Collections.Generic.IReadOnlyList<string> codeFiles)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are proposing concrete code changes for a Linux desktop app built with Avalonia.");
            sb.AppendLine();
            sb.AppendLine("Primary goals for this pass:");
            sb.AppendLine("1. Make the mod browser feel calmer and easier to scan.");
            sb.AppendLine("2. Reduce the visual noise of the search/filter/sort toolbar while keeping common filters visible.");
            sb.AppendLine("3. Make the bottom queue and preview/preflight area easier to parse at a glance.");
            sb.AppendLine("4. Keep the right-side details pane and bottom queue; do not redesign this into a tabbed or web-style app.");
            sb.AppendLine("5. Preserve desktop density, but improve spacing, grouping, and polish.");
            sb.AppendLine();
            sb.AppendLine("Constraints:");
            sb.AppendLine("- Desktop Linux app, not a web UI");
            sb.AppendLine("- Common filters must remain visible");
            sb.AppendLine("- The bottom queue stays part of the main screen");
            sb.AppendLine("- Prefer editing only the bundled files unless absolutely necessary");
            sb.AppendLine();
            sb.AppendLine("Return format:");
            sb.AppendLine("- Short rationale");
            sb.AppendLine("- File-by-file change plan");
            sb.AppendLine("- Exact replacement XAML/C# blocks for each file you want changed");
            sb.AppendLine("- Keep the output implementation-oriented, not generic design advice");
            sb.AppendLine();
            sb.AppendLine("Bundled screenshots:");
            foreach (var file in screenshotFiles)
            {
                sb.AppendLine($"- {Path.GetFileName(file)}");
            }
            sb.AppendLine();
            sb.AppendLine("Bundled code files:");
            foreach (var file in codeFiles)
            {
                sb.AppendLine($"- {Path.GetFileName(file)}");
            }
            return sb.ToString();
        }

        private static string BuildWorkContext(string repoRoot,
                                               System.Collections.Generic.IReadOnlyList<string> screenshotFiles,
                                               System.Collections.Generic.IReadOnlyList<string> codeFiles)
        {
            string changelogPath = Path.Combine(repoRoot, "CKAN-LINUX-CHANGELOG.md");
            string codeRoot = Path.Combine(repoRoot, ".gemini-review", "work", "code");
            string changelogSnippet = File.Exists(changelogPath)
                ? File.ReadAllText(changelogPath)
                : "CKAN Linux changelog not found.";

            var sb = new StringBuilder();
            sb.AppendLine("# CKAN Linux Implementation Context");
            sb.AppendLine();
            sb.AppendLine("## UI Direction");
            sb.AppendLine("- Linux-first Avalonia shell over existing CKAN Core");
            sb.AppendLine("- Main browse flow is already present and functional");
            sb.AppendLine("- Current work is refinement and visual hierarchy, not shell scaffolding");
            sb.AppendLine();
            sb.AppendLine("## What To Improve");
            sb.AppendLine("- make the toolbar less loud");
            sb.AppendLine("- improve mod row scanability");
            sb.AppendLine("- clarify the queue / preview of changes section");
            sb.AppendLine("- keep the browser as the hero surface");
            sb.AppendLine("- preserve visible filters and right-side details");
            sb.AppendLine();
            sb.AppendLine("## Bundled Screenshots");
            foreach (var file in screenshotFiles)
            {
                sb.AppendLine($"- `{Path.GetFileName(file)}`");
            }
            sb.AppendLine();
            sb.AppendLine("## Bundled Code Files");
            foreach (var file in codeFiles)
            {
                sb.AppendLine($"- `{Path.GetRelativePath(codeRoot, file)}`");
            }
            sb.AppendLine();
            sb.AppendLine("## Changelog Snapshot");
            sb.AppendLine("```md");
            sb.AppendLine(changelogSnippet.TrimEnd());
            sb.AppendLine("```");
            return sb.ToString();
        }
    }
}
