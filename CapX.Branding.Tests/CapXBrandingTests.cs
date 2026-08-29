using System;
using System.IO;

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
        AssertContains(Path.Combine(root, "ShareX.Setup", "InnoSetup", "ShareX-setup.iss"), "#define MyAppName \"CapX\"");
        AssertContains(Path.Combine(root, "ShareX.Setup", "MicrosoftStore", "AppxManifest.xml"), "Executable=\"CapX.exe\"");
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
}
