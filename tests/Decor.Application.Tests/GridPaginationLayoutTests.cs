using System.ComponentModel;
using System.Reflection;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Decor.AvaloniaUI.Controls;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.Application.Tests;

public sealed partial class DecorGridPresentationTests
{
    [Theory]
    [InlineData(1000, 1, 0, 2)]
    [InlineData(600, 0, 1, 1)]
    public void Native_scrollbar_is_moved_without_losing_it_on_repeated_initialization(double width, int column, int row, int columns)
    {
        var control = new DecorDataGridControl { PaginationSource = new PaginationSource() };
        typeof(Visual).GetProperty(nameof(Visual.Bounds))!.SetValue(control, new Rect(0, 0, width, 400));
        var horizontal = new ScrollBar { Name = "PART_HorizontalScrollbar", Orientation = Orientation.Horizontal };
        var vertical = new ScrollBar { Name = "PART_VerticalScrollbar", Orientation = Orientation.Vertical };
        var rows = new Border { Name = "PART_RowsPresenter" };
        var nativePanel = new Grid();
        nativePanel.Children.Add(rows);
        nativePanel.Children.Add(horizontal);
        nativePanel.Children.Add(vertical);
        var grid = new DataGrid { Template = new FuncControlTemplate<DataGrid>((_, _) => nativePanel) };
        grid.ApplyTemplate();
        typeof(DecorDataGridControl).GetField("_innerGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(control, grid);
        var initialize = typeof(DecorDataGridControl).GetMethod("KeepScrollBarsExpanded", BindingFlags.Instance | BindingFlags.NonPublic)!;

        for (var iteration = 0; iteration < 3; iteration++)
        {
            initialize.Invoke(control, null);
            var footer = control.FindControl<Grid>("PaginationLayout")!;
            Assert.Same(footer, horizontal.Parent);
            Assert.Single(footer.Children.OfType<ScrollBar>());
            Assert.Equal(column, Grid.GetColumn(horizontal));
            Assert.Equal(row, Grid.GetRow(horizontal));
            Assert.Equal(columns, footer.ColumnDefinitions.Count);
            Assert.Equal(10, horizontal.Height);
            Assert.Equal(10, vertical.Width);
            Assert.Equal(0, rows.Margin.Bottom);
            Assert.False(horizontal.AllowAutoHide);
            Assert.NotNull(horizontal.Theme);
        }

        var replacement = new ScrollBar { Name = horizontal.Name, Orientation = Orientation.Horizontal };
        grid.Template = new FuncControlTemplate<DataGrid>((_, _) => new Grid { Children = { replacement } });
        grid.ApplyTemplate();
        initialize.Invoke(control, null);
        var updated = control.FindControl<Grid>("PaginationLayout")!;
        Assert.Same(replacement, Assert.Single(updated.Children.OfType<ScrollBar>()));
        Assert.Null(horizontal.Parent);
    }

    [Fact]
    public void Auxiliary_grids_have_no_paging_controls()
    {
        var control = new DecorDataGridControl();
        Assert.Null(control.PaginationSource);
        Assert.False(control.FindControl<Border>("PaginationFooter")!.IsVisible);
    }

    private sealed class PaginationSource : IStatusBarSource
    {
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public string StatusMessage => string.Empty;
        public string? StatusPrimary => null;
        public string? StatusSecondary => null;
        public bool HasStatusPrimary => false;
        public bool HasStatusSecondary => false;
        public string? PaginationStatus => "Registros encontrados: 25";
        public string? PaginationPageStatus => "Página 1 de 3";
        public bool HasPagination => true;
        public ICommand? PreviousPageCommand => null;
        public ICommand? NextPageCommand => null;
        public ICommand? FirstPageCommand => null;
        public ICommand? LastPageCommand => null;
        public bool HasPreviousPage => false;
        public bool HasNextPage => true;
        public bool HasFirstPage => false;
        public bool HasLastPage => true;
        public IReadOnlyList<int> PageSizeOptions => [10, 25, 50, 100];
        public int SelectedPageSize { get; set; } = 10;
    }
}