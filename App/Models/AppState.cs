namespace CKAN.App.Models
{
    public sealed class AppState
    {
        public string? CurrentInstanceName { get; init; }

        public string StatusMessage { get; init; } = "";

        public int ProgressPercent { get; init; }

        public bool IsBusy { get; init; }
    }
}
