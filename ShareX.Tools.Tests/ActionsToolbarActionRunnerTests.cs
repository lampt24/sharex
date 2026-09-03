using ShareX;

internal static class ActionsToolbarActionRunnerTests
{
    public static void Run()
    {
        HidesBeforeExecutingAndShowsAfterCompletion();
        ShowsAgainWhenActionFails();
    }

    private static void HidesBeforeExecutingAndShowsAfterCompletion()
    {
        List<string> events = [];

        ActionsToolbarActionRunner.RunAsync(
            () => events.Add("hide"),
            () => events.Add("show"),
            () =>
            {
                events.Add("execute");
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();

        AssertSequence(["hide", "execute", "show"], events,
            "Action toolbar must remain hidden while the selected action starts.");
    }

    private static void ShowsAgainWhenActionFails()
    {
        List<string> events = [];

        try
        {
            ActionsToolbarActionRunner.RunAsync(
                () => events.Add("hide"),
                () => events.Add("show"),
                () => Task.FromException(new InvalidOperationException("expected"))).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException)
        {
        }

        AssertSequence(["hide", "show"], events,
            "Action toolbar must be restored after a failed action.");
    }

    private static void AssertSequence(IReadOnlyList<string> expected, IReadOnlyList<string> actual, string description)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException($"{description} Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
        }
    }
}
