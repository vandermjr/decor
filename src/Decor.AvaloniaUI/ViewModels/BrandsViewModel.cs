using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class BrandsViewModel : IStatusBarSource, IWorkspaceDocumentState
{
    private readonly IBrandService _brandService;
    private BrandDTO? _selectedBrand;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private bool _isEditing;
    private bool _isNew;
    private int _brandId;
    private string? _brandName;
    private readonly Dictionary<string, string> _fieldErrors = [];
    private bool _showDeleteConfirmation;
    private BrandDTO? _brandToDelete;

    public BrandsViewModel(IBrandService brandService)
    {
        _brandService = brandService;
        SearchCommand = new RelayCommand(async () => await LoadBrandsAsync());
        NewCommand = new RelayCommand(BeginNew, () => !IsBusy);
        EditCommand = new RelayCommand(BeginEdit, () => SelectedBrand is not null && !IsBusy && !IsEditing);
        DeleteCommand = new RelayCommand(BeginDelete, () => SelectedBrand is not null && !IsBusy && !IsEditing);
        ConfirmDeleteCommand = new RelayCommand(async () => await ConfirmDeleteAsync(), () => !IsBusy);
        CancelDeleteCommand = new RelayCommand(CancelDelete, () => true);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => IsEditing && !IsBusy);
        CancelCommand = new RelayCommand(CancelEdit, () => IsEditing && !IsBusy);
    }

    public ObservableCollection<BrandDTO> Brands { get; } = [];

    public ICommand SearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set => SetField(ref _searchText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string? StatusPrimary => null;
    public string? StatusSecondary => null;
    public bool HasStatusPrimary => false;
    public bool HasStatusSecondary => false;
    public string? PaginationStatus => null;
    public string? PaginationPageStatus => null;
    public bool HasPagination => false;
    public ICommand? PreviousPageCommand => null;
    public ICommand? NextPageCommand => null;
    public ICommand? FirstPageCommand => null;
    public ICommand? LastPageCommand => null;
    public bool HasPreviousPage => false;
    public bool HasNextPage => false;
    public bool HasFirstPage => false;
    public bool HasLastPage => false;
    public IReadOnlyList<int> PageSizeOptions => [];
    public int SelectedPageSize { get => 0; set { } }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RaiseCommandStates();
                NotifyCommandVisibilityChanged();
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetField(ref _isEditing, value))
            {
                RaiseCommandStates();
                NotifyCommandVisibilityChanged();
            }
        }
    }

    public bool CanNew => !IsBusy && !IsEditing;
    public bool CanEdit => SelectedBrand is not null && !IsBusy && !IsEditing;
    public bool CanDelete => SelectedBrand is not null && !IsBusy && !IsEditing;
    public bool CanSave => IsEditing && !IsBusy;
    public bool CanCancel => IsEditing && !IsBusy;

    public BrandDTO? SelectedBrand
    {
        get => _selectedBrand;
        set
        {
            if (SetField(ref _selectedBrand, value))
            {
                RaiseCommandStates();
                NotifyCommandVisibilityChanged();
            }
        }
    }

    public int BrandId
    {
        get => _brandId;
        private set => SetField(ref _brandId, value);
    }

    public string? BrandName
    {
        get => _brandName;
        set => SetField(ref _brandName, value);
    }

    // Propriedades de erro para cada campo
    public string BrandNameError => GetFieldError("BrandName");
    public bool HasBrandNameError => HasFieldError("BrandName");

    // Propriedades para diálogo de confirmação de delete
    public bool ShowDeleteConfirmation
    {
        get => _showDeleteConfirmation;
        private set => SetField(ref _showDeleteConfirmation, value);
    }

    public BrandDTO? BrandToDelete => _brandToDelete;

    public string DeleteConfirmationMessage => BrandToDelete != null 
        ? $"Tem certeza que deseja excluir a marca \"{BrandToDelete.BrandName}\"?" 
        : string.Empty;

    public Task InitializeAsync()
    {
        Brands.Clear();
        SelectedBrand = null;
        StatusMessage = string.Empty;
        return Task.CompletedTask;
    }

    public void BeginNew()
    {
        _isNew = true;
        IsEditing = true;
        SelectedBrand = null;
        BrandId = 0;
        BrandName = string.Empty;
        ClearFieldErrors();
        StatusMessage = "Nova marca.";
    }

    public void BeginEdit()
    {
        if (SelectedBrand is null)
        {
            return;
        }

        _isNew = false;
        IsEditing = true;
        BrandId = SelectedBrand.BrandID;
        BrandName = SelectedBrand.BrandName;
        ClearFieldErrors();
        StatusMessage = $"Editando marca {BrandId}.";
    }

    public void CancelEdit()
    {
        IsEditing = false;
        _isNew = false;
        BrandId = 0;
        BrandName = string.Empty;
        ClearFieldErrors();
        SelectedBrand = null;
        StatusMessage = string.Empty;
    }

    public void BeginDelete()
    {
        if (SelectedBrand is null)
        {
            return;
        }

        _brandToDelete = SelectedBrand;
        ShowDeleteConfirmation = true;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }

    public void CancelDelete()
    {
        _brandToDelete = null;
        ShowDeleteConfirmation = false;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }

    public async Task ConfirmDeleteAsync()
    {
        if (_brandToDelete is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _brandService.DeleteBrandAsync(_brandToDelete.BrandID);
            StatusMessage = "Marca excluída com sucesso.";
            _brandToDelete = null;
            ShowDeleteConfirmation = false;
            SelectedBrand = null;
            await LoadBrandsAsync();
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Você não possui permissão para excluir marcas.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível excluir a marca.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadBrandsAsync()
    {
        IsBusy = true;
        try
        {
            Replace(Brands, await _brandService.SearchBrandsAsync(SearchText));
            StatusMessage = $"{Brands.Count} marca(s).";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Você não possui permissão para consultar marcas.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar as marcas.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        ClearFieldErrors();
        try
        {
            var dto = new BrandDTO(BrandId, BrandName);
            await _brandService.SaveBrandAsync(dto);
            StatusMessage = _isNew ? "Marca incluída com sucesso." : "Marca atualizada com sucesso.";
            IsEditing = false;
            _isNew = false;
            await LoadBrandsAsync();
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException vex)
        {
            PopulateFieldErrors(vex.Message);
            StatusMessage = "Verifique os erros realçados nos campos abaixo.";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Você não possui permissão para salvar marcas.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível salvar a marca.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void RaiseCommandStates()
    {
        if (EditCommand is RelayCommand relayEdit) relayEdit.RaiseCanExecuteChanged();
        if (DeleteCommand is RelayCommand relayDelete) relayDelete.RaiseCanExecuteChanged();
        if (SaveCommand is RelayCommand relaySave) relaySave.RaiseCanExecuteChanged();
        if (CancelCommand is RelayCommand relayCancel) relayCancel.RaiseCanExecuteChanged();
        if (NewCommand is RelayCommand relayNew) relayNew.RaiseCanExecuteChanged();
    }

    private void NotifyCommandVisibilityChanged()
    {
        OnPropertyChanged(nameof(CanNew));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanCancel));
    }

    public string GetFieldError(string fieldName) => _fieldErrors.TryGetValue(fieldName, out var error) ? error : string.Empty;

    public bool HasFieldError(string fieldName) => _fieldErrors.ContainsKey(fieldName);

    private void ClearFieldErrors()
    {
        _fieldErrors.Clear();
        NotifyErrorPropertiesChanged();
    }

    private void PopulateFieldErrors(string errorMessage)
    {
        var lines = errorMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var fieldName = ExtractFieldName(line);
            if (!string.IsNullOrEmpty(fieldName))
            {
                _fieldErrors[fieldName] = line;
            }
        }
        NotifyErrorPropertiesChanged();
    }

    private void NotifyErrorPropertiesChanged()
    {
        OnPropertyChanged(nameof(BrandNameError));
        OnPropertyChanged(nameof(HasBrandNameError));
    }

    private static string ExtractFieldName(string errorMessage)
    {
        // Map error message keywords to field names
        if (errorMessage.Contains("nome", StringComparison.OrdinalIgnoreCase) || 
            errorMessage.Contains("marca", StringComparison.OrdinalIgnoreCase))
            return "BrandName";

        return string.Empty;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
