using Avalonia;
using Decor.Application.CrossCutting.IoC;
using Decor.Core.Configuration;
using Decor.AvaloniaUI.ViewModels;
using Decor.AvaloniaUI.Views;
using Decor.Infrastructure.CrossCutting.IoC;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Decor.AvaloniaUI;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var host = BuildHost(args);

        BuildAvaloniaApp(host.Services)
            .StartWithClassicDesktopLifetime(args);
    }

    private static IHost BuildHost(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((context, services) =>
            {
                services.Configure<ApplicationSettings>(
                    context.Configuration.GetSection(
                        ConfigurationKeys.ApplicationSettingsSection));

                services.AddInfrastructureServices(context.Configuration);
                services.AddApplicationServices();
                services.AddTransient<ProductsViewModel>();
                services.AddTransient<BrandsViewModel>();
                services.AddTransient<TermDeliveryViewModel>();
                services.AddTransient<ClassificationsViewModel>();
                services.AddTransient<UsersViewModel>();
                services.AddTransient<CreateUserViewModel>();
                services.AddTransient<DatabaseMaintenanceViewModel>();
                services.AddTransient<ProductsView>();
                services.AddTransient<BrandsView>();
                services.AddTransient<TermDeliveryView>();
                services.AddTransient<ClassificationsView>();
                services.AddTransient<UsersView>();
                services.AddTransient<DatabaseMaintenanceView>();
                services.AddTransient<CreateUserWindow>();
                services.AddTransient<MainViewModel>();
                services.AddTransient<LoginViewModel>();
                services.AddTransient<LoginWindow>();
                services.AddTransient<ChangePasswordViewModel>();
                services.AddTransient<ChangePasswordWindow>();
                services.AddTransient<MainWindow>();
            })
            .Build();

    private static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .LogToTrace();
}
