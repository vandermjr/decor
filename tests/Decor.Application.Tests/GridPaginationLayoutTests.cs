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

[Collection("Shared grid pagination")]
public sealed class GridPaginationLayoutTests
{
    [Fact]
    public void Shared_grid_frame_encloses_the_data_grid_empty_state_and_pagination_footer()
    {
        var control = new DecorDataGridControl();
        var frame = control.FindControl<Border>("GridFrame")!;

        Assert.Equal(new Thickness(1), frame.BorderThickness);
        Assert.NotNull(frame.FindControl<DataGrid>("InnerDataGrid"));
        Assert.NotNull(frame.FindControl<Panel>("EmptyOverlay"));
        Assert.NotNull(frame.FindControl<Border>("PaginationFooter"));
    }

    [Theory]
    [InlineData(1000, 1, 0, 2, false)]
    [InlineData(600, 0, 1, 1, false)]
    [InlineData(1000, 1, 0, 2, true)]
    [InlineData(600, 0, 1, 1, true)]
    [InlineData(300, 0, 1, 1, true)]
    public void Native_scrollbar_is_moved_without_losing_it_on_repeated_initialization(double width, int column, int row, int columns, bool empty)
    {
        var control = new DecorDataGridControl { PaginationSource = empty ? null : new PaginationSource() };
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
            Assert.True(control.FindControl<Border>("PaginationFooter")!.IsVisible);
            Assert.Equal(1, Grid.GetRow(control.FindControl<Border>("PaginationFooter")!));
            Assert.Equal(0, Grid.GetRow(control.FindControl<Panel>("EmptyOverlay")!));
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
    public void Auxiliary_grids_have_paging_controls_before_the_first_query()
    {
        var control = new DecorDataGridControl();
        Assert.Null(control.PaginationSource);
        var state = Assert.IsType<GridPaginationState>(control.EffectivePaginationSource);
        Assert.True(state.HasPagination);
        Assert.Equal("Página 0 de 0", state.PaginationPageStatus);
        Assert.True(control.FindControl<Border>("PaginationFooter")!.IsVisible);
        Assert.IsType<WrapPanel>(control.FindControl<Panel>("PaginationControls"));
        Assert.True(double.IsNaN(control.FindControl<Panel>("PaginationControls")!.Height));
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