using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Decor.Core.Entities;
using Xunit;

namespace Decor.ServiceCatalog.Tests;

public sealed class ServiceCatalogContractTests
{
    [Fact]
    public void Service_HasIndependentTableAndExactFields()
    {
        Assert.Equal("services", typeof(Service).GetCustomAttribute<TableAttribute>()!.Name);
        Assert.NotNull(typeof(Service).GetProperty(nameof(Service.ServiceID))!.GetCustomAttribute<KeyAttribute>());
        Assert.Equal(
            new[] { "ServiceID", "Description", "IsActive", "CostPrice", "SalePrice", "EmployeeCommissionValue", "Observations" },
            typeof(Service).GetProperties().Select(property => property.Name));
        foreach (var property in typeof(Service).GetProperties())
            Assert.Equal(property.Name, property.GetCustomAttribute<ColumnAttribute>()!.Name);
        Assert.Equal(0, new Service().ServiceID);
    }
}