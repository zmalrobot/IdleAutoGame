using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions.Handlers;

/// <summary>
/// Executes a single whitelisted Android KeyCode event.
/// </summary>
public sealed class KeyPressActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.KeyPress;

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var keyCode = context.Action.Parameters.KeyCode;
        if (!keyCode.HasValue || !Enum.IsDefined(typeof(AndroidKeyCode), keyCode.Value))
        {
            return ActionResult.RejectedResult(
                SupportedAction,
                $"Invalid or non-whitelisted KeyCode: '{keyCode}'. Only predefined AndroidKeyCode enum values are allowed.");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            if (context.IsInterrupted())
            {
                return ActionResult.CancelledResult(SupportedAction, "KeyPress cancelled due to engine state change.");
            }

            await context.DeviceController.SendKeyEventAsync(context.DeviceSerial, (int)keyCode.Value, ct).ConfigureAwait(false);
            sw.Stop();

            return ActionResult.Successful(
                SupportedAction,
                $"Sent KeyCode: {keyCode.Value} ({(int)keyCode.Value})",
                sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(SupportedAction, "KeyPress cancelled by user or timeout.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(SupportedAction, ex.Message, sw.Elapsed);
        }
    }
}

