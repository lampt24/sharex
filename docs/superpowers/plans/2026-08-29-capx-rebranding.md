# CapX Rebranding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the application under the user-facing CapX name with a new minimal camera identity while preserving ShareX-compatible settings, formats, and internal contracts.

**Architecture:** Treat branding as a presentation/package layer over the existing ShareX codebase. Keep namespaces, library assemblies, persistence paths, registry compatibility keys, file extensions, and network contracts stable; centralize the product name through `Program.AppName`, emit the main application as `CapX.exe`, and derive every bitmap/ICO asset reproducibly from one approved master image.

**Tech Stack:** .NET 10, C#, Avalonia, Windows Forms, MSBuild, PowerShell 7, System.Drawing, Inno Setup, Microsoft Store AppX assets.

**Spec:** `docs/superpowers/specs/2026-08-29-capx-rebranding-design.md`

## Global Constraints

- The visible product name is exactly `CapX`; the main executable is exactly `CapX.exe`.
- The logo is a minimal front-facing camera with a dark blue base, white body, cyan lens accent, no text, no gradients, no shadows, and no fine detail.
- Preserve C# namespaces, supporting-library assembly names, source/project directory names, existing data paths, compatibility registry keys, `.sxcu`/`.sxie`, serialization contracts, native-messaging IDs, public URLs, APIs, and update endpoints.
- Preserve all pre-existing modified and untracked work; inspect overlapping diffs before editing and stage only task-owned paths.
- Do not perform a repository-wide `ShareX` → `CapX` replacement. Every replacement must be classified as user-facing branding.
- Keep generated master and derivative assets in the repository and verify small-icon legibility.

---

### Task 1: Add an Executable Branding Verification Gate

**Files:**
- Create: `ShareX.ScreenCaptureLib.Tests/CapXBrandingTests.cs`
- Modify: `ShareX.ScreenCaptureLib.Tests/Program.cs`
- Read: `ShareX.ScreenCaptureLib.Tests/ShareX.ScreenCaptureLib.Tests.csproj`

**Interfaces:**
- Consumes: repository root discovered by walking upward from `AppContext.BaseDirectory` until `ShareX.sln` exists.
- Produces: `CapXBrandingTests.Run()`; failures throw `InvalidOperationException` with the failed contract.

- [ ] **Step 1: Inspect the existing hand-written test runner and preserve its conventions**

Run:

```powershell
Get-Content -Raw ShareX.ScreenCaptureLib.Tests\Program.cs
Get-Content -Raw ShareX.ScreenCaptureLib.Tests\ShareX.ScreenCaptureLib.Tests.csproj
```

Expected: identify the current test invocation pattern and exit behavior without changing unrelated scrolling-capture tests.

- [ ] **Step 2: Write the failing branding contract tests**

Create `CapXBrandingTests.cs` with tests equivalent to:

```csharp
internal static class CapXBrandingTests
{
    public static void Run()
    {
        string root = FindRepositoryRoot();
        AssertContains(Path.Combine(root, "Directory.build.props"), "<Product>CapX</Product>");
        AssertContains(Path.Combine(root, "ShareX", "Program.cs"), "public const string AppName = \"CapX\"");
        AssertContains(Path.Combine(root, "ShareX", "ShareX.csproj"), "<AssemblyName>CapX</AssemblyName>");
        AssertContains(Path.Combine(root, "ShareX.Setup", "InnoSetup", "ShareX-setup.iss"), "#define MyAppName \"CapX\"");
        AssertContains(Path.Combine(root, "ShareX.Setup", "MicrosoftStore", "AppxManifest.xml"), "Executable=\"CapX.exe\"");
    }

    private static void AssertContains(string path, string expected)
    {
        if (!File.ReadAllText(path).Contains(expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"{path} does not contain: {expected}");
    }
}
```

Add `CapXBrandingTests.Run();` to the existing runner before its success message. Implement `FindRepositoryRoot()` as a bounded parent-directory walk that throws if `ShareX.sln` is not found.

- [ ] **Step 3: Run the tests and verify the new contract fails**

Run:

