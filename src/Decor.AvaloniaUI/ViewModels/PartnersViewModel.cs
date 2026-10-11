using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class PartnersViewModel : IStatusBarSource, IWorkspaceDocumentState
{
    private readonly IPartnerService _partnerService;
    private readonly IAuthorizationService _authorization;
    private PartnerDTO? _selectedPartner;
    private PartnerDTO? _partnerToDelete;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _name = string.Empty;
    private string? _document;
    private string? _phone;
    private PartnerType _partnerType = PartnerType.Fabricante;
    private bool _isActive = true;
    private bool _isBusy;
    private bool _isEditing;
    private bool _isNew;
    private bool _selectionMode;
    private bool _showDeleteConfirmation;
    private int _partnerId;

    public PartnersViewModel(IPartnerService partnerService, IAuthorizationService authorization)
    {
        _partnerService = partnerService;
        _authorization = authorization;
        Listing = new GridListState<PartnerDTO>(Partners, () => !IsEditing);
        Listing.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        SearchCommand = new RelayCommand(async () => await LoadPartnersAsync(), () => CanSearch);
        ClearSearchCommand = new RelayCommand(ClearSearch, () => CanSearch);
        NewCommand = new RelayCommand(BeginNew, () => CanNew);
        EditCommand = new RelayCommand(async () => await BeginEditAsync(), () => CanEdit);
        DeleteCommand = new RelayCommand(BeginDelete, () => CanDelete);
        ConfirmDeleteCommand = new RelayCommand(async () => await ConfirmDeleteAsync(), () => CanConfirmDelete);
        CancelDeleteCommand = new RelayCommand(CancelDelete, () => ShowDeleteConfirmation && !IsBusy);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        CancelCommand = new RelayCommand(CancelEdit, () => CanCancel);
    }

    public ObservableCollection<PartnerDTO> Partners { get; } = [];
    public GridListState<PartnerDTO> Listing { get; }
    public IReadOnlyList<PartnerType> PartnerTypes { get; } = Enum.GetValues<PartnerType>();
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public string SearchText { get => _searchText; set => SetField(ref _searchText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set { if (SetField(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string Name { get => _name; set => SetField(ref _name, value); }
    public string? Document { get => _document; set => SetField(ref _document, value); }
    public string? Phone { get => _phone; set => SetField(ref _phone, value); }
    public PartnerType PartnerType { get => _partnerType; set => SetField(ref _partnerType, value); }
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
    public int PartnerId { get => _partnerId; private set { if (SetField(ref _partnerId, value)) OnPropertyChanged(nameof(PartnerCodeDisplay)); } }
    public string PartnerCodeDisplay => IsAdding ? "Novo" : RecordCodeDisplay.ForExistingRecord(PartnerId);
    public bool IsAdding => IsEditing && _isNew;
    public string DeleteConfirmationMessage => $"Excluir o parceiro \"{_partnerToDelete?.Name}\"?";

    public bool SelectionMode
    {
        get => _selectionMode;
        set
        {
            if (!SetField(ref _selectionMode, value)) return;
            if (value)
            {
                if (IsEditing) CancelEdit();
                CancelDelete();
            }
            RefreshCommands();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetField(ref _isBusy, value)) RefreshCommands(); }
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (!SetField(ref _isEditing, value)) return;
            OnPropertyChanged(nameof(IsAdding));
            OnPropertyChanged(nameof(PartnerCodeDisplay));
            Listing.Refresh();
            RefreshCommands();
        }
    }

    public bool ShowDeleteConfirmation
    {
        get => _showDeleteConfirmation;
        private set { if (SetField(ref _showDeleteConfirmation, value)) RefreshCommands(); }
    }

    public bool CanSearch => !IsBusy && !IsEditing && !ShowDeleteConfirmation && _authorization.HasPermission(DecorPermissions.PartnersView);
    public bool CanNew => !SelectionMode && CanSearch && _authorization.HasPermission(DecorPermissions.PartnersCreate);
    public bool CanEdit => !SelectionMode && CanSearch && SelectedPartner is not null && _authorization.HasPermission(DecorPermissions.PartnersEdit);
    public bool CanDelete => !SelectionMode && CanSearch && SelectedPartner is not null && _authorization.HasPermission(DecorPermissions.PartnersDelete);
    public bool CanSave => !SelectionMode && IsEditing && !IsBusy && _authorization.HasPermission(_isNew ? DecorPermissions.PartnersCreate : DecorPermissions.PartnersEdit);
    public bool CanCancel => IsEditing && !IsBusy;
    private bool CanConfirmDelete => !SelectionMode && ShowDeleteConfirmation && _partnerToDelete is not null && !IsBusy && _authorization.HasPermission(DecorPermissions.PartnersDelete);

    public PartnerDTO? SelectedPartner
    {
        get => _selectedPartner;
        set { if (SetField(ref _selectedPartner, value)) RefreshCommands(); }
    }

    public string? StatusPrimary => null;
    public bool HasStatusPrimary => false;
    public string? StatusSecondary => Listing.StatusSecondary;
    public bool HasStatusSecondary => Listing.HasStatusSecondary;
    public string? PaginationStatus => Listing.PaginationStatus;
    public string? PaginationPageStatus => Listing.PaginationPageStatus;
    public bool HasPagination => Listing.HasPagination;
    public ICommand? PreviousPageCommand => Listing.PreviousPageCommand;
    public ICommand? NextPageCommand => Listing.NextPageCommand;
    public ICommand? FirstPageCommand => Listing.FirstPageCommand;
    public ICommand? LastPageCommand => Listing.LastPageCommand;
    public bool HasPreviousPage => Listing.HasPreviousPage;
    public bool HasNextPage => Listing.HasNextPage;
    public bool HasFirstPage => Listing.HasFirstPage;
    public bool HasLastPage => Listing.HasLastPage;
    public IReadOnlyList<int> PageSizeOptions => Listing.PageSizeOptions;
    public int SelectedPageSize { get => Listing.SelectedPageSize; set => Listing.SelectedPageSize = value; }

    public void SetValueMatch(string columnName, int matchCount) => Listing.SetValueMatch(columnName, matchCount);
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task LoadPartnersAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSearch) return;
        await RunAsync(async () =>
        {
            var results = await RefreshListingAsync(cancellationToken);
            StatusMessage = results.Count == 0 ? "Nenhum parceiro encontrado." : $"{results.Count} parceiro(s).";
        }, "consultar", cancellationToken);
    }

    private async Task<List<PartnerDTO>> RefreshListingAsync(CancellationToken cancellationToken)
    {
        const int batchSize = 200;
        var results = new List<PartnerDTO>();
        for (var page = 1; ; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = (await _partnerService.SearchPartnersAsync(SearchText, page, batchSize, cancellationToken)).ToArray();
            results.AddRange(batch);
            if (batch.Length < batchSize) break;
        }

        cancellationToken.ThrowIfCancellationRequested();
        SelectedPartner = null;
        Listing.Load(results);
        return results;
    }

    private void ClearSearch()
    {
        if (!CanSearch) return;
        SearchText = string.Empty;
        SelectedPartner = null;
        Listing.Clear();
        StatusMessage = "Pesquisa limpa.";
    }

    public void BeginNew()
    {
        if (!CanNew) return;
        ResetForm();
        _isNew = true;
        SelectedPartner = null;
        IsEditing = true;
        StatusMessage = "Cadastrando um parceiro.";
    }

    public async Task BeginEditAsync(CancellationToken cancellationToken = default)
    {
        if (!CanEdit) return;
        var id = SelectedPartner!.PartnerID;
        await RunAsync(async () =>
        {
            var partner = await _partnerService.GetPartnerByIdAsync(id, cancellationToken);
            _isNew = false;
            PartnerId = partner.PartnerID;
            Name = partner.Name ?? string.Empty;
            Document = partner.Document;
            Phone = partner.Phone;
            PartnerType = partner.PartnerType;
            IsActive = partner.IsActive;
            ErrorMessage = string.Empty;
            IsEditing = true;
            StatusMessage = $"Editando o parceiro {PartnerId}.";
        }, "carregar", cancellationToken);
    }

    public void CancelEdit()
    {
        if (!CanCancel) return;
        IsEditing = false;
        _isNew = false;
        ResetForm();
        SelectedPartner = null;
        StatusMessage = string.Empty;
    }

    private async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSave) return;
        ErrorMessage = string.Empty;
        await RunAsync(async () =>
        {
            var partner = new PartnerDTO(PartnerId, NullIfEmpty(Name), NullIfEmpty(Document), NullIfEmpty(Phone), PartnerType, IsActive);
            await _partnerService.SavePartnerAsync(partner, cancellationToken);
            IsEditing = false;
            _isNew = false;
            StatusMessage = "Parceiro salvo com sucesso.";
            await RefreshAfterMutationAsync(cancellationToken);
        }, "salvar", cancellationToken);
    }

    private void BeginDelete()
    {
        if (!CanDelete) return;
        ErrorMessage = string.Empty;
        _partnerToDelete = SelectedPartner;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
        ShowDeleteConfirmation = true;
    }

    private void CancelDelete()
    {
        if (IsBusy) return;
        _partnerToDelete = null;
        ShowDeleteConfirmation = false;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }

    private async Task ConfirmDeleteAsync(CancellationToken cancellationToken = default)
    {
        if (!CanConfirmDelete) return;
        var id = _partnerToDelete!.PartnerID;
        await RunAsync(async () =>
        {
            await _partnerService.DeletePartnerAsync(id, cancellationToken);
            _partnerToDelete = null;
            ShowDeleteConfirmation = false;
            SelectedPartner = null;
            StatusMessage = "Parceiro excluído com sucesso.";
            await RefreshAfterMutationAsync(cancellationToken);
        }, "excluir", cancellationToken);
    }

    private async Task RefreshAfterMutationAsync(CancellationToken cancellationToken)
    {
        try { await RefreshListingAsync(cancellationToken); }
        catch (Exception) { Listing.Clear(); StatusMessage += " Pesquise novamente para atualizar a lista."; }
    }

    private async Task RunAsync(Func<Task> operation, string action, CancellationToken cancellationToken)
    {
        IsBusy = true;
        try { await operation(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { StatusMessage = "Operação cancelada."; }
        catch (ValidationException exception) { ErrorMessage = exception.Message; StatusMessage = IsEditing ? "Verifique os dados informados." : exception.Message; }
        catch (UnauthorizedAccessException) { StatusMessage = $"Você não possui permissão para {action} parceiros."; }
        catch (Exception) { StatusMessage = $"Não foi possível {action} o parceiro."; }
        finally { IsBusy = false; }
    }

    private void ResetForm()
    {
        PartnerId = 0;
        Name = string.Empty;
        Document = null;
        Phone = null;
        PartnerType = PartnerType.Fabricante;
        IsActive = true;
        ErrorMessage = string.Empty;
    }

    private void RefreshCommands()
    {
        foreach (var command in new[] { SearchCommand, ClearSearchCommand, NewCommand, EditCommand, DeleteCommand, ConfirmDeleteCommand, CancelDeleteCommand, SaveCommand, CancelCommand })
            ((RelayCommand)command).RaiseCanExecuteChanged();
        foreach (var property in new[] { nameof(CanSearch), nameof(CanNew), nameof(CanEdit), nameof(CanDelete), nameof(CanSave), nameof(CanCancel) })
            OnPropertyChanged(property);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}