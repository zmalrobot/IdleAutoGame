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

    public async IAsyncEnumerable<LlmOutputChunk> StreamAnalyzeAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var inferenceId = Guid.NewGuid().ToString("N");
        yield return new LlmOutputChunk
        {
            InferenceId = inferenceId,
            State = LlmStreamState.Preparing,
            ChunkIndex = 0
        };

        yield return new LlmOutputChunk
        {
            InferenceId = inferenceId,
            State = LlmStreamState.Inferring,
            ChunkIndex = 1
        };

        var response = await AnalyzeAsync(request, ct).ConfigureAwait(false);
        var raw = response.RawContent ?? string.Empty;

        // Emit simulated chunks (e.g. 2 pieces) if content is present
        if (raw.Length > 0)
        {
            int mid = raw.Length / 2;
            var chunk1 = raw[..mid];
            var chunk2 = raw[mid..];

            yield return new LlmOutputChunk
            {
                InferenceId = inferenceId,
                DeltaText = chunk1,
                AccumulatedText = chunk1,
                ChunkIndex = 2,
                State = LlmStreamState.Streaming,
                TotalTokensSoFar = 10,
                TokensPerSecond = 20.0,
                ElapsedMs = 25
            };

            yield return new LlmOutputChunk
            {
                InferenceId = inferenceId,
                DeltaText = chunk2,
                AccumulatedText = raw,
                ChunkIndex = 3,
                State = LlmStreamState.Streaming,
                TotalTokensSoFar = 20,
                TokensPerSecond = 25.0,
                ElapsedMs = 50
            };
        }

        yield return new LlmOutputChunk
        {
            InferenceId = inferenceId,
            DeltaText = string.Empty,
            AccumulatedText = raw,
            ChunkIndex = 4,
            State = response.IsSuccess ? LlmStreamState.Completed : LlmStreamState.Failed,
            Error = response.Error,
            TotalTokensSoFar = 20,
            TokensPerSecond = 25.0,
            ElapsedMs = 50,
            FinalResponse = response
        };
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<ModelCapabilities> GetCapabilitiesAsync(CancellationToken ct = default) =>
        Task.FromResult(new ModelCapabilities { SupportsStreaming = true });
}