```powershell
$env:DOTNET_CLI_HOME="$PWD\.dotnet-cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
dotnet run --project ShareX.ScreenCaptureLib.Tests\ShareX.ScreenCaptureLib.Tests.csproj --configuration Debug --no-restore
```

Expected: FAIL because `Directory.build.props` still contains `<Product>ShareX</Product>`.

- [ ] **Step 4: Commit the failing contract test**

```powershell
git add -- ShareX.ScreenCaptureLib.Tests/CapXBrandingTests.cs ShareX.ScreenCaptureLib.Tests/Program.cs
git commit -m "test: define CapX branding contracts"
```

### Task 2: Create the CapX Master Logo and Reproducible Asset Pipeline

**Files:**
- Create: `ShareX.Setup/Branding/CapX_Logo_Master.png`
- Create: `ShareX.Setup/Branding/Generate-CapXAssets.ps1`
- Modify: `ShareX/ShareX_Icon.ico`
- Modify: `ShareX/ShareX_File_Icon.ico`
- Modify: `ShareX.Steam/ShareX_Icon.ico`
- Modify: `ShareX.HelpersLib/Resources/ShareX_Logo.png`
- Modify: `ShareX.HelpersLib/Resources/ShareX_Icon.ico`
- Modify: `ShareX.HelpersLib/Resources/ShareX_Icon_White.ico`
- Modify: every `ShareX.Setup/MicrosoftStore/Assets/*.png`

**Interfaces:**
- Consumes: one 1024×1024 transparent PNG master produced by the built-in image generator.
- Produces: `Generate-CapXAssets.ps1 -MasterPath ShareX.Setup/Branding/CapX_Logo_Master.png` and the existing project-consumed filenames, so resource code requires no compatibility-sensitive rename.

- [ ] **Step 1: Generate one project-bound master mark**

Use the built-in image generator with this exact production prompt:

```text
Use case: logo-brand
Asset type: Windows desktop application master icon
Primary request: Create a minimal front-facing camera symbol for CapX.
Style/medium: flat, vector-friendly geometric logo rendered as a crisp bitmap.
Composition/framing: centered square mark with generous balanced padding; readable at 16×16.
Color palette: dark navy blue base, pure white camera body, bright cyan circular lens accent.
Constraints: transparent outer background; no text; no letters; no wordmark; no gradient; no shadow; no watermark; no fine detail; strong silhouette.
```

Copy the selected output into `ShareX.Setup/Branding/CapX_Logo_Master.png`, confirm it is 1024×1024 with alpha, and inspect it visually before proceeding.

- [ ] **Step 2: Write the asset generator and its validation-first entry points**

Implement `Generate-CapXAssets.ps1` with these functions:

```powershell
param([Parameter(Mandatory)][string]$MasterPath)

function Export-Png {
    param([string]$Source, [string]$Destination, [int]$Width, [int]$Height)
    # Load without locking, draw with HighQualityBicubic, preserve alpha, save PNG.
}

function Export-PngIco {
    param([string]$Source, [string]$Destination, [int[]]$Sizes)
    # Encode one PNG per size and write ICONDIR + ICONDIRENTRY records followed by PNG payloads.
}

function Assert-ImageSize {
    param([string]$Path, [int]$Width, [int]$Height)
    # Throw when the decoded dimensions differ.
}
```

Use explicit target tables matching the current Store dimensions: LargeTile 310/388/465/620/1240 square; SmallTile 71/89/107/142/284 square; Square150 150/188/225/300/600; Square44 scale 44/55/66/88/176; target-size variants 16/24/32/48/256; StoreLogo 50/63/75/100/200; WideTile 310×150, 388×188, 465×225, 620×300, 1240×600. Generate ICO payloads at 16, 24, 32, 48, 64, 128, and 256 pixels.

- [ ] **Step 3: Run the generator and validate every derivative**

Run:

```powershell
pwsh -NoProfile -File ShareX.Setup\Branding\Generate-CapXAssets.ps1 -MasterPath ShareX.Setup\Branding\CapX_Logo_Master.png
```

Expected: exit 0; every expected file exists; the script prints each verified dimension; ICO files contain seven image directory entries.

- [ ] **Step 4: Inspect representative outputs**

