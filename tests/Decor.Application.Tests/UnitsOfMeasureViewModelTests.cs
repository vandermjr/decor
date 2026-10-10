using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using Moq;

namespace Decor.Application.Tests;

public sealed class UnitsOfMeasureViewModelTests
{
    private static UnitOfMeasureDTO Unit(int id = 7, bool isActive = true) => new(id, "M", "Metro", true, isActive);

    private static (UnitsOfMeasureViewModel ViewModel, Mock<IUnitOfMeasureService> Service, Mock<IAuthorizationService> Authorization) Create()
    {
        var service = new Mock<IUnitOfMeasureService>();
        service.Setup(item => item.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string _, int page, int _, CancellationToken _) => Task.FromResult<IEnumerable<UnitOfMeasureDTO>>(page == 1 ? [Unit()] : []));
        service.Setup(item => item.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Unit());
        service.Setup(item => item.SaveUnitOfMeasureAsync(It.IsAny<UnitOfMeasureDTO>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        service.Setup(item => item.ActivateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        service.Setup(item => item.DeactivateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(item => item.HasPermission(It.IsAny<string>())).Returns(true);
        return (new UnitsOfMeasureViewModel(service.Object, authorization.Object), service, authorization);
    }

    [Fact]
    public async Task InitializeLoadsUnitsAndNewFormSavesFractionalUnit()
    {
        var (viewModel, service, _) = Create();
        await viewModel.InitializeAsync();

        Assert.Single(viewModel.Units);
        Assert.Equal("M", viewModel.Units[0].Code);
        viewModel.BeginNew();
        viewModel.Code = "M2";
        viewModel.Description = "Metro quadrado";
        viewModel.AllowsFraction = true;
        await viewModel.SaveAsync();

        service.Verify(item => item.SaveUnitOfMeasureAsync(new UnitOfMeasureDTO(0, "M2", "Metro quadrado", true, true), It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(viewModel.IsEditing);
        Assert.Contains("salva", viewModel.StatusMessage);
    }

    [Fact]
    public async Task EditFormCanChangeAndSaveUnitStatus()
    {
        var (viewModel, service, _) = Create();
        await viewModel.InitializeAsync();
        viewModel.SelectedUnit = Unit();
        await viewModel.BeginEditAsync();
        Assert.True(viewModel.IsActive);
        Assert.True(viewModel.CanChangeActive);
        viewModel.IsActive = false;
        Assert.False(viewModel.IsActive);
        await viewModel.SaveAsync();

        service.Verify(item => item.SaveUnitOfMeasureAsync(new UnitOfMeasureDTO(7, "M", "Metro", true, false), It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(item => item.DeactivateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditFormCanReactivateAnInactiveUnit()
    {
        var (viewModel, service, _) = Create();
        service.Setup(item => item.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(Unit(isActive: false));
        viewModel.SelectedUnit = Unit(isActive: false);
        await viewModel.BeginEditAsync();

        Assert.False(viewModel.IsActive);
        viewModel.IsActive = true;
        await viewModel.SaveAsync();

        service.Verify(item => item.SaveUnitOfMeasureAsync(new UnitOfMeasureDTO(7, "M", "Metro", true, true), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(DecorPermissions.UnitsOfMeasureView)]
    [InlineData(DecorPermissions.UnitsOfMeasureCreate)]
    [InlineData(DecorPermissions.UnitsOfMeasureEdit)]
    [InlineData(DecorPermissions.UnitsOfMeasureDeactivate)]
    public async Task MissingPermissionBlocksCorrespondingAction(string permission)
    {
        var (viewModel, service, authorization) = Create();
        authorization.Setup(item => item.HasPermission(permission)).Returns(false);
        viewModel.SelectedUnit = Unit();

        if (permission == DecorPermissions.UnitsOfMeasureView)
        {
            await viewModel.LoadUnitsAsync();
            Assert.False(viewModel.CanSearch);
            service.Verify(item => item.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        else if (permission == DecorPermissions.UnitsOfMeasureCreate)
        {
            viewModel.BeginNew();
            await viewModel.SaveAsync();
            Assert.False(viewModel.IsEditing);
            service.Verify(item => item.SaveUnitOfMeasureAsync(It.IsAny<UnitOfMeasureDTO>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        else if (permission == DecorPermissions.UnitsOfMeasureEdit)
        {
            Assert.False(viewModel.CanEdit);
            await viewModel.BeginEditAsync();
            service.Verify(item => item.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            await viewModel.BeginEditAsync();
            Assert.False(viewModel.CanChangeActive);
        }
    }
}