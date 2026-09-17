using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions.Handlers;

/// <summary>
/// Executes the hardware Volume Up key event.
/// </summary>
public sealed class VolumeUpActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.VolumeUp;

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            if (context.IsInterrupted())
            {
                return ActionResult.CancelledResult(SupportedAction, "VolumeUp cancelled.");
            }

            await context.DeviceController.VolumeUpAsync(context.DeviceSerial, ct).ConfigureAwait(false);
            sw.Stop();

            return ActionResult.Successful(SupportedAction, "Volume Up pressed", sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(SupportedAction, "Volume Up cancelled.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(SupportedAction, ex.Message, sw.Elapsed);
        }
    }
}

/// <summary>
/// Executes the hardware Volume Down key event.
/// </summary>
public sealed class VolumeDownActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.VolumeDown;

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            if (context.IsInterrupted())
            {
                return ActionResult.CancelledResult(SupportedAction, "VolumeDown cancelled.");
            }

            await context.DeviceController.VolumeDownAsync(context.DeviceSerial, ct).ConfigureAwait(false);
            sw.Stop();

            return ActionResult.Successful(SupportedAction, "Volume Down pressed", sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(SupportedAction, "Volume Down cancelled.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(SupportedAction, ex.Message, sw.Elapsed);
        }
    }
}

