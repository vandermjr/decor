using Avalonia.Controls;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace Decor.AvaloniaUI.Views;
public partial class UsersView : UserControl
{
 public UsersView(){InitializeComponent(); AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.Focus());}
 public UsersView(UsersViewModel viewModel, IServiceProvider services) : this(){DataContext=viewModel; viewModel.NewUserRequested+=async (_, _) => { var window=services.GetRequiredService<CreateUserWindow>(); if(TopLevel.GetTopLevel(this) is not Window owner)return; await window.ShowDialog(owner); if(window.CreatedUsername is not null) await viewModel.HandleUserCreatedAsync(window.CreatedUsername); }; _=viewModel.InitializeAsync();}
}
