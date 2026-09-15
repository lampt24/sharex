using ShareX;
using System.Windows.Forms;

internal static class ActionsToolbarTooltipBuilderTests
{
    public static void Run()
    {
        ShowsConfiguredHotkeyForMatchingAction();
        ShowsAllDistinctHotkeysForMatchingAction();
        LeavesDescriptionUnchangedWithoutConfiguredHotkey();
    }

    private static void ShowsConfiguredHotkeyForMatchingAction()
    {
        HotkeySettings[] hotkeys = [new(HotkeyType.RectangleRegion, Keys.Control | Keys.R)];

        string tooltip = ActionsToolbarTooltipBuilder.Build(HotkeyType.RectangleRegion, "Capture region", hotkeys);

        AssertEqual("Capture region (Ctrl + R)", tooltip);
    }

    private static void ShowsAllDistinctHotkeysForMatchingAction()
    {
        HotkeySettings[] hotkeys =
        [
            new(HotkeyType.RectangleRegion, Keys.Control | Keys.R),
            new(HotkeyType.PrintScreen, Keys.PrintScreen),
            new(HotkeyType.RectangleRegion, Keys.Shift | Keys.R),
            new(HotkeyType.RectangleRegion, Keys.Control | Keys.R)
        ];

        string tooltip = ActionsToolbarTooltipBuilder.Build(HotkeyType.RectangleRegion, "Capture region", hotkeys);

        AssertEqual("Capture region (Ctrl + R, Shift + R)", tooltip);
    }

    private static void LeavesDescriptionUnchangedWithoutConfiguredHotkey()
    {
        HotkeySettings[] hotkeys = [new(HotkeyType.RectangleRegion)];

        string tooltip = ActionsToolbarTooltipBuilder.Build(HotkeyType.RectangleRegion, "Capture region", hotkeys);

        AssertEqual("Capture region", tooltip);
    }

    private static void AssertEqual(string expected, string actual)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }
}
