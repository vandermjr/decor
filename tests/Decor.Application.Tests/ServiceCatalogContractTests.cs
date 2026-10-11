using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Decor.Core.Entities;
using FluentAssertions;
using Xunit;

namespace Decor.Application.Tests;

public sealed class ServiceCatalogContractTests
{
    [Fact]
    public void Service_HasIndependentTableAndExactFields()
    {
        typeof(Service).GetCustomAttribute<TableAttribute>()!.Name.Should().Be("services");
        typeof(Service).GetProperty(nameof(Service.ServiceID))!
            .GetCustomAttribute<KeyAttribute>().Should().NotBeNull();
        typeof(Service).GetProperties().Select(property => property.Name).Should().BeEquivalentTo(
            "ServiceID", "Description", "IsActive", "CostPrice", "SalePrice",
            "EmployeeCommissionValue", "Observations");
        foreach (var property in typeof(Service).GetProperties())
            property.GetCustomAttribute<ColumnAttribute>()!.Name.Should().Be(property.Name);
        new Service().ServiceID.Should().Be(0);
    }
}