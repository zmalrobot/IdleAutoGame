using System.Text.Json;
using FluentAssertions;
using IdleAutoGame.Application.Prompts;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Core.Prompts;
using IdleAutoGame.Games.TapTitans2;
using IdleAutoGame.Games.TapTitans2.Prompts;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Prompts;

public class ModularPromptTests
{
    [Fact]
    public void GenericMicroPrompts_ContainsAllRequiredModulesAndIsGameAgnostic()
    {
        var modules = GenericMicroPrompts.All;
        modules.Should().ContainKey("GENERIC_CORE");
        modules.Should().ContainKey("GENERIC_SAFETY");
        modules.Should().ContainKey("GENERIC_VISUAL_GROUNDING");
        modules.Should().ContainKey("GENERIC_STATE_CLASSIFIER");
        modules.Should().ContainKey("GENERIC_PRIORITY");
        modules.Should().ContainKey("GENERIC_ACTION_EXECUTOR");
        modules.Should().ContainKey("GENERIC_ACTION_VERIFIER");
        modules.Should().ContainKey("GENERIC_ANTI_STUCK");
        modules.Should().ContainKey("GENERIC_RESOURCE_CHECK");
        modules.Should().ContainKey("GENERIC_PURCHASE_POLICY");

        // Ensure generic modules do not contain game-specific leaks
        foreach (var (key, content) in modules)
        {
            content.Should().NotContain("Tap Titans", $"Generic module {key} must not mention Tap Titans");
            content.Should().NotContain("Sword Master", $"Generic module {key} must not mention Sword Master");
            content.Should().NotContain("Hand of Midas", $"Generic module {key} must not mention Hand of Midas");
        }
    }

    [Fact]
    public void TapTitans2MicroPrompts_ContainsAllRequiredModules()
    {
        var modules = TapTitans2MicroPrompts.All;
        modules.Should().ContainKey("TT2_INITIALIZATION");
        modules.Should().ContainKey("TT2_UPGRADE_TRIGGER");
        modules.Should().ContainKey("TT2_UPGRADE_CHECK");
        modules.Should().ContainKey("TT2_HERO_UPGRADE");
        modules.Should().ContainKey("TT2_BOSS");
        modules.Should().ContainKey("TT2_SKILLS");
        modules.Should().ContainKey("TT2_FAIRY");
        modules.Should().ContainKey("TT2_FARMING");
        modules.Should().ContainKey("TT2_UI_RULES");
        modules.Should().ContainKey("TT2_FORBIDDEN_AREAS");

        // Verify specific TT2 concepts exist in their respective modules
        modules["TT2_INITIALIZATION"].Should().Contain("Sword Master");
        modules["TT2_INITIALIZATION"].Should().Contain("Heroes");
        modules["TT2_HERO_UPGRADE"].Should().Contain("scroll");
        modules["TT2_BOSS"].Should().Contain("boss");
        modules["TT2_SKILLS"].Should().Contain("Hand of Midas");
        modules["TT2_FORBIDDEN_AREAS"].Should().Contain("Shop");
    }

    [Fact]
    public void SessionState_Transitions_WorkAsExpected()
    {
        var state = new SessionState();
        state.InitializationComplete.Should().BeFalse();
        state.UpgradeCheckDue.Should().BeTrue();
        state.FarmingBurstsSinceCheck.Should().Be(0);

        // Reset check
        state.ResetUpgradeCheck();
        state.UpgradeCheckDue.Should().BeFalse();
        state.FarmingBurstsSinceCheck.Should().Be(0);

        // Record farming bursts up to threshold
        state.RecordFarmingBurst(burstThreshold: 4);
        state.FarmingBurstsSinceCheck.Should().Be(1);
        state.UpgradeCheckDue.Should().BeFalse();

        state.RecordFarmingBurst(burstThreshold: 4);
        state.RecordFarmingBurst(burstThreshold: 4);
        state.UpgradeCheckDue.Should().BeFalse();

        state.RecordFarmingBurst(burstThreshold: 4);
        state.FarmingBurstsSinceCheck.Should().Be(4);
        state.UpgradeCheckDue.Should().BeTrue();

        // Boss defeat trigger
        state.ResetUpgradeCheck();
        state.RecordBossOutcome("defeated");
        state.LastBossResult.Should().Be("defeated");
        state.UpgradeCheckDue.Should().BeTrue();

        // Anti-stuck tracking
        state.RecordActionResult(false);
        state.LastActionSuccess.Should().BeFalse();
        state.StuckCount.Should().Be(1);

        state.RecordActionResult(false);
        state.StuckCount.Should().Be(2);

        state.RecordActionResult(true);
        state.LastActionSuccess.Should().BeTrue();
        state.StuckCount.Should().Be(0);
    }

