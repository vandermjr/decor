using Decor.Application.Services;
using Decor.Core.Interfaces.Repositories;

namespace Decor.Application.Tests;

public sealed class StockSettingsServiceTests
{
    [Fact]
    public async Task GetAllowNegativeStockAsync_defaults_to_false()
    {
        var repository = new StubSystemSettingsRepository();
        var service = new StockSettingsService(repository);

        var result = await service.GetAllowNegativeStockAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllowNegativeStockAsync_reads_saved_value()
    {
        var service = new StockSettingsService(new StubSystemSettingsRepository
        {
            Settings = new Dictionary<string, string> { ["AllowNegativeStock"] = "true" }
        });

        var result = await service.GetAllowNegativeStockAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetAllowNegativeStockAsync_persists_value()
    {
        var repository = new StubSystemSettingsRepository();
        var service = new StockSettingsService(repository);

        await service.SetAllowNegativeStockAsync(true);

        repository.SavedSettings.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["AllowNegativeStock"] = "True"
        });
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