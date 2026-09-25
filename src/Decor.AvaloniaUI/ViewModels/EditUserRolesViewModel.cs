using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class EditUserRoleOption
{
    public required AdministrativeRoleDTO Role { get; init; }
    public bool IsSelected { get; set; }
}

public sealed class EditUserRolesViewModel : INotifyPropertyChanged
{
    private readonly IUserAdministrationService _userAdministrationService;
    private readonly IRoleAdministrationService _roleAdministrationService;
    private CancellationTokenSource? _operationCancellation;
    private int _userId;
    private string? _errorMessage;
    private bool _isBusy;
    private bool _isInitialized;

    public EditUserRolesViewModel(
        IUserAdministrationService userAdministrationService,
        IRoleAdministrationService roleAdministrationService)
    {
        _userAdministrationService = userAdministrationService;
        _roleAdministrationService = roleAdministrationService;
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty), () => !IsBusy);
    }

    public ObservableCollection<EditUserRoleOption> Roles { get; } = [];
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CloseRequested;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetField(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RaiseCommandStates();
                OnPropertyChanged(nameof(CanEditFields));
            }
        }
    }

    public bool CanEditFields => !IsBusy && IsInitialized;
    public bool CanSave => !IsBusy && IsInitialized;
    public bool IsInitialized
    {
        get => _isInitialized;
        private set
        {
            if (SetField(ref _isInitialized, value))
            {
                RaiseCommandStates();
                OnPropertyChanged(nameof(CanEditFields));
            }
        }
    }

    public void Initialize(AdministrativeUserDTO user)
    {
        _userId = user.UserID;
        IsInitialized = false;
        ErrorMessage = null;
        Roles.Clear();
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        _ = LoadRolesAsync(user, _operationCancellation.Token);
    }

    private async Task LoadRolesAsync(AdministrativeUserDTO user, CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var assignedRoleIds = user.Roles.Select(role => role.RoleID).ToHashSet();
            foreach (var role in await _roleAdministrationService.GetRolesAsync(cancellationToken))
            {
                Roles.Add(new EditUserRoleOption
                {
                    Role = role,
                    IsSelected = assignedRoleIds.Contains(role.RoleID)
                });
            }

            IsInitialized = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Roles.Clear();
            IsInitialized = false;
            ErrorMessage = "O carregamento dos grupos de permissões foi cancelado.";
        }
        catch (Exception)
        {
            Roles.Clear();
            IsInitialized = false;
            ErrorMessage = "Não foi possível carregar os grupos de permissões.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        var cancellationToken = _operationCancellation?.Token ?? CancellationToken.None;
        try
        {
            var roleIds = Roles
                .Where(option => option.IsSelected)
                .Select(option => option.Role.RoleID)
                .ToArray();
            await _userAdministrationService.ReplaceRolesAsync(_userId, roleIds, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "Você não possui permissão para atribuir um ou mais grupos de permissões.";
            return;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ErrorMessage = "A operação foi cancelada.";
            return;
        }
        catch (Exception)
        {
            ErrorMessage = "Não foi possível salvar os grupos de permissões.";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseCommandStates()
    {
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}