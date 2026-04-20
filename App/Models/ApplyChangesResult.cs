namespace CKAN.App.Models
{
    public sealed class ApplyChangesResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = "";
    }
}
