using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class EmployeesViewModel : IStatusBarSource, IWorkspaceDocumentState
{
    private readonly IEmployeeService _employeeService;
    private readonly IAuthorizationService _authorizationService;
    private EmployeeDTO? _selectedEmployee;
    private EmployeeDTO? _employeeToDelete;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private bool _isEditing;
    private bool _isNew;
    private bool _selectionMode;
    private int? _employeeSelectionUserId;
    private bool _showDeleteConfirmation;
    private int _employeeId;
    private string _name = string.Empty;
    private string _jobTitle = string.Empty;
    private string _baseSalaryInput = string.Empty;
    private string _workScheduleNote = string.Empty;
    private string _document = string.Empty;
    private string _phone = string.Empty;
    private string _userIdInput = string.Empty;
    private bool _isActive = true;
    private int _recordCount;

    public EmployeesViewModel(IEmployeeService employeeService, IAuthorizationService authorizationService)
    {
        _employeeService = employeeService;
        _authorizationService = authorizationService;
        SearchCommand = new RelayCommand(async () => await LoadEmployeesAsync(), () => !IsBusy && !IsEditing);
        ClearSearchCommand = new RelayCommand(ClearSearch, () => !IsBusy && !IsEditing);
        Listing = new GridListState<EmployeeDTO>(Employees, () => !IsEditing);
        Listing.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        NewCommand = new RelayCommand(async () => await BeginNewAsync(), () => CanNew);
        EditCommand = new RelayCommand(BeginEdit, () => CanEdit);
        DeleteCommand = new RelayCommand(BeginDelete, () => CanDelete);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        CancelCommand = new RelayCommand(CancelEdit, () => CanCancel);
        ConfirmDeleteCommand = new RelayCommand(async () => await ConfirmDeleteAsync(), () => CanConfirmDelete);
        CancelDeleteCommand = new RelayCommand(CancelDelete, () => true);
    }

    public ObservableCollection<EmployeeDTO> Employees { get; } = [];
    public GridListState<EmployeeDTO> Listing { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }

    public string SearchText { get => _searchText; set => SetField(ref _searchText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set { if (SetField(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) RefreshCommands(); } }
    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetField(ref _isEditing, value))
            {
                RefreshCommands();
                Listing.Refresh();
                OnPropertyChanged(nameof(IsAdding));
                OnPropertyChanged(nameof(EmployeeCodeDisplay));
            }
        }
    }
    public bool IsAdding => IsEditing && _isNew;
    public bool SelectionMode
    {
        get => _selectionMode;
        set
        {
            if (!SetField(ref _selectionMode, value)) return;
            if (value && IsEditing) CancelEdit();
            RefreshCommands();
        }
    }
    public int? EmployeeSelectionUserId
    {
        get => _employeeSelectionUserId;
        set => _employeeSelectionUserId = value;
    }
    public bool ShowDeleteConfirmation { get => _showDeleteConfirmation; private set { if (SetField(ref _showDeleteConfirmation, value)) RefreshCommands(); } }
    public string DeleteConfirmationMessage => _employeeToDelete is null ? string.Empty : $"Excluir o funcionário \"{_employeeToDelete.Name}\"?";
    public bool CanNew => !SelectionMode && _authorizationService.HasPermission(DecorPermissions.EmployeesCreate) && !IsBusy && !IsEditing;
    public bool CanEdit => !SelectionMode && _authorizationService.HasPermission(DecorPermissions.EmployeesEdit) && SelectedEmployee is not null && !IsBusy && !IsEditing;
    public bool CanDelete => !SelectionMode && _authorizationService.HasPermission(DecorPermissions.EmployeesDelete) && SelectedEmployee is not null && !IsBusy && !IsEditing;
    public bool CanSave => !SelectionMode && IsEditing && !IsBusy && _authorizationService.HasPermission(_isNew ? DecorPermissions.EmployeesCreate : DecorPermissions.EmployeesEdit);
    private bool CanConfirmDelete => !IsBusy && ShowDeleteConfirmation && _employeeToDelete is not null && _authorizationService.HasPermission(DecorPermissions.EmployeesDelete);
    public bool CanCancel => IsEditing && !IsBusy;
    public EmployeeDTO? SelectedEmployee
    {
        get => _selectedEmployee;
        set
        {
            if (!SetField(ref _selectedEmployee, value)) return;
            RefreshCommands();
        }
    }

    public int EmployeeId
    {
        get => _employeeId;
        private set
        {
            if (SetField(ref _employeeId, value))
                OnPropertyChanged(nameof(EmployeeCodeDisplay));
        }
    }
    public string EmployeeCodeDisplay => IsAdding
        ? RecordCodeDisplay.ForNewRecord(_recordCount)
        : RecordCodeDisplay.ForExistingRecord(EmployeeId);
    public string Name { get => _name; set => SetField(ref _name, value); }
    public string JobTitle { get => _jobTitle; set => SetField(ref _jobTitle, value); }
    public string BaseSalaryInput { get => _baseSalaryInput; set => SetField(ref _baseSalaryInput, value); }
    public string WorkScheduleNote { get => _workScheduleNote; set => SetField(ref _workScheduleNote, value); }
    public string Document { get => _document; set => SetField(ref _document, value); }
    public string Phone { get => _phone; set => SetField(ref _phone, value); }
    public string UserIdInput { get => _userIdInput; set => SetField(ref _userIdInput, value); }
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }

    public string? StatusPrimary => null;
    public string? StatusSecondary => Listing.StatusSecondary;
    public bool HasStatusPrimary => false;
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

    public Task InitializeAsync() => LoadEmployeesAsync();

    public async Task BeginNewAsync()
    {
        try
        {
            _recordCount = await RecordCodeDisplay.CountAllAsync(async (page, pageSize) =>
                await _employeeService.GetAllEmployeesAsync(page, pageSize));
        }
        catch
        {
            _recordCount = Employees.Count;
        }
        OnPropertyChanged(nameof(EmployeeCodeDisplay));
        _isNew = true;
        EmployeeId = 0;
        Name = string.Empty;
        JobTitle = string.Empty;
        BaseSalaryInput = string.Empty;
        WorkScheduleNote = string.Empty;
        Document = string.Empty;
        Phone = string.Empty;
        UserIdInput = string.Empty;
        IsActive = true;
        ErrorMessage = string.Empty;
        SelectedEmployee = null;
        IsEditing = true;
        StatusMessage = "Cadastrando um funcionário.";
    }

    public void BeginEdit()
    {
        if (SelectedEmployee is null) return;
        var employee = SelectedEmployee;
        _isNew = false;
        EmployeeId = employee.EmployeeID;
        Name = employee.Name ?? string.Empty;
        JobTitle = employee.JobTitle ?? string.Empty;
        BaseSalaryInput = employee.BaseSalary?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        WorkScheduleNote = employee.WorkScheduleNote ?? string.Empty;
        Document = employee.Document ?? string.Empty;
        Phone = employee.Phone ?? string.Empty;
        UserIdInput = employee.UserID?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        IsActive = employee.IsActive;
        ErrorMessage = string.Empty;
        IsEditing = true;
        StatusMessage = $"Editando o funcionário {EmployeeId}.";
    }

    public void CancelEdit()
    {
        IsEditing = false;
        _isNew = false;
        SelectedEmployee = null;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
        SelectedEmployee = null;
        Listing.Clear();
        StatusMessage = "Pesquisa limpa.";
    }

    public void BeginDelete()
    {
        if (SelectedEmployee is null) return;
        _employeeToDelete = SelectedEmployee;
        ShowDeleteConfirmation = true;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }

    public void CancelDelete()
    {
        _employeeToDelete = null;
        ShowDeleteConfirmation = false;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }

    public async Task ConfirmDeleteAsync()
    {
        if (!CanConfirmDelete) return;
        if (_employeeToDelete is null) return;
        IsBusy = true;
        try
        {
            await _employeeService.DeleteEmployeeAsync(_employeeToDelete.EmployeeID);
            StatusMessage = "Funcionário excluído com sucesso.";
            CancelDelete();
            SelectedEmployee = null;
            await LoadEmployeesAsync();
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Você não possui permissão para excluir funcionários.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível excluir o funcionário.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadEmployeesAsync()
    {
        IsBusy = true;
        try
        {
            const int pageSize = 500;
            var page = 1;
            var all = new List<EmployeeDTO>();
            IReadOnlyCollection<EmployeeDTO> employees;
            do
            {
                employees = (await _employeeService.SearchEmployeesAsync(SearchText, page, pageSize)).ToArray();
                all.AddRange(employees);
                page++;
            } while (employees.Count == pageSize);
            if (SelectionMode)
                all = all.Where(employee => employee.IsActive
                    && (employee.UserID is null || employee.UserID == EmployeeSelectionUserId)).ToList();
            Listing.Load(all);
            OnPropertyChanged(nameof(EmployeeCodeDisplay));

            if (!IsEditing)
                StatusMessage = all.Count == 0
                    ? "Nenhum funcionário encontrado."
                    : $"{all.Count} {(all.Count == 1 ? "funcionário" : "funcionários")}.";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Você não possui permissão para consultar funcionários.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar os funcionários.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        if (!CanSave) return;
        ErrorMessage = string.Empty;
        decimal? salary = null;
        if (!string.IsNullOrWhiteSpace(BaseSalaryInput))
        {
            if (!decimal.TryParse(BaseSalaryInput, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedSalary)
                && !decimal.TryParse(BaseSalaryInput, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedSalary))
            {
                ErrorMessage = "Informe um salário base válido.";
                return;
            }
            salary = parsedSalary;
        }

        int? userId = null;
        if (!string.IsNullOrWhiteSpace(UserIdInput))
        {
            if (!int.TryParse(UserIdInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedUserId) || parsedUserId <= 0)
            {
                ErrorMessage = "Informe um código de usuário válido ou deixe o campo vazio.";
                return;
            }
            userId = parsedUserId;
        }

        IsBusy = true;
        try
        {
            var employee = new EmployeeDTO(EmployeeId, Name, NullIfEmpty(JobTitle), salary, NullIfEmpty(WorkScheduleNote),
                NullIfEmpty(Document), NullIfEmpty(Phone), IsActive, userId);
            await _employeeService.SaveEmployeeAsync(employee);
            StatusMessage = _isNew ? "Funcionário incluído com sucesso." : "Funcionário atualizado com sucesso.";
            IsEditing = false;
            _isNew = false;
            await LoadEmployeesAsync();
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = "Verifique os dados informados.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "Você não possui permissão para salvar funcionários.";
        }
        catch (Exception)
        {
            ErrorMessage = "Não foi possível salvar o funcionário.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshCommands()
    {
        OnPropertyChanged(nameof(CanNew));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanCancel));
        foreach (var command in new[] { SearchCommand, ClearSearchCommand, NewCommand, EditCommand, DeleteCommand, SaveCommand, CancelCommand, ConfirmDeleteCommand })
            ((RelayCommand)command).RaiseCanExecuteChanged();
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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