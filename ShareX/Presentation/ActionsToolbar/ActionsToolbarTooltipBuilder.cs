#region License Information (GPL v3)

/*
    ShareX - A program developed by ShareX Team
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace ShareX;

public static class ActionsToolbarTooltipBuilder
{
    public static string Build(HotkeyType action, string description, IEnumerable<HotkeySettings>? hotkeys)
    {
        string[] configuredHotkeys = hotkeys?
            .Where(x => x.TaskSettings?.Job == action && x.HotkeyInfo?.IsValidHotkey == true)
            .Select(x => x.HotkeyInfo.ToString())
            .Distinct()
            .ToArray() ?? [];

        return configuredHotkeys.Length > 0
            ? $"{description} ({string.Join(", ", configuredHotkeys)})"
            : description;
    }
}
