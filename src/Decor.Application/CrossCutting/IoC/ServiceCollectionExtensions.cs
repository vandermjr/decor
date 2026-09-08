using Decor.Application.Services;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Decor.Application.CrossCutting.IoC;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.Scan(scan => scan
            // Escaneia os projetos Core e AppLogic
            .FromAssemblies(typeof(IProductService).Assembly, typeof(ProductService).Assembly)

                // REGRA 1: Registra todos os Serviços
                .AddClasses(classes => classes.Where(c => c.Name.EndsWith("Service")))
                    .AsMatchingInterface()
                    .WithTransientLifetime()

                // REGRA 2: Registra os validadores de DTO
                .AddClasses(classes => classes.AssignableTo(typeof(IDTOValidator<>)))
                    .AsImplementedInterfaces()
                    .WithTransientLifetime()

                // REGRA 3: Registra os validadores de Repositório
                .AddClasses(classes => classes.AssignableTo(typeof(IRepositoryValidator<>)))
                    .AsImplementedInterfaces()
                    .WithTransientLifetime()
        );

        // A injeção do IThemeService permanece manual pois é Singleton
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IAuthenticatedUserContext, AuthenticatedUserContext>();
        services.AddSingleton<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IPasswordPolicy, PasswordPolicy>();

        return services;
    }
}
