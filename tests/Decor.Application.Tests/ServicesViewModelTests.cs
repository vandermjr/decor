using System.ComponentModel.DataAnnotations;
using System.Text;
using Avalonia.Controls;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using Moq;

namespace Decor.Application.Tests;

public sealed class ServicesViewModelTests
{
    private static ServiceDTO Service(int id = 42) => new(id, "Installation", false, 10m, 25m, 3m, "Notes");

    private static (ServicesViewModel ViewModel, Mock<IServiceCatalogService> Catalog, Mock<IAuthorizationService> Authorization) Create()
    {
        var catalog = new Mock<IServiceCatalogService>();
        catalog.Setup(service => service.SearchServicesAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServiceDTO>());
        catalog.Setup(service => service.GetServiceByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Service());
        catalog.Setup(service => service.SaveServiceAsync(It.IsAny<ServiceDTO>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        catalog.Setup(service => service.DeleteServiceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.HasPermission(It.IsAny<string>())).Returns(true);
        return (new ServicesViewModel(catalog.Object, authorization.Object), catalog, authorization);
    }

    [Fact]
    public async Task Search_LoadsEveryBatchAndUsesSharedPaginationAndMatchStatus()
    {
        var (viewModel, catalog, _) = Create();
        using var cancellation = new CancellationTokenSource();
        viewModel.SearchText = "Installation";
        catalog.Setup(service => service.SearchServicesAsync("Installation", 1, 200, cancellation.Token))
            .ReturnsAsync(Enumerable.Range(1, 200).Select(Service).ToArray());
        catalog.Setup(service => service.SearchServicesAsync("Installation", 2, 200, cancellation.Token))
            .ReturnsAsync(Enumerable.Range(201, 5).Select(Service).ToArray());

        await viewModel.LoadServicesAsync(cancellation.Token);

        Assert.Equal(205, viewModel.Listing.TotalCount);
        Assert.Equal(10, viewModel.Services.Count);
        Assert.True(viewModel.HasPagination);
        viewModel.NextPageCommand!.Execute(null);
        Assert.Equal(11, viewModel.Services[0].ServiceID);
        viewModel.SelectedPageSize = 25;
        Assert.Equal(25, viewModel.Services.Count);
        viewModel.SetValueMatch("Description", 5);
        Assert.True(viewModel.HasStatusSecondary);
        viewModel.BeginNew();
        Assert.False(viewModel.HasPagination);
        Assert.False(viewModel.HasStatusSecondary);
        Assert.False(viewModel.SearchCommand.CanExecute(null));
        viewModel.CancelEdit();
        viewModel.ClearSearchCommand.Execute(null);
        Assert.Empty(viewModel.Services);
        Assert.Empty(viewModel.SearchText);
        Assert.True(viewModel.HasPagination);
        Assert.Equal("Página 0 de 0", viewModel.PaginationPageStatus);
    }

    [Fact]
    public async Task Edit_LoadsLatestRecordAndSavesAllFieldsWithCancellation()
    {
        var (viewModel, catalog, _) = Create();
        using var cancellation = new CancellationTokenSource();
        viewModel.SelectedService = Service() with { Description = "Stale list value" };
        await viewModel.BeginEditAsync(cancellation.Token);

        Assert.Equal("Installation", viewModel.Description);
        Assert.Equal(42, viewModel.ServiceId);
        Assert.False(viewModel.IsAdding);
        Assert.False(viewModel.IsActive);
        Assert.Equal(10m, viewModel.CostPrice);
        Assert.Equal(25m, viewModel.SalePrice);
        Assert.Equal(3m, viewModel.EmployeeCommissionValue);
        Assert.Equal("Notes", viewModel.Observations);
        viewModel.Description = "Updated";
        viewModel.IsActive = true;
        viewModel.CostPrice = 11m;
        viewModel.SalePrice = 30m;
        viewModel.EmployeeCommissionValue = 4m;
        viewModel.Observations = "Changed";
        await viewModel.SaveAsync(cancellation.Token);

        catalog.Verify(service => service.SaveServiceAsync(new ServiceDTO(42, "Updated", true, 11m, 30m, 4m, "Changed"), cancellation.Token), Times.Once);
        Assert.False(viewModel.IsEditing);
        Assert.Contains("sucesso", viewModel.StatusMessage);
        Assert.Null(viewModel.SelectedService);
    }

    [Fact]
    public async Task NewAndCancel_NotifyWorkspaceAdditionAndModificationIndicators()
    {
        var (viewModel, catalog, _) = Create();
        var document = new WorkspaceDocumentViewModel("services", "Services", new UserControl { DataContext = viewModel }, _ => { }, _ => { });
        var notifications = new List<string?>();
        document.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        viewModel.BeginNew();

        Assert.Equal(0, viewModel.ServiceId);
        Assert.True(viewModel.IsActive);
        Assert.True(document.ShowAdditionIndicator);
        Assert.Same(viewModel, document.StatusSource);
        Assert.Contains(nameof(document.IsModified), notifications);
        viewModel.CancelEdit();
        Assert.False(document.IsModified);
        Assert.Empty(viewModel.StatusMessage);

        viewModel.SelectedService = Service();
        await viewModel.BeginEditAsync();
        Assert.True(document.ShowModificationIndicator);
        viewModel.CancelEdit();
        catalog.Verify(service => service.SaveServiceAsync(It.IsAny<ServiceDTO>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task New_SavesIndependentServiceWithZeroDefaults()
    {
        var (viewModel, catalog, _) = Create();
        viewModel.BeginNew();
        viewModel.Description = "New service";
        await viewModel.SaveAsync();
        catalog.Verify(service => service.SaveServiceAsync(new ServiceDTO(0, "New service", true, 0m, 0m, 0m, null), It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(viewModel.IsEditing);
        Assert.False(viewModel.IsAdding);
    }

    [Theory]
    [InlineData("a", 1)]
    [InlineData("\u00e9", 2)]
    [InlineData("\u754c", 3)]
    [InlineData("\U0001f600", 4)]
    public void Observations_LiveEditingTruncatesAtUtf8BoundaryAndNotifiesCounter(string character, int bytesPerCharacter)
    {
        var (viewModel, _, _) = Create();
        viewModel.BeginNew();
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        var fittingText = string.Concat(Enumerable.Repeat(character, QuoteNotesRules.MaximumBytes / bytesPerCharacter));
        var expectedBytes = Encoding.UTF8.GetByteCount(fittingText);

        viewModel.Observations = fittingText;
        Assert.Equal(fittingText, viewModel.Observations);
        Assert.Equal($"{expectedBytes}/65535 bytes", viewModel.ObservationsCounter);
        Assert.Equal(new[] { nameof(viewModel.Observations), nameof(viewModel.ObservationsCounter) }, notifications);

        notifications.Clear();
        viewModel.Observations = fittingText + character;
        Assert.Equal(fittingText, viewModel.Observations);
        Assert.Empty(notifications);

        viewModel.Observations = character;
        Assert.Equal($"{bytesPerCharacter}/65535 bytes", viewModel.ObservationsCounter);
        Assert.Contains(nameof(viewModel.ObservationsCounter), notifications);
        notifications.Clear();
        viewModel.Observations = null;
        Assert.Null(viewModel.Observations);
        Assert.Equal("0/65535 bytes", viewModel.ObservationsCounter);
        Assert.Equal(new[] { nameof(viewModel.Observations), nameof(viewModel.ObservationsCounter) }, notifications);
        viewModel.Observations = string.Empty;
        Assert.Equal(string.Empty, viewModel.Observations);
        Assert.Equal("0/65535 bytes", viewModel.ObservationsCounter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_PreservesOptionalNullsAndMaximumFieldValues(bool empty)
    {
        var (viewModel, catalog, _) = Create();
        viewModel.BeginNew();
        viewModel.Description = new string('a', 255);
        decimal? money = empty ? null : 99999999.99m;
        viewModel.CostPrice = viewModel.SalePrice = viewModel.EmployeeCommissionValue = money;
        viewModel.Observations = empty ? null : new string('a', 65532) + "\U0001f600";
        var expectedNotes = empty ? null : new string('a', 65532);
        Assert.Equal(expectedNotes, viewModel.Observations);
        Assert.Equal(empty ? "0/65535 bytes" : "65532/65535 bytes", viewModel.ObservationsCounter);

        await viewModel.SaveAsync();

        catalog.Verify(service => service.SaveServiceAsync(
            new ServiceDTO(0, new string('a', 255), true, money, money, money, expectedNotes),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SaveFailure_PreservesFormAndPublishesError(int failure)
    {
        var (viewModel, catalog, _) = Create();
        Exception exception = failure switch
        {
            0 => new ValidationException("Description is required"),
            1 => new UnauthorizedAccessException(),
            _ => new InvalidOperationException()
        };
        catalog.Setup(service => service.SaveServiceAsync(It.IsAny<ServiceDTO>(), It.IsAny<CancellationToken>())).ThrowsAsync(exception);
        viewModel.BeginNew();
        viewModel.Description = "Keep this";
        await viewModel.SaveAsync();

        Assert.True(viewModel.IsAdding);
        Assert.Equal("Keep this", viewModel.Description);
        Assert.False(viewModel.IsBusy);
        Assert.NotEmpty(viewModel.StatusMessage);
        Assert.Equal(failure == 0, viewModel.HasValidationMessage);
    }

    [Theory]
    [InlineData(DecorPermissions.ServicesView)]
    [InlineData(DecorPermissions.ServicesCreate)]
    [InlineData(DecorPermissions.ServicesEdit)]
    [InlineData(DecorPermissions.ServicesDelete)]
    public async Task DeniedPermission_BlocksCommandsAndDirectEntryPoints(string permission)
    {
        var (viewModel, catalog, authorization) = Create();
        authorization.Setup(service => service.HasPermission(permission)).Returns(false);
        viewModel.SelectedService = Service();
        if (permission == DecorPermissions.ServicesView)
        {
            await viewModel.LoadServicesAsync();
            viewModel.BeginNew();
            await viewModel.BeginEditAsync();
            viewModel.BeginDelete();
            Assert.False(viewModel.CanNew);
            Assert.False(viewModel.CanEdit);
            Assert.False(viewModel.CanDelete);
            Assert.Empty(catalog.Invocations);
        }
        else if (permission == DecorPermissions.ServicesCreate)
        {
            viewModel.NewCommand.Execute(null);
            await viewModel.SaveAsync();
            Assert.False(viewModel.IsEditing);
            Assert.Empty(catalog.Invocations);
        }
        else if (permission == DecorPermissions.ServicesEdit)
        {
            await viewModel.BeginEditAsync();
            Assert.False(viewModel.CanEdit);
            Assert.False(viewModel.IsEditing);
            Assert.Empty(catalog.Invocations);
        }
        else
        {
            viewModel.BeginDelete();
            await viewModel.ConfirmDeleteAsync();
            Assert.False(viewModel.ShowDeleteConfirmation);
            Assert.Empty(catalog.Invocations);
        }
    }

    [Fact]
    public async Task Delete_RequiresConfirmationRetainsFailureAndAllowsRetry()
    {
        var (viewModel, catalog, _) = Create();
        viewModel.SelectedService = Service();
        viewModel.BeginDelete();
        Assert.Contains("Installation", viewModel.DeleteConfirmationMessage);
        Assert.False(viewModel.SearchCommand.CanExecute(null));
        viewModel.CancelDelete();
        await viewModel.ConfirmDeleteAsync();
        catalog.Verify(service => service.DeleteServiceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        viewModel.BeginDelete();
        catalog.Setup(service => service.DeleteServiceAsync(42, It.IsAny<CancellationToken>())).ThrowsAsync(new ValidationException("Service in use"));
        await viewModel.ConfirmDeleteAsync();
        Assert.True(viewModel.ShowDeleteConfirmation);
        Assert.Contains("Service in use", viewModel.ValidationMessage);
        Assert.Equal("Service in use", viewModel.StatusMessage);
        catalog.Setup(service => service.DeleteServiceAsync(42, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await viewModel.ConfirmDeleteAsync();
        Assert.False(viewModel.ShowDeleteConfirmation);
        Assert.False(viewModel.HasValidationMessage);
        Assert.Null(viewModel.SelectedService);
        Assert.Contains("sucesso", viewModel.StatusMessage);
    }

    [Fact]
    public async Task PendingSave_DisablesOtherOperationsAndPreservesFormUntilComplete()
    {
        var (viewModel, catalog, _) = Create();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        catalog.Setup(service => service.SaveServiceAsync(It.IsAny<ServiceDTO>(), It.IsAny<CancellationToken>())).Returns(pending.Task);
        viewModel.BeginNew();
        var save = viewModel.SaveAsync();
        Assert.True(viewModel.IsBusy);
        Assert.True(viewModel.IsEditing);
        Assert.False(viewModel.CanSave);
        Assert.False(viewModel.CanCancel);
        viewModel.CancelEdit();
        await viewModel.SaveAsync();
        Assert.True(viewModel.IsEditing);
        pending.SetResult();
        await save;
        catalog.Verify(service => service.SaveServiceAsync(It.IsAny<ServiceDTO>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelledSearch_DoesNotPublishPartialResults()
    {
        var (viewModel, catalog, _) = Create();
        using var cancellation = new CancellationTokenSource();
        catalog.Setup(service => service.SearchServicesAsync(It.IsAny<string?>(), 1, 200, cancellation.Token)).ReturnsAsync(() =>
        {
            cancellation.Cancel();
            return new[] { Service() };
        });
        await viewModel.LoadServicesAsync(cancellation.Token);
        Assert.Empty(viewModel.Services);
        Assert.False(viewModel.IsBusy);
        Assert.Contains("cancelada", viewModel.StatusMessage);
    }

}