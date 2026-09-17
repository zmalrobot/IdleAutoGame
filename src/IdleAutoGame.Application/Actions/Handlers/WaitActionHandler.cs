using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions.Handlers;

/// <summary>
/// Handles explicit waiting without device interaction.
/// </summary>
public sealed class WaitActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.Wait;

    public Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        int waitMs = context.Action.WaitAfterMs ?? 1000;
        return Task.FromResult(ActionResult.Successful(
            SupportedAction,
            $"Waiting {waitMs}ms",
            TimeSpan.FromMilliseconds(0)));
    }
}

/// <summary>
/// Handles intentional no-op assessment without device interaction.
/// </summary>
public sealed class DoNothingActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.DoNothing;

    public Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        return Task.FromResult(ActionResult.Successful(
            SupportedAction,
            "No action required (DoNothing)",
            TimeSpan.FromMilliseconds(0)));
    }
}

