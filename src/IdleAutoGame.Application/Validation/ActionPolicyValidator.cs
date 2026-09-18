using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Validation;

/// <summary>
/// Enforces mandatory security policies (AllowPremiumCurrency, AllowCreditPurchases)
/// on LLM decisions before ADB execution. Implements defense-in-depth:
/// 1. Action Category verification
/// 2. Spatial bounding-box forbidden regions
/// 3. Heuristic content analysis against evasive prompts or circumvention attempts
/// 4. Fail-Safe defaults
/// </summary>
public static class ActionPolicyValidator
{
    private static readonly string[] PremiumKeywords =
    [
        "diamond", "diamonds", "gem", "gems", "premium currency", "spend diamond", "spend gem"
    ];

    private static readonly string[] CreditPurchaseKeywords =
    [
        "buy credit", "credit purchase", "real money", "in-app purchase", "iap",
        "buy pack", "google play billing", "checkout", "subscription"
    ];

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

        // 3. Defense-in-depth Heuristics: detect bypass attempts or prompt deviations
        if (!policy.AllowPremiumCurrency && ContainsAnyKeyword(action.Explanation, PremiumKeywords))
        {
            result.AddError("Policy violation [Heuristic]: Action explanation indicates intent to spend premium currency while policy is disabled.");
        }

        if (!policy.AllowCreditPurchases && ContainsAnyKeyword(action.Explanation, CreditPurchaseKeywords))
        {
            result.AddError("Policy violation [Heuristic]: Action explanation indicates intent to perform credit/real-money purchase while policy is disabled.");
        }

        return result;
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

    private static bool ContainsAnyKeyword(string text, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        foreach (var kw in keywords)
        {
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
