using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions;

/// <summary>
/// Strategy interface for executing a specific primitive action type on an Android device.
/// </summary>
public interface IActionHandler
{
    /// <summary>
    /// Gets the action type handled by this strategy.
    /// </summary>
    ActionType SupportedAction { get; }

    /// <summary>
    /// Executes the action within the given execution context.
    /// </summary>
    Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct);
}

