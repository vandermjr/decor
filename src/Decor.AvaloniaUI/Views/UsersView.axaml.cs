using Avalonia.Controls;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.AvaloniaUI.Services;
namespace Decor.AvaloniaUI.Views;
public partial class UsersView : UserControl
{
 private bool _isEditUserOpen;
 public UsersView(){InitializeComponent(); AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.Focus());}
 public UsersView(UsersViewModel viewModel, INavigationService navigationService) : this(){DataContext=viewModel; viewModel.NewUserRequested+=async (_, _) => { var window=navigationService.Resolve<CreateUserWindow>(); if(TopLevel.GetTopLevel(this) is not Window owner)return; await navigationService.ShowDialogAsync(owner, window); if(window.CreatedUsername is not null) await viewModel.HandleUserCreatedAsync(window.CreatedUsername); }; viewModel.EditUserRequested+=async (_, _) => { if(_isEditUserOpen||viewModel.SelectedUser is null)return; var userId=viewModel.SelectedUser.UserID; if(TopLevel.GetTopLevel(this) is not Window owner)return; _isEditUserOpen=true; try { var window=navigationService.Resolve<EditUserWindow>(); window.Initialize(viewModel.SelectedUser); await navigationService.ShowDialogAsync(owner, window); if(window.WasSaved) await viewModel.HandleUserUpdatedAsync(userId); } finally { _isEditUserOpen=false; } }; _=viewModel.InitializeAsync();}
}
