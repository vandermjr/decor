using Avalonia.Controls;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Views;

public partial class GroupsView : UserControl
{
    public GroupsView()
    {
        InitializeComponent();
        RolesGrid.InitializeColumns(
            dtoType: typeof(AdministrativeRoleDTO),
            getDisplayName: name => name switch
            {
                nameof(AdministrativeRoleDTO.RoleID) => "Código",
                nameof(AdministrativeRoleDTO.RoleName) => "Nome",
                nameof(AdministrativeRoleDTO.Description) => "Descrição",
                nameof(AdministrativeRoleDTO.IsSystemProtected) => "Protegido",
                _ => name
            },
            propertyNames: [nameof(AdministrativeRoleDTO.RoleID), nameof(AdministrativeRoleDTO.RoleName),
                nameof(AdministrativeRoleDTO.Description), nameof(AdministrativeRoleDTO.IsSystemProtected)]);
        RolesGrid.ValueMatchChanged += (_, eventArgs) =>
        {
            if (DataContext is GroupRegistrationViewModel viewModel)
                viewModel.SetValueMatch(eventArgs.ColumnName, eventArgs.MatchCount);
        };
    }

    public GroupsView(GroupRegistrationViewModel viewModel) : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }
}