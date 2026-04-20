namespace CKAN.App.Models
{
    public enum ApplyResultKind
    {
        None,
        Success,
        Warning,
        Blocked,
        Error,
        Canceled,
    }

    public sealed class ApplyChangesResult
    {
        public ApplyResultKind Kind { get; init; }

        public bool Success { get; init; }

        public string Title { get; init; } = "";

        public string Message { get; init; } = "";

        public System.Collections.Generic.IReadOnlyList<string> SummaryLines { get; init; }
            = System.Array.Empty<string>();

        public System.Collections.Generic.IReadOnlyList<string> FollowUpLines { get; init; }
            = System.Array.Empty<string>();
    }
}
