using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions.Handlers;

/// <summary>
/// Executes a single tap gesture at normalized coordinates.
/// </summary>
public sealed class TapActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.Tap;

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var p = context.Action.Parameters;
        if (!p.X.HasValue || !p.Y.HasValue)
        {
            return ActionResult.RejectedResult(SupportedAction, "Missing X or Y coordinate for Tap.");
        }

        // If count > 1, delegate execution to MultiTap semantics
        if (p.Count > 1)
        {
            var multiTapHandler = new MultiTapActionHandler();
            return await multiTapHandler.ExecuteAsync(context, ct).ConfigureAwait(false);
        }

        var res = context.EffectiveResolution;
        int absX = (int)Math.Clamp(Math.Round(p.X.Value * (res.Width - 1)), 0, res.Width - 1);
        int absY = (int)Math.Clamp(Math.Round(p.Y.Value * (res.Height - 1)), 0, res.Height - 1);

        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            if (context.IsInterrupted())
            {
                return ActionResult.CancelledResult(SupportedAction, "Tap cancelled due to engine state change.");
            }

            await context.DeviceController.TapAsync(context.DeviceSerial, absX, absY, ct).ConfigureAwait(false);
            sw.Stop();

            return ActionResult.Successful(
                SupportedAction,
                $"Tapped at ({absX}, {absY})",
                sw.Elapsed,
                "1/1 tap executed");
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(SupportedAction, "Tap cancelled by user or timeout.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(SupportedAction, ex.Message, sw.Elapsed);
        }
    }
}

