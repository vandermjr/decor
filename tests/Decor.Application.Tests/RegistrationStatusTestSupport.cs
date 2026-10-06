using System.ComponentModel;
using System.Windows.Input;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Interfaces.Services;
using FluentAssertions;

namespace Decor.Application.Tests;

internal static class RegistrationStatusTestSupport
{
    internal static Task ExecuteAsync(INotifyPropertyChanged viewModel, ICommand command, Func<bool> isBusy)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (args.PropertyName != "IsBusy" || isBusy()) return;
            viewModel.PropertyChanged -= handler;
            completed.TrySetResult();
        };
        viewModel.PropertyChanged += handler;
        command.Execute(null);
        return completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    internal static List<string> ObserveStatus(IStatusBarSource viewModel)
    {
        var published = new List<string>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.StatusMessage))
                published.Add(viewModel.StatusMessage);
        };
        return published;
    }

    internal static void AssertNoListingStatus(IStatusBarSource viewModel)
    {
        viewModel.StatusPrimary.Should().BeNull();
        viewModel.StatusSecondary.Should().BeNull();
        viewModel.HasStatusPrimary.Should().BeFalse();
        viewModel.HasStatusSecondary.Should().BeFalse();
        viewModel.HasPagination.Should().BeFalse();
        viewModel.PaginationStatus.Should().BeNull();
        viewModel.PaginationPageStatus.Should().BeNull();
    }

    internal static Exception Failure(int kind) => kind switch
    {
        0 => new InvalidOperationException("Falha simulada"),
        1 => new UnauthorizedAccessException(),
        _ => new System.ComponentModel.DataAnnotations.ValidationException("Nome invalido")
    };

    internal sealed class AuthorizationService : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => true;
        public bool CanView(string resource) => true;
        public bool CanCreate(string resource) => true;
        public bool CanEdit(string resource) => true;
        public bool CanDelete(string resource) => true;
    }
}