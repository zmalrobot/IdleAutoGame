using FluentAssertions;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Events;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Persistence;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Security;

public class GamePolicyTests
{
    [Fact]
    public void DefaultPolicy_AdheresTo_DenyByDefault()
    {
        var policy = GamePolicy.Default;

        policy.AllowPremiumCurrency.Should().BeFalse();
        policy.AllowCreditPurchases.Should().BeFalse();
    }

    [Fact]
    public void PremiumCurrency_WhenDisabled_BlocksPremiumActionCategory()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.PremiumCurrency,
            Explanation = "Opening premium chest",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5 }
        };

        var policy = new GamePolicy { AllowPremiumCurrency = false, AllowCreditPurchases = false };
        var result = ActionPolicyValidator.Validate(action, policy);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Premium currency usage is DISABLED"));
    }

    [Fact]
    public void PremiumCurrency_WhenEnabled_AllowsPremiumActionCategory()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.PremiumCurrency,
            Explanation = "Opening premium chest",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5 }
        };

        var policy = new GamePolicy { AllowPremiumCurrency = true, AllowCreditPurchases = false };
        var result = ActionPolicyValidator.Validate(action, policy);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreditPurchases_WhenDisabled_BlocksCreditPurchaseCategory()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.CreditPurchase,
            Explanation = "Purchasing credit pack",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5 }
        };

        var policy = new GamePolicy { AllowPremiumCurrency = true, AllowCreditPurchases = false };
        var result = ActionPolicyValidator.Validate(action, policy);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Credit / in-app purchases are DISABLED"));
    }

    [Fact]
    public void CreditPurchases_WhenEnabled_AllowsCreditPurchaseCategory()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.CreditPurchase,
            Explanation = "Purchasing pack",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5 }
        };

        var policy = new GamePolicy { AllowPremiumCurrency = true, AllowCreditPurchases = true };
        var result = ActionPolicyValidator.Validate(action, policy);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MissingPolicy_FallsBackSafelyTo_DenyByDefault()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.PremiumCurrency,
            Explanation = "Buy with diamonds",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5 }
        };

        var result = ActionPolicyValidator.Validate(action, policy: null);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Premium currency usage is DISABLED"));
    }

    [Fact]
    public void HeuristicCheck_DetectsPremiumKeywordCircumvention()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.Normal, // Attempted to disguise as normal
            Explanation = "Tapping to spend diamond pack",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5 }
        };

        var policy = new GamePolicy { AllowPremiumCurrency = false, AllowCreditPurchases = false };
        var result = ActionPolicyValidator.Validate(action, policy);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Action explanation indicates intent to spend premium currency"));
    }

    [Fact]
    public void HeuristicCheck_DetectsCreditPurchaseKeywordCircumvention()
    {
        var action = new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.Normal, // Attempted to disguise as normal
            Explanation = "Tapping buy credit button with real money",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5 }
        };

        var policy = new GamePolicy { AllowPremiumCurrency = true, AllowCreditPurchases = false };
        var result = ActionPolicyValidator.Validate(action, policy);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Action explanation indicates intent to perform credit/real-money purchase"));
    }

    [Fact]
    public void SpatialCheck_BlocksForbiddenShopRegion_WhenPurchasesDisabled()
    {
        var constraint = new GameConstraint(
            Id: "TT2-FORBIDDEN-SHOP-TOP",
            Description: "Shop region",
            Type: ConstraintType.ForbiddenRegion,
            Parameters: new NormalizedRect(0.8, 0.0, 0.2, 0.2));

        var action = new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.Normal,
            Explanation = "Normal gameplay tap",
            Parameters = new ActionParameters { X = 0.9, Y = 0.1 }
        };

        var policy = new GamePolicy { AllowPremiumCurrency = false, AllowCreditPurchases = false };
        var result = ActionPolicyValidator.Validate(action, policy, [constraint]);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("TT2-FORBIDDEN-SHOP-TOP"));
    }

    [Fact]
    public async Task GamePolicyService_UpdatePolicyAsync_PersistsAndNotifies()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"policy_test_{Guid.NewGuid():N}.json");
        var repo = new JsonSettingsRepository(tempFile);
        var configService = new ConfigurationService(repo, new SettingsValidator());
        await configService.InitializeAsync();

        var service = new GamePolicyService(configService);
        GamePolicyChangedEvent? receivedEvent = null;
        service.PolicyChanged += (s, e) => receivedEvent = e;

        await service.UpdatePolicyAsync("tap-titans-2", allowPremiumCurrency: true, allowCreditPurchases: false);

        service.CurrentPolicy.AllowPremiumCurrency.Should().BeTrue();
        service.CurrentPolicy.AllowCreditPurchases.Should().BeFalse();
        receivedEvent.Should().NotBeNull();
        receivedEvent!.NewPolicy.AllowPremiumCurrency.Should().BeTrue();

        // Verify persisted
        var effective = service.GetEffectivePolicy("tap-titans-2");
        effective.AllowPremiumCurrency.Should().BeTrue();
        effective.AllowCreditPurchases.Should().BeFalse();

        if (File.Exists(tempFile)) File.Delete(tempFile);
    }
}

