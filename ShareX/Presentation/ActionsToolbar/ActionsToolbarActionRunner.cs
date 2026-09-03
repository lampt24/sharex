#region License Information (GPL v3)

/*
    ShareX - A program developed by ShareX Team
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

#nullable enable

using System;
using System.Threading.Tasks;

namespace ShareX;

public static class ActionsToolbarActionRunner
{
    public static async Task RunAsync(Action hide, Action show, Func<Task> execute)
    {
        hide();

        try
        {
            await execute();
        }
        finally
        {
            show();
        }
    }
}
