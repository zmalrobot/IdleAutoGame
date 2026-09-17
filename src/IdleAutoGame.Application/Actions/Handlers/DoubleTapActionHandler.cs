using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions.Handlers;

/// <summary>
/// Executes a dedicated double-tap gesture with fast consecutive timing at normalized coordinates.
/// </summary>
public sealed class DoubleTapActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.DoubleTap;

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var p = context.Action.Parameters;
        if (!p.X.HasValue || !p.Y.HasValue)
        {
            return ActionResult.RejectedResult(SupportedAction, "Missing X or Y coordinate for DoubleTap.");
        }

        var res = context.EffectiveResolution;
        int absX = (int)Math.Clamp(Math.Round(p.X.Value * (res.Width - 1)), 0, res.Width - 1);
        int absY = (int)Math.Clamp(Math.Round(p.Y.Value * (res.Height - 1)), 0, res.Height - 1);

        int intervalMs = p.IntervalMs.HasValue && p.IntervalMs.Value >= 40
            ? p.IntervalMs.Value
            : context.Settings.Automation.DoubleTapIntervalMs;

        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            if (context.IsInterrupted())
            {
                return ActionResult.CancelledResult(SupportedAction, "Double-tap cancelled due to engine state change.");
            }

            await context.DeviceController.DoubleTapAsync(context.DeviceSerial, absX, absY, intervalMs, ct).ConfigureAwait(false);
            sw.Stop();

            return ActionResult.Successful(
                SupportedAction,
                $"Double-tapped at ({absX}, {absY}) (interval: {intervalMs}ms)",
                sw.Elapsed,
                "2/2 taps executed");
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(SupportedAction, "Double-tap cancelled by user or timeout.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(SupportedAction, ex.Message, sw.Elapsed);
        }
    }
}

