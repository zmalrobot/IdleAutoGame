using System.Text.RegularExpressions;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Validation;

/// <summary>
/// Enforces mandatory security policies (AllowPremiumCurrency, AllowCreditPurchases)
/// on LLM decisions before ADB execution. Implements defense-in-depth:
/// 1. Action Category verification
/// 2. Spatial bounding-box forbidden regions
/// 3. Negation-aware heuristic content analysis against evasive prompts or circumvention attempts
/// 4. Fail-Safe defaults
/// </summary>
public static partial class ActionPolicyValidator
{
    // Regex matching affirmative intent to spend premium currency
    [GeneratedRegex(
        @"\b(?:spend|spending|spent|consume|consuming|consumed|pay|paying|paid|buy\s+with|buying\s+with|purchase\s+with|purchasing\s+with|unlock\s+with|unlocking\s+with|use|using)\s+(?:[a-z0-9_\$#]+\s+){0,3}(?:diamond|diamonds|gem|gems|premium\s+currency|valuta\s+premium|diamant[ie]|gemm[ae])\b" +
        @"|\b(?:diamond|gem|premium\s+currency)\s+(?:pack\s+purchase|purchase|checkout)\b" +
        @"|\b(?:spend\s+diamond|spend\s+gem|spend\s+premium)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex PremiumIntentRegex();

    // Regex matching negation or policy-compliance patterns around premium currency
    [GeneratedRegex(
        @"\b(?:no|not|never|without|avoid|avoiding|avoided|skip|skipping|skipped|decline|declining|declined|refuse|refusing|refused|deny|denying|denied|neither|nor|zero|free|non|senza|evita|evitando|evitare)\s+(?:[a-z0-9_\$#-]+\s+){0,4}(?:diamond|diamonds|gem|gems|premium\s+currency|spend|spending|spent|use|using|buy|buying|purchase|purchasing|pay|paying|valuta\s+premium|diamant[ie]|gemm[ae])\b" +
        @"|\b(?:diamond|diamonds|gem|gems|premium\s+currency|valuta\s+premium)\s+(?:[a-z0-9_\$#-]+\s+){0,4}(?:is\s+disabled|are\s+disabled|disabled|forbidden|prohibited|not\s+allowed|not\s+permitted|turned\s+off|disabilitat\w*|vietat\w*)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex PremiumNegationRegex();

    // Regex matching affirmative intent to make credit / real-money purchases
    [GeneratedRegex(
        @"\b(?:buy|buying|bought|purchase|purchasing|purchased|spend|spending|spent|pay|paying|paid)\s+(?:[a-z0-9_\$#-]+\s+){0,3}(?:with\s+)?real\s*[-]?\s*money\b" +
        @"|\b(?:buy|buying|bought|purchase|purchasing|purchased)\s+(?:[a-z0-9_\$#-]+\s+){0,3}credit\b" +
        @"|\b(?:credit|real\s*[-]?\s*money)\s+purchase\b" +
        @"|\b(?:in\s*[-]?\s*app\s+purchase|iap\s+(?:purchase|checkout)|google\s+play\s+billing|checkout\s+store|subscription\s+purchase)\b" +
        @"|\bbuy\s+pack\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex CreditPurchaseIntentRegex();

    // Regex matching negation or policy-compliance patterns around credit / real-money purchases
    [GeneratedRegex(
        @"\b(?:no|not|never|without|avoid|avoiding|avoided|skip|skipping|skipped|decline|declining|declined|refuse|refusing|refused|neither|nor|zero|free|non|senza|evita|evitando)\s+(?:[a-z0-9_\$#-]+\s+){0,4}(?:credit|real\s*[-]?\s*money|in\s*[-]?\s*app|iap|pack|purchase|purchases|buy|buying|checkout|billing)\b" +
        @"|\b(?:credit|real\s*[-]?\s*money|in\s*[-]?\s*app\s+purchase|iap)\s+(?:[a-z0-9_\$#-]+\s+){0,4}(?:is\s+disabled|are\s+disabled|disabled|forbidden|prohibited|not\s+allowed|not\s+permitted|turned\s+off|disabilitat\w*|vietat\w*)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex CreditPurchaseNegationRegex();

    /// <summary>
    /// Validates an action against the active security policy.
    /// </summary>
    public static ValidationResult Validate(
        GameAction action,
        GamePolicy? policy,
        IEnumerable<GameConstraint>? constraints = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        // Fail-safe: null or missing policy defaults to strict Deny By Default
        policy ??= GamePolicy.Default;

        var result = new ValidationResult();

        // 1. Action Category Validation
        if (!Enum.IsDefined(typeof(ActionCategory), action.Category))
        {
            result.AddError("Policy violation: Unknown or invalid ActionCategory. Blocked by fail-safe policy.");
            return result;
        }

        if (action.Category == ActionCategory.PremiumCurrency && !policy.AllowPremiumCurrency)
        {
            result.AddError("Policy violation: Premium currency usage is DISABLED (AllowPremiumCurrency = false).");
        }

        if (action.Category == ActionCategory.CreditPurchase && !policy.AllowCreditPurchases)
        {
            result.AddError("Policy violation: Credit / in-app purchases are DISABLED (AllowCreditPurchases = false).");
        }

        // 2. Spatial Constraints linked to premium/shop areas
        if (constraints != null)
        {
            foreach (var c in constraints.Where(c => c.Type == ConstraintType.ForbiddenRegion))
            {
                if (IsPremiumOrShopConstraint(c))
                {
                    bool isShopRestricted = (!policy.AllowCreditPurchases && IsShopConstraint(c)) ||
                                            (!policy.AllowPremiumCurrency && IsPremiumConstraint(c));

                    if (isShopRestricted && IsActionWithinConstraint(action, c))
                    {
                        result.AddError($"Policy violation [{c.Id}]: Action targets forbidden region (restricted store/currency region while policy is disabled). {c.Description}");
                    }
                }
            }
        }

        // 3. Defense-in-depth Heuristics: detect affirmative circumvention attempts
        var textsToInspect = new[] { action.Explanation, action.DecisionSummary, action.Objective, action.Parameters?.Target };
        foreach (var text in textsToInspect)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;

            if (!policy.AllowPremiumCurrency && IndicatesIntentToSpendPremiumCurrency(text))
            {
                result.AddError("Policy violation [Heuristic]: Action explanation indicates intent to spend premium currency while policy is disabled.");
                break;
            }

            if (!policy.AllowCreditPurchases && IndicatesIntentToMakeCreditPurchase(text))
            {
                result.AddError("Policy violation [Heuristic]: Action explanation indicates intent to perform credit/real-money purchase while policy is disabled.");
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Evaluates whether text affirmatively indicates an intent to spend premium currency,
    /// properly distinguishing spend actions from negations, policy acknowledgments, or observations.
    /// </summary>
    public static bool IndicatesIntentToSpendPremiumCurrency(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var match = PremiumIntentRegex().Match(text);
        if (!match.Success) return false;

        return !IsMatchNegated(text, match.Index, match.Length, PremiumNegationRegex());
    }

    /// <summary>
    /// Evaluates whether text affirmatively indicates an intent to make credit / real-money purchases,
    /// properly distinguishing purchase actions from negations, policy acknowledgments, or observations.
    /// </summary>
    public static bool IndicatesIntentToMakeCreditPurchase(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var match = CreditPurchaseIntentRegex().Match(text);
        if (!match.Success) return false;

        return !IsMatchNegated(text, match.Index, match.Length, CreditPurchaseNegationRegex());
    }

    private static bool IsMatchNegated(string text, int matchIndex, int matchLength, Regex negationRegex)
    {
        // Extract the enclosing clause or sentence around the match
        int start = Math.Max(0, text.LastIndexOfAny(['.', ';', '!', '?', '\n'], matchIndex));
        if (start > 0 && text[start] is '.' or ';' or '!' or '?' or '\n') start++;

        int end = text.IndexOfAny(['.', ';', '!', '?', '\n'], matchIndex + matchLength);
        if (end < 0) end = text.Length;

        string clause = text[start..end].Trim();
        return negationRegex.IsMatch(clause);
    }

    private static bool IsPremiumOrShopConstraint(GameConstraint constraint)
    {
        var id = constraint.Id.ToUpperInvariant();
        return id.Contains("SHOP") || id.Contains("STORE") || id.Contains("DIAMOND") || id.Contains("GEM") ||
               id.Contains("PREMIUM") || id.Contains("PROMO") || id.Contains("OFFER") || id.Contains("BUNDLE");
    }

    private static bool IsShopConstraint(GameConstraint constraint)
    {
        var id = constraint.Id.ToUpperInvariant();
        return id.Contains("SHOP") || id.Contains("STORE") || id.Contains("PURCHASE") ||
               id.Contains("PROMO") || id.Contains("OFFER") || id.Contains("BUNDLE");
    }

    private static bool IsPremiumConstraint(GameConstraint constraint)
    {
        var id = constraint.Id.ToUpperInvariant();
        return id.Contains("DIAMOND") || id.Contains("GEM") || id.Contains("PREMIUM") ||
               id.Contains("PROMO") || id.Contains("OFFER") || id.Contains("BUNDLE");
    }

    private static bool IsActionWithinConstraint(GameAction action, GameConstraint constraint)
    {
        if (constraint.Parameters is not NormalizedRect rect) return false;
        var p = action.Parameters;

        if (action.Action is ActionType.Tap or ActionType.MultiTap or ActionType.DoubleTap or ActionType.LongPress)
        {
            return p.X.HasValue && p.Y.HasValue && rect.Contains(p.X.Value, p.Y.Value);
        }

        if (action.Action is ActionType.Swipe or ActionType.Drag)
        {
            bool startHit = p.X.HasValue && p.Y.HasValue && rect.Contains(p.X.Value, p.Y.Value);
            bool endHit = p.EndX.HasValue && p.EndY.HasValue && rect.Contains(p.EndX.Value, p.EndY.Value);
            return startHit || endHit;
        }

        return false;
    }
}
