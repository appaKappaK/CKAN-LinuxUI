using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using ChinhDo.Transactions;
using log4net;

using CKAN.IO;
using CKAN.Extensions;

namespace CKAN
{
    public static class Utilities
    {
        public static readonly string[] AvailableLanguages =
        {
            "en-GB",
            "en-US",
            "de-DE",
            "zh-CN",
            "fr-FR",
            "pt-BR",
            "ru-RU",
            "ja-JP",
            "ko-KR",
            "pl-PL",
            "tr-TR",
            "it-IT",
            "nl-NL",
        };

        /// <summary>
        /// Call a function and take a default action if it throws an exception
        /// </summary>
        /// <typeparam name="T">Return type of the function</typeparam>
        /// <param name="func">Function to call</param>
        /// <param name="onThrow">Function to call if an exception is thrown</param>
        /// <returns>Return value of the function</returns>
        public static T? DefaultIfThrows<T>(Func<T?>             func,
                                            Func<Exception, T?>? onThrow = null) where T : class
        {
            try
            {
                return func();
            }
            catch (Exception exc)
            {
                return onThrow?.Invoke(exc) ?? default;
            }
        }

        /// <summary>
        /// Extract and rethrow the (first) inner exception if an aggregate exception is thrown
        /// </summary>
        /// <param name="func">Function to call</param>
        /// <returns>Return value of func, unless an exception is thrown</returns>
        public static T WithRethrowInner<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch (AggregateException agExc)
            {
                agExc.RethrowInner();
                throw;
            }
        }

        /// <summary>
        /// Copies a directory and its subdirectories as a transaction
        /// </summary>
        /// <param name="sourceDirPath">Source directory path</param>
        /// <param name="destDirPath">Destination directory path</param>
        /// <param name="fileRelPathsToIgnore">Files to skip</param>
        /// <param name="subFolderRelPathsToSymlink">Relative subdirs that should be symlinked to the originals instead of copied</param>
        /// <param name="subFolderRelPathsToLeaveEmpty">Relative subdirs that should be left empty</param>
        /// <param name="subFolderRelPathsToForbidHardlinks">Relative subdirs that should always have their files copied instead of hardlinked</param>
        public static void CopyDirectory(string   sourceDirPath,
                                         string   destDirPath,
                                         string[] fileRelPathsToIgnore,
                                         string[] subFolderRelPathsToSymlink,
                                         string[] subFolderRelPathsToLeaveEmpty,
                                         string[] subFolderRelPathsToForbidHardlinks)
        {
            using (var transaction = CkanTransaction.CreateTransactionScope())
            {
                CopyDirectory(sourceDirPath, destDirPath, new TxFileManager(),
                              fileRelPathsToIgnore,
                              subFolderRelPathsToSymlink,
                              subFolderRelPathsToLeaveEmpty,
                              subFolderRelPathsToForbidHardlinks);
                transaction.Complete();
            }
        }

