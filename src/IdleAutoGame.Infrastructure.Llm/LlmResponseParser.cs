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
    /// Parses a raw model completion string into a structured <see cref="GameAction"/>, or returns null on failure.
    /// </summary>
    public static GameAction? Parse(string? rawContent)
    {
        return TryParse(rawContent, out var action, out _) ? action : null;
    }

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

            var category = ActionCategory.Normal;
            if (root.TryGetProperty("category", out var catProp))
            {
                var catStr = catProp.GetString();
                if (!string.IsNullOrWhiteSpace(catStr) && Enum.TryParse<ActionCategory>(catStr.Replace("_", ""), ignoreCase: true, out var parsedCategory))
                {
                    category = parsedCategory;
                }
            }

            string? observationSummary = null;
            if (root.TryGetProperty("observation_summary", out var obsProp))
            {
                observationSummary = obsProp.GetString();
            }

            string? objective = null;
            if (root.TryGetProperty("objective", out var objProp))
            {
                objective = objProp.GetString();
            }

            string? decisionSummary = null;
            if (root.TryGetProperty("decision_summary", out var decProp))
            {
                decisionSummary = decProp.GetString();
            }

            var parameters = new ActionParameters();
            JsonElement paramsProp = default;
            bool hasParams = root.TryGetProperty("parameters", out paramsProp) && paramsProp.ValueKind == JsonValueKind.Object;

            double? x = null, y = null, endX = null, endY = null;
            int? durationMs = null;
            string? target = null;
            int count = (actionType == ActionType.DoubleTap) ? 2 : 1;
            int? intervalMs = null;
            ScrollDirection? direction = null;
            double? distance = null;
            string? text = null;
            AndroidKeyCode? keyCode = null;
            List<AndroidKeyCode>? keyCodes = null;

            if (hasParams)
            {
                if (TryGetDouble(paramsProp, "x", "x", out var xVal)) x = xVal;
                if (TryGetDouble(paramsProp, "y", "y", out var yVal)) y = yVal;
                if (TryGetDouble(paramsProp, "end_x", "endX", out var endXVal)) endX = endXVal;
                if (TryGetDouble(paramsProp, "end_y", "endY", out var endYVal)) endY = endYVal;
                if (TryGetInt32(paramsProp, "duration_ms", "durationMs", out var durVal)) durationMs = durVal;
                if (TryGetString(paramsProp, "target", "target", out var targetVal)) target = targetVal;
                if (TryGetInt32(paramsProp, "count", "count", out var countVal)) count = countVal;
                if (TryGetInt32(paramsProp, "interval_ms", "intervalMs", out var intervalVal)) intervalMs = intervalVal;
                if (TryGetDouble(paramsProp, "distance", "distance", out var distVal)) distance = distVal;
                if (TryGetString(paramsProp, "text", "text", out var textVal)) text = textVal;

                if (TryGetProperty(paramsProp, "direction", "direction", out var dirElem))
                {
                    direction = ParseScrollDirection(dirElem);
                }

                if (TryGetProperty(paramsProp, "key_code", "keyCode", out var keyElem))
                {
                    keyCode = ParseKeyCode(keyElem);
                }

                if (TryGetProperty(paramsProp, "key_codes", "keyCodes", out var keysElem) && keysElem.ValueKind == JsonValueKind.Array)
                {
                    keyCodes = new List<AndroidKeyCode>();
                    foreach (var item in keysElem.EnumerateArray())
                    {
                        var parsedKey = ParseKeyCode(item);
                        if (parsedKey.HasValue)
                        {
                            keyCodes.Add(parsedKey.Value);
                        }
                    }
                }
            }

            // Root-level fallbacks if not found in parameters
            if (!x.HasValue && TryGetDouble(root, "x", "x", out var rootX)) x = rootX;
            if (!y.HasValue && TryGetDouble(root, "y", "y", out var rootY)) y = rootY;
            if (!endX.HasValue && TryGetDouble(root, "end_x", "endX", out var rootEndX)) endX = rootEndX;
            if (!endY.HasValue && TryGetDouble(root, "end_y", "endY", out var rootEndY)) endY = rootEndY;

            parameters = new ActionParameters
            {
                X = x,
                Y = y,
                EndX = endX,
                EndY = endY,
                DurationMs = durationMs,
                Target = target,
                Count = count,
                IntervalMs = intervalMs,
                Direction = direction,
                Distance = distance,
                Text = text,
                KeyCode = keyCode,
                KeyCodes = keyCodes
            };

            parsedAction = new GameAction
            {
                Action = actionType,
                Parameters = parameters,
                Explanation = explanation,
                Confidence = confidence,
                GameState = gameState,
                WaitAfterMs = waitAfterMs,
                Category = category,
                ObservationSummary = observationSummary,
                Objective = objective,
                DecisionSummary = decisionSummary
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

        // Fallback: extract outermost JSON object if LLM provided conversational commentary
        var jsonObjMatch = Regex.Match(trimmed, @"\{[\s\S]*\}");
        if (jsonObjMatch.Success)
        {
            return jsonObjMatch.Value.Trim();
        }

        // If no JSON object pattern, return trimmed text directly
        return trimmed;
    }

    private static bool TryGetProperty(JsonElement element, string snakeCase, string camelCase, out JsonElement prop)
    {
        if (element.TryGetProperty(snakeCase, out prop))
        {
            return true;
        }
        if (element.TryGetProperty(camelCase, out prop))
        {
            return true;
        }
        prop = default;
        return false;
    }

    private static bool TryGetDouble(JsonElement element, string snakeCase, string camelCase, out double value)
    {
        value = 0;
        if (TryGetProperty(element, snakeCase, camelCase, out var prop) && prop.TryGetDouble(out var dVal))
        {
            value = dVal;
            return true;
        }
        return false;
    }

    private static bool TryGetInt32(JsonElement element, string snakeCase, string camelCase, out int value)
    {
        value = 0;
        if (TryGetProperty(element, snakeCase, camelCase, out var prop) && prop.TryGetInt32(out var iVal))
        {
            value = iVal;
            return true;
        }
        return false;
    }

    private static bool TryGetString(JsonElement element, string snakeCase, string camelCase, out string? value)
    {
        value = null;
        if (TryGetProperty(element, snakeCase, camelCase, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return true;
        }
        return false;
    }

    private static ScrollDirection? ParseScrollDirection(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var dirInt))
        {
            if (Enum.IsDefined(typeof(ScrollDirection), dirInt))
            {
                return (ScrollDirection)dirInt;
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            var str = element.GetString();
            if (!string.IsNullOrWhiteSpace(str) && Enum.TryParse<ScrollDirection>(str.Replace("_", ""), ignoreCase: true, out var parsed))
            {
                return parsed;
            }
        }
        return null;
    }

    private static AndroidKeyCode? ParseKeyCode(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var codeInt))
        {
            if (Enum.IsDefined(typeof(AndroidKeyCode), codeInt))
            {
                return (AndroidKeyCode)codeInt;
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            var str = element.GetString();
            if (!string.IsNullOrWhiteSpace(str))
            {
                // Clean common prefixes like KEYCODE_ or android.view.KeyEvent.KEYCODE_
                str = Regex.Replace(str, @"^(?:android\.view\.KeyEvent\.)?(?:KEYCODE_)?", "", RegexOptions.IgnoreCase);
                str = str.Replace("_", "");
                if (Enum.TryParse<AndroidKeyCode>(str, ignoreCase: true, out var parsed))
                {
                    return parsed;
                }
            }
        }
        return null;
    }
}

