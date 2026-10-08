using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Application.Services;
using Decor.Application.Validation;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;
using Moq;
using Xunit;

namespace Decor.ServiceCatalog.Tests;

public sealed class ServiceCatalogServiceTests
{
    private readonly Mock<IServiceRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IAuthorizationService> _authorization = new(MockBehavior.Strict);
    private static ServiceDTO ValidDto => new(0, "Installation", true, 10m, 20m, 3m, "Notes");

    private ServiceCatalogService CreateService(params string[] permissions)
    {
        _authorization.Setup(auth => auth.HasPermission(It.IsAny<string>()))
            .Returns((string permission) => permissions.Contains(permission));
        return new(_repository.Object, new ServiceDTOValidator(),
            new ServiceRepositoryValidator(_repository.Object), _authorization.Object);
    }

    [Theory]
    [InlineData("search")]
    [InlineData("get")]
    [InlineData("create")]
    [InlineData("edit")]
    [InlineData("delete")]
    public async Task Operations_RequireOwnPermission(string operation)
    {
        var service = CreateService(DecorPermissions.ProductsView, DecorPermissions.ProductsCreate, DecorPermissions.ProductsEdit);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Invoke(service, operation));
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("search")]
    [InlineData("get")]
    [InlineData("create")]
    [InlineData("edit")]
    [InlineData("delete")]
    public async Task Operations_PreCanceled_DoNotAccessRepository(string operation)
    {
        var service = CreateService();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Invoke(service, operation, new CancellationToken(true)));
        _repository.VerifyNoOtherCalls();
        _authorization.Verify(auth => auth.HasPermission(It.IsAny<string>()), Times.Never);
    }

    private static Task Invoke(IServiceCatalogService service, string operation, CancellationToken token = default) => operation switch
    {
        "search" => service.SearchServicesAsync(null, 1, 100, token),
        "get" => service.GetServiceByIdAsync(1, token),
        "create" => service.SaveServiceAsync(ValidDto, token),
        "edit" => service.SaveServiceAsync(ValidDto with { ServiceID = 1 }, token),
        "delete" => service.DeleteServiceAsync(1, token),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 501)]
    public async Task Search_RejectsInvalidPagination(int page, int pageSize)
    {
        var service = CreateService(DecorPermissions.ServicesView);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SearchServicesAsync(null, page, pageSize));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Search_ForwardsNullPaginationAndToken_MapsAllFields()
    {
        using var source = new CancellationTokenSource();
        var entity = ValidDto.FromDTO();
        entity.ServiceID = 91;
        _repository.Setup(repo => repo.SearchGetByAsync(null, 2, 25, source.Token))
            .ReturnsAsync(new[] { entity });
        var result = await CreateService(DecorPermissions.ServicesView).SearchServicesAsync(null, 2, 25, source.Token);
        Assert.Equal(ValidDto with { ServiceID = 91 }, Assert.Single(result));
        _repository.VerifyAll();
    }

    [Fact]
    public async Task Get_UsesIndependentIdAndToken()
    {
        using var source = new CancellationTokenSource();
        _repository.Setup(repo => repo.SearchGetByAsync("91", 1, 1, source.Token))
            .ReturnsAsync(new[] { (ValidDto with { ServiceID = 91 }).FromDTO() });
        var result = await CreateService(DecorPermissions.ServicesView).GetServiceByIdAsync(91, source.Token);
        Assert.Equal(91, result.ServiceID);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task Get_MissingOrWrongId_ThrowsNotFound()
    {
        _repository.Setup(repo => repo.SearchGetByAsync("91", 1, 1, default))
            .ReturnsAsync(new[] { (ValidDto with { ServiceID = 90 }).FromDTO() });
        await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateService(DecorPermissions.ServicesView).GetServiceByIdAsync(91));
    }

    [Theory]
    [InlineData("get", 0)]
    [InlineData("get", -1)]
    [InlineData("delete", 0)]
    [InlineData("delete", -1)]
    public async Task IdOperations_RejectNonPositiveIds(string operation, int id)
    {
        var service = CreateService(DecorPermissions.ServicesView, DecorPermissions.ServicesDelete);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => operation == "get"
            ? service.GetServiceByIdAsync(id) : service.DeleteServiceAsync(id));
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public async Task Save_CreateAndEdit_PreserveAllFieldsAndToken(int id)
    {
        using var source = new CancellationTokenSource();
        var dto = ValidDto with { ServiceID = id, IsActive = false };
        if (id > 0) _repository.Setup(repo => repo.ServiceExists(id)).Returns(true);
        Service? saved = null;
        _repository.Setup(repo => repo.SaveAsync(It.IsAny<Service>(), source.Token))
            .Callback<Service, CancellationToken>((entity, token) => saved = entity).ReturnsAsync(1);
        await CreateService(id == 0 ? DecorPermissions.ServicesCreate : DecorPermissions.ServicesEdit).SaveServiceAsync(dto, source.Token);
        Assert.Equal(dto, saved!.ToDTO());
        _repository.VerifyAll();
    }

    [Fact]
    public async Task Save_MissingEdit_FailsBeforePersistence()
    {
        _repository.Setup(repo => repo.ServiceExists(91)).Returns(false);
        await Assert.ThrowsAsync<ValidationException>(() => CreateService(DecorPermissions.ServicesEdit)
            .SaveServiceAsync(ValidDto with { ServiceID = 91 }));
        _repository.Verify(repo => repo.SaveAsync(It.IsAny<Service>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("negativeId")]
    [InlineData("missingDescription")]
    [InlineData("whitespaceDescription")]
    [InlineData("longDescription")]
    [InlineData("cost")]
    [InlineData("sale")]
    [InlineData("commission")]
    [InlineData("observations")]
    public async Task Save_InvalidDto_FailsBeforeRepository(string field)
    {
        var dto = field switch
        {
            "negativeId" => ValidDto with { ServiceID = -1 },
            "missingDescription" => ValidDto with { Description = null },
            "whitespaceDescription" => ValidDto with { Description = "   " },
            "longDescription" => ValidDto with { Description = new string('x', 256) },
            "cost" => ValidDto with { CostPrice = -0.01m },
            "sale" => ValidDto with { SalePrice = -0.01m },
            "commission" => ValidDto with { EmployeeCommissionValue = -0.01m },
            "observations" => ValidDto with { Observations = new string('x', 65536) },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        await Assert.ThrowsAsync<ValidationException>(() => CreateService(DecorPermissions.ServicesCreate, DecorPermissions.ServicesEdit).SaveServiceAsync(dto));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void Validator_AcceptsOptionalMoneyAndBoundaryLengths()
    {
        var dto = ValidDto with { Description = new string('x', 255), Observations = new string('x', 65535),
            CostPrice = null, SalePrice = null, EmployeeCommissionValue = null };
        Assert.Empty(new ServiceDTOValidator().Validate(dto));
        Assert.Equal(dto, dto.FromDTO().ToDTO());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Save_UnexpectedAffectedRows_Throws(int affectedRows)
    {
        _repository.Setup(repo => repo.SaveAsync(It.IsAny<Service>(), default)).ReturnsAsync(affectedRows);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(DecorPermissions.ServicesCreate).SaveServiceAsync(ValidDto));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Delete_UnexpectedAffectedRows_Throws(int affectedRows)
    {
        _repository.Setup(repo => repo.DeleteAsync(91, default)).ReturnsAsync(affectedRows);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(DecorPermissions.ServicesDelete).DeleteServiceAsync(91));
    }

    [Fact]
    public async Task Delete_ForwardsToken()
    {
        using var source = new CancellationTokenSource();
        _repository.Setup(repo => repo.DeleteAsync(91, source.Token)).ReturnsAsync(1);
        await CreateService(DecorPermissions.ServicesDelete).DeleteServiceAsync(91, source.Token);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task Delete_PreservesDatabaseReferenceFailure()
    {
        var failure = new InvalidOperationException("Foreign key restricts deletion");
        _repository.Setup(repo => repo.DeleteAsync(91, default)).ThrowsAsync(failure);
        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(DecorPermissions.ServicesDelete).DeleteServiceAsync(91));
        Assert.Same(failure, result);
    }
}