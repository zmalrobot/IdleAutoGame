using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions.Handlers;

/// <summary>
/// Executes the Android Back navigation button.
/// </summary>
public sealed class BackActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.Back;

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            if (context.IsInterrupted())
            {
                return ActionResult.CancelledResult(SupportedAction, "Back navigation cancelled.");
            }

            await context.DeviceController.BackAsync(context.DeviceSerial, ct).ConfigureAwait(false);
            sw.Stop();

            return ActionResult.Successful(SupportedAction, "Back button pressed", sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(SupportedAction, "Back button cancelled.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(SupportedAction, ex.Message, sw.Elapsed);
        }
    }
}

/// <summary>
/// Executes the Android Home navigation button.
/// </summary>
public sealed class HomeActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.Home;

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            if (context.IsInterrupted())
            {
                return ActionResult.CancelledResult(SupportedAction, "Home navigation cancelled.");
            }

            await context.DeviceController.HomeAsync(context.DeviceSerial, ct).ConfigureAwait(false);
            sw.Stop();

            return ActionResult.Successful(SupportedAction, "Home button pressed", sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(SupportedAction, "Home button cancelled.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(SupportedAction, ex.Message, sw.Elapsed);
        }
    }
}

/// <summary>
/// Executes the Android Recents / App Switch navigation button.
/// </summary>
public sealed class RecentsActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.Recents;

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            if (context.IsInterrupted())
            {
                return ActionResult.CancelledResult(SupportedAction, "Recents navigation cancelled.");
            }

            await context.DeviceController.RecentsAsync(context.DeviceSerial, ct).ConfigureAwait(false);
            sw.Stop();

            return ActionResult.Successful(SupportedAction, "Recents / App Switch button pressed", sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(SupportedAction, "Recents button cancelled.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(SupportedAction, ex.Message, sw.Elapsed);
        }
    }
}