    [Fact]
    public void SessionState_ToCompactJson_IncludesPolicyFlagsAndAllFields()
    {
        var state = new SessionState
        {
            InitializationComplete = true,
            UpgradeCheckDue = false,
            FarmingBurstsSinceCheck = 3,
            LastBossResult = "defeated",
            LastActionSuccess = true,
            StuckCount = 1,
            ActiveMenuTab = "heroes"
        };

        var json = state.ToCompactJson(premiumCurrencyEnabled: false, realMoneyPurchaseEnabled: false);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("initialization_complete").GetBoolean().Should().BeTrue();
        root.GetProperty("upgrade_check_due").GetBoolean().Should().BeFalse();
        root.GetProperty("farming_bursts_since_check").GetInt32().Should().Be(3);
        root.GetProperty("last_boss_result").GetString().Should().Be("defeated");
        root.GetProperty("last_action_success").GetBoolean().Should().BeTrue();
        root.GetProperty("stuck_count").GetInt32().Should().Be(1);
        root.GetProperty("active_menu_tab").GetString().Should().Be("heroes");
        root.GetProperty("premium_currency_enabled").GetBoolean().Should().BeFalse();
        root.GetProperty("real_money_purchase_enabled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void BuildModularSystemPrompt_InitialState_IncludesInitializationAndUpgradeModules()
    {
        var game = new TapTitans2Definition();
        var sessionState = new SessionState
        {
            InitializationComplete = false,
            UpgradeCheckDue = true
        };

        var prompt = PromptBuilder.BuildModularSystemPrompt(game, sessionState);

        prompt.Should().Contain("SYSTEM CORE & SAFETY");
        prompt.Should().Contain("The screenshot is the ONLY source of truth");
        prompt.Should().Contain("MANDATORY STARTUP WORKFLOW");
        prompt.Should().Contain("Sword Master");
        prompt.Should().Contain("Heroes");
        prompt.Should().Contain("Filter and reject touch coordinates that fall into forbidden commercial shop areas");
        prompt.Should().Contain("Output exactly one raw JSON object matching");
        // Farming should not be active during initial initialization
        prompt.Should().NotContain("Tap Titans 2 Titan farming");
    }

    [Fact]
    public void BuildModularSystemPrompt_FarmingState_IncludesCombatModules()
    {
        var game = new TapTitans2Definition();
        var sessionState = new SessionState
        {
            InitializationComplete = true,
            UpgradeCheckDue = false,
            FarmingBurstsSinceCheck = 1
        };

        var prompt = PromptBuilder.BuildModularSystemPrompt(game, sessionState);

        prompt.Should().Contain("SYSTEM CORE & SAFETY");
        prompt.Should().Contain("Execute standard combat farming on normal titans");
        prompt.Should().Contain("Evaluate when a full upgrade check is required in Tap Titans 2");
        prompt.Should().Contain("Manage boss encounters in Tap Titans 2");
        prompt.Should().Contain("Visually identify skill readiness and decide skill activations");
        prompt.Should().Contain("Collect free flying fairies while avoiding premium/ad offers");
        prompt.Should().NotContain("MANDATORY STARTUP WORKFLOW");
    }

    [Fact]
    public void BuildModularSystemPrompt_WhenStuck_IncludesAntiStuckModule()
    {
        var game = new TapTitans2Definition();
        var sessionState = new SessionState
        {
            InitializationComplete = true,
            StuckCount = 3
        };

        var prompt = PromptBuilder.BuildModularSystemPrompt(game, sessionState);

        prompt.Should().Contain("ACTIVE STRATEGY: RECOVERY / ANTI-STUCK");
        prompt.Should().Contain("You resolve stalled states when actions produce no visible effect");
    }

    [Fact]
    public void BuildModularSystemPrompt_ReflectsSpendingAuthorizationFlags()
    {
        var game = new TapTitans2Definition();
        var sessionState = new SessionState();
        var policy = new GamePolicy
        {
            AllowPremiumCurrency = true,
            AllowCreditPurchases = false
        };

        var prompt = PromptBuilder.BuildModularSystemPrompt(game, sessionState, policy: policy);

        prompt.Should().Contain("Premium currency usage: ENABLED");
        prompt.Should().Contain("Credit / real-money purchases: DISABLED");
    }

    [Fact]
    public void BuildUserPrompt_IncludesCompactSessionState()
    {
        var sessionState = new SessionState
        {
            InitializationComplete = true,
            UpgradeCheckDue = true,
            FarmingBurstsSinceCheck = 4
        };

        var userPrompt = PromptBuilder.BuildUserPrompt(
            cycleNumber: 5,
            elapsed: TimeSpan.FromSeconds(30),
            previousAction: null,
            userOverrides: null,
            sessionState: sessionState,
            allowPremiumCurrency: false,
            allowCreditPurchases: false);

        userPrompt.Should().Contain("SESSION STATE:");
        userPrompt.Should().Contain("\"initialization_complete\":true");
        userPrompt.Should().Contain("\"upgrade_check_due\":true");
        userPrompt.Should().Contain("\"farming_bursts_since_check\":4");
    }

    [Fact]
    public void TapTitans2MicroPrompts_SupportsItalianLocalizationTerms()
    {
        var modules = TapTitans2MicroPrompts.All;

        // Boss module Italian terms & critical safety retreat prevention
        modules["TT2_BOSS"].Should().Contain("COMBATTI IL BOSS");
        modules["TT2_BOSS"].Should().Contain("ABBANDONA LA BATTAGLIA");

        // Hero upgrade module Italian button keywords
        modules["TT2_HERO_UPGRADE"].Should().Contain("Arruola");
        modules["TT2_HERO_UPGRADE"].Should().Contain("Livello successivo");

        // Upgrade check module Italian labels and spell headers
        modules["TT2_UPGRADE_CHECK"].Should().Contain("Livello successivo");
        modules["TT2_UPGRADE_CHECK"].Should().Contain("Incantesimi");
        modules["TT2_UPGRADE_CHECK"].Should().Contain("Guarda Un Video");

        // Skill names in Italian
        modules["TT2_SKILLS"].Should().Contain("Attacco celestiale");
        modules["TT2_SKILLS"].Should().Contain("Colpo Mortale");
        modules["TT2_SKILLS"].Should().Contain("Grido di Guerra");
        modules["TT2_SKILLS"].Should().Contain("Mano di Mida");
        modules["TT2_SKILLS"].Should().Contain("Clone d'ombra");

        // UI rules Italian keywords & critical button distinction
        modules["TT2_UI_RULES"].Should().Contain("Arruola");
        modules["TT2_UI_RULES"].Should().Contain("Livello successivo");
        modules["TT2_UI_RULES"].Should().Contain("COMBATTI IL BOSS");
        modules["TT2_UI_RULES"].Should().Contain("ABBANDONA LA BATTAGLIA");
        modules["TT2_UI_RULES"].Should().Contain("Guarda Un Video");
        modules["TT2_UI_RULES"].Should().Contain("Raccogli!");
    }
}
