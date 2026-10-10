using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Collections;
using Avalonia.Controls;
using Decor.AvaloniaUI.Controls;
using Decor.Core.DTOs;

namespace Decor.Application.Tests;

public sealed partial class DecorGridPresentationTests
{
    [Fact]
    public void Brand_search_reapplies_default_sort_after_reload_and_clears_on_empty_results()
    {
        var items = new ObservableCollection<BrandDTO> { new(2, "B"), new(1, "A") };
        var grid = new DataGrid { ItemsSource = items };
        var column = new DataGridTextColumn { Header = "Nome", SortMemberPath = nameof(BrandDTO.BrandName) };
        grid.Columns.Add(column);
        DecorGridSorting.SetDefaultSortMemberPath(grid, nameof(BrandDTO.BrandName));

        DecorGridSorting.ApplyDefault(grid);
        Assert.Equal(ListSortDirection.Ascending, Assert.Single(grid.CollectionView.SortDescriptions).Direction);
        items.Clear();
        items.Add(new BrandDTO(3, "C"));
        items.Add(new BrandDTO(1, "A"));
        DecorGridSorting.ApplyDefault(grid);

        Assert.Equal(new[] { "A", "C" }, grid.CollectionView.Cast<BrandDTO>().Select(brand => brand.BrandName));
        Assert.Equal(ListSortDirection.Ascending, Assert.Single(grid.CollectionView.SortDescriptions).Direction);
        items.Clear();
        DecorGridSorting.ApplyDefault(grid);
        Assert.Empty(grid.CollectionView.SortDescriptions);
    }
}