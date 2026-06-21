using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using NUnit.Framework;

using CKAN;
using CKAN.Games;
using CKAN.Games.KerbalSpaceProgram;
using CKAN.Games.KerbalSpaceProgram2;
using CKAN.IO;
using Tests.Data;

namespace Tests.Core.IO
{
    [TestFixture]
    public sealed class ModuleImporterTests
    {
        [TestCaseSource(nameof(ImportableArguments))]
        public void ImportFiles_InternalCkanFile_Works(string zipPath, IGame game)
        {
            // Arrange
            var user = new CapturingUser(true, q => true, (msg, objs) => 0);
            using (var inst     = new DisposableKSP("disposable", game))
            using (var repoData = new TemporaryRepositoryData(user))
            using (var cacheDir = new TemporaryDirectory())
            using (var cache    = new NetModuleCache(cacheDir))
            {
                var registry  = CKAN.Registry.Empty(repoData.Manager);
                var files     = new HashSet<FileInfo> { new FileInfo(zipPath) };
                var toInstall = new List<CkanModule>();

                // Act
                var result = ModuleImporter.ImportFiles(files, user, toInstall.Add,
                                                        registry, inst.KSP, cache, false);

                // Assert
                Assert.IsTrue(result);
                CollectionAssert.IsEmpty(user.RaisedErrors);
                Assert.AreEqual(1, cacheDir.Directory.EnumerateFiles("*").Count());
                Assert.AreEqual(1, toInstall.Count);
            }
        }

        [Test]
        public void ImportFiles_InternalCkanFile_DoesNotPromptToDeleteUntrackedZip()
        {
            // Arrange
            var user = new CapturingUser(true, q => true, (msg, objs) => 0);
            using (var inst     = new DisposableKSP())
            using (var repoData = new TemporaryRepositoryData(user))
            using (var cacheDir = new TemporaryDirectory())
            using (var cache    = new NetModuleCache(cacheDir))
            {
                var registry  = CKAN.Registry.Empty(repoData.Manager);
                var files     = new HashSet<FileInfo> { new FileInfo(TestData.DogeCoinFlagImportableZip()) };
                var toInstall = new List<CkanModule>();

                // Act
                var result = ModuleImporter.ImportFiles(files, user, toInstall.Add,
                                                        registry, inst.KSP, cache);

                // Assert
                Assert.IsTrue(result);
                CollectionAssert.DoesNotContain(user.RaisedYesNoDialogQuestions,
                    string.Format(CKAN.Properties.Resources.ModuleInstallerImportDeletePrompt, 1));
                CollectionAssert.Contains(user.RaisedYesNoDialogQuestions,
                    string.Format(CKAN.Properties.Resources.ModuleInstallerImportInstallPrompt,
                                  1, inst.KSP.Name, inst.KSP.GameDir));
            }
        }

        [Test]
        public void ImportFiles_CachedFileWrongSize_ReplacesCacheEntry()
        {
            // Arrange
            var user = new CapturingUser(true, q => true, (msg, objs) => 0);
            using (var inst     = new DisposableKSP())
            using (var repoData = new TemporaryRepositoryData(user))
            using (var cacheDir = new TemporaryDirectory())
            using (var cache    = new NetModuleCache(cacheDir))
            {
                var registry        = CKAN.Registry.Empty(repoData.Manager);
                var importableZip   = TestData.DogeCoinFlagImportableZip();
                var importableInfo  = new FileInfo(importableZip);
                var files           = new HashSet<FileInfo> { importableInfo };
                var firstToInstall  = new List<CkanModule>();

                Assert.IsTrue(ModuleImporter.ImportFiles(files, user, firstToInstall.Add,
                                                         registry, inst.KSP, cache, false));
                Assert.That(firstToInstall, Has.Count.EqualTo(1));

                var module     = firstToInstall.Single();
                var cachedPath = cache.GetCachedFilename(module);
                Assert.That(cachedPath, Is.Not.Null);

                File.WriteAllBytes(cachedPath!, File.ReadAllBytes(importableZip).Take(64).ToArray());
                Assert.That(new FileInfo(cachedPath!).Length, Is.Not.EqualTo(importableInfo.Length));

                user.RaisedMessages.Clear();
                user.RaisedYesNoDialogQuestions.Clear();
                var secondToInstall = new List<CkanModule>();

                // Act
                var result = ModuleImporter.ImportFiles(files, user, secondToInstall.Add,
                                                        registry, inst.KSP, cache, false);

                // Assert
                Assert.IsTrue(result);
                Assert.That(new FileInfo(cachedPath!).Length, Is.EqualTo(importableInfo.Length));
                Assert.That(user.RaisedMessages.Any(msg => msg.StartsWith(
                                string.Format(CKAN.Properties.Resources.ModuleInstallerImportAlreadyCached,
                                              ""), StringComparison.Ordinal)),
                            Is.False);
            }
        }

        private static IEnumerable<TestCaseData> ImportableArguments()
        {
            yield return new TestCaseData(TestData.DogeCoinFlagImportableZip(),   new KerbalSpaceProgram());
            yield return new TestCaseData(TestData.BurnControllerImportableZip(), new KerbalSpaceProgram2());
        }
    }
}
