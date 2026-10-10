using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.AvaloniaUI.Services;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.Views;

public partial class UsersView : UserControl
{
	public UsersView()
	{
		InitializeComponent();
		UsersGrid.InitializeColumns(
			dtoType: typeof(AdministrativeUserDTO),
			getDisplayName: name => name switch
			{
				nameof(AdministrativeUserDTO.UserID) => "Código",
				nameof(AdministrativeUserDTO.PresentationName) => "Usuário",
				_ => name
			},
			propertyNames: [nameof(AdministrativeUserDTO.UserID), nameof(AdministrativeUserDTO.PresentationName), nameof(AdministrativeUserDTO.IsActive)]);
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
		_ = viewModel.InitializeAsync();
	}

	public UsersView(UsersViewModel viewModel, INavigationService navigationService, IAuthenticatedUserContext authenticatedUserContext)
		: this(viewModel)
	{
	}

}
