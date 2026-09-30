using Decor.Application.Services;
using Decor.Core.Configuration;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

public sealed class IconAppearanceServiceTests
{
    [Fact]
    public async Task InitializeAsync_loads_the_persisted_appearance()
    {
        var settings = new StubUserSettingsService
        {
            Settings = new UserSettings { IconAppearance = new IconAppearance(300, 0.9) }
        };
        var service = new IconAppearanceService(settings);

        await service.InitializeAsync();

        service.CurrentAppearance.Should().Be(new IconAppearance(300, 0.9));
    }

    [Fact]
    public async Task SetAppearanceAsync_persists_then_notifies_the_new_appearance()
    {
        var settings = new StubUserSettingsService();
        var service = new IconAppearanceService(settings);
        IconAppearance? notifiedAppearance = null;
        service.AppearanceChanged += appearance => notifiedAppearance = appearance;

        await service.SetAppearanceAsync(600, 1.26);

        var expected = new IconAppearance(600, 1.3);
        service.CurrentAppearance.Should().Be(expected);
        settings.SavedAppearance.Should().Be(expected);
        notifiedAppearance.Should().Be(expected);
    }

    [Fact]
    public async Task SetAppearanceAsync_rejects_unsupported_weights()
    {
        var service = new IconAppearanceService(new StubUserSettingsService());

        var action = () => service.SetAppearanceAsync(450, 1.1);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private sealed class StubUserSettingsService : IUserSettingsService
    {
        public UserSettings Settings { get; init; } = new();
        public IconAppearance? SavedAppearance { get; private set; }

        public Task<UserSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Settings);
        public Task SetThemeAsync(Decor.Core.Common.DecorThemeStyle theme, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetLanguageAsync(string language, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetIconAppearanceAsync(IconAppearance appearance, CancellationToken cancellationToken = default)
        {
            SavedAppearance = appearance;
            return Task.CompletedTask;
        }
    }
}
