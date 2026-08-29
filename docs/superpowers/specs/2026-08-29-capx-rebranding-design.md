# CapX Rebranding Design

## Goal

Rebrand the user-facing ShareX application as **CapX** and replace its existing visual identity with a minimal camera logo. Preserve compatibility with existing ShareX user data and integrations wherever changing an internal identifier would cause migration or breakage.

## Scope

### User-facing name

Replace `ShareX` with `CapX` wherever the value is branding presented to users, including:

- application and window titles;
- About, menus, dialogs, notifications, tray UI, and other localized copy;
- executable product metadata and the main executable filename;
- installer name, installed application name, shortcuts, uninstall entry, and Store display metadata;
- documentation and bundled user-facing manifests where they describe the product;
- Steam-facing display assets and metadata that are built from this repository.

The primary executable should be `CapX.exe`. Build and packaging references that launch the primary executable must follow the new filename.

### Compatibility boundary

Do not mechanically rename technical identifiers merely because they contain `ShareX`. Retain existing values when they are part of compatibility-sensitive behavior, including:

- C# namespaces, type names, assembly names for supporting libraries, project filenames, and source directories;
- existing user settings, history, logs, and data paths;
- registry keys and identifiers used to discover or migrate an existing installation;
- command-line contracts, native messaging identifiers, URLs, API identifiers, and update endpoints unless they are purely display text;
- established `.sxcu` and `.sxie` extensions and their underlying association identifiers;
- serialization names and persisted data contracts.

The main application assembly may retain its internal assembly identity if changing it would disrupt compatibility, while explicitly setting its output filename and product-facing metadata to CapX. Any unavoidable remaining user-visible `ShareX` string must be documented during implementation.

## Logo System

Create one master CapX mark with these characteristics:

- a minimal front-facing camera silhouette;
- dark blue base, white camera body, and cyan lens accent;
- simple geometry that remains legible at 16×16 pixels;
- square composition with balanced padding;
- no wordmark, letters, gradients, shadows, watermark, or fine detail;
- transparent outer background where the target asset supports transparency.

Use the generated master as the visual source, then produce deterministic size-specific assets matching every existing application, file, tray, Steam, installer, and Microsoft Store logo target. Small icons must be visually inspected rather than assumed to downscale cleanly. ICO files must contain the sizes required by the existing Windows build and shell integration.

The generation prompt will use the `logo-brand` category and request a vector-friendly, production-ready bitmap mark. The final master and all project-consumed derivatives must live inside the repository.

## Implementation Strategy

1. Inventory every textual occurrence and image reference, classifying it as user-facing branding or compatibility-sensitive identity.
2. Generate and review the master camera mark.
3. Replace raster and ICO assets using the existing filenames where doing so minimizes code churn; rename asset files only when their filename itself becomes user-facing or packaging requires it.
4. Update product metadata, UI/localization text, executable output naming, installer, shortcuts, and Store manifests.
5. Update runtime references that must locate or launch `CapX.exe` while leaving compatibility contracts intact.
6. Run targeted searches to find remaining user-visible `ShareX` references and explicitly classify intentional leftovers.

Bulk global replacement is prohibited because the repository currently contains roughly 1,500 files with `ShareX`, many of which are technical identities rather than branding.

## Existing Worktree Protection

The worktree already contains unrelated modified and untracked files. Implementation must:

- preserve those changes;
- inspect diffs before editing overlapping files;
- avoid formatting or broad rewrites that obscure existing work;
- stage or commit only files belonging to the rebranding task.

## Verification

Verification is complete when:

- the main application builds and emits `CapX.exe`;
- the relevant installer/package build resolves the renamed executable and all replacement assets;
- application launch, main window, tray UI, About UI, and Windows file properties display CapX branding;
- generated ICO and Store PNG assets have the expected formats and dimensions;
- a repository search finds no unexplained user-facing `ShareX` text;
- compatibility-sensitive identifiers remain unchanged and existing settings/data paths are still used;
- `git diff --check` passes and no unrelated user changes are included in task commits.

## Non-goals

- Renaming every namespace, project, source folder, or supporting assembly.
- Migrating existing user data to a new CapX directory.
- Changing public protocols, file formats, endpoints, or update infrastructure.
- Redesigning application UI beyond the name and logo replacement.
