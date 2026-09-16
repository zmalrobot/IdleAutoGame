using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Llm;

/// <summary>
/// Robust parser and deserializer for structured action responses emitted by LLMs.
/// Strips markdown code blocks and handles common LLM response variations.
/// </summary>
public static class LlmResponseParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Parses a raw model completion string into a structured <see cref="GameAction"/>.
    /// </summary>
    /// <param name="rawContent">The raw completion text from the model.</param>
    /// <param name="parsedAction">The resulting action if deserialization succeeded.</param>
    /// <param name="errorMessage">Error explanation if parsing failed.</param>
    /// <returns>True if successfully parsed; otherwise false.</returns>
    public static bool TryParse(string? rawContent, out GameAction? parsedAction, out string? errorMessage)
    {
        parsedAction = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            errorMessage = "LLM response content was empty.";
            return false;
        }

        // Clean markdown code blocks (e.g. ```json ... ``` or ``` ...)
        var cleaned = CleanMarkdownFences(rawContent);

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            if (!root.TryGetProperty("action", out var actionProp))
            {
                errorMessage = "Missing required 'action' field in JSON response.";
                return false;
            }

            var actionStr = actionProp.GetString();
            if (string.IsNullOrWhiteSpace(actionStr) || !Enum.TryParse<ActionType>(actionStr.Replace("_", ""), ignoreCase: true, out var actionType))
            {
                errorMessage = $"Invalid or unknown action type: '{actionStr}'.";
                return false;
            }

            if (!root.TryGetProperty("explanation", out var expProp) || string.IsNullOrWhiteSpace(expProp.GetString()))
            {
                errorMessage = "Missing or empty required 'explanation' field in JSON response.";
                return false;
            }
            string explanation = expProp.GetString()!;

            double confidence = 1.0;
            if (root.TryGetProperty("confidence", out var confProp) && confProp.TryGetDouble(out var confVal))
            {
                confidence = Math.Clamp(confVal, 0.0, 1.0);
            }

            var gameState = GameStateAssessment.Normal;
            if (root.TryGetProperty("game_state", out var stateProp))
            {
                var stateStr = stateProp.GetString();
                if (!string.IsNullOrWhiteSpace(stateStr) && Enum.TryParse<GameStateAssessment>(stateStr.Replace("_", ""), ignoreCase: true, out var parsedState))
                {
                    gameState = parsedState;
                }
            }

            int? waitAfterMs = null;
            if (root.TryGetProperty("wait_after_ms", out var waitProp) && waitProp.TryGetInt32(out var waitVal))
            {
                waitAfterMs = waitVal;
            }

            var parameters = new ActionParameters();
            if (root.TryGetProperty("parameters", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Object)
            {
                double? x = null, y = null, endX = null, endY = null;
                int? durationMs = null;
                string? target = null;

                if (paramsProp.TryGetProperty("x", out var xProp) && xProp.TryGetDouble(out var xVal)) x = xVal;
                if (paramsProp.TryGetProperty("y", out var yProp) && yProp.TryGetDouble(out var yVal)) y = yVal;
                if (paramsProp.TryGetProperty("end_x", out var endXProp) && endXProp.TryGetDouble(out var endXVal)) endX = endXVal;
                if (paramsProp.TryGetProperty("end_y", out var endYProp) && endYProp.TryGetDouble(out var endYVal)) endY = endYVal;
                if (paramsProp.TryGetProperty("duration_ms", out var durProp) && durProp.TryGetInt32(out var durVal)) durationMs = durVal;
                if (paramsProp.TryGetProperty("target", out var targetProp)) target = targetProp.GetString();

                parameters = new ActionParameters
                {
                    X = x,
                    Y = y,
                    EndX = endX,
                    EndY = endY,
                    DurationMs = durationMs,
                    Target = target
                };
            }

            parsedAction = new GameAction
            {
                Action = actionType,
                Parameters = parameters,
                Explanation = explanation,
                Confidence = confidence,
                GameState = gameState,
                WaitAfterMs = waitAfterMs
            };

            return true;
        }
        catch (JsonException ex)
        {
            errorMessage = $"Malformed JSON structure: {ex.Message}";
            return false;
        }
    }

    private static string CleanMarkdownFences(string input)
    {
        var trimmed = input.Trim();

        // Regex to extract JSON content inside markdown code blocks
        var match = Regex.Match(trimmed, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // If no code fence, return trimmed text directly
        return trimmed;
    }
}
