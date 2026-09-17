using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions;

/// <summary>
/// Default implementation of <see cref="IActionExecutor"/> that routes game actions to dedicated handlers,
/// enforces game capability policies, manages timeouts, and reports structured execution results.
/// </summary>
public sealed class ActionExecutor : IActionExecutor
{
    private readonly Dictionary<ActionType, IActionHandler> _handlers;

    public ActionExecutor(IEnumerable<IActionHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _handlers = handlers.ToDictionary(h => h.SupportedAction, h => h);
    }

    /// <inheritdoc />
    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var action = context.Action;
        var actionType = action.Action;

        // 1. Game Capability Check: verify that the action is allowed by this game profile
        if (context.Game.AllowedActions.Count > 0 && !context.Game.AllowedActions.Contains(actionType))
        {
            return ActionResult.RejectedResult(
                actionType,
                $"Action '{actionType}' is not supported by game profile '{context.Game.Name}'. Allowed actions: {string.Join(", ", context.Game.AllowedActions)}.");
        }

        // 2. Resolve handler strategy
        if (!_handlers.TryGetValue(actionType, out var handler))
        {
            return ActionResult.RejectedResult(
                actionType,
                $"No action handler registered for action type: '{actionType}'.");
        }

        // 3. Pre-execution interruption / cancellation check
        if (ct.IsCancellationRequested || context.IsInterrupted())
        {
            return ActionResult.CancelledResult(actionType, "Action aborted before execution due to cancellation or pause.");
        }

        // 4. Execute with configurable timeout
        int timeoutSec = context.Settings.Automation.ActionExecutionTimeoutSeconds > 0
            ? context.Settings.Automation.ActionExecutionTimeoutSeconds
            : 15;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var sw = Stopwatch.StartNew();
        try
        {
            return await handler.ExecuteAsync(context, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            sw.Stop();
            return ActionResult.TimeoutResult(
                actionType,
                $"Action '{actionType}' timed out after {timeoutSec}s.",
                sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return ActionResult.CancelledResult(
                actionType,
                $"Action '{actionType}' cancelled by user or engine lifecycle.",
                sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ActionResult.FailedResult(
                actionType,
                $"Action '{actionType}' encountered unexpected error: {ex.Message}",
                sw.Elapsed);
        }
    }
}

