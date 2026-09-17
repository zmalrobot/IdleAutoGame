using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions.Handlers;

/// <summary>
/// Executes an ordered sequence of whitelisted Android KeyCodes with inter-key delay.
/// </summary>
public sealed class KeySequenceActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.KeySequence;

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var keys = context.Action.Parameters.KeyCodes;
        if (keys == null || keys.Count == 0)
        {
            return ActionResult.RejectedResult(SupportedAction, "KeySequence requires at least one KeyCode in KeyCodes list.");
        }

        int maxLength = context.Settings.Automation.MaxKeySequenceLength > 0
            ? context.Settings.Automation.MaxKeySequenceLength
            : 10;

        if (keys.Count > maxLength)
        {
            return ActionResult.RejectedResult(
                SupportedAction,
                $"KeySequence length ({keys.Count}) exceeds maximum allowed of {maxLength} keys.");
        }

        foreach (var k in keys)
        {
            if (!Enum.IsDefined(typeof(AndroidKeyCode), k))
            {
                return ActionResult.RejectedResult(SupportedAction, $"Invalid or non-whitelisted KeyCode in sequence: '{k}'.");
            }
        }

        int intervalMs = context.Action.Parameters.IntervalMs.HasValue && context.Action.Parameters.IntervalMs.Value >= 10
            ? context.Action.Parameters.IntervalMs.Value
            : 100;

        var sw = Stopwatch.StartNew();
        int completed = 0;

        try
        {
            for (int i = 0; i < keys.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (context.IsInterrupted())
                {
                    return ActionResult.CancelledResult(
                        SupportedAction,
                        $"KeySequence interrupted after {completed}/{keys.Count} keys due to engine state change.",
                        sw.Elapsed,
                        $"{completed}/{keys.Count} keys executed");
                }

                await context.DeviceController.SendKeyEventAsync(context.DeviceSerial, (int)keys[i], ct).ConfigureAwait(false);
                completed++;

                if (i < keys.Count - 1 && intervalMs > 0)
                {
                    await Task.Delay(intervalMs, ct).ConfigureAwait(false);
                }
            }

            sw.Stop();
            return ActionResult.Successful(
                SupportedAction,
                $"Sent KeySequence: [{string.Join(", ", keys)}] ({completed}/{keys.Count} keys)",
                sw.Elapsed,
                $"{completed}/{keys.Count} keys executed");
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(
                SupportedAction,
                $"KeySequence cancelled after {completed}/{keys.Count} keys.",
                sw.Elapsed,
                $"{completed}/{keys.Count} keys executed");
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(
                SupportedAction,
                $"KeySequence failed after {completed}/{keys.Count} keys: {ex.Message}",
                sw.Elapsed);
        }
    }
}

