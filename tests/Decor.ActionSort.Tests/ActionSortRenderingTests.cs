using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Decor.Application.Services;
using Decor.AvaloniaUI;
using Decor.AvaloniaUI.Controls;
using Decor.AvaloniaUI.Icons;
using Decor.AvaloniaUI.ViewModels;
using Decor.AvaloniaUI.Views;
using Decor.Core.DTOs;
using Decor.Core.Common;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

[assembly: AvaloniaTestApplication(typeof(Decor.ActionSort.Tests.ActionSortTestApplication))]

namespace Decor.ActionSort.Tests;

public static class ActionSortTestApplication
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() => new App(new ServiceCollection()
        .AddSingleton<IIconAppearanceService>(new IconAppearanceService(new TestSettings()))
        .BuildServiceProvider())).UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    private sealed class TestSettings : ISystemSettingsRepository
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        public Task SetAsync(IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

public sealed class ActionSortRenderingTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Quotes_actions_hide_for_missing_permissions_busy_state_and_unavailable_commands(bool allowEdit)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.HasPermission(It.IsAny<string>())).Returns((string permission) =>
            permission != DecorPermissions.QuotesEdit || allowEdit);
        var quotes = new Mock<IQuoteService>();
        var createdAt = new DateTime(2026, 10, 8);
        var quote = new QuoteDTO(42, 0, 0, null, 1, createdAt, null,
            [new QuoteSectionDTO(81, 42, 1, (int)QuoteSectionStatus.Draft, null, null, createdAt, [])]);
        quotes.Setup(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<UnitOfMeasureDTO>());
        quotes.Setup(service => service.CreateOpenQuoteAsync(null, null, It.IsAny<CancellationToken>())).ReturnsAsync(quote);
        var employees = new Mock<IEmployeeService>();
        employees.Setup(service => service.SearchEmployeesAsync(It.IsAny<string>(), 1, 100, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<EmployeeDTO>());
        var viewModel = new QuotesViewModel(quotes.Object, authorization.Object, Mock.Of<ICustomerService>(),
            employees.Object, Mock.Of<IPartnerService>(), Mock.Of<IProductService>(), Mock.Of<IAuthenticatedUserContext>());
        var view = new QuotesView { DataContext = viewModel };
        var window = new Window { Width = 1400, Height = 900, Content = view };
        window.Show();
        try
        {
            Settle(window);
            Assert.False(ButtonFor(view, viewModel.EditCommand).IsVisible);
            Assert.False(ButtonFor(view, viewModel.DeleteCommand).IsVisible);
            var newButton = ButtonFor(view, viewModel.NewCommand);
            Assert.True(newButton.IsVisible);
            authorization.Setup(service => service.HasPermission(DecorPermissions.QuotesCreate)).Returns(false);
            await viewModel.InitializeAsync();
            Settle(window);
            Assert.False(newButton.IsVisible);
            authorization.Setup(service => service.HasPermission(DecorPermissions.QuotesCreate)).Returns(true);
            await viewModel.BeginNewAsync();
            Settle(window);
            Assert.True(viewModel.IsEditing);
            var saveButton = ButtonFor(view, viewModel.SaveCommand);
            Assert.Equal(allowEdit, saveButton.IsVisible);
            Assert.False(ButtonFor(view, viewModel.GeneratePdfCommand).IsVisible);
            Assert.False(ButtonFor(view, viewModel.ConvertToOrderCommand).IsVisible);
            Assert.False(ButtonFor(view, viewModel.SaveLineCommand).IsVisible);

            var pending = new TaskCompletionSource<QuoteDTO>();
            quotes.Setup(service => service.CreateOpenQuoteAsync(null, null, It.IsAny<CancellationToken>())).Returns(pending.Task);
            var opening = viewModel.BeginNewAsync();
            Settle(window);
            Assert.True(viewModel.IsBusy);
            Assert.False(saveButton.IsVisible);
            Assert.False(ButtonFor(view, viewModel.CancelCommand).IsVisible);
            pending.SetResult(quote);
            await opening;
            Settle(window);
            Assert.Equal(allowEdit, saveButton.IsVisible);
            Assert.True(ButtonFor(view, viewModel.CancelCommand).IsVisible);
        }
        finally { window.Close(); }
    }

    private static Button ButtonFor(Control view, System.Windows.Input.ICommand command) =>
        Assert.Single(view.GetVisualDescendants().OfType<Button>(), button => ReferenceEquals(button.Command, command));

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Brands_view_search_shows_sort_glyph_and_hides_unavailable_crud_actions(bool dark)
    {
        var service = new BrandService();
        var viewModel = new BrandsViewModel(service);
        var view = new BrandsView { DataContext = viewModel };
        var window = new Window { Width = 900, Height = 500, Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            viewModel.SearchCommand.Execute(null);
            Settle(window);
            AssertIndicator(view, DecorIconId.Navigation.SortAscending);
            var edit = Assert.Single(view.GetVisualDescendants().OfType<Button>(), button => ReferenceEquals(button.Command, viewModel.EditCommand));
            var delete = Assert.Single(view.GetVisualDescendants().OfType<Button>(), button => ReferenceEquals(button.Command, viewModel.DeleteCommand));
            Assert.False(edit.IsVisible);
            Assert.False(delete.IsVisible);
            viewModel.SelectedBrand = viewModel.Brands[0];
            Settle(window);
            Assert.True(edit.IsVisible);
            Assert.True(delete.IsVisible);
            AssertIconBrush(window, edit, "DecorEditIndicatorBrush");
            AssertIconBrush(window, delete, "DecorDangerBrush");
            AssertIconBrush(window, ButtonFor(view, viewModel.NewCommand), "DecorAddIndicatorBrush");
            edit.IsEnabled = false;
            Settle(window);
            Assert.False(edit.IsVisible);
            edit.IsEnabled = true;
            Settle(window);
            Assert.True(edit.IsVisible);
            service.Results = [new(3, "C"), new(1, "A")];
            viewModel.SearchText = "A";
            viewModel.SearchCommand.Execute(null);
            Settle(window);
            AssertIndicator(view, DecorIconId.Navigation.SortAscending);
            await viewModel.BeginNewAsync();
            Settle(window);
            AssertIconBrush(window, ButtonFor(view, viewModel.SaveCommand), "DecorAccentBrush");
            AssertIconBrush(window, ButtonFor(view, viewModel.CancelCommand), "DecorDangerBrush");
            service.Results = [];
            viewModel.SearchCommand.Execute(null);
            Settle(window);
            service.Results = [new(1, "A")];
            viewModel.ClearSearchCommand.Execute(null);
            Settle(window);
            Assert.Empty(viewModel.Brands);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<PathIcon>(), icon =>
                icon.Opacity > 0 && DecorIcon.GetId(icon) == DecorIconId.Navigation.SortAscending);
            viewModel.SearchCommand.Execute(null);
            Settle(window);
            AssertIndicator(view, DecorIconId.Navigation.SortAscending);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Brand_search_sort_glyph_survives_reload_empty_results_and_manual_sort(bool dark)
    {
        var items = new ObservableCollection<BrandDTO>();
        var listing = new GridListState<BrandDTO>(items, () => true);
        var control = new DecorDataGridControl
        {
            ItemsSource = items,
            PaginationSource = listing,
            DefaultSortMemberPath = nameof(BrandDTO.BrandName)
        };
        control.InitializeColumns(typeof(BrandDTO));
        var window = new Window { Width = 800, Height = 450, Content = control,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            listing.Load([new(2, "B"), new(1, "A")]);
            Settle(window);
            AssertIndicator(control, DecorIconId.Navigation.SortAscending);
            var header = Assert.Single(control.GetVisualDescendants().OfType<DataGridColumnHeader>(),
                header => Equals(header.Content, "Nome da Marca"));
            ((IPseudoClasses)header.Classes).Set(":sortascending", false);
            AssertIndicator(control, DecorIconId.Navigation.SortAscending);
            listing.Load([new(3, "C"), new(1, "A")]);
            Settle(window);
            AssertIndicator(control, DecorIconId.Navigation.SortAscending);
            listing.Load([]);
            Settle(window);
            Assert.DoesNotContain(control.GetVisualDescendants().OfType<PathIcon>(), icon =>
                icon.Opacity > 0 && DecorIcon.GetId(icon) == DecorIconId.Navigation.SortAscending);
            listing.Load([new(4, "D"), new(1, "A")]);
            Settle(window);
            AssertIndicator(control, DecorIconId.Navigation.SortAscending);
            var grid = control.FindControl<DataGrid>("InnerDataGrid")!;
            grid.CollectionView.SortDescriptions[0] = DataGridSortDescription.FromPath(nameof(BrandDTO.BrandName), ListSortDirection.Descending);
            Settle(window);
            AssertIndicator(control, DecorIconId.Navigation.SortDescending);
            window.Content = null;
            Settle(window);
            window.Content = control;
            Settle(window);
            AssertIndicator(control, DecorIconId.Navigation.SortDescending);
            control.ItemsSource = new ObservableCollection<BrandDTO> { new(2, "B"), new(1, "A") };
            Settle(window);
            AssertIndicator(control, DecorIconId.Navigation.SortAscending);
        }
        finally { window.Close(); }
    }

    private static void AssertIndicator(Control control, DecorIconId expected)
    {
        var icon = Assert.Single(control.GetVisualDescendants().OfType<PathIcon>(), icon => DecorIcon.GetId(icon) == expected);
        Assert.Equal(1, icon.Opacity);
        Assert.NotNull(icon.Data);
        Assert.True(icon.Bounds.Width > 0);
        Assert.True(icon.Bounds.Height > 0);
    }

    private static void AssertIconBrush(Window window, Button button, string resource)
    {
        var icon = Assert.Single(button.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>());
        Assert.True(button.TryFindResource(resource, button.ActualThemeVariant, out var brush));
        Assert.Equal(brush, icon.Fill);
        Assert.NotNull(icon.Data);
        Assert.True(icon.Bounds.Width > 0);
        Assert.True(icon.Bounds.Height > 0);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using var pixels = frame.Lock();
        Assert.True(pixels.Format == PixelFormat.Bgra8888 || pixels.Format == PixelFormat.Rgba8888);
        var expected = Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
        var origin = icon.TranslatePoint(default, window)!.Value;
        var redOffset = pixels.Format == PixelFormat.Bgra8888 ? 2 : 0;
        var blueOffset = pixels.Format == PixelFormat.Bgra8888 ? 0 : 2;
        var matches = 0;
        for (var horizontal = 0; horizontal < (int)icon.Bounds.Width; horizontal++)
        for (var vertical = 0; vertical < (int)icon.Bounds.Height; vertical++)
        {
            var pixelX = (int)((origin.X + horizontal) * window.RenderScaling);
            var pixelY = (int)((origin.Y + vertical) * window.RenderScaling);
            var offset = pixelY * pixels.RowBytes + pixelX * 4;
            if (Math.Abs(Marshal.ReadByte(pixels.Address, offset + redOffset) - expected.R) < 12
                && Math.Abs(Marshal.ReadByte(pixels.Address, offset + 1) - expected.G) < 12
                && Math.Abs(Marshal.ReadByte(pixels.Address, offset + blueOffset) - expected.B) < 12) matches++;
        }
        Assert.True(matches >= 3, $"No rendered {resource} pixels in {button.ActualThemeVariant}: {matches}.");
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class BrandService : IBrandService
    {
        public IEnumerable<BrandDTO> Results { get; set; } = [new(2, "B"), new(1, "A")];
        public Task<BrandDTO> GetBrandByIdAsync(int brandId, CancellationToken cancellationToken = default) => Task.FromResult(Results.First());
        public Task<IEnumerable<BrandDTO>> GetAllBrandsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult(Results);
        public Task<IEnumerable<BrandDTO>> SearchBrandsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult(Results);
        public Task SaveBrandAsync(BrandDTO brand, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteBrandAsync(int brandId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}