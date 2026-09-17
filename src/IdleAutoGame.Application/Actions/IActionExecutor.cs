using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions;

/// <summary>
/// Orchestrates the dispatch and execution of structured actions across registered handlers.
/// </summary>
public interface IActionExecutor
{
    /// <summary>
    /// Dispatches and executes an action within the provided runtime context.
    /// </summary>
    Task<ActionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct);
}

