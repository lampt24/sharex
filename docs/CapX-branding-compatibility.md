# CapX branding compatibility boundaries

User-facing product copy is CapX. Any still-visible `ShareX` value below is retained only where it is necessary for compatibility or provenance; it is not the current product name.

| Legacy identifier | Location | Reason retained | User-visible? |
| --- | --- | --- | --- |
| Namespaces, project, and source names: `ShareX`, `ShareX.*` | C# namespaces, project files, and source directories | Renaming would break assembly/source references and exceeds the compatibility boundary. | No |
| Settings and data directories: `CompatibilityAppName = "ShareX"`, `ShareXImageEffects` | `ShareX/Program.cs` persisted paths and special-folder key | Existing settings, portable data, history, and image-effect data must continue to resolve. | No |
| Registry and file-association keys: `ShareX.sxcu`, `ShareX.sxie`, `ShareXImageEditor` | `ShareX.Setup/InnoSetup/ShareX-setup.iss` | Existing shell registrations and file handlers must keep working. | Identifiers may appear in Windows integration. |
| File extensions: `.sxcu`, `.sxie` | Inno Setup and Microsoft Store file associations | Existing custom-uploader and image-effect files remain associated. | Extensions may be shown by the OS. |
| Store identity: `19568ShareX.ShareX`, app/task ID `ShareX` | `ShareX.Setup/MicrosoftStore/AppxManifest.xml` | Microsoft Store package identity and upgrade continuity. | No |
| Native messaging IDs and origins: `com.getsharex.sharex`, `ShareX`, `firefox@getsharex.com` | Native-host manifests and Inno Setup registry entries | Browser host discovery and extension allowlists require their established values. | No |
| Updater and FFmpeg URLs: `https://github.com/ShareX/FFmpeg/releases/`, `https://getsharex.com` | Setup tooling, updater code, and documentation links | Existing update/dependency endpoints and upstream service links are network contracts and provenance. | URLs can be visible; they are labeled legacy/upstream provenance. |
| Serialized names: `ShareXClicker*` | `ShareX/EasterEggs/ShareXClicker` types and persisted state | Serialized type/state identifiers must remain readable. | No |
| Steam launcher executable: `ShareX_Launcher.exe` | Steam project, install script, setup copy, and startup lookup | Existing Steam entrypoint and launcher compatibility. | No |
| Resource keys: `MainWindow_ShareXHotkeys`, `UseWhiteShareXIcon` | `.resx` catalogs and generated designer members | Resource lookup and generated-member contracts remain stable. | No |
| Theme keys: `ShareX.Brush.*` | Avalonia resource dictionaries and views | Theme/resource lookup keys are technical contracts. | No |
| Provenance and license headers: `ShareX` notices and upstream repository URLs | Source headers, license notices, README links, and API/repository references | Attribution and upstream provenance must not be rewritten. | Some links/notices may be visible; they identify provenance, not the current product. |

This boundary also preserves compatibility command examples, API endpoints, and existing external service identifiers. New product-facing titles, prose, installer metadata, and localized values use CapX.