Visually inspect the master, `Square44x44Logo.targetsize-16.png`, `StoreLogo.scale-400.png`, `Wide310x150Logo.scale-400.png`, and a rendered frame from each ICO. Confirm the camera silhouette and cyan lens remain distinct at 16×16.

- [ ] **Step 5: Commit the logo system**

```powershell
git add -- ShareX.Setup/Branding ShareX/ShareX_Icon.ico ShareX/ShareX_File_Icon.ico ShareX.Steam/ShareX_Icon.ico ShareX.HelpersLib/Resources/ShareX_Logo.png ShareX.HelpersLib/Resources/ShareX_Icon.ico ShareX.HelpersLib/Resources/ShareX_Icon_White.ico ShareX.Setup/MicrosoftStore/Assets
git commit -m "feat: replace application branding assets"
```

### Task 3: Rename the Product and Main Executable Without Renaming Internal APIs

**Files:**
- Modify: `Directory.build.props`
- Modify: `ShareX/ShareX.csproj`
- Modify: `ShareX/Program.cs`
- Modify: `ShareX.Steam/Launcher.cs`
- Read/classify for Task 5: `ShareX.Setup/Program.cs`
- Read/classify for Task 5: `ShareX.Setup/MicrosoftStore/AppxManifest.xml`
- Test: `ShareX.ScreenCaptureLib.Tests/CapXBrandingTests.cs`

**Interfaces:**
- Consumes: existing `Program.AppName` callers and compatibility-sensitive namespaces rooted at `ShareX`.
- Produces: `Program.AppName == "CapX"`, MSBuild product/company metadata for CapX, and `ShareX/bin/Debug/CapX.exe` while `RootNamespace` remains `ShareX`.

- [ ] **Step 1: Expand the failing tests for metadata and namespace compatibility**

Add assertions that `Directory.build.props` contains `<Company>CapX Team</Company>` and `<Product>CapX</Product>`, and that `ShareX.csproj` contains both `<AssemblyName>CapX</AssemblyName>` and `<RootNamespace>ShareX</RootNamespace>`. Add a negative assertion that no `<RootNamespace>CapX</RootNamespace>` exists.

- [ ] **Step 2: Run the branding tests and confirm failure**

Run the Task 1 test command.

Expected: FAIL on the first missing CapX metadata value.

- [ ] **Step 3: Implement the main product identity**

Set the shared user-facing metadata in `Directory.build.props` to `CapX`, `CapX Team`, and `Copyright (c) 2007-2026 CapX Team`. In `ShareX.csproj`, add:

```xml
<AssemblyName>CapX</AssemblyName>
<RootNamespace>ShareX</RootNamespace>
```

Change only `Program.AppName` to `CapX`; preserve the `ShareX` namespace, `ShareXBuild`, settings/log filenames, special-folder identifiers, and updater contracts. Replace hard-coded visible captions with `Program.AppName` where the file already belongs to the main executable.

- [ ] **Step 4: Update runtime executable lookups**

For every result from the scoped `ShareX.exe` search, classify it. Change paths that launch or package the main executable to `CapX.exe`; retain compatibility-only references and add them to the compatibility report in Task 6. Do not rename `ShareX_Launcher.exe` unless it is the main app payload rather than the Steam compatibility launcher.

- [ ] **Step 5: Build and run the contract tests**

Run:

```powershell
$env:DOTNET_CLI_HOME="$PWD\.dotnet-cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
dotnet build ShareX\ShareX.csproj --configuration Debug --no-restore --nologo
Test-Path ShareX\bin\Debug\CapX.exe
dotnet run --project ShareX.ScreenCaptureLib.Tests\ShareX.ScreenCaptureLib.Tests.csproj --configuration Debug --no-restore
```

Expected: build succeeds, `Test-Path` prints `True`, and all tests pass.

- [ ] **Step 6: Commit product identity changes**

```powershell
git add -- Directory.build.props ShareX/ShareX.csproj ShareX/Program.cs ShareX.Steam/Launcher.cs
git commit -m "feat: emit CapX application executable"
```

### Task 4: Replace User-Facing Application Copy

