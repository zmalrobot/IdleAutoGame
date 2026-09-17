using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions.Handlers;

/// <summary>
/// Executes a sustained drag gesture between normalized coordinates (e.g. dragging items, cards, sliders).
/// </summary>
public sealed class DragActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.Drag;

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var p = context.Action.Parameters;
        if (!p.X.HasValue || !p.Y.HasValue || !p.EndX.HasValue || !p.EndY.HasValue)
        {
            return ActionResult.RejectedResult(SupportedAction, "Missing start (X, Y) or end (EndX, EndY) coordinates for Drag.");
        }

        var res = context.EffectiveResolution;
        int x1 = (int)Math.Clamp(Math.Round(p.X.Value * (res.Width - 1)), 0, res.Width - 1);
        int y1 = (int)Math.Clamp(Math.Round(p.Y.Value * (res.Height - 1)), 0, res.Height - 1);
        int x2 = (int)Math.Clamp(Math.Round(p.EndX.Value * (res.Width - 1)), 0, res.Width - 1);
        int y2 = (int)Math.Clamp(Math.Round(p.EndY.Value * (res.Height - 1)), 0, res.Height - 1);

        int durationMs = p.DurationMs.HasValue && p.DurationMs.Value >= context.Settings.Automation.MinDragDurationMs
            ? Math.Min(p.DurationMs.Value, context.Settings.Automation.MaxDragDurationMs)
            : context.Settings.Automation.DefaultDragDurationMs;

        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            if (context.IsInterrupted())
            {
                return ActionResult.CancelledResult(SupportedAction, "Drag cancelled due to engine state change.");
            }

            await context.DeviceController.DragAsync(context.DeviceSerial, x1, y1, x2, y2, durationMs, ct).ConfigureAwait(false);
            sw.Stop();

            return ActionResult.Successful(
                SupportedAction,
                $"Dragged ({x1}, {y1}) ➔ ({x2}, {y2}) in {durationMs}ms",
                sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(SupportedAction, "Drag cancelled by user or timeout.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(SupportedAction, ex.Message, sw.Elapsed);
        }
    }
}

