using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
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

        AssertTask5Packaging(root);
        AssertTask6CompatibilityDocumentation(root);
    }

    private static void AssertTask5Packaging(string root)
    {
        string setupProgramPath = Path.Combine(root, "ShareX.Setup", "Program.cs");
        string innoSetupPath = Path.Combine(root, "ShareX.Setup", "InnoSetup", "ShareX-setup.iss");
        string storeManifestPath = Path.Combine(root, "ShareX.Setup", "MicrosoftStore", "AppxManifest.xml");
        string chromeHostManifestPath = Path.Combine(root, "ShareX", "host-manifest-chrome.json");
        string firefoxHostManifestPath = Path.Combine(root, "ShareX", "host-manifest-firefox.json");
        string steamProjectPath = Path.Combine(root, "ShareX.Steam", "ShareX.Steam.csproj");
        string steamInstallScriptPath = Path.Combine(root, "ShareX.Steam", "installscript.vdf");
        string steamHelpersPath = Path.Combine(root, "ShareX.Steam", "Helpers.cs");
        string steamLauncherPath = Path.Combine(root, "ShareX.Steam", "Launcher.cs");

        AssertContains(innoSetupPath, "#define MyAppName \"CapX\"");
        AssertContains(innoSetupPath, "#define MyAppFileName \"CapX.exe\"");
        AssertContains(innoSetupPath, "#define MyAppPublisher \"CapX Team\"");
        AssertContains(innoSetupPath, "AppCopyright=Copyright (c) 2007-2026 CapX Team");
        AssertContains(innoSetupPath, "OutputBaseFilename={#MyAppName}-{#MyAppVersion}-setup-{#Platform}");
        AssertContains(innoSetupPath, "Description: \"Show \"\"Upload with CapX\"\" button in Windows Explorer context menu\"");
        AssertContains(innoSetupPath, "Description: \"Run CapX when Windows starts\"");
        AssertContains(innoSetupPath, "DefaultGroupName={#MyAppName}");
        AssertContains(innoSetupPath, "UsePreviousGroup=no");

        AssertContains(innoSetupPath, "Type: files; Name: \"{app}\\ShareX.exe\"");
        AssertContains(innoSetupPath, "Type: files; Name: \"{userdesktop}\\ShareX.lnk\"");
        AssertContains(innoSetupPath, "Type: files; Name: \"{usersendto}\\ShareX.lnk\"");
        AssertContains(innoSetupPath, "Type: files; Name: \"{userstartup}\\ShareX.lnk\"");
        AssertContains(innoSetupPath, "Type: files; Name: \"{userprograms}\\ShareX\\ShareX.lnk\"");
        AssertContains(innoSetupPath, "Type: files; Name: \"{userprograms}\\ShareX\\Uninstall ShareX.lnk\"");
        AssertContains(innoSetupPath, "Type: dirifempty; Name: \"{userprograms}\\ShareX\"");
        AssertContains(innoSetupPath, "Type: files; Name: \"{commonprograms}\\ShareX\\ShareX.lnk\"");
        AssertContains(innoSetupPath, "Type: files; Name: \"{commonprograms}\\ShareX\\Uninstall ShareX.lnk\"");
        AssertContains(innoSetupPath, "Type: dirifempty; Name: \"{commonprograms}\\ShareX\"");
        AssertDoesNotContain(innoSetupPath, "*ShareX*.lnk");
        AssertContains(innoSetupPath,
            "Subkey: \"Software\\Classes\\*\\shell\\ShareX\"; Flags: deletekey dontcreatekey");
        AssertContains(innoSetupPath,
            "Subkey: \"Software\\Classes\\Directory\\shell\\ShareX\"; Flags: deletekey dontcreatekey");

        AssertContains(innoSetupPath, "#define MyAppId \"82E6AC09-0FEF-4390-AD9F-0DD3F5561EFC\"");
        AssertContains(innoSetupPath, "AppId={#MyAppId}");
        AssertContains(innoSetupPath, "AppMutex={#MyAppId}");
        AssertContains(innoSetupPath, "Subkey: \"Software\\Classes\\.sxcu\"");
        AssertContains(innoSetupPath, "Subkey: \"Software\\Classes\\ShareX.sxcu\"");
        AssertContains(innoSetupPath, "Subkey: \"Software\\Classes\\.sxie\"");
        AssertContains(innoSetupPath, "Subkey: \"Software\\Classes\\ShareX.sxie\"");
        AssertContains(innoSetupPath, "SystemFileAssociations\\image\\shell\\ShareXImageEditor");
        AssertContains(innoSetupPath, "NativeMessagingHosts\\com.getsharex.sharex");
        AssertContains(innoSetupPath, "NativeMessagingHosts\\ShareX");
        AssertDoesNotContain(innoSetupPath, "Subkey: \"Software\\Classes\\ShareX.sxcu\"; Flags: deletekey");
        AssertDoesNotContain(innoSetupPath, "Subkey: \"Software\\Classes\\ShareX.sxie\"; Flags: deletekey");
        AssertDoesNotContain(innoSetupPath, "ShareXImageEditor\"; Flags: deletekey");

        AssertContains(setupProgramPath, "Path.Combine(BinDir, \"CapX.exe\")");
        AssertContains(setupProgramPath, "Path.Combine(OutputDir, \"CapX-portable\")");
        AssertContains(setupProgramPath, "Path.Combine(OutputDir, \"CapX-debug\")");
        AssertContains(setupProgramPath, "Path.Combine(OutputDir, \"CapX-Steam\")");
        AssertContains(setupProgramPath, "Path.Combine(OutputDir, \"CapX-MicrosoftStore\")");
        AssertContains(setupProgramPath, "Path.Combine(OutputDir, \"CapX-MicrosoftStore-debug\")");
        AssertContains(setupProgramPath, "$\"CapX-{AppVersion}-setup-{Platform}.exe\"");
        AssertContains(setupProgramPath, "$\"CapX-{AppVersion}-portable-{Platform}.zip\"");
        AssertContains(setupProgramPath, "$\"CapX-{AppVersion}-debug-{Platform}.zip\"");
        AssertContains(setupProgramPath, "$\"CapX-{AppVersion}-Steam-{Platform}.zip\"");
        AssertContains(setupProgramPath, "$\"CapX-{AppVersion}-MicrosoftStore-{Platform}.appx\"");
        AssertContains(setupProgramPath, "$\"CapX-{AppVersion}-MicrosoftStore-debug-{Platform}.appx\"");
        AssertContains(setupProgramPath, "Console.WriteLine(\"CapX setup started.\")");
        AssertContains(setupProgramPath, "Console.WriteLine(\"CapX setup successfully completed.\")");
        AssertContains(setupProgramPath, "https://github.com/ShareX/FFmpeg/releases/");
        AssertContains(setupProgramPath, "Path.Combine(ParentDir, \"ShareX.sln\")");
        AssertContains(setupProgramPath, "Path.Combine(ParentDir, \"ShareX\", \"bin\"");
        AssertContains(setupProgramPath, "\"ShareX_Launcher.exe\"");

        AssertStoreManifest(storeManifestPath);
        AssertNativeHostManifest(
            chromeHostManifestPath,
            "com.getsharex.sharex",
            "allowed_origins",
            "chrome-extension://nlkoigbdolhchiicbonbihbphgamnaoc/");
        AssertNativeHostManifest(
            firefoxHostManifestPath,
            "ShareX",
            "allowed_extensions",
            "firefox@getsharex.com");

        AssertContains(steamHelpersPath, "\"CapX - Error\"");
        AssertContains(steamLauncherPath, "Path.Combine(ContentFolderPath, \"CapX.exe\")");
        AssertContains(steamLauncherPath, "Path.Combine(UpdateFolderPath, \"CapX.exe\")");
        AssertContains(steamLauncherPath, "CapX is currently running.");
        AssertContains(steamLauncherPath, "\"CapX - Uninstaller\"");
        AssertDoesNotContain(steamLauncherPath, "Path.Combine(ContentFolderPath, \"ShareX.exe\")");
        AssertDoesNotContain(steamLauncherPath, "Path.Combine(UpdateFolderPath, \"ShareX.exe\")");
        AssertContains(steamProjectPath, "<AssemblyName>ShareX_Launcher</AssemblyName>");
        AssertContains(steamInstallScriptPath, "%INSTALLDIR%\\\\ShareX_Launcher.exe");
    }

    private static void AssertTask6CompatibilityDocumentation(string root)
    {
        string programPath = Path.Combine(root, "ShareX", "Program.cs");
        string innoSetupPath = Path.Combine(root, "ShareX.Setup", "InnoSetup", "ShareX-setup.iss");
        string setupProgramPath = Path.Combine(root, "ShareX.Setup", "Program.cs");
        string chromeHostManifestPath = Path.Combine(root, "ShareX", "host-manifest-chrome.json");
        string firefoxHostManifestPath = Path.Combine(root, "ShareX", "host-manifest-firefox.json");
        string storeManifestPath = Path.Combine(root, "ShareX.Setup", "MicrosoftStore", "AppxManifest.xml");
        string compatibilityDocumentPath = Path.Combine(root, "docs", "CapX-branding-compatibility.md");

        AssertContains(programPath, "private const string CompatibilityAppName = \"ShareX\"");
        AssertContains(innoSetupPath, "Subkey: \"Software\\Classes\\ShareX.sxcu\"");
        AssertContains(innoSetupPath, "Subkey: \"Software\\Classes\\ShareX.sxie\"");
        AssertContains(innoSetupPath, "SystemFileAssociations\\image\\shell\\ShareXImageEditor");
        AssertContains(setupProgramPath, "https://github.com/ShareX/FFmpeg/releases/");
        AssertContains(storeManifestPath, "Name=\"19568ShareX.ShareX\"");
        AssertContains(chromeHostManifestPath, "\"name\": \"com.getsharex.sharex\"");
        AssertContains(chromeHostManifestPath, "\"chrome-extension://nlkoigbdolhchiicbonbihbphgamnaoc/\"");
        AssertContains(firefoxHostManifestPath, "\"name\": \"ShareX\"");
        AssertContains(firefoxHostManifestPath, "\"firefox@getsharex.com\"");

        AssertFileExists(compatibilityDocumentPath);
        AssertContains(compatibilityDocumentPath, "User-facing product copy is CapX");
        string[] protectedCategories =
        {
            "Namespaces, project, and source names",
            "Settings and data directories",
            "Registry and file-association keys",
            "File extensions",
            "Store identity",
            "Native messaging IDs and origins",
            "Updater and FFmpeg URLs",
            "Serialized names",
            "Steam launcher executable",
            "Resource keys",
            "Theme keys",
            "Provenance and license headers"
        };

        foreach (string category in protectedCategories)
        {
            AssertContains(compatibilityDocumentPath, category);
        }
    }

    private static void AssertStoreManifest(string path)
    {
        XDocument document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        XElement package = document.Root ?? throw new InvalidOperationException($"Store manifest '{path}' has no root element.");
        XNamespace foundation = package.Name.Namespace;
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
        XNamespace uap3 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/3";
        XNamespace desktop = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";

        XElement identity = RequireSingle(package.Elements(foundation + "Identity"), "Store identity");
        AssertEqual("19568ShareX.ShareX", identity.Attribute("Name")?.Value, "Store identity name");
        AssertEqual("CN=366A5DE5-2EC7-43FD-B559-05986578C4CC", identity.Attribute("Publisher")?.Value,
            "Store publisher certificate");

        XElement properties = RequireSingle(package.Elements(foundation + "Properties"), "Store properties");
        AssertEqual("CapX", properties.Element(foundation + "DisplayName")?.Value, "Store display name");
        AssertEqual("CapX Team", properties.Element(foundation + "PublisherDisplayName")?.Value,
            "Store publisher display name");

        XElement application = RequireSingle(package.Descendants(foundation + "Application"), "Store application");
        AssertEqual("ShareX", application.Attribute("Id")?.Value, "Store application ID");

        string[] executableReferences = package.Descendants()
            .Attributes("Executable")
            .Select(attribute => attribute.Value)
            .ToArray();
        if (executableReferences.Length != 2 || executableReferences.Any(value => value != "CapX.exe"))
        {
            throw new InvalidOperationException(
                $"Branding contract failed for Store executable references: expected two 'CapX.exe' values, got [{string.Join(", ", executableReferences)}].");
        }

        XElement visualElements = RequireSingle(application.Elements(uap + "VisualElements"), "Store visual elements");
        AssertEqual("CapX", visualElements.Attribute("DisplayName")?.Value, "Store visual display name");
        AssertEqual("CapX", visualElements.Attribute("Description")?.Value, "Store visual description");

        XElement startupTask = RequireSingle(application.Descendants(desktop + "StartupTask"), "Store startup task");
        AssertEqual("ShareX", startupTask.Attribute("TaskId")?.Value, "Store startup TaskId");
        AssertEqual("CapX", startupTask.Attribute("DisplayName")?.Value, "Store startup display name");

        Dictionary<string, XElement> associations = application.Descendants(uap3 + "FileTypeAssociation")
            .ToDictionary(element => element.Attribute("Name")?.Value ?? string.Empty, StringComparer.Ordinal);
        AssertFileTypeAssociation(associations, "sharex-custom-uploader", "CapX custom uploader", ".sxcu", uap);
        AssertFileTypeAssociation(associations, "sharex-image-effect", "CapX image effect", ".sxie", uap);
    }

    private static void AssertFileTypeAssociation(
        IReadOnlyDictionary<string, XElement> associations,
        string associationId,
        string displayName,
        string extension,
        XNamespace uap)
    {
        if (!associations.TryGetValue(associationId, out XElement? association))
        {
            throw new InvalidOperationException($"Branding contract failed: Store association '{associationId}' is missing.");
        }

        AssertEqual(displayName, association.Element(uap + "DisplayName")?.Value,
            $"Store association '{associationId}' display name");
        AssertEqual(extension, RequireSingle(association.Descendants(uap + "FileType"),
            $"Store association '{associationId}' file type").Value,
            $"Store association '{associationId}' extension");
    }

    private static void AssertNativeHostManifest(
        string path,
        string hostId,
        string originProperty,
        string origin)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement manifest = document.RootElement;

        AssertEqual(hostId, manifest.GetProperty("name").GetString(), $"Native host ID in '{path}'");
        AssertEqual("CapX", manifest.GetProperty("description").GetString(), $"Native host description in '{path}'");
        AssertEqual("ShareX_NativeMessagingHost.exe", manifest.GetProperty("path").GetString(),
            $"Native host executable in '{path}'");

        string[] origins = manifest.GetProperty(originProperty)
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray();
        if (origins.Length != 1 || origins[0] != origin)
        {
            throw new InvalidOperationException(
                $"Branding contract failed for native host origins in '{path}': expected '{origin}', got [{string.Join(", ", origins)}].");
        }
    }

    private static XElement RequireSingle(IEnumerable<XElement> elements, string contract)
    {
        XElement[] matches = elements.ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Branding contract failed for {contract}: expected one element, got {matches.Length}.");
        }

        return matches[0];
    }

    private static void AssertEqual(string expected, string? actual, string contract)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Branding contract failed for {contract}: expected '{expected}', got '{actual ?? "<null>"}'.");
        }
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

    private static void AssertFileExists(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Branding contract failed: required file is missing: '{path}'.");
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
