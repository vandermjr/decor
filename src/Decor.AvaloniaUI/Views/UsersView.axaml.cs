using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Decor.AvaloniaUI.ViewModels;
using Decor.AvaloniaUI.Services;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.Views;

public partial class UsersView : UserControl
{
	private INavigationService? _navigationService;
	private IEmployeeService? _employeeService;
	private IAuthorizationService? _authorization;
	private UserFormView? _userForm;

	public UsersView()
	{
		InitializeComponent();
		_userForm = this.FindControl<UserFormView>("UserForm");
		UsersGrid.InitializeColumns(
			dtoType: typeof(AdministrativeUserDTO),
			getDisplayName: name => name switch
			{
				nameof(AdministrativeUserDTO.UserID) => "Código",
				nameof(AdministrativeUserDTO.PresentationName) => "Usuário",
				nameof(AdministrativeUserDTO.EmployeeName) => "Funcionário",
				_ => name
			},
			propertyNames: [nameof(AdministrativeUserDTO.UserID), nameof(AdministrativeUserDTO.PresentationName),
				nameof(AdministrativeUserDTO.EmployeeName), nameof(AdministrativeUserDTO.IsActive)]);
		UsersGrid.ValueMatchChanged += (_, eventArgs) =>
		{
			if (DataContext is UsersViewModel viewModel)
				viewModel.SetValueMatch(eventArgs.ColumnName, eventArgs.MatchCount);
		};
		AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() =>
		{
			if (DataContext is UsersViewModel { IsEditing: false }) SearchTextBox.FocusTextInput();
		});
	}

	public UsersView(UsersViewModel viewModel) : this()
	{
		DataContext = viewModel;
		viewModel.PropertyChanged += (_, args) =>
		{
			if (args.PropertyName == nameof(UsersViewModel.IsEditing) && !viewModel.IsEditing)
				Dispatcher.UIThread.Post(() => SearchTextBox.FocusTextInput());
		};
		viewModel.NewUserRequested += async (_, _) =>
		{
			if (viewModel.ActiveForm is { } form) await form.InitializeAsync();
		};
		viewModel.EditUserRequested += async (_, _) =>
		{
			if (viewModel.ActiveForm is { } form) await form.InitializeAsync();
		};
	}

	public UsersView(UsersViewModel viewModel, INavigationService navigationService, IAuthenticatedUserContext authenticatedUserContext,
		IEmployeeService? employeeService = null, IAuthorizationService? authorization = null)
		: this(viewModel)
	{
		_navigationService = navigationService;
		_employeeService = employeeService;
		_authorization = authorization;
		if (_userForm is not null)
			_userForm.EmployeeLookupRequested += async (_, _) => await OpenEmployeeLookupAsync();
	}

	private async Task OpenEmployeeLookupAsync()
	{
		if (_navigationService is null || _employeeService is null || _authorization is null
			|| DataContext is not UsersViewModel { ActiveForm: { CanSelectEmployee: true } form }
			|| TopLevel.GetTopLevel(this) is not Window owner)
			return;

		var viewModel = new EmployeesViewModel(_employeeService, _authorization)
		{
			SelectionMode = true,
			EmployeeSelectionUserId = form.UserId == 0 ? null : form.UserId
		};
		var view = new EmployeesView(viewModel);
		await viewModel.InitializeAsync();
		var dialog = new LookupSelectionWindow(view, viewModel,
			() => viewModel.SelectedEmployee is { IsActive: true } employee ? employee : null,
			nameof(viewModel.SelectedEmployee), "Selecionar funcionário");
		if (await _navigationService.ShowDialogAsync<bool>(owner, dialog)
			&& dialog.SelectedValue is EmployeeDTO selectedEmployee)
			form.SetEmployee(selectedEmployee);
	}

}
