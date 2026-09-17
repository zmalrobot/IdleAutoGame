using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions.Handlers;

/// <summary>
/// Executes a sustained touch gesture at normalized coordinates.
/// </summary>
public sealed class LongPressActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.LongPress;

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var p = context.Action.Parameters;
        if (!p.X.HasValue || !p.Y.HasValue)
        {
            return ActionResult.RejectedResult(SupportedAction, "Missing X or Y coordinate for LongPress.");
        }

        var res = context.EffectiveResolution;
        int absX = (int)Math.Clamp(Math.Round(p.X.Value * (res.Width - 1)), 0, res.Width - 1);
        int absY = (int)Math.Clamp(Math.Round(p.Y.Value * (res.Height - 1)), 0, res.Height - 1);

        int durationMs = p.DurationMs.HasValue && p.DurationMs.Value >= context.Settings.Automation.MinLongPressDurationMs
            ? Math.Min(p.DurationMs.Value, context.Settings.Automation.MaxLongPressDurationMs)
            : context.Settings.Automation.DefaultLongPressDurationMs;

        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            if (context.IsInterrupted())
            {
                return ActionResult.CancelledResult(SupportedAction, "Long-press cancelled due to engine state change.");
            }

            await context.DeviceController.LongPressAsync(context.DeviceSerial, absX, absY, durationMs, ct).ConfigureAwait(false);
            sw.Stop();

            return ActionResult.Successful(
                SupportedAction,
                $"Long-pressed at ({absX}, {absY}) for {durationMs}ms",
                sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(SupportedAction, "Long-press cancelled by user or timeout.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(SupportedAction, ex.Message, sw.Elapsed);
        }
    }
}

