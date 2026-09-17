using System.Threading.Tasks;
using IdleAutoGame.Presentation.Services;

namespace IdleAutoGame.Tests.Unit.Fakes;

public class FakeClipboardService : IClipboardService
{
    public string? Text { get; set; }

    public Task SetTextAsync(string? text)
    {
        Text = text;
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync()
    {
        return Task.FromResult(Text);
    }
}