        private static void CopyDirectory(string        sourceDirPath,
                                          string        destDirPath,
                                          TxFileManager txFileMgr,
                                          string[]      fileRelPathsToIgnore,
                                          string[]      subFolderRelPathsToSymlink,
                                          string[]      subFolderRelPathsToLeaveEmpty,
                                          string[]      subFolderRelPathsToForbidHardlinks,
                                          bool          allowHardLinks = true)
        {
            var sourceDir = new DirectoryInfo(sourceDirPath);
            if (!sourceDir.Exists)
            {
                throw new DirectoryNotFoundKraken(
                    sourceDirPath,
                    $"Source directory {sourceDirPath} does not exist or could not be found.");
            }

            // If the destination directory doesn't exist, create it
            if (!Directory.Exists(destDirPath))
            {
                txFileMgr.CreateDirectory(destDirPath);
            }
            else if (Directory.GetDirectories(destDirPath).Length != 0 || Directory.GetFiles(destDirPath).Length != 0)
            {
                throw new PathErrorKraken(destDirPath,
                                          string.Format(Properties.Resources.DirectoryNotEmpty,
                                                        destDirPath));
            }

            if (new DirectoryInfo(sourceDirPath).AncestorPathOf(new DirectoryInfo(destDirPath)))
            {
                throw new PathErrorKraken(destDirPath,
                                          string.Format(Properties.Resources.CannotCloneIntoSelf,
                                                        Platform.FormatPath(sourceDirPath)));
            }

            // Get the files in the directory and copy them to the new location
            foreach (var file in sourceDir.GetFiles())
            {
                if (fileRelPathsToIgnore.Contains(file.Name, Platform.PathComparer))
                {
                    continue;
                }
                if (allowHardLinks)
                {
                    InstalledFilesDeduplicator.CreateOrCopy(file,
                                                            Path.Combine(destDirPath, file.Name),
                                                            txFileMgr);
                }
                else
                {
                    txFileMgr.Copy(file.FullName, Path.Combine(destDirPath, file.Name), false);
                }
            }

            // Create all first level subdirectories
            foreach (var subdir in sourceDir.GetDirectories())
            {
                var temppath = Path.Combine(destDirPath, subdir.Name);
                // If already a sym link, replicate it in the new location
                if (DirectoryLink.TryGetTarget(subdir.FullName, out string? existingLinkTarget)
                    && existingLinkTarget is not null)
                {
                    DirectoryLink.Create(existingLinkTarget, temppath, txFileMgr);
                }
                else
                {
                    if (subFolderRelPathsToSymlink.Contains(subdir.Name, Platform.PathComparer))
                    {
                        DirectoryLink.Create(subdir.FullName, temppath, txFileMgr);
                    }
                    else
                    {
                        txFileMgr.CreateDirectory(temppath);

                        if (!subFolderRelPathsToLeaveEmpty.Contains(subdir.Name, Platform.PathComparer))
                        {
                            // Copy subdir contents to new location
                            CopyDirectory(subdir.FullName, temppath, txFileMgr,
                                          SubPaths(subdir.Name, fileRelPathsToIgnore).ToArray(),
                                          SubPaths(subdir.Name, subFolderRelPathsToSymlink).ToArray(),
                                          SubPaths(subdir.Name, subFolderRelPathsToLeaveEmpty).ToArray(),
                                          SubPaths(subdir.Name, subFolderRelPathsToForbidHardlinks).ToArray(),
                                          allowHardLinks && !subFolderRelPathsToForbidHardlinks.Contains(subdir.Name));
                        }
                    }
                }
            }
        }

        public static long DirectoryNonHardLinkableSize(DirectoryInfo where,
                                                        string[]      fileRelPathsToIgnore,
                                                        string[]      subFolderRelPathsToSymlink,
                                                        string[]      subFolderRelPathsToLeaveEmpty,
                                                        string[]      subFolderRelPathsToForbidHardlinks,
                                                        bool          allowHardLinks = true)
            => where.EnumerateFiles()
                    .Where(f => !fileRelPathsToIgnore.Contains(f.Name, Platform.PathComparer)
                                && !(allowHardLinks && InstalledFilesDeduplicator.UseHardLink(f)))
                    .Sum(f => f.Length)
             + where.EnumerateDirectories()
                    .Where(d => !subFolderRelPathsToLeaveEmpty.Contains(d.Name, Platform.PathComparer)
                                && !subFolderRelPathsToSymlink.Contains(d.Name, Platform.PathComparer))
                    .Sum(d => DirectoryNonHardLinkableSize(
                                  d,
                                  SubPaths(d.Name, fileRelPathsToIgnore).ToArray(),
                                  SubPaths(d.Name, subFolderRelPathsToSymlink).ToArray(),
                                  SubPaths(d.Name, subFolderRelPathsToLeaveEmpty).ToArray(),
                                  SubPaths(d.Name, subFolderRelPathsToForbidHardlinks).ToArray(),
                                  allowHardLinks && !subFolderRelPathsToForbidHardlinks.Contains(d.Name)));

        // Select only paths within subdir, prune prefixes
        private static IEnumerable<string> SubPaths(string parent, string[] paths)
            => paths.Where(p => p.StartsWith($"{parent}/", Platform.PathComparison))
                    .Select(p => p[(parent.Length + 1)..]);

        /// <summary>
        /// Launch a URL. For YEARS this was done by Process.Start in a
        /// cross-platform way, but Microsoft chose to break that,
        /// so now every .NET app has to write its own custom code for it,
        /// with special code for each platform.
        /// https://github.com/dotnet/corefx/issues/10361
        /// </summary>
        /// <param name="url">URL to launch</param>
        /// <returns>
        /// true if launched, false otherwise
        /// </returns>
        [ExcludeFromCodeCoverage]
        public static bool ProcessStartURL(string url)
        {
            try
            {
                if (Platform.IsMac)
                {
                    return StartProcess("open", url);
                }
                else if (Platform.IsUnix)
                {
                    return Directory.Exists(url)
                        ? StartUnixDefaultFileManager(url)
                        : StartProcess("xdg-open", url);
                }
                else
                {
                    // Try the old way
                    Process.Start(new ProcessStartInfo(url)
                    {
                        UseShellExecute = true,
                        Verb            = "open"
                    });
                    return true;
                }
            }
            catch (Exception exc)
            {
                log.Error($"Exception for URL {url}", exc);
            }
            return false;
        }

