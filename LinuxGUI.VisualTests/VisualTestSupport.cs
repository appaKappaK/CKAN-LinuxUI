using System;
using System.IO;

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
    }
}