**Files:**
- Modify: `ShareX/Localization/Strings.resx`
- Modify: `ShareX/Localization/Strings.*.resx` only where a translated value contains the visible product name
- Modify: `ShareX.HelpersLib/Localization/Strings.resx`
- Modify: `ShareX.HelpersLib/Localization/Strings.*.resx` under the same rule
- Modify: `ShareX.ScreenCaptureLib/Localization/Strings.resx`
- Modify: `ShareX.ScreenCaptureLib/Localization/Strings.*.resx` under the same rule
- Modify: scoped UI/code files identified by the classification command below, including `ShareX/Presentation/About/AboutWindow.axaml` and window title assignments
- Test: `ShareX.ScreenCaptureLib.Tests/CapXBrandingTests.cs`

**Interfaces:**
- Consumes: `Program.AppName` and existing localization resource keys.
- Produces: visible application copy that says CapX while resource keys, generated designer property names, namespaces, and theme keys remain stable.

- [ ] **Step 1: Produce a reviewable user-facing candidate list**

Run:

```powershell
rg -n -F 'ShareX' ShareX ShareX.HelpersLib ShareX.ScreenCaptureLib -g '*.resx' -g '*.axaml' -g '*.cs' | rg -v 'namespace |using ShareX|x:Class="ShareX|using:ShareX|ShareX\.Brush|ShareXResources|ShareXBuild|ShareXSpecialFolders|ShareXClicker|Resources\.ShareX_|Copyright|^.*:[0-9]+:\s*ShareX -'
```

Review each remaining line. Include captions, messages, descriptions, About text, and notifications; exclude identifiers, resource keys, persisted names, endpoints, and code-license headers.

- [ ] **Step 2: Add failing assertions for key visible surfaces**

Extend `CapXBrandingTests.Run()` to assert that About markup contains `Text="CapX"`, that English resource values do not contain the sentence-level token `ShareX`, and that `Program.cs` no longer contains hard-coded window captions such as `"ShareX - " + Strings.Error`.

- [ ] **Step 3: Run tests and verify failure**

Run the Task 1 test command.

Expected: FAIL on About or English resource branding.

- [ ] **Step 4: Replace only visible values**

Change `ShareX` to `CapX` inside localized `<value>` elements and visible `Text`, `Title`, description, tooltip, and message literals. Prefer `Program.AppName` interpolation in main-app C# code. Do not rename localization keys such as `ShareXCannotBeClosedWhileScreenRecordingIsActive`, generated resource members, theme keys, `UseWhiteShareXIcon`, or clicker class/state identifiers.

- [ ] **Step 5: Regenerate resources only through the repository's existing mechanism**

Build the affected projects. If SDK resource generation changes `*.Designer.cs`, inspect the diff and keep only deterministic changes required by modified resource structure; value-only `.resx` edits should not require member renames.

- [ ] **Step 6: Run tests and scoped searches**

Run the main Debug build, Task 1 tests, and the candidate-list command again.

Expected: build and tests pass; remaining search hits are technical identities or compatibility entries and are ready for Task 6 documentation.

- [ ] **Step 7: Commit user-facing copy changes**

Stage only the explicitly reviewed `.resx`, `.axaml`, and `.cs` files, then run:

```powershell
git diff --cached --check
git commit -m "feat: apply CapX name across application UI"
```

### Task 5: Rebrand Installer, Portable Artifacts, Steam, and Microsoft Store Packaging

**Files:**
- Modify: `ShareX.Setup/Program.cs`
- Modify: `ShareX.Setup/InnoSetup/ShareX-setup.iss`
- Modify: `ShareX.Setup/MicrosoftStore/AppxManifest.xml`
- Modify: `ShareX/host-manifest-chrome.json`
- Modify: `ShareX/host-manifest-firefox.json`
- Modify: Steam packaging files returned by `rg -l -F 'ShareX' ShareX.Steam`
- Test: `ShareX.ScreenCaptureLib.Tests/CapXBrandingTests.cs`

**Interfaces:**
- Consumes: `CapX.exe` from Task 3 and replacement assets from Task 2.
- Produces: `CapX-21.0.0-setup-x64.exe` for the current version/platform build, equivalently CapX-named artifacts for other configured versions/platforms, and packages that launch `CapX.exe`.

