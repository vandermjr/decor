using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Collections;

namespace Decor.AvaloniaUI.ViewModels;

/// <summary>
/// Shared listing behavior for every grid view: client-side pagination of the query result
/// and the value-match summary shown in the status bar. Property names mirror <see cref="IStatusBarSource"/>
/// so view models can forward them and re-raise the same notifications.
/// </summary>
public sealed class GridListState<T> : IStatusBarSource
{
    private static readonly IReadOnlyList<int> AvailablePageSizes = [10, 25, 50, 100];
    private readonly ObservableCollection<T> _page;
    private readonly Func<bool> _isListVisible;
    private IReadOnlyList<T> _all = [];
    private int _pageSize = 10;
    private int _currentPage = 1;
    private string? _valueMatchColumn;
    private int _valueMatchCount;

    public GridListState(ObservableCollection<T> page, Func<bool> isListVisible)
    {
        _page = page;
        _isListVisible = isListVisible;
        FirstPageCommand = new RelayCommand(() => GoTo(1), () => CanNavigate && HasPreviousPage);
        PreviousPageCommand = new RelayCommand(() => GoTo(_currentPage - 1), () => CanNavigate && HasPreviousPage);
        NextPageCommand = new RelayCommand(() => GoTo(_currentPage + 1), () => CanNavigate && HasNextPage);
        LastPageCommand = new RelayCommand(() => GoTo(TotalPages), () => CanNavigate && HasNextPage);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StatusMessage => string.Empty;
    public string? StatusPrimary => PaginationStatus;
    public bool HasStatusPrimary => HasPagination;
    public IReadOnlyList<T> AllItems => _all;
    public int TotalCount => _all.Count;
    public int CurrentPage => _currentPage;
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / _pageSize);
    private bool CanNavigate => _isListVisible();

    public ICommand FirstPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand LastPageCommand { get; }
    public bool HasPreviousPage => _currentPage > 1;
    public bool HasNextPage => _currentPage < TotalPages;
    public bool HasFirstPage => HasPreviousPage;
    public bool HasLastPage => HasNextPage;
    public bool HasPagination => _isListVisible() && TotalCount > 0;
    public string? PaginationStatus => HasPagination ? $"Registros encontrados: {TotalCount}" : null;
    public string? PaginationPageStatus => HasPagination ? $"Página {_currentPage} de {TotalPages}" : null;
    public IReadOnlyList<int> PageSizeOptions => AvailablePageSizes;

    public int SelectedPageSize
    {
        get => _pageSize;
        set
        {
            if (!AvailablePageSizes.Contains(value) || value == _pageSize)
                return;
            _pageSize = value;
            _currentPage = Math.Clamp(_currentPage, 1, Math.Max(1, TotalPages));
            ShowPage();
            OnPropertyChanged();
        }
    }

    public string? StatusSecondary => _isListVisible() && !string.IsNullOrEmpty(_valueMatchColumn)
        ? $"{_valueMatchColumn}: {_valueMatchCount} correspondência(s)"
        : null;
    public bool HasStatusSecondary => StatusSecondary is not null;

    /// <summary>Shows a new query result starting at the first page.</summary>
    public void Load(IEnumerable<T> items, bool keepPage = false)
    {
        _all = items.ToArray();
        _currentPage = keepPage ? Math.Clamp(_currentPage, 1, Math.Max(1, TotalPages)) : 1;
        // Clear() signals a new query to the grid, which restores the query ordering.
        _page.Clear();
        foreach (var item in _all.Skip((_currentPage - 1) * _pageSize).Take(_pageSize))
            _page.Add(item);
        SetValueMatch(string.Empty, 0);
        Refresh();
    }

    public void Clear() => Load([]);

    public void SetValueMatch(string columnName, int matchCount)
    {
        _valueMatchColumn = string.IsNullOrWhiteSpace(columnName) ? null : columnName;
        _valueMatchCount = matchCount;
        OnPropertyChanged(nameof(StatusSecondary));
        OnPropertyChanged(nameof(HasStatusSecondary));
    }

