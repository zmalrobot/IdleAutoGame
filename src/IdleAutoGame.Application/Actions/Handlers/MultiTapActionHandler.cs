using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions.Handlers;

/// <summary>
/// Executes a sequence of N rapid taps at normalized coordinates with interval delay.
/// </summary>
public sealed class MultiTapActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.MultiTap;

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var p = context.Action.Parameters;
        if (!p.X.HasValue || !p.Y.HasValue)
        {
            return ActionResult.RejectedResult(SupportedAction, "Missing X or Y coordinate for MultiTap.");
        }

        var res = context.EffectiveResolution;
        int absX = (int)Math.Clamp(Math.Round(p.X.Value * (res.Width - 1)), 0, res.Width - 1);
        int absY = (int)Math.Clamp(Math.Round(p.Y.Value * (res.Height - 1)), 0, res.Height - 1);

        int maxCount = context.Settings.Automation.MaxTapCount > 0 ? context.Settings.Automation.MaxTapCount : 50;
        int count = Math.Clamp(p.Count > 0 ? p.Count : 1, 1, maxCount);

        int intervalMs = p.IntervalMs.HasValue && p.IntervalMs.Value >= context.Settings.Automation.MinTapIntervalMs
            ? p.IntervalMs.Value
            : context.Settings.Automation.DefaultTapIntervalMs;

        var sw = Stopwatch.StartNew();
        int completedTaps = 0;

        try
        {
            for (int i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (context.IsInterrupted())
                {
                    return ActionResult.CancelledResult(
                        SupportedAction,
                        $"Multi-tap interrupted after {completedTaps}/{count} taps due to engine state change.",
                        sw.Elapsed,
                        $"{completedTaps}/{count} taps executed");
                }

                await context.DeviceController.TapAsync(context.DeviceSerial, absX, absY, ct).ConfigureAwait(false);
                completedTaps++;

                if (i < count - 1 && intervalMs > 0)
                {
                    await Task.Delay(intervalMs, ct).ConfigureAwait(false);
                }
            }

            sw.Stop();
            return ActionResult.Successful(
                SupportedAction,
                $"Tapped {completedTaps}/{count} times at ({absX}, {absY}) (interval: {intervalMs}ms)",
                sw.Elapsed,
                $"{completedTaps}/{count} taps executed");
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(
                SupportedAction,
                $"Multi-tap cancelled after {completedTaps}/{count} taps.",
                sw.Elapsed,
                $"{completedTaps}/{count} taps executed");
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(
                SupportedAction,
                $"Multi-tap failed after {completedTaps}/{count} taps: {ex.Message}",
                sw.Elapsed);
        }
    }
}

