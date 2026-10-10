using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;
using Moq;

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

    [Fact]
    public async Task UserIdInput_LoadsThemePreviewForThatUser()
    {
        var settings = new Mock<IUserSettingsService>(MockBehavior.Strict);
        settings.Setup(service => service.GetThemeForUserAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DecorThemeStyle.Light);
        var viewModel = new LoginViewModel(new SuccessfulAuthenticationService(), settings.Object);
        var preview = new TaskCompletionSource<DecorThemeStyle>(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.ThemePreviewChanged += theme => preview.TrySetResult(theme);

        viewModel.UserIdInput = "42";

        (await preview.Task.WaitAsync(TimeSpan.FromSeconds(3))).Should().Be(DecorThemeStyle.Light);
        settings.Verify(service => service.GetThemeForUserAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        viewModel.StopThemePreview();
    }

    [Fact]
    public async Task RefreshDatabaseStatusAsync_ReportsOnlineAndOffline()
    {
        var database = new MutableDatabaseHealthService { IsOnline = true };
        var viewModel = new LoginViewModel(new SuccessfulAuthenticationService(), databaseHealthService: database);

        await viewModel.RefreshDatabaseStatusAsync();
        viewModel.IsDatabaseOnline.Should().BeTrue();
        viewModel.DatabaseStatusText.Should().Be("Online");

        database.IsOnline = false;
        await viewModel.RefreshDatabaseStatusAsync();
        viewModel.IsDatabaseOnline.Should().BeFalse();
        viewModel.DatabaseStatusText.Should().Be("Offline");
    }

    [Fact]
    public async Task RefreshDatabaseStatusAsync_ReportsOfflineWhenHealthCheckThrows()
    {
        var database = new Mock<IDatabaseHealthService>(MockBehavior.Strict);
        database.Setup(service => service.IsOnlineAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection failed"));
        var viewModel = new LoginViewModel(new SuccessfulAuthenticationService(), databaseHealthService: database.Object);

        await viewModel.RefreshDatabaseStatusAsync();

        viewModel.IsDatabaseOnline.Should().BeFalse();
        viewModel.DatabaseStatusText.Should().Be("Offline");
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

    private sealed class MutableDatabaseHealthService : IDatabaseHealthService
    {
        public bool IsOnline { get; set; }
        public string ConnectionDescription => "Banco: decor_test | Conexão: localhost";
        public Task<bool> IsOnlineAsync(CancellationToken cancellationToken = default) => Task.FromResult(IsOnline);
    }
}