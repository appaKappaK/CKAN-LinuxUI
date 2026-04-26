using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;
using CKAN.App.Services;

namespace CKAN.LinuxGUI.VisualTests
{
    internal sealed class FakeModActionService : IModActionService
    {
        private readonly IChangesetService changesetService;
        private readonly ApplyChangesResult applyResult;
        private readonly int applyDelayMs;
        private readonly string[] updateRecommendations;
        private readonly string[] updateSupporters;

        public FakeModActionService(IChangesetService changesetService,
                                    ApplyChangesResult? applyResult = null,
                                    int applyDelayMs = 0,
                                    string[]? updateRecommendations = null,
                                    string[]? updateSupporters = null)
        {
            this.changesetService = changesetService;
            this.applyResult = applyResult ?? new ApplyChangesResult
            {
                Kind = ApplyResultKind.Error,
                Success = false,
                Title = "Apply Failed",
                Message = "Fake visual test apply result.",
            };
            this.applyDelayMs = applyDelayMs;
            this.updateRecommendations = updateRecommendations ?? new[] { "Module Manager recommended by restock" };
            this.updateSupporters = updateSupporters ?? System.Array.Empty<string>();
        }

        public Task<ChangesetPreviewModel> PreviewChangesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var queue = changesetService.CurrentApplyQueue;
            if (queue.Count == 0)
            {
                return Task.FromResult(new ChangesetPreviewModel
                {
                    SummaryText = "Queue install, update, or remove actions to build a preview.",
                    CanApply    = false,
                });
            }

            var dependencyInstalls = queue
                .Where(action => action.ActionKind == QueuedActionKind.Install
                              || action.ActionKind == QueuedActionKind.Update)
                .Select(action => $"Harmony ({action.Identifier}-dep 2.3.3)")
                .Distinct()
                .ToList();

            var recommendations = queue.Any(action => action.ActionKind == QueuedActionKind.Update)
                ? updateRecommendations
                : System.Array.Empty<string>();
            var supporters = queue.Any(action => action.ActionKind == QueuedActionKind.Update)
                ? updateSupporters
                : System.Array.Empty<string>();
            var downloadsRequired = queue
                .Where(action => action.ActionKind != QueuedActionKind.Remove)
                .Select(action => $"{action.Name} will download from GitHub (32 MiB)")
                .Distinct()
                .ToList();

            return Task.FromResult(new ChangesetPreviewModel
            {
                SummaryText = $"{queue.Count} requested action{(queue.Count == 1 ? "" : "s")} • {downloadsRequired.Count} download{(downloadsRequired.Count == 1 ? "" : "s")} required • {dependencyInstalls.Count} dependency install{(dependencyInstalls.Count == 1 ? "" : "s")}",
                CanApply           = true,
                DownloadsRequired  = downloadsRequired,
                DependencyInstalls = dependencyInstalls,
                AutoRemovals       = System.Array.Empty<string>(),
                AttentionNotes     = queue.Any(action => action.ActionKind == QueuedActionKind.Update)
                    ? new[] { "A provider choice may be required for one virtual dependency during apply." }
                    : System.Array.Empty<string>(),
                Recommendations    = recommendations,
                Suggestions        = System.Array.Empty<string>(),
                Supporters         = supporters,
                Conflicts          = System.Array.Empty<string>(),
            });
        }

        public Task<ApplyChangesResult> ApplyChangesAsync(CancellationToken cancellationToken)
            => CompleteApplyAsync(cancellationToken);

        private async Task<ApplyChangesResult> CompleteApplyAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (applyDelayMs > 0)
            {
                await Task.Delay(applyDelayMs, cancellationToken);
            }
            if (applyResult.Success)
            {
                changesetService.ClearApplyQueue();
            }
            return applyResult;
        }

        public Task<ApplyChangesResult> InstallNowAsync(ModListItem mod,
                                                        CancellationToken cancellationToken,
                                                        string? targetVersion = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ApplyChangesResult
            {
                Kind = ApplyResultKind.Success,
                Success = true,
                Title = "Installed",
                Message = string.IsNullOrWhiteSpace(targetVersion)
                    ? $"Installed {mod.Name}."
                    : $"Installed {mod.Name} {targetVersion}.",
                SummaryLines = new[]
                {
                    "1 direct install",
                },
            });
        }

        public Task<ApplyChangesResult> RemoveNowAsync(ModListItem mod,
                                                       CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ApplyChangesResult
            {
                Kind = ApplyResultKind.Success,
                Success = true,
                Title = "Removed",
                Message = $"Removed {mod.Name}.",
                SummaryLines = new[]
                {
                    "1 direct removal",
                },
            });
        }

        public Task<ApplyChangesResult> DownloadQueuedAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (applyResult.Success)
            {
                changesetService.ClearDownloadQueue();
            }
            return Task.FromResult(new ApplyChangesResult
            {
                Kind = ApplyResultKind.Success,
                Success = true,
                Title = "Downloads Completed",
                Message = "Downloaded queued items for later install.",
            });
        }
    }
}
