using Decor.AvaloniaUI.ViewModels;
using System.Reflection;

namespace Decor.Application.Tests;

public sealed class RecordCodeDisplayTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(9, "0")]
    [InlineData(10, "00")]
    [InlineData(999, "000")]
    [InlineData(1000, "0.000")]
    [InlineData(50000, "00.000")]
    [InlineData(1000000, "0.000.000")]
    public void ForNewRecord_UsesRecordCountDigitWidth(int recordCount, string expected)
    {
        var formatter = typeof(WorkspaceDocumentViewModel).Assembly
            .GetType("Decor.AvaloniaUI.ViewModels.RecordCodeDisplay", throwOnError: true)!;
        var method = formatter.GetMethod("ForNewRecord", BindingFlags.Public | BindingFlags.Static)!;

        Assert.Equal(expected, method.Invoke(null, [recordCount]));
    }

    [Fact]
    public void ForExistingRecord_ShowsPersistedId()
    {
        var formatter = typeof(WorkspaceDocumentViewModel).Assembly
            .GetType("Decor.AvaloniaUI.ViewModels.RecordCodeDisplay", throwOnError: true)!;
        var method = formatter.GetMethod("ForExistingRecord", BindingFlags.Public | BindingFlags.Static)!;

        Assert.Equal("50.000", method.Invoke(null, [50000]));
    }

    [Fact]
    public async Task CountAllAsync_ReadsEveryPage()
    {
        var pagesRead = new List<int>();
        var formatter = typeof(WorkspaceDocumentViewModel).Assembly
            .GetType("Decor.AvaloniaUI.ViewModels.RecordCodeDisplay", throwOnError: true)!;
        var method = formatter.GetMethod("CountAllAsync", BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(typeof(int));
        var loadPage = new Func<int, int, Task<IEnumerable<int>>>((page, pageSize) =>
        {
            pagesRead.Add(page);
            var resultLength = page switch { 1 or 2 => pageSize, _ => 1 };
            return Task.FromResult<IEnumerable<int>>(Enumerable.Range(0, resultLength));
        });

        var result = await (Task<int>)method.Invoke(null, [loadPage])!;

        Assert.Equal(1_001, result);
        Assert.Equal([1, 2, 3], pagesRead);
    }
}