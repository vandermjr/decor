using Decor.Application.Services;
using Decor.Application.Validation;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Decor.Infrastructure.Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Decor.ServiceCatalog.Tests;

public sealed class ServiceCatalogInfrastructureTests
{
    [Fact]
    public void ExistingScanning_RegistersCatalogAndValidatorsWithoutOpeningDatabase()
    {
        var services = new ServiceCollection();
        Decor.Application.CrossCutting.IoC.ServiceCollectionExtensions.AddApplicationServices(services);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MariaDBConnector"] = "Server=localhost;Database=decor;User ID=test;Password=test"
        }).Build();
        Decor.Infrastructure.CrossCutting.IoC.ServiceCollectionExtensions.AddInfrastructureServices(services, configuration);
        Assert.Contains(services, registration => registration.ServiceType == typeof(IServiceCatalogService) && registration.ImplementationType == typeof(ServiceCatalogService));
        Assert.Contains(services, registration => registration.ServiceType == typeof(IServiceRepository) && registration.ImplementationType == typeof(ServiceRepository));
        Assert.Contains(services, registration => registration.ServiceType == typeof(IDTOValidator<Decor.Core.DTOs.ServiceDTO>) && registration.ImplementationType == typeof(ServiceDTOValidator));
        Assert.Contains(services, registration => registration.ServiceType == typeof(IRepositoryValidator<Service>) && registration.ImplementationType == typeof(ServiceRepositoryValidator));
        using var provider = services.BuildServiceProvider();
        Assert.IsType<ServiceCatalogService>(provider.GetRequiredService<IServiceCatalogService>());
    }

    [Theory]
    [InlineData("save")]
    [InlineData("delete")]
    [InlineData("search")]
    public async Task Repository_PreCanceled_DoesNotOpenDatabase(string operation)
    {
        var database = new Mock<IDatabaseConnection>(MockBehavior.Strict);
        var repository = new ServiceRepository(database.Object, () => FluentCommandBuilder.Create(new MariaDBDialect()));
        var token = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation switch
        {
            "save" => repository.SaveAsync(new Service(), token),
            "delete" => repository.DeleteAsync(1, token),
            "search" => repository.SearchGetByAsync(null, 1, 100, token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        });
        database.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 0)]
    [InlineData(1, 501)]
    public async Task Repository_InvalidPage_DoesNotOpenDatabase(int page, int pageSize)
    {
        var database = new Mock<IDatabaseConnection>(MockBehavior.Strict);
        var repository = new ServiceRepository(database.Object, () => FluentCommandBuilder.Create(new MariaDBDialect()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.SearchGetByAsync(null, page, pageSize));
        database.VerifyNoOtherCalls();
    }

    [Fact]
    public void Repository_NonPositiveReferenceIds_DoNotExist()
    {
        var database = new Mock<IDatabaseConnection>(MockBehavior.Strict);
        var repository = new ServiceRepository(database.Object, () => FluentCommandBuilder.Create(new MariaDBDialect()));
        Assert.False(repository.ServiceExists(0));
        Assert.False(repository.ServiceExists(-1));
        database.VerifyNoOtherCalls();
    }

    [Fact]
    public void FluentSql_MapsCrudToIndependentTableAndOmitsGeneratedInsertId()
    {
        var entity = new Service { ServiceID = 91, Description = "Installation", SalePrice = 20m };
        var (insert, _) = FluentCommandBuilder.Create(new MariaDBDialect()).Insert(statement => statement.Entity(entity)).Build();
        var (update, _) = FluentCommandBuilder.Create(new MariaDBDialect()).Update(statement => statement.Entity(entity))
            .Where(where => where.Equals<Service>(service => service.ServiceID, entity.ServiceID)).Build();
        var (delete, _) = FluentCommandBuilder.Create(new MariaDBDialect()).Delete<Service>()
            .Where(where => where.Equals<Service>(service => service.ServiceID, entity.ServiceID)).Build();
        var (select, _) = FluentCommandBuilder.Create(new MariaDBDialect()).Select<Service>(selection => selection.AllColumns<Service>())
            .Where(where => where.WithDynamicSearchFilter<Service, Service>("Installation", service => service.ServiceID, service => service.Description))
            .Take(25).Skip(25).Build();
        foreach (var sql in new[] { insert, update, delete, select })
        {
            Assert.Contains("services", sql);
            Assert.DoesNotContain("products", sql);
        }
        Assert.DoesNotContain("ServiceID", insert);
        Assert.Contains("ServiceID", update);
        Assert.Contains("ServiceID", delete);
        Assert.Contains("s.*", select);
        Assert.Contains("EmployeeCommissionValue", insert);
        Assert.Contains("Observations", update);
    }
}