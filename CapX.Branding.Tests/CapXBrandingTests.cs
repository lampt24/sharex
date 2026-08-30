using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class CapXBrandingTests
{
    public static void Run()
    {
        string root = FindRepositoryRoot();

        AssertContains(Path.Combine(root, "Directory.build.props"), "<Company>CapX Team</Company>");
        AssertContains(Path.Combine(root, "Directory.build.props"), "<Product>CapX</Product>");
        AssertContains(Path.Combine(root, "ShareX", "Program.cs"), "public const string AppName = \"CapX\"");
        AssertContains(Path.Combine(root, "ShareX", "ShareX.csproj"), "<AssemblyName>CapX</AssemblyName>");
        AssertContains(Path.Combine(root, "ShareX", "ShareX.csproj"), "<RootNamespace>ShareX</RootNamespace>");
        AssertDoesNotContain(Path.Combine(root, "ShareX", "ShareX.csproj"), "<RootNamespace>CapX</RootNamespace>");
        AssertContains(Path.Combine(root, "ShareX.NativeMessagingHost", "Program.cs"), "FileHelpers.GetAbsolutePath(\"CapX.exe\")");

        string aboutWindowPath = Path.Combine(root, "ShareX", "Presentation", "About", "AboutWindow.axaml");
        AssertContains(aboutWindowPath, "Text=\"CapX\"");
        AssertDoesNotContain(aboutWindowPath, "Text=\"ShareX\"");
        AssertDoesNotContain(Path.Combine(root, "ShareX", "Program.cs"), "\"ShareX - \" + Strings.Error");

        AssertEnglishResourceValuesUseCapX(root);

        string integrationHelpersPath = Path.Combine(root, "ShareX", "IntegrationHelpers.cs");
        AssertContains(integrationHelpersPath, "ShellCustomUploaderAssociateValue = \"CapX custom uploader\"");
        AssertContains(integrationHelpersPath, "ShellImageEffectAssociateValue = \"CapX image effect\"");
        AssertContains(integrationHelpersPath, "ShellCustomUploaderExtensionValue = \"ShareX.sxcu\"");
        AssertContains(integrationHelpersPath, "ShellImageEffectExtensionValue = \"ShareX.sxie\"");

        AssertContains(Path.Combine(root, "ShareX.Steam", "Helpers.cs"),
            "MessageBox.Show(e.ToString(), \"CapX - Error\"");
        string steamLauncherPath = Path.Combine(root, "ShareX.Steam", "Launcher.cs");
        AssertContains(steamLauncherPath,
            "\"CapX is currently running.\\r\\n\\r\\nPlease close CapX and press \\\"Retry\\\" button after it is closed.\"");
        AssertContains(steamLauncherPath, "\"CapX - Uninstaller\"");

        // Deferred Task 5 packaging contracts intentionally remain after Task 4 UI contracts.
        string setupProgramPath = Path.Combine(root, "ShareX.Setup", "Program.cs");
        string innoSetupPath = Path.Combine(root, "ShareX.Setup", "InnoSetup", "ShareX-setup.iss");
        string storeManifestPath = Path.Combine(root, "ShareX.Setup", "MicrosoftStore", "AppxManifest.xml");
        string chromeHostManifestPath = Path.Combine(root, "ShareX", "host-manifest-chrome.json");
        string firefoxHostManifestPath = Path.Combine(root, "ShareX", "host-manifest-firefox.json");

        AssertContains(innoSetupPath, "#define MyAppName \"CapX\"");
        AssertContains(storeManifestPath, "Executable=\"CapX.exe\"");
        AssertContains(storeManifestPath, "<DisplayName>CapX</DisplayName>");
        AssertContains(storeManifestPath, "Identity Name=\"19568ShareX.ShareX\"");

        AssertContains(setupProgramPath, "Path.Combine(BinDir, \"CapX.exe\")");
        AssertContains(setupProgramPath, "$\"CapX-{AppVersion}-setup-{Platform}.exe\"");
        AssertContains(setupProgramPath, "https://github.com/ShareX/FFmpeg/releases/");

        AssertContains(innoSetupPath, "Subkey: \"Software\\Classes\\.sxcu\"");
        AssertContains(innoSetupPath, "Subkey: \"Software\\Classes\\.sxie\"");
        AssertContains(chromeHostManifestPath, "\"name\": \"com.getsharex.sharex\"");
        AssertContains(firefoxHostManifestPath, "\"name\": \"ShareX\"");
        AssertContains(chromeHostManifestPath, "\"description\": \"CapX\"");
        AssertContains(firefoxHostManifestPath, "\"description\": \"CapX\"");
    }

    public static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ShareX.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root containing ShareX.sln.");
    }

    private static void AssertContains(string path, string expectedText)
    {
        if (!File.ReadAllText(path).Contains(expectedText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Branding contract failed for '{path}': expected text '{expectedText}'.");
        }
    }

    private static void AssertDoesNotContain(string path, string unexpectedText)
    {
        if (File.ReadAllText(path).Contains(unexpectedText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Branding contract failed for '{path}': unexpected text '{unexpectedText}'.");
        }
    }

    private static void AssertEnglishResourceValuesUseCapX(string root)
    {
        string[] relativePaths =
        {
            "ShareX/Localization/Strings.resx",
            "ShareX.HelpersLib/Localization/Strings.resx",
            "ShareX.ScreenCaptureLib/Localization/Strings.resx"
        };

        // Add compatibility values only when a visible standalone legacy token must remain.
        // Every entry must name one exact file, match one exact value or anchored pattern,
        // and explain the compatibility reason. There are currently no such English values.
        ResourceValueAllowlistEntry[] allowlist = Array.Empty<ResourceValueAllowlistEntry>();

        ValidateResourceAllowlist(allowlist, relativePaths);

        Regex standaloneLegacyName = new(@"(?<![A-Za-z0-9_])ShareX(?![A-Za-z0-9_])", RegexOptions.CultureInvariant);
        HashSet<ResourceValueAllowlistEntry> usedAllowlistEntries = new();
        List<string> failures = new();

        foreach (string relativePath in relativePaths)
        {
            string fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            XDocument document = XDocument.Load(fullPath, LoadOptions.PreserveWhitespace);

            foreach (string value in document.Descendants("value").Select(element => element.Value))
            {
                if (!standaloneLegacyName.IsMatch(value))
                {
                    continue;
                }

                ResourceValueAllowlistEntry? allowlistEntry = allowlist.FirstOrDefault(entry =>
                    entry.RelativePath == relativePath && entry.IsMatch(value));

                if (allowlistEntry is null)
                {
                    failures.Add($"{relativePath}: {value.Replace(Environment.NewLine, "\\n", StringComparison.Ordinal)}");
                }
                else
                {
                    usedAllowlistEntries.Add(allowlistEntry);
                }
            }
        }

        ResourceValueAllowlistEntry[] unusedEntries = allowlist.Except(usedAllowlistEntries).ToArray();
        if (unusedEntries.Length > 0)
        {
            failures.AddRange(unusedEntries.Select(entry =>
                $"Unused allowlist entry for {entry.RelativePath}: {entry.DescribeMatch()} ({entry.Reason})"));
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Branding contract failed: visible standalone ShareX resource values remain:\n" +
                string.Join("\n", failures));
        }
    }

    private static void ValidateResourceAllowlist(
        IEnumerable<ResourceValueAllowlistEntry> allowlist,
        IEnumerable<string> resourcePaths)
    {
        HashSet<string> knownPaths = new(resourcePaths, StringComparer.Ordinal);

        foreach (ResourceValueAllowlistEntry entry in allowlist)
        {
            if (!knownPaths.Contains(entry.RelativePath))
            {
                throw new InvalidOperationException($"Unknown resource allowlist path: {entry.RelativePath}");
            }

            bool hasExactValue = entry.ExactValue is not null;
            bool hasPattern = entry.AnchoredPattern is not null;
            if (hasExactValue == hasPattern)
            {
                throw new InvalidOperationException(
                    $"Allowlist entry for {entry.RelativePath} must specify exactly one exact value or anchored pattern.");
            }

            if (hasPattern &&
                (!entry.AnchoredPattern!.StartsWith('^') || !entry.AnchoredPattern.EndsWith('$')))
            {
                throw new InvalidOperationException(
                    $"Allowlist pattern for {entry.RelativePath} must be anchored: {entry.AnchoredPattern}");
            }

            if (string.IsNullOrWhiteSpace(entry.Reason))
            {
                throw new InvalidOperationException($"Allowlist entry for {entry.RelativePath} requires a reason.");
            }
        }
    }

    private sealed record ResourceValueAllowlistEntry(
        string RelativePath,
        string? ExactValue,
        string? AnchoredPattern,
        string Reason)
    {
        public bool IsMatch(string value) => ExactValue is not null
            ? string.Equals(ExactValue, value, StringComparison.Ordinal)
            : Regex.IsMatch(value, AnchoredPattern!, RegexOptions.CultureInvariant);

        public string DescribeMatch() => ExactValue is not null
            ? $"exact value '{ExactValue}'"
            : $"pattern '{AnchoredPattern}'";
    }
}