- [ ] **Step 1: Add failing package-contract assertions**

Assert exact visible values in the installer and Store manifest: `MyAppName "CapX"`, `Executable="CapX.exe"`, `<DisplayName>CapX</DisplayName>`, `DisplayName="CapX"`, and setup output interpolation beginning with `CapX-`. Also assert that `.sxcu`, `.sxie`, `19568ShareX.ShareX`, and existing native-messaging IDs remain present.

- [ ] **Step 2: Run tests and verify failure**

Run the Task 1 test command.

Expected: FAIL on the first missing CapX packaging value.

- [ ] **Step 3: Update build artifact names and payload lookup**

In `ShareX.Setup/Program.cs`, change user-facing output directories/files and console messages to CapX, and change `ExecutablePath` to `CapX.exe`. Preserve repository paths (`ShareX.sln`, `ShareX/bin`, `ShareX.Setup`) and external ShareX-hosted dependency URLs.

- [ ] **Step 4: Update Inno Setup presentation while preserving upgrade compatibility**

Set `MyAppName`, publisher text, copyright, shortcut/task descriptions, Explorer caption, and icon payload display references to CapX. Update the installed executable source/destination to `CapX.exe`. Preserve the existing AppId, uninstall/upgrade discovery values, `Software\Classes\ShareX.sxcu`, `ShareX.sxie`, image-editor verb key, and native-messaging registry key.

- [ ] **Step 5: Update Store and native messaging display metadata**

Set Store `DisplayName`, visual element name/description, startup display name, file-association display names, and executable paths to CapX. Preserve Store `Identity Name="19568ShareX.ShareX"`, application ID, task ID, file association names/extensions, publisher certificate identity, and package compatibility identifiers. In host manifests, change only human-readable `name`/`description`; retain native-messaging host IDs and browser extension origins.

- [ ] **Step 6: Update Steam-facing visible names**

Review each Steam hit and change captions/product descriptions and main payload `ShareX.exe` references to CapX. Preserve the launcher assembly/name and Steam protocol identifiers where changing them would break installed shortcuts or Steam integration.

- [ ] **Step 7: Build package tooling and validate manifests**

Run:

```powershell
$env:DOTNET_CLI_HOME="$PWD\.dotnet-cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
dotnet build ShareX.Setup\ShareX.Setup.csproj --configuration Release --no-restore --nologo
[xml](Get-Content -Raw ShareX.Setup\MicrosoftStore\AppxManifest.xml) | Out-Null
dotnet run --project ShareX.ScreenCaptureLib.Tests\ShareX.ScreenCaptureLib.Tests.csproj --configuration Debug --no-restore
```

Expected: setup tool builds, XML parses, and all branding/compatibility contracts pass.

- [ ] **Step 8: Build the x64 installer when release payload prerequisites are available**

Run the repository's existing x64 Release build and setup command. Expected: output filename starts with `CapX-21.0.0-setup-x64.exe`, the installer compiles without missing `ShareX.exe`, and its installed shortcut target is `CapX.exe`.

- [ ] **Step 9: Commit packaging changes**

Stage only the reviewed setup, manifest, native-messaging, and Steam files, then run:

```powershell
git diff --cached --check
git commit -m "feat: rebrand CapX distribution packages"
```

### Task 6: Document Intentional ShareX Compatibility Identities

**Files:**
- Create: `docs/CapX-branding-compatibility.md`
- Modify: `README.md`
- Modify: `ShareX.UploadersLib/Localization/README.md`
- Modify: `ShareX.Tools/Localization/README.md`
- Modify: `ShareX/Localization/README.md`
- Modify: `ShareX.Avalonia/Localization/README.md`
- Test: `ShareX.ScreenCaptureLib.Tests/CapXBrandingTests.cs`

**Interfaces:**
- Consumes: final classified search results from Tasks 3–5.
- Produces: an auditable list of intentional legacy identifiers and CapX-facing documentation.

- [ ] **Step 1: Write the compatibility report**

