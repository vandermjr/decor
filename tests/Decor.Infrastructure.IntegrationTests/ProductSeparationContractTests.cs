using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Decor.Application.Mappers;
using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;
using Decor.FluentSqlBuilder;
using Decor.Infrastructure.Data.Repositories;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class ProductSeparationContractTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    [InlineData(99)]
    public async Task ServiceAndUnknownTypesAreRejectedBeforeRepositoryAccess(int type)
    {
        var service = new ProductService(null!, new ProductDTOValidator(), null!, new AllowAuthorization());
        var action = () => service.SaveProductAsync(CreateProduct(type));
        await action.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public void LegacyDtoMembersAreHiddenAndOutboundTypeIsGood()
    {
        typeof(ProductDTO).GetProperty(nameof(ProductDTO.ProductType))!.GetCustomAttribute<BrowsableAttribute>()!.Browsable.Should().BeFalse();
        typeof(ProductDTO).GetProperty(nameof(ProductDTO.EmployeeCommissionValue))!.GetCustomAttribute<BrowsableAttribute>()!.Browsable.Should().BeFalse();
        var dto = new Product { ProductID = 10, ProductType = ProductType.Good }.ToDTO();
        dto.ProductType.Should().Be((int)ProductType.Good);
        dto.EmployeeCommissionValue.Should().BeNull();
        new ProductDTOValidator().Validate(CreateProduct(2)).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("piso")]
    [InlineData("tipo:produto")]
    [InlineData("tipo:servico 42")]
    [InlineData("tipo:servico estoque > 0")]
    public void SearchAlwaysIncludesGoodsPredicate(string? searchText)
    {
        var query = ProductSearchQuery.Parse(searchText);
        query.CatalogProductType.Should().Be(ProductType.Good);
        var repository = new ProductRepository(null!, () => new QueryContext(), FluentCommandBuilder.Create);
        var method = typeof(ProductRepository).GetMethod("BuildSearchQuery", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var (sql, parameters) = ((string, Dictionary<string, object?>))method.Invoke(repository, new object?[] { searchText, 1, 5 })!;
        sql.Should().Contain("p.ProductType = @ProductType");
        parameters["ProductType"].Should().Be(ProductType.Good);
        if (query.Type == ProductType.Service)
            parameters.Should().Contain(parameter => parameter.Key != "ProductType" && Equals(parameter.Value, ProductType.Service));
    }

    private static ProductDTO CreateProduct(int type) => new(
        0, null, true, "Produto", 0, 1, "Marca", 1, null, null, null, null, null, null, null,
        null, null, null, null, 0, type, null, null, null, null, 1);

    private sealed class AllowAuthorization : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => true;
        public bool CanView(string resource) => true;
        public bool CanCreate(string resource) => true;
        public bool CanEdit(string resource) => true;
        public bool CanDelete(string resource) => true;
    }

    private sealed class QueryContext : IQueryContext
    {
        public bool IsSingleIdSearch { get; set; }
    }
}