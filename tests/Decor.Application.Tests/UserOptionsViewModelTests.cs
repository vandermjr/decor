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
        var theme = new ThemeService(settings);
        var icons = new IconAppearanceService(settings);
        await theme.InitializeAsync();
        await icons.InitializeAsync();
        var viewModel = new UserOptionsViewModel(settings, theme, icons);

        await viewModel.LoadAsync();
        viewModel.IsLightTheme.Should().BeTrue();
        viewModel.IconWeight.Should().Be(300);
        viewModel.StrokeThickness.Should().Be(0.9);

        await viewModel.SetThemeAsync(DecorThemeStyle.Dark);
        viewModel.IsDarkTheme.Should().BeTrue();
        settings.Current.Theme.Should().Be(DecorThemeStyle.Dark);

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
        var theme = new ThemeService(settings);
        var icons = new IconAppearanceService(settings);
        var viewModel = new UserOptionsViewModel(settings, theme, icons);
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
    public async Task FailedThemeSave_KeepsPreviousSelection()
    {
        var settings = new SettingsStub { FailThemeSave = true };
        var theme = new ThemeService(settings);
        var viewModel = new UserOptionsViewModel(settings, theme, new IconAppearanceService(settings));
        await viewModel.LoadAsync();

        await viewModel.SetThemeAsync(DecorThemeStyle.Light);

        viewModel.IsDarkTheme.Should().BeTrue();
        theme.CurrentTheme.Should().Be(DecorThemeStyle.Dark);
        viewModel.HasError.Should().BeTrue();
        viewModel.CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task FailedLoad_DisablesEditingUntilRetrySucceeds()
    {
        var settings = new SettingsStub { FailNextLoad = true };
        var viewModel = new UserOptionsViewModel(settings, new ThemeService(settings), new IconAppearanceService(settings));

        await viewModel.LoadAsync();
        viewModel.CanEdit.Should().BeFalse();
        viewModel.CanRetry.Should().BeTrue();
        viewModel.HasError.Should().BeTrue();

        await viewModel.LoadAsync();
        viewModel.CanEdit.Should().BeTrue();
        viewModel.CanRetry.Should().BeFalse();
        viewModel.HasError.Should().BeFalse();
    }

    private sealed class SettingsStub : IUserSettingsService
    {
        public UserSettings Current { get; set; } = new() { Theme = DecorThemeStyle.Dark };
        public bool FailThemeSave { get; init; }
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
            if (FailThemeSave) throw new InvalidOperationException("Theme write failed");
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