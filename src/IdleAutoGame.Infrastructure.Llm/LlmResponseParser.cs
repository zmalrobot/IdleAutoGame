using System.Text;
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

        var candidates = ExtractJsonCandidates(rawContent);
        string? lastError = null;

        foreach (var candidate in candidates)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(candidate);
            }
            catch (JsonException ex)
            {
                lastError = $"Malformed JSON structure: {ex.Message}";
                continue;
            }

            if (TryParseJsonDocument(doc, out parsedAction, out var docError))
            {
                return true;
            }
            lastError = docError;
        }

        errorMessage = lastError ?? "No valid GameAction JSON found in LLM response.";
        return false;
    }

    private static bool TryParseJsonDocument(JsonDocument doc, out GameAction? parsedAction, out string? errorMessage)
    {
        parsedAction = null;
        errorMessage = null;

        try
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errorMessage = "JSON root is not an object.";
                return false;
            }

                // Support "action", "next_action", or "action_type"
                string? actionStr = null;
                if (root.TryGetProperty("action", out var actionProp))
                {
                    actionStr = actionProp.GetString();
                }
                else if (root.TryGetProperty("next_action", out var nextActionProp))
                {
                    actionStr = nextActionProp.GetString();
                }
                else if (root.TryGetProperty("action_type", out var actionTypeProp))
                {
                    actionStr = actionTypeProp.GetString();
                }

                if (string.IsNullOrWhiteSpace(actionStr) || !Enum.TryParse<ActionType>(actionStr.Replace("_", ""), ignoreCase: true, out var actionType))
                {
                    errorMessage = $"Invalid or unknown action type: '{actionStr}'.";
                    return false;
                }

                // Support "explanation", "decision_summary", "observation_summary", or "objective"
                string? explanation = null;
                if (root.TryGetProperty("explanation", out var expProp) && !string.IsNullOrWhiteSpace(expProp.GetString()))
                {
                    explanation = expProp.GetString();
                }
                else if (root.TryGetProperty("decision_summary", out var decProp) && !string.IsNullOrWhiteSpace(decProp.GetString()))
                {
                    explanation = decProp.GetString();
                }
                else if (root.TryGetProperty("observation_summary", out var obsProp) && !string.IsNullOrWhiteSpace(obsProp.GetString()))
                {
                    explanation = obsProp.GetString();
                }
                else if (root.TryGetProperty("objective", out var objProp) && !string.IsNullOrWhiteSpace(objProp.GetString()))
                {
                    explanation = objProp.GetString();
                }

                if (string.IsNullOrWhiteSpace(explanation))
                {
                    errorMessage = "Missing or empty required 'explanation' field in JSON response.";
                    return false;
                }

                double confidence = 1.0;
                if (root.TryGetProperty("confidence", out var confProp) && confProp.TryGetDouble(out var confVal))
                {
                    confidence = Math.Clamp(confVal, 0.0, 1.0);
                }

                // Support "game_state" or "state"
                var gameState = GameStateAssessment.Normal;
                JsonElement stateProp = default;
                if (root.TryGetProperty("game_state", out stateProp) || root.TryGetProperty("state", out stateProp))
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
                if (root.TryGetProperty("observation_summary", out var obsPropEl))
                {
                    observationSummary = obsPropEl.GetString();
                }

                string? objective = null;
                if (root.TryGetProperty("objective", out var objPropEl))
                {
                    objective = objPropEl.GetString();
                }

                string? decisionSummary = null;
                if (root.TryGetProperty("decision_summary", out var decPropEl))
                {
                    decisionSummary = decPropEl.GetString();
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

                    // Target as an object: parameters.target = { "x": 0.5, "y": 0.8, "name": "Button" }
                    if (paramsProp.TryGetProperty("target", out var targetObjElem) && targetObjElem.ValueKind == JsonValueKind.Object)
                    {
                        if (!x.HasValue && TryGetDouble(targetObjElem, "x", "x", out var tx)) x = tx;
                        if (!y.HasValue && TryGetDouble(targetObjElem, "y", "y", out var ty)) y = ty;
                        if (TryGetString(targetObjElem, "name", "name", out var tn)) target = tn;
                    }
                }

                // Root-level fallbacks if not found in parameters
                if (!x.HasValue && TryGetDouble(root, "x", "x", out var rootX)) x = rootX;
                if (!y.HasValue && TryGetDouble(root, "y", "y", out var rootY)) y = rootY;
                if (!endX.HasValue && TryGetDouble(root, "end_x", "endX", out var rootEndX)) endX = rootEndX;
                if (!endY.HasValue && TryGetDouble(root, "end_y", "endY", out var rootEndY)) endY = rootEndY;

                // Root-level target object: target = { "x": 0.5, "y": 0.8 }
                if (root.TryGetProperty("target", out var rootTargetElem) && rootTargetElem.ValueKind == JsonValueKind.Object)
                {
                    if (!x.HasValue && TryGetDouble(rootTargetElem, "x", "x", out var rx)) x = rx;
                    if (!y.HasValue && TryGetDouble(rootTargetElem, "y", "y", out var ry)) y = ry;
                    if (TryGetString(rootTargetElem, "name", "name", out var rn)) target = rn;
                }

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

                // Parse optional session_updates: { "initialization_complete": "true", "upgrade_check_done": "true", ... }
                IReadOnlyDictionary<string, string>? sessionUpdates = null;
                if (root.TryGetProperty("session_updates", out var sessionUpdatesProp) &&
                    sessionUpdatesProp.ValueKind == JsonValueKind.Object)
                {
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in sessionUpdatesProp.EnumerateObject())
                    {
                        var val = prop.Value.ValueKind switch
                        {
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            JsonValueKind.String => prop.Value.GetString() ?? "",
                            _ => prop.Value.GetRawText()
                        };
                        dict[prop.Name] = val;
                    }
                    if (dict.Count > 0)
                        sessionUpdates = dict;
                }

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
                    DecisionSummary = decisionSummary,
                    SessionUpdates = sessionUpdates
                };

                return true;

        }
        catch (Exception ex)
        {
            errorMessage = $"Unexpected parsing error: {ex.Message}";
            return false;
        }
    }

    private static List<string> ExtractJsonCandidates(string rawContent)
    {
        var candidates = new List<string>();
        if (string.IsNullOrWhiteSpace(rawContent)) return candidates;

        // 1. Strip <think>...</think> reasoning blocks
        var cleaned = Regex.Replace(rawContent, @"<think>[\s\S]*?(?:</think>|$)", "", RegexOptions.IgnoreCase).Trim();

        // 2. Extract JSON content inside markdown code blocks ```json ... ```
        var codeBlockMatches = Regex.Matches(cleaned, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
        foreach (Match match in codeBlockMatches)
        {
            var content = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(content) && !candidates.Contains(content))
            {
                candidates.Add(content);
            }
        }

        // 3. Scan for targeted balanced JSON object starting with action properties (e.g. {"action": ...)
        var actionKeyMatches = Regex.Matches(cleaned, @"\{(?=\s*""(?:action|action_type|next_action)"")", RegexOptions.IgnoreCase);
        foreach (Match m in actionKeyMatches)
        {
            var block = ExtractBalancedJson(cleaned, m.Index);
            if (!string.IsNullOrEmpty(block) && !candidates.Contains(block))
            {
                candidates.Insert(0, block);
            }
        }

        // 4. Scan for all top-level balanced { ... } blocks
        int depth = 0;
        int startIndex = -1;
        bool inString = false;
        bool escapeNext = false;

        for (int i = 0; i < cleaned.Length; i++)
        {
            char c = cleaned[i];
            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }
            if (c == '\\' && inString)
            {
                escapeNext = true;
                continue;
            }
            if (c == '"')
            {
                inString = !inString;
                continue;
            }
            if (inString) continue;

            if (c == '{')
            {
                if (depth == 0)
                {
                    startIndex = i;
                }
                depth++;
            }
            else if (c == '}' && depth > 0)
            {
                depth--;
                if (depth == 0 && startIndex >= 0)
                {
                    var block = cleaned.Substring(startIndex, i - startIndex + 1).Trim();
                    if (!string.IsNullOrEmpty(block) && !candidates.Contains(block))
                    {
                        candidates.Add(block);
                    }
                    startIndex = -1;
                }
            }
        }

        // 5. Fallback: extract outermost JSON object if LLM output had unbalanced outer wrapper
        var jsonObjMatch = Regex.Match(cleaned, @"\{[\s\S]*\}");
        if (jsonObjMatch.Success)
        {
            var outer = jsonObjMatch.Value.Trim();
            if (!candidates.Contains(outer))
            {
                candidates.Add(outer);
            }
        }

        // 6. Final fallback to cleaned text itself
        if (candidates.Count == 0)
        {
            candidates.Add(cleaned);
        }

        return candidates;
    }

    private static string? ExtractBalancedJson(string text, int startIndex)
    {
        int depth = 0;
        bool inString = false;
        bool escapeNext = false;
        for (int i = startIndex; i < text.Length; i++)
        {
            char c = text[i];
            if (escapeNext) { escapeNext = false; continue; }
            if (c == '\\' && inString) { escapeNext = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;

            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return text.Substring(startIndex, i - startIndex + 1).Trim();
                }
            }
        }
        return null;
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

