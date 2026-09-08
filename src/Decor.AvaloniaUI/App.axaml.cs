using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Decor.AvaloniaUI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Decor.AvaloniaUI;

public partial class App : Avalonia.Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        _services = services;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = _services.GetRequiredService<LoginWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
