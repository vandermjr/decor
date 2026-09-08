using System.ComponentModel;
using System.Windows.Input;

namespace Decor.AvaloniaUI.ViewModels;

public interface IStatusBarSource : INotifyPropertyChanged
{
    string StatusMessage { get; }
    string? StatusPrimary { get; }
    string? StatusSecondary { get; }
    bool HasStatusPrimary { get; }
    bool HasStatusSecondary { get; }
    string? PaginationStatus { get; }
    string? PaginationPageStatus { get; }
    bool HasPagination { get; }
    ICommand? PreviousPageCommand { get; }
    ICommand? NextPageCommand { get; }
    ICommand? FirstPageCommand { get; }
    ICommand? LastPageCommand { get; }
    bool HasPreviousPage { get; }
    bool HasNextPage { get; }
    bool HasFirstPage { get; }
    bool HasLastPage { get; }
    IReadOnlyList<int> PageSizeOptions { get; }
    int SelectedPageSize { get; set; }
}
