using Avalonia.Controls;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.AvaloniaUI.Services;
namespace Decor.AvaloniaUI.Views;
public partial class UsersView : UserControl
{
 public UsersView(){InitializeComponent(); AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.Focus());}
 public UsersView(UsersViewModel viewModel, INavigationService navigationService) : this(){DataContext=viewModel; viewModel.NewUserRequested+=async (_, _) => { var window=navigationService.Resolve<CreateUserWindow>(); if(TopLevel.GetTopLevel(this) is not Window owner)return; await navigationService.ShowDialogAsync(owner, window); if(window.CreatedUsername is not null) await viewModel.HandleUserCreatedAsync(window.CreatedUsername); }; _=viewModel.InitializeAsync();}
}
