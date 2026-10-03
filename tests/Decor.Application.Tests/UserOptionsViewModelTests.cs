using Decor.Application.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.Configuration;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

public sealed class UserOptionsViewModelTests
{
    [Fact]
    public async Task LoadAndSave_UsesExistingSettingsAndAppearanceServices()
    {
        var settings = new SettingsStub
        {
            Current = new UserSettings { Theme = DecorThemeStyle.Light, IconAppearance = new IconAppearance(300, 0.9) }
        };
        var icons = new IconAppearanceService(settings);
        await icons.InitializeAsync();
        var viewModel = new UserOptionsViewModel(settings, icons);

        await viewModel.LoadAsync();
        viewModel.IconWeight.Should().Be(300);
        viewModel.StrokeThickness.Should().Be(0.9);

        viewModel.IconWeight = 600;
        viewModel.StrokeThickness = 1.3;
        await viewModel.SaveIconAppearanceAsync();
        settings.Current.IconAppearance.Should().Be(new IconAppearance(600, 1.3));
        icons.CurrentAppearance.Should().Be(new IconAppearance(600, 1.3));
        viewModel.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task FailedSave_ReloadsPartiallyPersistedAppearanceAndNotifiesIcons()
    {
        var settings = new SettingsStub { FailIconSaveAfterWeight = true };
        var icons = new IconAppearanceService(settings);
        var viewModel = new UserOptionsViewModel(settings, icons);
        await viewModel.LoadAsync();
        IconAppearance? notifiedAppearance = null;
        icons.AppearanceChanged += appearance => notifiedAppearance = appearance;

        viewModel.IconWeight = 600;
        viewModel.StrokeThickness = 1.3;
        await viewModel.SaveIconAppearanceAsync();

        var persisted = new IconAppearance(600, IconAppearance.Default.StrokeThickness);
        settings.Current.IconAppearance.Should().Be(persisted);
        icons.CurrentAppearance.Should().Be(persisted);
        notifiedAppearance.Should().Be(persisted);
        viewModel.IconWeight.Should().Be(600);
        viewModel.StrokeThickness.Should().Be(persisted.StrokeThickness);
        viewModel.HasError.Should().BeTrue();
        viewModel.CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task FailedLoad_DisablesEditingUntilRetrySucceeds()
    {
        var settings = new SettingsStub { FailNextLoad = true };
        var viewModel = new UserOptionsViewModel(settings, new IconAppearanceService(settings));

        await viewModel.LoadAsync();
        viewModel.CanEdit.Should().BeFalse();
        viewModel.CanRetry.Should().BeTrue();
        viewModel.HasError.Should().BeTrue();
        viewModel.ErrorMessage.Should().Be("Não foi possível carregar as preferências.");

        await viewModel.LoadAsync();
        viewModel.CanEdit.Should().BeTrue();
        viewModel.CanRetry.Should().BeFalse();
        viewModel.HasError.Should().BeFalse();
    }

    private sealed class SettingsStub : IUserSettingsService
    {
        public UserSettings Current { get; set; } = new() { Theme = DecorThemeStyle.Dark };
        public bool FailIconSaveAfterWeight { get; init; }
        public bool FailNextLoad { get; set; }

        public Task<UserSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            if (FailNextLoad)
            {
                FailNextLoad = false;
                throw new InvalidOperationException("Load failed");
            }
            return Task.FromResult(Current);
        }

        public Task SetThemeAsync(DecorThemeStyle theme, CancellationToken cancellationToken = default)
        {
            Current = new UserSettings { Theme = theme, IconAppearance = Current.IconAppearance };
            return Task.CompletedTask;
        }

        public Task SetLanguageAsync(string language, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetIconAppearanceAsync(IconAppearance appearance, CancellationToken cancellationToken = default)
        {
            Current = new UserSettings
            {
                Theme = Current.Theme,
                IconAppearance = FailIconSaveAfterWeight
                    ? new IconAppearance(appearance.MaterialSymbolWeight, Current.IconAppearance.StrokeThickness)
                    : appearance
            };
            if (FailIconSaveAfterWeight) throw new InvalidOperationException("Stroke write failed");
            return Task.CompletedTask;
        }
    }
}