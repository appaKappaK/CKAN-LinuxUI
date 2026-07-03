using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;
using CKAN.App.Services;

namespace CKAN.LinuxGUI.VisualTests
{
    internal sealed class FakeDisabledModService : IDisabledModService
    {
        public DisabledModsSnapshot GetCurrentSnapshot()
            => new DisabledModsSnapshot();

        public Task<DisabledModOperationPreview> PreviewDisableAsync(string identifier,
                                                                      CancellationToken cancellationToken)
            => Task.FromResult(BlockedPreview);

        public Task<DisabledModOperationPreview> PreviewEnableAsync(string identifier,
                                                                     CancellationToken cancellationToken)
            => Task.FromResult(BlockedPreview);

        public Task<ApplyChangesResult> DisableAsync(string identifier,
                                                     CancellationToken cancellationToken)
            => Task.FromResult(BlockedResult);

        public Task<ApplyChangesResult> EnableAsync(string identifier,
                                                    CancellationToken cancellationToken)
            => Task.FromResult(BlockedResult);

        private static DisabledModOperationPreview BlockedPreview
            => new DisabledModOperationPreview
            {
                CanApply = false,
                Title    = "Disabled Mods Unavailable",
                Message  = "Disabled mod operations are not part of this visual scenario.",
            };

        private static ApplyChangesResult BlockedResult
            => new ApplyChangesResult
            {
                Kind    = ApplyResultKind.Blocked,
                Success = false,
                Title   = "Disabled Mods Unavailable",
                Message = "Disabled mod operations are not part of this visual scenario.",
            };
    }
}
