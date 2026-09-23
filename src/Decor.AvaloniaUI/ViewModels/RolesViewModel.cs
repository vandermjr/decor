using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class RolePermissionItemViewModel
{
    public required AdministrativePermissionDTO Permission { get; init; }
    public bool IsGranted { get; init; }
    public string StateText => IsGranted ? "Concedida" : "Não concedida";
}

public sealed class RolesViewModel : INotifyPropertyChanged
{
    private readonly IRoleAdministrationService _roleAdministrationService;
    private CancellationTokenSource? _permissionsCancellation;
    private AdministrativeRoleDTO? _selectedRole;
    private bool _isLoading;
    private bool _isPermissionsLoading;
    private string? _errorMessage;

    public RolesViewModel(IRoleAdministrationService roleAdministrationService)
    {
        _roleAdministrationService = roleAdministrationService;
    }

    public ObservableCollection<AdministrativeRoleDTO> Roles { get; } = [];
    public ObservableCollection<RolePermissionItemViewModel> Permissions { get; } = [];
    public bool HasRoles => Roles.Count > 0;
        public bool HasNoRoles => !IsLoading && Roles.Count == 0;
    public bool HasNoPermissions => !IsPermissionsLoading && Permissions.Count == 0;

    public AdministrativeRoleDTO? SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (!SetField(ref _selectedRole, value)) return;
            Permissions.Clear();
            OnPropertyChanged(nameof(HasSelectedRole));
            OnPropertyChanged(nameof(HasPermissions));
            _ = LoadPermissionsAsync(value);
        }
    }

    public bool HasSelectedRole => SelectedRole is not null;
    public bool HasPermissions => Permissions.Count > 0;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetField(ref _isLoading, value)) return;
            OnPropertyChanged(nameof(HasNoRoles));
        }
    }

    public bool IsPermissionsLoading
    {
        get => _isPermissionsLoading;
        private set
        {
            if (!SetField(ref _isPermissionsLoading, value)) return;
            OnPropertyChanged(nameof(HasNoPermissions));
        }
    }

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

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Roles.Clear();
            foreach (var role in await _roleAdministrationService.GetRolesAsync())
                Roles.Add(role);
            OnPropertyChanged(nameof(HasRoles));
                OnPropertyChanged(nameof(HasNoRoles));
        }
        catch (Exception)
        {
            ErrorMessage = "Não foi possível carregar os grupos de permissões.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPermissionsAsync(AdministrativeRoleDTO? role)
    {
        _permissionsCancellation?.Cancel();
        _permissionsCancellation?.Dispose();
        _permissionsCancellation = null;

        if (role is null)
        {
            IsPermissionsLoading = false;
            return;
        }

        var cancellation = new CancellationTokenSource();
        _permissionsCancellation = cancellation;
        IsPermissionsLoading = true;
        ErrorMessage = null;

        try
        {
            var allPermissionsTask = _roleAdministrationService.GetAllPermissionsAsync(cancellation.Token);
            var grantedPermissionsTask = _roleAdministrationService.GetPermissionsAsync(role.RoleID, cancellation.Token);
            await Task.WhenAll(allPermissionsTask, grantedPermissionsTask);

            if (cancellation.IsCancellationRequested || !ReferenceEquals(SelectedRole, role)) return;

            var grantedPermissionIds = grantedPermissionsTask.Result
                .Select(permission => permission.PermissionID)
                .ToHashSet();

            Permissions.Clear();
            OnPropertyChanged(nameof(HasNoPermissions));
            foreach (var permission in allPermissionsTask.Result.OrderBy(permission => permission.PermissionCode, StringComparer.OrdinalIgnoreCase))
            {
                Permissions.Add(new RolePermissionItemViewModel
                {
                    Permission = permission,
                    IsGranted = grantedPermissionIds.Contains(permission.PermissionID)
                });
            }

            OnPropertyChanged(nameof(HasPermissions));
            OnPropertyChanged(nameof(HasNoPermissions));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (!cancellation.IsCancellationRequested && ReferenceEquals(SelectedRole, role))
            {
                Permissions.Clear();
                OnPropertyChanged(nameof(HasPermissions));
                OnPropertyChanged(nameof(HasNoPermissions));
                ErrorMessage = "Não foi possível carregar as permissions do grupo.";
            }
        }
        finally
        {
            if (ReferenceEquals(_permissionsCancellation, cancellation))
            {
                _permissionsCancellation = null;
                IsPermissionsLoading = false;
            }

            cancellation.Dispose();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
