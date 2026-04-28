using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Xml.Linq;

using CKAN.Extensions;
using CKAN.IO;

namespace CKAN.LinuxGUI
{
    internal static class GameCommandLineConfigStore
    {
        public static IReadOnlyList<string> Load(GameInstance instance,
                                                 SteamLibrary steamLibrary)
        {
            var defaults = instance.Game.DefaultCommandLines(steamLibrary,
                                                             new DirectoryInfo(instance.GameDir))
                                        .Where(line => !string.IsNullOrWhiteSpace(line))
                                        .Distinct()
                                        .ToList();

            if (TryLoadFromJson(instance, defaults) is { Count: > 0 } jsonLines)
            {
                return jsonLines;
            }

            if (TryLoadFromLegacyXml(instance, defaults) is { Count: > 0 } xmlLines)
            {
                return xmlLines;
            }

            return defaults;
        }

        public static void Save(GameInstance            instance,
                                IReadOnlyCollection<string> commandLines)
        {
            var configPath = JsonConfigPath(instance);
            JsonObject root;

            if (File.Exists(configPath))
            {
                try
                {
                    root = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject
                           ?? new JsonObject();
                }
                catch
                {
                    root = new JsonObject();
                }
            }
            else
            {
                root = new JsonObject();
            }

            root["CommandLines"] = new JsonArray(commandLines.Select(line => (JsonNode?)line)
                                                            .ToArray());

            root.ToJsonString(new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
            })
                .WriteThroughTo(configPath);
        }

        private static List<string>? TryLoadFromJson(GameInstance  instance,
                                                     List<string> defaults)
        {
            var path = JsonConfigPath(instance);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
                if (root == null)
                {
                    return null;
                }

                var commandLines = root["CommandLines"]?.AsArray()
                                              .Select(node => node?.GetValue<string>())
                                              .OfType<string>()
                                              .Where(line => !string.IsNullOrWhiteSpace(line))
                                              .ToList()
                                   ?? new List<string>();

                if (commandLines.Count > 0)
                {
                    return commandLines;
                }

                if (root["CommandLineArguments"]?.GetValue<string>() is string singleLine
                    && !string.IsNullOrWhiteSpace(singleLine))
                {
                    return new[] { singleLine }
                        .Concat(defaults)
                        .Distinct()
                        .ToList();
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static List<string>? TryLoadFromLegacyXml(GameInstance  instance,
                                                          List<string> defaults)
        {
            var path = LegacyXmlConfigPath(instance);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var doc = XDocument.Load(path);
                var root = doc.Element("Configuration");
                if (root == null)
                {
                    return null;
                }

                var commandLines = root.Element("CommandLines")?
                                       .Elements("CommandLine")
                                       .Select(el => el.Value)
                                       .Where(line => !string.IsNullOrWhiteSpace(line))
                                       .ToList()
                                   ?? new List<string>();

                var singleLine = root.Element("CommandLineArguments")?.Value;
                if (!string.IsNullOrWhiteSpace(singleLine))
                {
                    commandLines = new[] { singleLine }
                        .Concat(commandLines)
                        .Concat(defaults)
                        .Distinct()
                        .ToList();
                }
                else if (commandLines.Count == 0)
                {
                    commandLines = defaults;
                }

                return commandLines;
            }
            catch
            {
                return null;
            }
        }

        private static string JsonConfigPath(GameInstance instance)
            => Path.Combine(instance.CkanDir, "GUIConfig.json");

        private static string LegacyXmlConfigPath(GameInstance instance)
            => Path.Combine(instance.CkanDir, "GUIConfig.xml");
    }
}
