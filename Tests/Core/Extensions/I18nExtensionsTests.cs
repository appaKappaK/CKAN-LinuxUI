using NUnit.Framework;

using CKAN;
using CKAN.Extensions;

namespace Tests.Core.Extensions
{
    [TestFixture]
    public class I18nExtensionsTests
    {
        [TestCase(ReleaseStatus.testing,     ExpectedResult = "Testing")]
        public string LocalizeName_WithLocalizedEnums_Works<T>(T val)
            where T : System.Enum
            => val.LocalizeName();

        [TestCase(ReleaseStatus.testing,     ExpectedResult = "Pre-releases for adventurous users")]
        public string LocalizeDescription_WithLocalizedEnums_Works<T>(T val)
            where T : System.Enum
            => val.LocalizeDescription();
    }
}
