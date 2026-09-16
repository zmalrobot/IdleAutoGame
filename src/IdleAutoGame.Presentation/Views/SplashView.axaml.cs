using Avalonia.Controls;
using IdleAutoGame.Presentation.ViewModels;

namespace IdleAutoGame.Presentation.Views;

public partial class SplashView : UserControl
{
    public SplashView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is SplashViewModel vm && !vm.IsCompleted)
            {
                await vm.RunPreflightCheckAsync();
            }
        };
    }
}

