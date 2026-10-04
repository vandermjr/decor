using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

public sealed class LoginViewModelTests
{
    [Fact]
    public async Task LoginAsync_WhenUserIdIsInvalid_UsesCodigoInVisibleMessage()
    {
        var authentication = new SuccessfulAuthenticationService();
        var viewModel = new LoginViewModel(authentication)
        {
            UserIdInput = "invalid",
            Password = "valid-password"
        };

        await viewModel.LoginAsync();

        viewModel.ErrorMessage.Should().Be("Informe um código de usuário válido.");
        authentication.LastUserId.Should().Be(0);
    }

    [Fact]
    public async Task LoginAsync_WhenPostAuthenticationInitializationFails_ShowsCopyableError()
    {
        var authentication = new SuccessfulAuthenticationService();
        var viewModel = new LoginViewModel(authentication)
        {
            UserIdInput = "42",
            Password = "valid-password"
        };
        viewModel.LoginSucceeded += () => throw new InvalidOperationException("system_settings table is missing");

        await viewModel.LoginAsync();

        authentication.LastUserId.Should().Be(42);
        viewModel.ErrorMessage.Should().Contain("O login foi validado").And.Contain("system_settings table is missing");
        viewModel.CanCopyError.Should().BeTrue();
        viewModel.Password.Should().BeEmpty();
        viewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_WhenAuthenticationSucceeds_AwaitsMainWindowStartup()
    {
        var viewModel = new LoginViewModel(new SuccessfulAuthenticationService())
        {
            UserIdInput = "42",
            Password = "valid-password"
        };
        var startupCompleted = false;
        viewModel.LoginSucceeded += async () =>
        {
            await Task.Yield();
            startupCompleted = true;
        };

        await viewModel.LoginAsync();

        startupCompleted.Should().BeTrue();
        viewModel.HasError.Should().BeFalse();
    }

    private sealed class SuccessfulAuthenticationService : IAuthenticationService
    {
        public int LastUserId { get; private set; }

        public Task<AuthenticationResult> AuthenticateAsync(int userId, string password, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            return Task.FromResult(new AuthenticationResult(true, new ApplicationUser
            {
                UserID = userId,
                Username = "test-user"
            }));
        }
    }
}