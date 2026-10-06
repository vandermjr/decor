using System.IO.Compression;
using System.Reflection;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

public sealed class DatabaseMaintenanceRestoreViewModelTests : IDisposable
{
    private const string ConfirmationPhrase = "RESTAURAR BANCO";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"decor-restore-vm-{Guid.NewGuid():N}");
    private readonly List<DatabaseMaintenanceViewModel> _viewModels = [];
    private readonly FakeBackupService _backup = new();
    private readonly FakeRestoreService _restore = new();
    private readonly FakeAuthorizationService _authorization = new();
    private readonly FakeAuthenticatedUserContext _context = new();

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public async Task CanRestore_RequiresRestorePermissionAndAuthenticatedSession(
        bool hasPermission, bool authenticated, bool expected)
    {
        _authorization.HasRestorePermission = hasPermission;
        if (!authenticated)
            _context.SignOut();
        var viewModel = CreateViewModel();
        viewModel.RestoreFile = CreateZip();
        viewModel.RestoreConfirmation = ConfirmationPhrase;

        Assert.Equal(expected, viewModel.CanRestore);
        Assert.Equal(expected, viewModel.RequestRestoreCommand.CanExecute(null));
        Assert.False(viewModel.ConfirmRestoreCommand.CanExecute(null));
        if (authenticated)
            Assert.Contains(DecorPermissions.DatabaseMaintenanceRestore, _authorization.RequestedPermissions);
        else
            Assert.Empty(_authorization.RequestedPermissions);

        if (!expected)
        {
            Assert.Empty(viewModel.RestoreFile);
            viewModel.RequestRestoreCommand.Execute(null);
            await viewModel.RestoreAsync();
            Assert.False(viewModel.ShowRestoreConfirmation);
            Assert.Equal(0, _restore.CallCount);
        }
        Assert.Equal(0, _backup.CallCount);
    }

    [Theory]
    [InlineData("restore")]
    [InlineData("authorization")]
    [InlineData("context")]
    public async Task CanRestore_MissingDependencyDisablesRestore(string missingDependency)
    {
        var viewModel = Track(new DatabaseMaintenanceViewModel(_backup,
            missingDependency == "restore" ? null : _restore,
            missingDependency == "authorization" ? null : _authorization,
            missingDependency == "context" ? null : _context));
        viewModel.RestoreFile = CreateZip();
        viewModel.RestoreConfirmation = ConfirmationPhrase;

        Assert.False(viewModel.CanRestore);
        Assert.False(viewModel.RequestRestoreCommand.CanExecute(null));
        Assert.False(viewModel.ConfirmRestoreCommand.CanExecute(null));
        await viewModel.RestoreAsync();
        Assert.Equal(0, _restore.CallCount);
        Assert.Equal(0, _backup.CallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("missing.zip")]
    public async Task RequestRestore_InvalidOrNonexistentFileDoesNotOpenConfirmation(string selection)
    {
        var viewModel = CreateViewModel();
        viewModel.RestoreFile = selection == "missing.zip" ? Path.Combine(_directory, selection) : selection;

        viewModel.RequestRestoreCommand.Execute(null);
        viewModel.RestoreConfirmation = ConfirmationPhrase;
        await viewModel.RestoreAsync();

        Assert.False(viewModel.ShowRestoreConfirmation);
        Assert.False(viewModel.ConfirmRestoreCommand.CanExecute(null));
        Assert.False(viewModel.RestoreCompleted);
        Assert.Equal("Selecione um arquivo de backup existente.", viewModel.RestoreStatus);
        Assert.Equal(0, _restore.CallCount);
        Assert.Equal(0, _backup.CallCount);
    }

    [Fact]
    public void RequestRestore_ExistingZipOpensConfirmationAndClearsPreviousPhrase()
    {
        var viewModel = CreateViewModel();
        var file = CreateZip();
        viewModel.RestoreFile = file;
        viewModel.RestoreConfirmation = ConfirmationPhrase;

        Assert.True(viewModel.RequestRestoreCommand.CanExecute(null));
        viewModel.RequestRestoreCommand.Execute(null);

        Assert.Equal(file, viewModel.RestoreFile);
        Assert.True(viewModel.ShowRestoreConfirmation);
        Assert.Empty(viewModel.RestoreConfirmation);
        Assert.Empty(viewModel.RestoreStatus);
        Assert.False(viewModel.ConfirmRestoreCommand.CanExecute(null));
        Assert.Equal(0, _restore.CallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("restaurar banco")]
    [InlineData("RESTAURAR")]
    [InlineData(" RESTAURAR BANCO")]
    [InlineData("RESTAURAR BANCO ")]
    public async Task RestoreAsync_RequiresExactConfirmationPhrase(string phrase)
    {
        var viewModel = PrepareConfirmation();
        viewModel.RestoreConfirmation = phrase;

        Assert.False(viewModel.ConfirmRestoreCommand.CanExecute(null));
        await viewModel.RestoreAsync();

        Assert.Equal(0, _restore.CallCount);
        Assert.False(viewModel.RestoreCompleted);
        Assert.True(viewModel.ShowRestoreConfirmation);
        viewModel.RestoreConfirmation = ConfirmationPhrase;
        Assert.True(viewModel.ConfirmRestoreCommand.CanExecute(null));
    }

    [Fact]
    public async Task ChangingSelection_CancelsConfirmationAndClearsPhrase()
    {
        var viewModel = PrepareConfirmation();
        var replacement = CreateZip();

        viewModel.RestoreFile = replacement;

        Assert.Equal(replacement, viewModel.RestoreFile);
        Assert.False(viewModel.ShowRestoreConfirmation);
        Assert.Empty(viewModel.RestoreConfirmation);
        Assert.False(viewModel.ConfirmRestoreCommand.CanExecute(null));
        await viewModel.RestoreAsync();
        Assert.Equal(0, _restore.CallCount);
    }

    [Fact]
    public async Task CancelRestore_ClearsConfirmationWithoutRestoring()
    {
        var viewModel = PrepareConfirmation();

        Assert.True(viewModel.CancelRestoreCommand.CanExecute(null));
        viewModel.CancelRestoreCommand.Execute(null);
        await viewModel.RestoreAsync();

        Assert.False(viewModel.ShowRestoreConfirmation);
        Assert.Empty(viewModel.RestoreConfirmation);
        Assert.Equal(0, _restore.CallCount);
    }

    [Fact]
    public async Task RestoreAsync_SuccessLocksMaintenanceAndFinishSignsOut()
    {
        var safetyFile = Path.Combine(_directory, "safety.zip");
        _restore.Handler = _ => Task.FromResult(new DatabaseRestoreResult(DateTime.UtcNow, safetyFile));
        var viewModel = PrepareConfirmation();
        var selectedFile = viewModel.RestoreFile;
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        var confirmNotifications = 0;
        viewModel.ConfirmRestoreCommand.CanExecuteChanged += (_, _) => confirmNotifications++;

        Assert.False(viewModel.FinishRestoreCommand.CanExecute(null));
        await viewModel.RestoreAsync();

        Assert.Equal(1, _restore.CallCount);
        Assert.Equal(selectedFile, _restore.LastFile);
        Assert.Equal(safetyFile, viewModel.SafetyBackupFile);
        Assert.Contains(nameof(DatabaseMaintenanceViewModel.RestoreCompleted), changedProperties);
        Assert.Contains(nameof(DatabaseMaintenanceViewModel.CanRestore), changedProperties);
        Assert.Contains(nameof(DatabaseMaintenanceViewModel.CanUseBackup), changedProperties);
        Assert.True(confirmNotifications > 0);
        Assert.Contains("reinicie o Decor", viewModel.RestoreStatus);
        AssertMaintenanceLocked(viewModel);
        viewModel.RestoreFile = CreateZip();
        Assert.Equal(selectedFile, viewModel.RestoreFile);
        await viewModel.RestoreAsync();
        Assert.Equal(1, _restore.CallCount);
        Assert.Equal(0, _backup.CallCount);
        Assert.True(_context.IsAuthenticated);

        viewModel.FinishRestoreCommand.Execute(null);

        Assert.Equal(1, _context.SignOutCount);
        Assert.False(_context.IsAuthenticated);
    }

    [Fact]
    public async Task RestoreAsync_InvalidDataBeforeChangesDoesNotCompleteOrLockMaintenance()
    {
        const string error = "Backup invalido antes de alterar o banco.";
        _restore.Handler = _ => Task.FromException<DatabaseRestoreResult>(new InvalidDataException(error));
        var viewModel = PrepareConfirmation();

        await viewModel.RestoreAsync();

        Assert.Equal(1, _restore.CallCount);
        Assert.Equal(error, viewModel.RestoreStatus);
        Assert.Empty(viewModel.SafetyBackupFile);
        Assert.False(viewModel.IsRestoring);
        Assert.False(viewModel.RestoreCompleted);
        Assert.True(viewModel.ShowRestoreConfirmation);
        Assert.True(viewModel.CanRestore);
        Assert.True(viewModel.CanUseBackup);
        Assert.True(viewModel.ConfirmRestoreCommand.CanExecute(null));
        Assert.True(viewModel.StartImmediateBackupCommand.CanExecute(null));
        Assert.False(viewModel.FinishRestoreCommand.CanExecute(null));
        Assert.Equal(0, _context.SignOutCount);
        Assert.Equal(0, _backup.CallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoreAsync_CompletedOperationAwaitsResultAcknowledgementAndSignsOut(bool partiallyFailed)
    {
        var safetyFile = Path.Combine(_directory, "ack-safety.zip");
        if (partiallyFailed)
        {
            var exception = new InvalidOperationException("Partial restore");
            exception.Data[nameof(DatabaseRestoreResult.SafetyBackupFile)] = safetyFile;
            _restore.Handler = _ => Task.FromException<DatabaseRestoreResult>(exception);
        }
        else _restore.Handler = _ => Task.FromResult(new DatabaseRestoreResult(DateTime.UtcNow, safetyFile));
        var viewModel = PrepareConfirmation();
        var resultShown = false;
        viewModel.RestoreFinishedRequested += () =>
        {
            Assert.True(viewModel.RestoreCompleted);
            Assert.False(viewModel.IsRestoring);
            Assert.Equal(safetyFile, viewModel.SafetyBackupFile);
            resultShown = true;
            viewModel.FinishRestoreCommand.Execute(null);
            return Task.CompletedTask;
        };

        await viewModel.RestoreAsync();

        Assert.True(resultShown);
        Assert.Equal(1, _context.SignOutCount);
        Assert.False(_context.IsAuthenticated);
    }

    [Fact]
    public async Task RestoreAsync_FailureWithSafetyBackupLocksMaintenanceAndAllowsSignOut()
    {
        var safetyFile = Path.Combine(_directory, "recovery.zip");
        var exception = new InvalidOperationException("Restore failed after changes.");
        exception.Data[nameof(DatabaseRestoreResult.SafetyBackupFile)] = safetyFile;
        _restore.Handler = _ => Task.FromException<DatabaseRestoreResult>(exception);
        var viewModel = PrepareConfirmation();

        await viewModel.RestoreAsync();

        Assert.Equal(safetyFile, viewModel.SafetyBackupFile);
        Assert.Contains("parcialmente alterado", viewModel.RestoreStatus);
        AssertMaintenanceLocked(viewModel);
        await viewModel.RestoreAsync();
        Assert.Equal(1, _restore.CallCount);
        Assert.Equal(0, _backup.CallCount);
        viewModel.FinishRestoreCommand.Execute(null);
        Assert.Equal(1, _context.SignOutCount);
        Assert.False(_context.IsAuthenticated);
    }

    [Fact]
    public async Task RestoreAsync_WhileBusyDisablesConfirmationAndBackupAndRejectsSecondRestore()
    {
        var completion = new TaskCompletionSource<DatabaseRestoreResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _restore.Handler = _ => completion.Task;
        var viewModel = PrepareConfirmation();
        var selectedFile = viewModel.RestoreFile;
        var pendingRestore = viewModel.RestoreAsync();

        try
        {
            Assert.False(pendingRestore.IsCompleted);
            Assert.True(viewModel.IsRestoring);
            Assert.False(viewModel.CanRestore);
            Assert.False(viewModel.CanUseBackup);
            Assert.False(viewModel.RequestRestoreCommand.CanExecute(null));
            Assert.False(viewModel.ConfirmRestoreCommand.CanExecute(null));
            Assert.False(viewModel.CancelRestoreCommand.CanExecute(null));
            Assert.False(viewModel.StartImmediateBackupCommand.CanExecute(null));
            Assert.False(viewModel.FinishRestoreCommand.CanExecute(null));
            viewModel.RestoreFile = CreateZip();
            Assert.Equal(selectedFile, viewModel.RestoreFile);

            await viewModel.RestoreAsync();

            Assert.Equal(1, _restore.CallCount);
            Assert.Equal(0, _backup.CallCount);
        }
        finally
        {
            completion.TrySetResult(new DatabaseRestoreResult(DateTime.UtcNow, Path.Combine(_directory, "safety.zip")));
            await pendingRestore;
        }

        AssertMaintenanceLocked(viewModel);
    }

    private DatabaseMaintenanceViewModel CreateViewModel() =>
        Track(new DatabaseMaintenanceViewModel(_backup, _restore, _authorization, _context));

    private DatabaseMaintenanceViewModel Track(DatabaseMaintenanceViewModel viewModel)
    {
        _viewModels.Add(viewModel);
        return viewModel;
    }

    private DatabaseMaintenanceViewModel PrepareConfirmation()
    {
        var viewModel = CreateViewModel();
        viewModel.RestoreFile = CreateZip();
        viewModel.RequestRestoreCommand.Execute(null);
        viewModel.RestoreConfirmation = ConfirmationPhrase;
        Assert.True(viewModel.ConfirmRestoreCommand.CanExecute(null));
        return viewModel;
    }

    private string CreateZip()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, $"backup-{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        archive.CreateEntry("database.sql");
        return path;
    }

    private static void AssertMaintenanceLocked(DatabaseMaintenanceViewModel viewModel)
    {
        Assert.True(viewModel.RestoreCompleted);
        Assert.False(viewModel.IsRestoring);
        Assert.False(viewModel.ShowRestoreConfirmation);
        Assert.False(viewModel.CanRestore);
        Assert.False(viewModel.CanUseBackup);
        Assert.False(viewModel.RequestRestoreCommand.CanExecute(null));
        Assert.False(viewModel.ConfirmRestoreCommand.CanExecute(null));
        Assert.False(viewModel.StartImmediateBackupCommand.CanExecute(null));
        Assert.True(viewModel.FinishRestoreCommand.CanExecute(null));
    }

    public void Dispose()
    {
        foreach (var viewModel in _viewModels)
        {
            var timer = (DispatcherTimer)typeof(DatabaseMaintenanceViewModel)
                .GetField("_clockTimer", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(viewModel)!;
            timer.Stop();
        }
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private sealed class FakeBackupService : IDatabaseBackupService
    {
        public int CallCount { get; private set; }

        public Task<DatabaseBackupResult> CreateBackupAsync(IReadOnlyCollection<string> backupFiles,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromException<DatabaseBackupResult>(new InvalidOperationException("Unexpected backup call."));
        }
    }

    private sealed class FakeRestoreService : IDatabaseRestoreService
    {
        public int CallCount { get; private set; }
        public string? LastFile { get; private set; }
        public Func<string, Task<DatabaseRestoreResult>> Handler { get; set; } =
            _ => Task.FromException<DatabaseRestoreResult>(new InvalidOperationException("Unexpected restore call."));

        public Task<DatabaseRestoreResult> RestoreBackupAsync(string backupFile,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastFile = backupFile;
            return Handler(backupFile);
        }
    }

    private sealed class FakeAuthorizationService : IAuthorizationService
    {
        public bool HasRestorePermission { get; set; } = true;
        public List<string> RequestedPermissions { get; } = [];

        public bool HasPermission(string permissionCode)
        {
            RequestedPermissions.Add(permissionCode);
            return HasRestorePermission && permissionCode == DecorPermissions.DatabaseMaintenanceRestore;
        }

        public bool CanView(string resource) => false;
        public bool CanCreate(string resource) => false;
        public bool CanEdit(string resource) => false;
        public bool CanDelete(string resource) => false;
    }

    private sealed class FakeAuthenticatedUserContext : IAuthenticatedUserContext
    {
        public event EventHandler? SignedOut;
        public ApplicationUser? User { get; private set; } = new() { Username = "restore-test" };
        public bool IsAuthenticated => User is not null;
        public int SignOutCount { get; private set; }
        public void SignIn(ApplicationUser user) => User = user;

        public void SignOut()
        {
            SignOutCount++;
            User = null;
            SignedOut?.Invoke(this, EventArgs.Empty);
        }
    }
}