Create a table with columns `Legacy identifier`, `Location`, `Reason retained`, and `User-visible?`. Include at minimum namespaces/project names, settings/data directories, registry/file-association keys, `.sxcu/.sxie`, Store identity, native messaging IDs, updater URLs, serialized names, `ShareX_Launcher.exe`, resource keys, and theme keys. Mark each as not user-visible or explain the unavoidable surface.

- [ ] **Step 2: Update user-facing documentation**

Change the product title and prose to CapX. Preserve historical upstream links, license notices, repository URLs, API endpoints, command examples whose identifiers must remain compatible, and attribution; label retained upstream ShareX references as compatibility or provenance where readers could confuse them with the current product name.

- [ ] **Step 3: Extend the verification test with protected identifiers**

Add positive assertions for `.sxcu`, `.sxie`, Store identity `19568ShareX.ShareX`, the existing data-directory token, and native-messaging host identity. Add an assertion that `docs/CapX-branding-compatibility.md` lists each protected identifier.

- [ ] **Step 4: Run tests and review all remaining text hits**

Run:

```powershell
dotnet run --project ShareX.ScreenCaptureLib.Tests\ShareX.ScreenCaptureLib.Tests.csproj --configuration Debug --no-restore
rg -n -F 'ShareX' -g '!**/bin/**' -g '!**/obj/**' -g '!docs/superpowers/**'
```

Expected: tests pass; every remaining hit is an internal identifier, compatibility contract, provenance/license header, or an entry covered by the report.

- [ ] **Step 5: Commit documentation and compatibility contracts**

```powershell
git add -- docs/CapX-branding-compatibility.md README.md ShareX.ScreenCaptureLib.Tests/CapXBrandingTests.cs ShareX.UploadersLib/Localization/README.md ShareX.Tools/Localization/README.md ShareX/Localization/README.md ShareX.Avalonia/Localization/README.md
git commit -m "docs: record CapX compatibility boundaries"
```

### Task 7: Final Build, Launch, and Branding Audit

**Files:**
- Modify: only files required to fix a failed acceptance check, scoped to the responsible earlier task.
- Verify: all files changed since commit `ff11a07db`.

**Interfaces:**
- Consumes: complete CapX executable, assets, UI copy, packages, and compatibility report.
- Produces: evidence that all acceptance conditions in the spec pass without unrelated worktree changes.

- [ ] **Step 1: Run automated verification**

Run:

```powershell
$env:DOTNET_CLI_HOME="$PWD\.dotnet-cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
dotnet build ShareX\ShareX.csproj --configuration Debug --no-restore --nologo
dotnet run --project ShareX.ScreenCaptureLib.Tests\ShareX.ScreenCaptureLib.Tests.csproj --configuration Debug --no-restore
git diff --check ff11a07db..HEAD
```

Expected: build succeeds, all tests pass, and diff check has no output.

- [ ] **Step 2: Inspect binary metadata and asset structure**

Use PowerShell file version APIs to assert `ProductName == "CapX"`, confirm `CapX.exe` exists and `ShareX.exe` is not emitted as the primary executable, rerun the asset generator in validation mode, and confirm Store PNG dimensions and ICO frame counts.

- [ ] **Step 3: Launch and manually inspect visible surfaces**

Start `ShareX/bin/Debug/CapX.exe`, then verify the process is responsive and inspect main window title/icon, tray icon/menu, About logo/name, one settings dialog, one notification/message, and Windows executable properties. Close only the CapX process launched for this check.

- [ ] **Step 4: Validate installer behavior**

Inspect or run the generated installer in the established safe test flow. Confirm the installer name, wizard copy, shortcut, uninstall entry, context-menu caption, installed icon, and target executable all say CapX while an existing ShareX settings directory remains readable.

- [ ] **Step 5: Audit the final diff against pre-existing work**

Run:

```powershell
git status --short
git diff --name-status ff11a07db..HEAD
git diff --stat ff11a07db..HEAD
```

Expected: task commits contain only rebranding/tests/docs/assets; the modified and untracked files that predated this plan remain preserved and are not accidentally committed.

- [ ] **Step 6: Commit only acceptance-fix changes if any**

If Step 1–5 required corrections, stage their explicit paths and commit:

```powershell
git diff --cached --check
git commit -m "fix: complete CapX branding audit"
```

If no correction was required, do not create an empty commit.