    /// <summary>Re-evaluates visibility-dependent values (e.g. after entering or leaving edit mode).</summary>
    public void Refresh()
    {
        foreach (var name in new[] { nameof(TotalCount), nameof(CurrentPage), nameof(TotalPages), nameof(HasPreviousPage),
                     nameof(HasNextPage), nameof(HasFirstPage), nameof(HasLastPage), nameof(HasPagination),
                     nameof(PaginationStatus), nameof(PaginationPageStatus), nameof(StatusPrimary), nameof(HasStatusPrimary),
                     nameof(StatusSecondary), nameof(HasStatusSecondary) })
            OnPropertyChanged(name);
        foreach (var command in new[] { FirstPageCommand, PreviousPageCommand, NextPageCommand, LastPageCommand })
            ((RelayCommand)command).RaiseCanExecuteChanged();
    }

    private void GoTo(int page)
    {
        if (page < 1 || page > TotalPages || page == _currentPage)
            return;
        _currentPage = page;
        ShowPage();
    }

    private void ShowPage()
    {
        // Remove items one by one (no Reset) so a column sort chosen by the user survives page changes.
        while (_page.Count > 0)
            _page.RemoveAt(_page.Count - 1);
        foreach (var item in _all.Skip((_currentPage - 1) * _pageSize).Take(_pageSize))
            _page.Add(item);
        Refresh();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class GridPaginationState : IStatusBarSource, IDisposable
{
    private static readonly IReadOnlyList<int> AvailablePageSizes = [10, 25, 50, 100];
    private bool _disposed;

    public GridPaginationState(DataGridCollectionView view)
    {
        View = view;
        if (View.PageSize == 0)
            View.PageSize = 10;
        FirstPageCommand = new RelayCommand(() => View.MoveToPage(0), () => CanNavigate && HasPreviousPage);
        PreviousPageCommand = new RelayCommand(() => View.MoveToPage(View.PageIndex - 1), () => CanNavigate && HasPreviousPage);
        NextPageCommand = new RelayCommand(() => View.MoveToPage(View.PageIndex + 1), () => CanNavigate && HasNextPage);
        LastPageCommand = new RelayCommand(() => View.MoveToPage(TotalPages - 1), () => CanNavigate && HasNextPage);
        View.PropertyChanged += OnViewChanged;
    }

    public DataGridCollectionView View { get; }
    private bool CanNavigate => !_disposed && View.CanChangePage && !View.IsEditingItem && !View.IsAddingNew;
    public event PropertyChangedEventHandler? PropertyChanged;
    public int TotalCount => View.TotalItemCount;
    public int TotalPages => TotalCount == 0 ? 0 : View.PageSize == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / View.PageSize);
    public int CurrentPage => Math.Max(1, View.PageIndex + 1);
    public string StatusMessage => string.Empty;
    public string? StatusPrimary => PaginationStatus;
    public string? StatusSecondary => null;
    public bool HasStatusPrimary => HasPagination;
    public bool HasStatusSecondary => false;
    public bool HasPagination => TotalCount > 0;
    public string? PaginationStatus => HasPagination ? $"Registros encontrados: {TotalCount}" : null;
    public string? PaginationPageStatus => HasPagination ? $"Página {CurrentPage} de {TotalPages}" : null;
    public bool HasPreviousPage => View.PageIndex > 0;
    public bool HasNextPage => CurrentPage < TotalPages;
    public bool HasFirstPage => HasPreviousPage;
    public bool HasLastPage => HasNextPage;
    public ICommand FirstPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand LastPageCommand { get; }
    public IReadOnlyList<int> PageSizeOptions => AvailablePageSizes;
    public int SelectedPageSize
    {
        get => View.PageSize;
        set
        {
            if (CanNavigate && AvailablePageSizes.Contains(value) && value != View.PageSize)
                View.PageSize = value;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        View.PropertyChanged -= OnViewChanged;
    }

    private void OnViewChanged(object? sender, PropertyChangedEventArgs args)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        foreach (var command in new[] { FirstPageCommand, PreviousPageCommand, NextPageCommand, LastPageCommand })
            ((RelayCommand)command).RaiseCanExecuteChanged();
    }
}
