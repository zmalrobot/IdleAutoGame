using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IdleAutoGame.Presentation.ViewModels;

/// <summary>
/// Elemento visualizzatore ed editor reattivo per un singolo micro-prompt modulare.
/// Fornisce metadati, conteggio caratteri/token stimati e tracciamento dello stato di modifica rispetto al default.
/// </summary>
public partial class MicroPromptEditorItemViewModel : ObservableObject
{
    public string Key { get; }
    public string Name { get; }
    public string Category { get; }
    public string Group { get; }
    public string Description { get; }
    public string DefaultContent { get; }

    private string _content;

    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value))
            {
                OnPropertyChanged(nameof(IsModified));
                OnPropertyChanged(nameof(StatusBadge));
                OnPropertyChanged(nameof(StatusBadgeColor));
                OnPropertyChanged(nameof(CharCount));
                OnPropertyChanged(nameof(EstimatedTokens));
            }
        }
    }

    public bool IsModified => !string.Equals(Content, DefaultContent, StringComparison.Ordinal);

    public string StatusBadge => IsModified ? "Personalizzato" : "Predefinito";

    public string StatusBadgeColor => IsModified ? "#CE9178" : "#4EC9B0";

    public int CharCount => Content?.Length ?? 0;

    public int EstimatedTokens => (int)Math.Ceiling((Content?.Length ?? 0) / 4.0);

    public MicroPromptEditorItemViewModel(
        string key,
        string name,
        string category,
        string group,
        string description,
        string defaultContent,
        string? customContent = null)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Name = name ?? key;
        Category = category ?? "Generico";
        Group = group ?? "Altro";
        Description = description ?? string.Empty;
        DefaultContent = defaultContent ?? string.Empty;
        _content = !string.IsNullOrWhiteSpace(customContent) ? customContent : DefaultContent;
    }

    /// <summary>
    /// Ripristina il contenuto del micro-prompt al testo originale predefinito.
    /// </summary>
    public void ResetToDefault()
    {
        Content = DefaultContent;
    }
}
