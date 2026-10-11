using Decor.Application.CrossCutting.IoC;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Infrastructure.CrossCutting.IoC;
using Decor.Infrastructure.Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Decor.Application.Tests;

public sealed class QuoteDependencyInjectionTests
{
    [Fact]
    public void Quote_editor_and_order_dependencies_resolve_without_opening_a_database_connection()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MariaDBConnector"] = "Server=localhost;Database=decor_test;User ID=test;Password=test"
        }).Build();
        var services = new ServiceCollection();
        services.AddInfrastructureServices(configuration);
        services.AddApplicationServices();
        services.AddSingleton(Mock.Of<IAuthorizationService>());
        services.AddSingleton(Mock.Of<IAuthenticatedUserContext>());
        services.AddTransient<QuotesViewModel>();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<PaymentMethodsRepository>(provider.GetRequiredService<IPaymentMethodRepository>());
        Assert.NotNull(provider.GetRequiredService<IOrderService>());
        Assert.NotNull(provider.GetRequiredService<QuotesViewModel>());
    }
}