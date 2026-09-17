using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions.Handlers;

/// <summary>
/// Executes a semantic scroll gesture (Up, Down, Left, Right) with calibrated distance and duration.
/// </summary>
public sealed class ScrollActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.Scroll;

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var p = context.Action.Parameters;
        var direction = p.Direction ?? ScrollDirection.Down;
        double distance = Math.Clamp(p.Distance ?? context.Settings.Automation.DefaultScrollDistance, 0.05, 0.90);
        int durationMs = p.DurationMs.HasValue && p.DurationMs.Value >= 100
            ? p.DurationMs.Value
            : context.Settings.Automation.DefaultScrollDurationMs;

        // Calculate normalized swipe points based on scroll direction
        double startX = p.X ?? 0.5;
        double startY = p.Y ?? 0.5;
        double endX = startX;
        double endY = startY;

        switch (direction)
        {
            case ScrollDirection.Up:
                // Drag upwards to scroll down into lower content
                startY = p.Y ?? Math.Min(0.80, 0.5 + (distance / 2.0));
                endY = Math.Max(0.10, startY - distance);
                break;

            case ScrollDirection.Down:
                // Drag downwards to scroll up into higher content
                startY = p.Y ?? Math.Max(0.20, 0.5 - (distance / 2.0));
                endY = Math.Min(0.90, startY + distance);
                break;

            case ScrollDirection.Left:
                startX = p.X ?? Math.Min(0.80, 0.5 + (distance / 2.0));
                endX = Math.Max(0.10, startX - distance);
                break;

            case ScrollDirection.Right:
                startX = p.X ?? Math.Max(0.20, 0.5 - (distance / 2.0));
                endX = Math.Min(0.90, startX + distance);
                break;
        }

        var res = context.EffectiveResolution;
        int x1 = (int)Math.Clamp(Math.Round(startX * (res.Width - 1)), 0, res.Width - 1);
        int y1 = (int)Math.Clamp(Math.Round(startY * (res.Height - 1)), 0, res.Height - 1);
        int x2 = (int)Math.Clamp(Math.Round(endX * (res.Width - 1)), 0, res.Width - 1);
        int y2 = (int)Math.Clamp(Math.Round(endY * (res.Height - 1)), 0, res.Height - 1);

        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            if (context.IsInterrupted())
            {
                return ActionResult.CancelledResult(SupportedAction, "Scroll cancelled due to engine state change.");
            }

            await context.DeviceController.SwipeAsync(context.DeviceSerial, x1, y1, x2, y2, durationMs, ct).ConfigureAwait(false);
            sw.Stop();

            return ActionResult.Successful(
                SupportedAction,
                $"Scrolled {direction} ({x1}, {y1}) ➔ ({x2}, {y2}) (distance: {distance:P0}, {durationMs}ms)",
                sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(SupportedAction, "Scroll cancelled by user or timeout.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(SupportedAction, ex.Message, sw.Elapsed);
        }
    }
}

