using Decor.Application.Services;
using Decor.Core.Configuration;
using Decor.Core.Interfaces.Repositories;

namespace Decor.Application.Tests;

public sealed class IconAppearanceServiceTests
{
    [Fact]
    public async Task InitializeAsync_loads_the_persisted_appearance()
    {
        var settings = new StubSystemSettingsRepository
        {
            Settings = new Dictionary<string, string> { ["IconWeight"] = "300", ["IconStrokeThickness"] = "0.9" }
        };
        var service = new IconAppearanceService(settings);

        await service.InitializeAsync();

        service.CurrentAppearance.Should().Be(new IconAppearance(300, 0.9));
    }

    [Fact]
    public async Task InitializeAsync_notifies_when_reloading_changed_appearance()
    {
        var settings = new StubSystemSettingsRepository
        {
            Settings = new Dictionary<string, string> { ["IconWeight"] = "300", ["IconStrokeThickness"] = "0.9" }
        };
        var service = new IconAppearanceService(settings);
        IconAppearance? notifiedAppearance = null;
        service.AppearanceChanged += appearance => notifiedAppearance = appearance;

        await service.InitializeAsync();

        notifiedAppearance.Should().Be(new IconAppearance(300, 0.9));
    }

    [Fact]
    public async Task SetAppearanceAsync_persists_then_notifies_the_new_appearance()
    {
        var settings = new StubSystemSettingsRepository();
        var service = new IconAppearanceService(settings);
        IconAppearance? notifiedAppearance = null;
        service.AppearanceChanged += appearance => notifiedAppearance = appearance;

        await service.SetAppearanceAsync(600, 1.26);

        var expected = new IconAppearance(600, 1.3);
        service.CurrentAppearance.Should().Be(expected);
        settings.SavedSettings.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["IconWeight"] = "600",
            ["IconStrokeThickness"] = "1.3",
            ["IconColors"] = ""
        });
        notifiedAppearance.Should().Be(expected);
    }

    [Fact]
    public async Task SetIconColorOverrideAsync_persists_and_allows_reset()
    {
        var settings = new StubSystemSettingsRepository();
        var service = new IconAppearanceService(settings);
        IconAppearance? notifiedAppearance = null;
        service.AppearanceChanged += appearance => notifiedAppearance = appearance;

        await service.SetIconColorOverrideAsync("Actions.Save", "#ff5722");
        await service.SetIconColorOverrideAsync("Actions.Search", "#3b82f6");
        await service.SetIconColorOverrideAsync("Actions.Save", null);

        service.CurrentAppearance.GetColor("Actions.Save").Should().BeNull();
        service.CurrentAppearance.GetColor("Actions.Search").Should().Be("#3b82f6");
        settings.SavedSettings.Should().ContainKey("IconColors");
        notifiedAppearance.Should().NotBeNull();

        await service.ResetIconColorsAsync();

        service.CurrentAppearance.GetColor("Actions.Search").Should().BeNull();
    }

    [Fact]
    public async Task SetAppearanceAsync_rejects_unsupported_weights()
    {
        var service = new IconAppearanceService(new StubSystemSettingsRepository());

        var action = () => service.SetAppearanceAsync(450, 1.1);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private sealed class StubSystemSettingsRepository : ISystemSettingsRepository
    {
        public IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string>? SavedSettings { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Settings);
        public Task SetAsync(IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken = default)
        {
            SavedSettings = settings;
            return Task.CompletedTask;
        }
    }
}