        [ExcludeFromCodeCoverage]
        public static void OpenFileBrowser(string location)
        {
            // We need the folder of the file
            // Otherwise the OS would try to open the file in its default application
            if (DirPath(location) is string path)
            {
                OpenDirectory(path);
            }
        }

        [ExcludeFromCodeCoverage]
        private static bool OpenDirectory(string path)
            => Platform.IsMac
                ? StartProcess("open", path)
             : Platform.IsUnix
                ? StartUnixDefaultFileManager(path)
             : ProcessStartURL(path);

        [ExcludeFromCodeCoverage]
        private static bool StartUnixDefaultFileManager(string path)
            => StartKdeDefaultFileManager(path)
               || StartDesktopFile(QueryDefaultDirectoryDesktopFile(), path)
               || StartProcess("gio", "open", path)
               || StartProcess("xdg-open", path);

        [ExcludeFromCodeCoverage]
        private static bool StartKdeDefaultFileManager(string path)
            => IsKdeDesktop()
               && (StartIfCommandExists("kioclient6", "exec", path)
                   || StartIfCommandExists("kioclient5", "exec", path)
                   || StartIfCommandExists("kde-open6", path)
                   || StartIfCommandExists("kde-open5", path));

        private static bool IsKdeDesktop()
            => (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "")
                   .Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)
                   .Any(desktop => string.Equals(desktop, "KDE", StringComparison.OrdinalIgnoreCase))
               || string.Equals(Environment.GetEnvironmentVariable("KDE_FULL_SESSION"),
                                "true",
                                StringComparison.OrdinalIgnoreCase);

        [ExcludeFromCodeCoverage]
        private static string? QueryDefaultDirectoryDesktopFile()
            => DesktopFilePath(
                RunAndReadFirstLine("xdg-mime", "query", "default", "inode/directory"));

        private static string? DesktopFilePath(string? desktopId)
        {
            if (string.IsNullOrWhiteSpace(desktopId))
            {
                return null;
            }

            foreach (var applicationsDir in XdgApplicationsDirs())
            {
                var direct = Path.Combine(applicationsDir, desktopId);
                if (File.Exists(direct))
                {
                    return direct;
                }
            }
            return null;
        }

        private static IEnumerable<string> XdgApplicationsDirs()
        {
            var home = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            yield return Path.Combine(
                string.IsNullOrWhiteSpace(home)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
                    : home,
                "applications");

            var dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
            foreach (var dir in (string.IsNullOrWhiteSpace(dataDirs) ? "/usr/local/share:/usr/share" : dataDirs)
                     .Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return Path.Combine(dir, "applications");
            }
        }

        [ExcludeFromCodeCoverage]
        private static bool StartDesktopFile(string? desktopFile,
                                             string  path)
        {
            if (desktopFile == null
                || ReadDesktopExec(desktopFile) is not string execLine)
            {
                return false;
            }

            var args = ExpandDesktopExec(execLine, path);
            if (args.Count == 0)
            {
                return false;
            }

            return StartProcess(args[0], args.Skip(1));
        }

        private static string? ReadDesktopExec(string desktopFile)
        {
            try
            {
                foreach (var line in File.ReadLines(desktopFile))
                {
                    if (line.StartsWith("Exec=", StringComparison.Ordinal))
                    {
                        return line.Substring("Exec=".Length);
                    }
                }
            }
            catch (Exception exc)
            {
                log.Debug($"Failed to read desktop file {desktopFile}", exc);
            }
            return null;
        }

        private static List<string> ExpandDesktopExec(string execLine,
                                                      string path)
        {
            var tokens = SplitDesktopExec(execLine);
            var expanded = new List<string>();
            var usedPath = false;
            var uri = new Uri(Path.GetFullPath(path)).AbsoluteUri;

            foreach (var token in tokens)
            {
                if (token is "%f" or "%F")
                {
                    expanded.Add(path);
                    usedPath = true;
                }
                else if (token is "%u" or "%U")
                {
                    expanded.Add(uri);
                    usedPath = true;
                }
                else if (token.StartsWith("%", StringComparison.Ordinal))
                {
                    // Desktop Entry field codes such as %i, %c, and %k are not needed
                    // to launch a file manager for one directory.
                    continue;
                }
                else
                {
                    var replaced = token.Replace("%f", path)
                                        .Replace("%F", path)
                                        .Replace("%u", uri)
                                        .Replace("%U", uri);
                    usedPath |= !string.Equals(replaced, token, StringComparison.Ordinal);
                    expanded.Add(replaced);
                }
            }

            if (expanded.Count > 0 && !usedPath)
            {
                expanded.Add(path);
            }

            return expanded;
        }

        private static List<string> SplitDesktopExec(string execLine)
        {
            var tokens = new List<string>();
            var current = "";
            var quoted = false;
            var escaped = false;

            foreach (var ch in execLine)
            {
                if (escaped)
                {
                    current += ch;
                    escaped = false;
                }
                else if (ch == '\\')
                {
                    escaped = true;
                }
                else if (ch == '"')
                {
                    quoted = !quoted;
                }
                else if (char.IsWhiteSpace(ch) && !quoted)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current);
                        current = "";
                    }
                }
                else
                {
                    current += ch;
                }
            }

            if (current.Length > 0)
            {
                tokens.Add(current);
            }

            return tokens;
        }

        [ExcludeFromCodeCoverage]
        private static string? RunAndReadFirstLine(string command,
                                                   params string[] args)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = BuildProcessStartInfo(command, args)
                };
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                var line = process.StandardOutput.ReadLine();
                process.WaitForExit(2000);
                return line;
            }
            catch (Exception exc)
            {
                log.Debug($"Failed to run {command}", exc);
                return null;
            }
        }

        [ExcludeFromCodeCoverage]
        private static bool StartProcess(string command,
                                         params string[] args)
            => StartProcess(command, args.AsEnumerable());

        [ExcludeFromCodeCoverage]
        private static bool StartIfCommandExists(string command,
                                                 params string[] args)
            => CommandExists(command) && StartProcess(command, args);

        private static bool CommandExists(string command)
            => Path.IsPathRooted(command)
                ? File.Exists(command)
                : (Environment.GetEnvironmentVariable("PATH") ?? "")
                    .Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(dir => File.Exists(Path.Combine(dir, command)));

        [ExcludeFromCodeCoverage]
        private static bool StartProcess(string command,
                                         IEnumerable<string> args)
        {
            try
            {
                return Process.Start(BuildProcessStartInfo(command, args)) != null;
            }
            catch (Exception exc)
            {
                log.Error($"Exception starting {command}", exc);
                return false;
            }
        }

        private static ProcessStartInfo BuildProcessStartInfo(string              command,
                                                              IEnumerable<string> args)
        {
            var startInfo = new ProcessStartInfo(command)
            {
                UseShellExecute = false,
            };
#if NET5_0_OR_GREATER
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }
#else
            startInfo.Arguments = string.Join(" ", args.Select(QuoteProcessArgument));
#endif
            return startInfo;
        }

#if !NET5_0_OR_GREATER
        private static string QuoteProcessArgument(string arg)
        {
            if (arg.Length == 0)
            {
                return "\"\"";
            }
            if (!arg.Any(ch => char.IsWhiteSpace(ch) || ch == '"'))
            {
                return arg;
            }

            var quoted = new StringBuilder();
            quoted.Append('"');
            var backslashes = 0;
            foreach (var ch in arg)
            {
                if (ch == '\\')
                {
                    backslashes++;
                }
                else if (ch == '"')
                {
                    quoted.Append('\\', backslashes * 2 + 1);
                    quoted.Append(ch);
                    backslashes = 0;
                }
                else
                {
                    quoted.Append('\\', backslashes);
                    quoted.Append(ch);
                    backslashes = 0;
                }
            }

            quoted.Append('\\', backslashes * 2);
            quoted.Append('"');
            return quoted.ToString();
        }
#endif

        private static string? DirPath(string path)
            => Directory.Exists(path) ? path
             : File.Exists(path) && Path.GetDirectoryName(path) is string parent
                                 && Directory.Exists(parent)
                 ? parent
             : null;

        private static readonly ILog log = LogManager.GetLogger(typeof(Utilities));
    }
}
