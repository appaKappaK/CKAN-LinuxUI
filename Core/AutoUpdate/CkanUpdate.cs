using CKAN.Versioning;

namespace CKAN
{
    /// <summary>
    /// Release metadata for the upstream CKAN core bundled by CKAN Linux.
    /// </summary>
    public abstract class CkanUpdate
    {
        public CkanModuleVersion? Version      { get; protected set; }
        public string?            ReleaseNotes { get; protected set; }
    }
}
