using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions.Handlers;

/// <summary>
/// Executes safe text entry into the focused Android UI element with strict command injection prevention.
/// </summary>
public sealed class TextInputActionHandler : IActionHandler
{
    public ActionType SupportedAction => ActionType.TextInput;

    private static readonly char[] ShellMetacharacters =
        ['$', ';', '&', '|', '`', '<', '>', '"', '\\', '\r', '\n'];

    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var text = context.Action.Parameters.Text;
        if (string.IsNullOrEmpty(text))
        {
            return ActionResult.RejectedResult(SupportedAction, "Text payload cannot be empty for TextInput.");
        }

        int maxLength = context.Settings.Automation.MaxTextInputLength > 0
            ? context.Settings.Automation.MaxTextInputLength
            : 100;

        if (text.Length > maxLength)
        {
            return ActionResult.RejectedResult(
                SupportedAction,
                $"Text input length ({text.Length}) exceeds configured maximum of {maxLength} characters.");
        }

        // Security check against shell injection vectors
        if (text.IndexOfAny(ShellMetacharacters) >= 0)
        {
            return ActionResult.RejectedResult(
                SupportedAction,
                "Text input contains forbidden shell metacharacters and was blocked by security policy.");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            if (context.IsInterrupted())
            {
                return ActionResult.CancelledResult(SupportedAction, "TextInput cancelled due to engine state change.");
            }

            await context.DeviceController.SendTextAsync(context.DeviceSerial, text, ct).ConfigureAwait(false);
            sw.Stop();

            // Mask text for logging/display if sensitive
            string displayPreview = text.Length > 20 ? $"{text[..17]}..." : text;
            return ActionResult.Successful(
                SupportedAction,
                $"Typed text: \"{displayPreview}\" ({text.Length} chars)",
                sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ActionResult.CancelledResult(SupportedAction, "TextInput cancelled by user or timeout.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return ActionResult.FailedResult(SupportedAction, ex.Message, sw.Elapsed);
        }
    }
}

