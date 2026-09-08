using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Infrastructure.Data;
using Decor.Infrastructure.Data.Repositories;
using Decor.Infrastructure.Data.Utils;
using Decor.Infrastructure.Services;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Decor.Infrastructure.CrossCutting.IoC;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MariaDBConnector");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException($"Erro - A conexão do banco de dados não foi configurada corretamente.");
        }

        services.AddSingleton<IDatabaseConnection>(sp => new DatabaseConnection(connectionString));
        services.AddSingleton<IDialect, MariaDBDialect>();
        services.AddTransient(sp => FluentCommandBuilder.Create(sp.GetRequiredService<IDialect>()));
        services.AddTransient<Func<FluentCommandBuilder>>(sp => () => sp.GetRequiredService<FluentCommandBuilder>());
        services.AddSingleton<IUserSettingsService, JsonUserSettingsService>();
        services.AddTransient<IDatabaseBackupService, DatabaseBackupService>();

        // Como o QueryContext mantém estado por requisição (ou por operação de query),
        // ele deve ser registrado como Transient.
        // 1. REGISTRO DO IQueryContext (Esta linha é crucial e deve vir primeiro)
        services.AddTransient<IQueryContext, QueryContext>();

        // 2. REGISTRO DA FÁBRICA Func<IQueryContext> (Esta linha depende do registro acima)
        services.AddTransient<Func<IQueryContext>>(sp => () => sp.GetRequiredService<IQueryContext>());

        // REGISTRO AUTOMÁTICO DOS REPOSITÓRIOS
        services.Scan(scan => scan
            .FromAssemblies(typeof(IProductRepository).Assembly, typeof(ProductRepository).Assembly)
            .AddClasses(classes => classes.Where(c => c.Name.EndsWith("Repository")))
            .AsMatchingInterface()
            .WithTransientLifetime()
        );

        services.AddTransient<IBrandRepository, BrandsRepository>();

        return services;
    }
}