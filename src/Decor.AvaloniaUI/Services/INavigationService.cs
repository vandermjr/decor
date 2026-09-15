using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Decor.AvaloniaUI.Services;

public interface INavigationService
{
    T Resolve<T>() where T : class;
    Task ShowDialogAsync(Window owner, Window dialog);
}

public sealed class NavigationService(IServiceProvider services) : INavigationService
{
    public T Resolve<T>() where T : class => services.GetRequiredService<T>();

    public Task ShowDialogAsync(Window owner, Window dialog) => dialog.ShowDialog(owner);
}