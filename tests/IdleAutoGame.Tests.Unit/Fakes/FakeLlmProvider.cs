using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Tests.Unit.Fakes;

public class FakeLlmProvider : ILlmProvider
{
    public string ProviderId => "fake-llm";

    public Queue<LlmResponse> NextResponses { get; } = new();
    public List<LlmRequest> CapturedRequests { get; } = new();

    public void EnqueueResponse(GameAction action)
    {
        NextResponses.Enqueue(new LlmResponse
        {
            IsSuccess = true,
            RawContent = "{}",
            ParsedAction = action,
            LatencyMs = 50
        });
    }

    public Task<LlmResponse> AnalyzeAsync(LlmRequest request, CancellationToken ct = default)
    {
        CapturedRequests.Add(request);

        if (NextResponses.Count > 0)
        {
            return Task.FromResult(NextResponses.Dequeue());
        }

        // Default valid tap action
        return Task.FromResult(new LlmResponse
        {
            IsSuccess = true,
            RawContent = "{\"action\":\"tap\",\"parameters\":{\"x\":0.5,\"y\":0.5},\"explanation\":\"Default tap\",\"confidence\":1.0,\"game_state\":\"normal\"}",
            ParsedAction = new GameAction
            {
                Action = ActionType.Tap,
                Parameters = new ActionParameters { X = 0.5, Y = 0.5 },
                Explanation = "Default tap",
                Confidence = 1.0,
                GameState = GameStateAssessment.Normal
            },
            LatencyMs = 50
        });
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<ModelCapabilities> GetCapabilitiesAsync(CancellationToken ct = default) =>
        Task.FromResult(new ModelCapabilities());
}

