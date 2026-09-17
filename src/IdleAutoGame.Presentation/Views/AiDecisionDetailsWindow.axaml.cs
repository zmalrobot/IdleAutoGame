using Avalonia.Controls;
using IdleAutoGame.Presentation.ViewModels;

namespace IdleAutoGame.Presentation.Views;

public partial class AiDecisionDetailsWindow : Window
{
    public AiDecisionDetailsWindow()
    {
        InitializeComponent();

        var tb = this.FindControl<TextBox>("RawOutputTextBox");
        if (tb != null)
        {
            tb.PropertyChanged += (s, e) =>
            {
                if (e.Property == TextBox.TextProperty &&
                    DataContext is AiDecisionDetailsViewModel vm &&
                    vm.AutoScrollEnabled)
                {
                    tb.CaretIndex = tb.Text?.Length ?? 0;
                }
            };
        }
    }
}
