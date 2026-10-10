using Decor.Application.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Decor.Application.Tests;

public sealed class UsersAdministrationWorkflowTests
{
    private static readonly AdministrativeRoleDTO AdministratorRole = new(1, SystemRoleDefaults.Administrators, null, 100, true);
    private static readonly AdministrativeRoleDTO OperatorRole = new(2, "Operador", null, 10, false);
    private static AdministrativeUserDTO User(int id = 2, string username = "ana") => new(id, username, "legacy", true, [OperatorRole], [], "Ana Silva");

    [Theory]
    [InlineData("update")]
    [InlineData("rename")]
    [InlineData("activate")]
    [InlineData("deactivate")]
    [InlineData("groups")]
    [InlineData("overrides")]
    [InlineData("restore")]
    public async Task SystemAdministrator_RejectsStructuralWritesEvenByAdministrator(string operation)
    {
        var repository = new Repository { Target = User(1, "admin") };
        var service = Service(repository);

        Func<Task> action = operation switch
        {
            "update" => () => service.UpdateAsync(1, "admin", "anything"),
            "rename" => () => service.UpdateAsync(1, "other", "anything"),
            "activate" => () => service.SetActiveAsync(1, true),
            "deactivate" => () => service.SetActiveAsync(1, false),
            "groups" => () => service.ReplaceRolesAsync(1, [2]),
            "overrides" => () => service.ReplacePermissionOverridesAsync(1, [new(1, DecorPermissions.UsersView, false)]),
            _ => () => service.RestorePermissionsAsync(1)
        };

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*conta reservada do sistema*");
        repository.Writes.Should().Be(0);
    }

    [Fact]
    public async Task SystemAdministrator_ResetPasswordRemainsAllowed()
    {
        var repository = new Repository { Target = User(1, "admin") };
        var result = await Service(repository).ResetPasswordAsync(1);

        result.TemporaryPassword.Should().NotBeNullOrWhiteSpace();
        repository.Writes.Should().Be(1);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData(" admin ")]
    public async Task ReservedUsername_CannotBeCreatedOrAssignedToAnotherUser(string username)
    {
        var repository = new Repository();
        var service = Service(repository);

        await ((Func<Task>)(() => service.CreateAsync(username, "ignored", []))).Should().ThrowAsync<InvalidOperationException>();
        await ((Func<Task>)(() => service.UpdateAsync(2, username, "ignored"))).Should().ThrowAsync<InvalidOperationException>();
        repository.Writes.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unrelated display name")]
    public async Task Service_PersistsNormalizedUsernameAsLegacyDisplayName(string displayName)
    {
        var repository = new Repository();
        var service = Service(repository);

        await service.CreateAsync(" ana ", displayName, []);
        repository.LastNames.Should().Be(("ana", "ana"));
        await service.UpdateAsync(2, " renamed ", displayName);
        repository.LastNames.Should().Be(("renamed", "renamed"));
    }

    [Fact]
    public async Task ProtectedRole_DoesNotMakeRegularAccountImmutable()
    {
        var repository = new Repository { Target = User() with { Roles = [AdministratorRole] } };
        var service = Service(repository);

        await service.UpdateAsync(2, "ana", "");
        await service.SetActiveAsync(2, false);
        await service.ReplaceRolesAsync(2, [2]);

        repository.Writes.Should().Be(3);
    }

    [Fact]
    public async Task Service_RequiresAuthorizationBeforeWriting()
    {
        var repository = new Repository();
        var service = Service(repository, new Authorization(false));

        await ((Func<Task>)(() => service.ReplaceRolesAsync(2, [2]))).Should().ThrowAsync<UnauthorizedAccessException>();
        repository.Writes.Should().Be(0);
    }

    [Fact]
    public void Listing_SystemAdministratorCannotBeEdited()
    {
        var viewModel = new UsersViewModel(new UsersService(), new RolesService()) { SelectedUser = User(1, "admin") };

        viewModel.EditUserCommand.CanExecute(null).Should().BeFalse();
        viewModel.EditUserRolesCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Listing_RegularAdministratorRoleDoesNotBlockEditing()
    {
        var viewModel = new UsersViewModel(new UsersService(), new RolesService())
        { SelectedUser = User() with { Roles = [AdministratorRole] } };

        viewModel.EditUserCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Listing_DeniedPermissionsDisableCommands()
    {
        var viewModel = new UsersViewModel(new UsersService(), new RolesService(), new Authorization(false)) { SelectedUser = User() };

        viewModel.NewUserCommand.CanExecute(null).Should().BeFalse();
        viewModel.EditUserCommand.CanExecute(null).Should().BeFalse();
        viewModel.SearchCommand.CanExecute(null).Should().BeFalse();
        viewModel.ClearSearchCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Listing_EmptySearchLoadsAllAndClearSearchResetsResults()
    {
        var users = new UsersService();
        var viewModel = new UsersViewModel(users, new RolesService()) { SearchText = "ana" };
        await viewModel.InitializeAsync();
        viewModel.SelectedUser = viewModel.Users.Single();

        viewModel.ClearSearchCommand.Execute(null);
        viewModel.SearchText.Should().BeEmpty();
        viewModel.Users.Should().BeEmpty();
        viewModel.SelectedUser.Should().BeNull();

        viewModel.SearchCommand.CanExecute(null).Should().BeTrue();
        viewModel.SearchCommand.Execute(null);
        users.SearchCalls.Should().Be(2);
        viewModel.Users.Should().ContainSingle();
    }

    [Fact]
    public void UsersViewModel_OptionalDependenciesRemainCompatible()
    {
        var viewModel = new UsersViewModel(new UsersService()) { SelectedUser = User() };
        viewModel.EditUserCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task UsersViewModel_StandardDIResolvesRoleAndAuthorizationServices()
    {
        var roles = new RolesService();
        var services = new ServiceCollection();
        services.AddSingleton<IUserAdministrationService>(new UsersService());
        services.AddSingleton<IRoleAdministrationService>(roles);
        services.AddSingleton<IAuthorizationService>(new Authorization());
        services.AddSingleton<IAuthenticatedUserContext>(Context());
        services.AddTransient<UsersViewModel>();
        using var provider = services.BuildServiceProvider();
        var viewModel = provider.GetRequiredService<UsersViewModel>();
        viewModel.NewUserCommand.Execute(null);
        await viewModel.FormLoadTask;

        roles.LoadCalls.Should().Be(1);
        viewModel.ActiveForm.Should().NotBeNull();
    }

    [Fact]
    public void CreateAndEdit_IgnoreLegacyDisplayNameAndNormalizeUsername()
    {
        var users = new UsersService();
        var create = new CreateUserViewModel(users, new RolesService()) { Username = " ana ", DisplayName = "" };
        create.CreateCommand.Execute(null);
        users.LastNames.Should().Be(("ana", "ana"));
        create.IsCompleted.Should().BeTrue();
        var edit = new EditUserViewModel(users);
        edit.Initialize(User());
        edit.UserCodeDisplay.Should().Be("2");
        edit.Username = " renamed ";
        edit.DisplayName = "a second name that must be ignored";
        edit.SaveCommand.Execute(null);
        users.LastNames.Should().Be(("renamed", "renamed"));
        edit.HasError.Should().BeFalse();
    }

    [Fact]
    public void Edit_ProtectedAccountCannotBeSavedEvenWhenCommandInvokedDirectly()
    {
        var users = new UsersService();
        var viewModel = new EditUserViewModel(users);
        viewModel.Initialize(User(1, "admin"));
        viewModel.Username = "other";
        viewModel.SaveCommand.CanExecute(null).Should().BeFalse();
        viewModel.CanEditFields.Should().BeFalse();
        viewModel.SaveCommand.Execute(null);
        users.LastNames.Should().BeNull();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData(" ADMIN ")]
    public void CreateAndEdit_ReservedUsernameIsRejectedBeforeServiceCall(string username)
    {
        var users = new UsersService();
        var create = new CreateUserViewModel(users, new RolesService()) { Username = username };
        create.CreateCommand.Execute(null);
        create.ErrorMessage.Should().Contain("reservado");
        var edit = new EditUserViewModel(users);
        edit.Initialize(User());
        edit.Username = username;
        edit.SaveCommand.Execute(null);
        edit.ErrorMessage.Should().Contain("reservado");
        users.LastNames.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Edit_SavesChangedActiveState(bool initiallyActive)
    {
        var users = new UsersService();
        var viewModel = new EditUserViewModel(users);
        viewModel.Initialize(User() with { IsActive = initiallyActive });
        viewModel.IsActive = !initiallyActive;
        await viewModel.SaveAsync();
        users.LastActive.Should().Be(!initiallyActive);
        viewModel.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task Edit_DeniedStatusChangeDoesNotWrite()
    {
        var users = new UsersService();
        var viewModel = new EditUserViewModel(users, new SelectiveAuthorization(DecorPermissions.UsersDeactivate));
        viewModel.Initialize(User());
        viewModel.CanChangeActive.Should().BeFalse();
        viewModel.IsActive = false;
        await viewModel.SaveAsync();
        viewModel.ErrorMessage.Should().Contain("permissão");
        users.LastNames.Should().BeNull();
        users.LastActive.Should().BeNull();
    }

    [Fact]
    public async Task UnifiedForm_LoadFailurePreventsCreatingWithUnloadedGroupsAndAllowsRetry()
    {
        var users = new UsersService();
        var roles = new RolesService { Load = () => Task.FromException<IReadOnlyList<AdministrativeRoleDTO>>(new UnauthorizedAccessException()) };
        var form = new UserFormViewModel(users, roles) { Username = "ana" };
        await form.InitializeAsync();
        form.ErrorMessage.Should().Contain("permissão");
        form.CanEditGroups.Should().BeFalse();
        form.SaveCommand.CanExecute(null).Should().BeFalse();
        await form.SaveAsync();
        users.CreateCalls.Should().Be(0);
        roles.Load = () => Task.FromResult<IReadOnlyList<AdministrativeRoleDTO>>([OperatorRole]);
        await form.InitializeAsync();
        form.CanEditGroups.Should().BeTrue();
        form.SaveCommand.CanExecute(null).Should().BeTrue();
        form.HasError.Should().BeFalse();
    }

    [Theory]
    [InlineData(DecorPermissions.UsersAssignRoles)]
    [InlineData(DecorPermissions.UsersDeactivate)]
    [InlineData(DecorPermissions.UsersActivate)]
    public async Task UnifiedForm_DeniedSpecificMutationDoesNotWriteAnything(string deniedPermission)
    {
        var users = new UsersService();
        var user = User() with { IsActive = deniedPermission != DecorPermissions.UsersActivate };
        var form = new UserFormViewModel(users, new RolesService(), new SelectiveAuthorization(deniedPermission), user: user);
        await form.InitializeAsync();
        form.Username = "renamed";
        if (deniedPermission == DecorPermissions.UsersAssignRoles)
        {
            form.CanEditGroups.Should().BeFalse();
            form.Groups.Single(group => group.Role.RoleID == 1).IsSelected = true;
        }
        else
        {
            form.CanChangeActive.Should().BeFalse();
            form.IsActive = !user.IsActive;
        }
        await form.SaveAsync();
        form.ErrorMessage.Should().Contain("permissão");
        users.UpdateCalls.Should().Be(0);
        users.SaveCalls.Should().Be(0);
        users.LastActive.Should().BeNull();
    }

    [Fact]
    public async Task UnifiedForm_WithoutAssignmentPermissionCanSaveUsernameAndPreservesGroups()
    {
        var users = new UsersService();
        var form = new UserFormViewModel(users, new RolesService(), new SelectiveAuthorization(DecorPermissions.UsersAssignRoles), user: User());
        await form.InitializeAsync();
        form.Username = "renamed";
        await form.SaveAsync();
        users.UpdateCalls.Should().Be(1);
        users.SaveCalls.Should().Be(0);
        form.Groups.Single(group => group.Role.RoleID == 2).IsSelected.Should().BeTrue();
        form.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task UnifiedForm_MissingRoleServiceDoesNotEraseAssignedGroups()
    {
        var users = new UsersService();
        var form = new UserFormViewModel(users, null, user: User());
        await form.InitializeAsync();
        form.Groups.Single().IsSelected.Should().BeTrue();
        form.CanEditGroups.Should().BeFalse();
        form.Username = "renamed";
        await form.SaveAsync();
        users.UpdateCalls.Should().Be(1);
        users.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task UnifiedForm_ProtectedAdministratorAndUnauthenticatedContextCannotWrite()
    {
        var users = new UsersService();
        var protectedForm = new UserFormViewModel(users, new RolesService(), user: User(1, "admin"));
        await protectedForm.InitializeAsync();
        protectedForm.Username = "other";
        protectedForm.CanEditFields.Should().BeFalse();
        protectedForm.CanChangeActive.Should().BeFalse();
        protectedForm.CanEditGroups.Should().BeFalse();
        await protectedForm.SaveAsync();
        var signedOut = new UserFormViewModel(users, new RolesService(), context: new AuthenticatedUserContext(), user: User());
        await signedOut.InitializeAsync();
        signedOut.Username = "other";
        await signedOut.SaveAsync();
        users.UpdateCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData(" ADMIN ")]
    [InlineData("")]
    public async Task UnifiedForm_InvalidOrReservedUsernameNeverReachesService(string username)
    {
        var users = new UsersService();
        foreach (var user in new AdministrativeUserDTO?[] { null, User() })
        {
            var form = new UserFormViewModel(users, new RolesService(), user: user);
            await form.InitializeAsync();
            form.Username = username;
            await form.SaveAsync();
            form.HasError.Should().BeTrue();
        }
        users.UpdateCalls.Should().Be(0);
        users.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task UnifiedForm_CreatesUserWithSelectedEmployeeAssociation()
    {
        var users = new UsersService();
        var form = new UserFormViewModel(users, new RolesService()) { Username = "ana" };
        await form.InitializeAsync();
        form.SetEmployee(new EmployeeDTO(19, "Ana Silva", null, null, null, null, null, true, null));

        await form.SaveAsync();

        users.CreatedEmployeeId.Should().Be(19);
        users.CreateCalls.Should().Be(1);
    }

    [Fact]
    public async Task UnifiedForm_ChangesAndClearsExistingEmployeeAssociation()
    {
        var users = new UsersService();
        var user = User() with { EmployeeID = 15, EmployeeName = "Pessoa anterior" };
        var form = new UserFormViewModel(users, new RolesService(), user: user);
        await form.InitializeAsync();
        form.SetEmployee(new EmployeeDTO(19, "Ana Silva", null, null, null, null, null, true, null));
        await form.SaveAsync();

        users.AssignedEmployeeId.Should().Be(19);
        form.WasSaved.Should().BeTrue();

        var clear = new UserFormViewModel(users, new RolesService(), user: user);
        await clear.InitializeAsync();
        clear.ClearEmployeeCommand.Execute(null);
        await clear.SaveAsync();
        users.AssignedEmployeeId.Should().BeNull();
        users.AssignedEmployeeUserId.Should().Be(user.UserID);
    }

    [Fact]
    public async Task UnifiedForm_PartialSaveRetryDoesNotRepeatSuccessfulUsernameWrite()
    {
        var users = new UsersService { Save = () => Task.FromException(new UnauthorizedAccessException("Atribuição negada")) };
        var form = new UserFormViewModel(users, new RolesService(), user: User());
        await form.InitializeAsync();
        form.Username = "renamed";
        form.Groups.Single(group => group.Role.RoleID == 1).IsSelected = true;
        await form.SaveAsync();
        form.ErrorMessage.Should().Be("Atribuição negada");
        form.WasSaved.Should().BeTrue();
        form.SaveCommand.CanExecute(null).Should().BeTrue();
        users.Save = () => Task.CompletedTask;
        await form.SaveAsync();
        users.UpdateCalls.Should().Be(1);
        users.SaveCalls.Should().Be(2);
        form.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task Inline_CancelAfterPartialWriteRefreshesPersistedUser()
    {
        var users = new UsersService { Save = () => Task.FromException(new InvalidOperationException("Falha de grupos")) };
        var viewModel = new UsersViewModel(users, new RolesService()) { SelectedUser = User() };
        viewModel.EditUserCommand.Execute(null);
        await viewModel.FormLoadTask;
        var form = viewModel.ActiveForm!;
        form.Username = "renamed";
        form.Groups.Single(group => group.Role.RoleID == 1).IsSelected = true;
        await form.SaveAsync();
        viewModel.ActiveForm.Should().BeSameAs(form);
        users.SearchCalls.Should().Be(0);
        form.DismissCommand.Execute(null);
        await viewModel.FormCloseTask;
        users.SearchCalls.Should().Be(1);
        viewModel.ActiveForm.Should().BeNull();
    }

    [Theory]
    [InlineData("username")]
    [InlineData("status")]
    [InlineData("groups")]
    public async Task Inline_RealServiceSelfChangeInvalidatesSessionWithoutReload(string mutation)
    {
        var context = Context();
        var repository = new Repository { Target = User(10, "ana") };
        var service = new UserAdministrationService(repository, new Hasher(), new Policy(), context, new Authorization());
        var viewModel = new UsersViewModel(service, new RolesService(), new Authorization(), context) { SelectedUser = repository.Target };
        viewModel.EditUserCommand.Execute(null);
        await viewModel.FormLoadTask;
        var form = viewModel.ActiveForm!;
        switch (mutation)
        {
            case "username": form.Username = "renamed"; break;
            case "status": form.IsActive = false; break;
            default: form.Groups.Single(group => group.Role.RoleID == 1).IsSelected = true; break;
        }
        await form.SaveAsync();
        await viewModel.FormCloseTask;
        context.IsAuthenticated.Should().BeFalse();
        repository.Writes.Should().Be(1);
        repository.SearchCalls.Should().Be(0);
        viewModel.ActiveForm.Should().BeNull();
        form.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task UnifiedForm_MultipleSelfChangesAreRejectedBeforeAnyWrite()
    {
        var context = Context();
        var repository = new Repository { Target = User(10, "ana") };
        var service = new UserAdministrationService(repository, new Hasher(), new Policy(), context, new Authorization());
        var form = new UserFormViewModel(service, new RolesService(), new Authorization(), context, repository.Target);
        await form.InitializeAsync();
        form.Username = "renamed";
        form.IsActive = false;
        await form.SaveAsync();
        form.ErrorMessage.Should().Contain("própria conta");
        context.IsAuthenticated.Should().BeTrue();
        repository.Writes.Should().Be(0);
    }

    [Theory]
    [InlineData(true, "Cadastrando um usuário.")]
    [InlineData(false, "Editando o usuário 2.")]
    public async Task Inline_StatusAndWorkspaceIndicatorsFollowModeAndCancelRestoresListing(bool creating, string message)
    {
        var users = new UsersService();
        var viewModel = new UsersViewModel(users, new RolesService());
        await viewModel.InitializeAsync();
        viewModel.SelectedUser = viewModel.Users.Single();
        var listStatus = viewModel.StatusMessage;
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        var document = new WorkspaceDocumentViewModel("users", "Usuários",
            new Avalonia.Controls.ContentControl { DataContext = viewModel }, _ => { }, _ => { });
        if (creating) viewModel.NewUserCommand.Execute(null);
        else viewModel.EditUserCommand.Execute(null);
        await viewModel.FormLoadTask;
        viewModel.StatusMessage.Should().Be(message);
        viewModel.IsAdding.Should().Be(creating);
        document.ShowAdditionIndicator.Should().Be(creating);
        document.ShowModificationIndicator.Should().Be(!creating);
        document.ShowCloseButton.Should().BeFalse();
        notifications.Should().Contain(nameof(viewModel.IsEditing)).And.Contain(nameof(viewModel.IsAdding)).And.Contain(nameof(viewModel.StatusMessage));
        viewModel.ActiveForm!.DismissCommand.Execute(null);
        await viewModel.FormCloseTask;
        viewModel.StatusMessage.Should().Be(listStatus);
        document.ShowAdditionIndicator.Should().BeFalse();
        document.ShowModificationIndicator.Should().BeFalse();
        document.ShowCloseButton.Should().BeTrue();
        users.SearchCalls.Should().Be(1);
    }

    [Fact]
    public async Task Inline_PendingInitialSearchCannotReplaceFormStatusWithRecordCount()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<AdministrativeUserDTO>>();
        var users = new UsersService { Search = () => pending.Task };
        var viewModel = new UsersViewModel(users, new RolesService());
        viewModel.NewUserCommand.Execute(null);
        await viewModel.FormLoadTask;
        var load = viewModel.InitializeAsync();
        pending.SetResult([User()]);
        await load;
        viewModel.StatusMessage.Should().Be("Cadastrando um usuário.");
        viewModel.ActiveForm!.DismissCommand.Execute(null);
        await viewModel.FormCloseTask;
        viewModel.StatusMessage.Should().Be("1 usuário carregado.");
    }

    [Fact]
    public async Task UnifiedForm_UsesUserIdForBothModesAndPersistsGroups()
    {
        var users = new UsersService();
        var create = new UserFormViewModel(users, new RolesService());
        create.UserId.Should().Be(0);
        create.UserCodeDisplay.Should().Be("0");
        create.IsActive.Should().BeTrue();
        create.CanChangeActive.Should().BeFalse();
        await create.InitializeAsync();
        create.Username = " ana ";
        create.Groups.Single(group => group.Role.RoleID == 2).IsSelected = true;
        await create.SaveAsync();
        users.CreatedRoles.Should().Equal(2);
        users.LastNames.Should().Be(("ana", "ana"));
        create.HasTemporaryPassword.Should().BeTrue();
        create.IsCompleted.Should().BeTrue();
        create.DismissButtonText.Should().Be("Concluir");
        create.SaveCommand.CanExecute(null).Should().BeFalse();

        var edit = new UserFormViewModel(users, new RolesService(), user: User());
        await edit.InitializeAsync();
        edit.UserId.Should().Be(2);
        edit.UserCodeDisplay.Should().Be("2");
        edit.Groups.Single(group => group.Role.RoleID == 2).IsSelected.Should().BeTrue();
        edit.Username = "renamed";
        edit.IsActive = false;
        edit.Groups.Single(group => group.Role.RoleID == 1).IsSelected = true;
        await edit.SaveAsync();
        users.LastNames.Should().Be(("renamed", "renamed"));
        users.LastActive.Should().BeFalse();
        users.SavedRoles.Should().BeEquivalentTo(new[] { 1, 2 });
        edit.WasSaved.Should().BeTrue();
    }

    [Fact]
    public async Task Inline_WorkspaceStateNotifiesWhenOpeningAndCancellingCreate()
    {
        var viewModel = new UsersViewModel(new UsersService(), new RolesService());
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        IWorkspaceDocumentState state = viewModel;

        viewModel.NewUserCommand.Execute(null);
        await viewModel.FormLoadTask;
        state.IsEditing.Should().BeTrue();
        state.IsAdding.Should().BeTrue();
        notifications.Should().Contain(nameof(state.IsEditing)).And.Contain(nameof(state.IsAdding));
        notifications.Clear();
        viewModel.ActiveForm!.DismissCommand.Execute(null);
        await viewModel.FormCloseTask;

        state.IsEditing.Should().BeFalse();
        state.IsAdding.Should().BeFalse();
        notifications.Should().Contain(nameof(state.IsEditing)).And.Contain(nameof(state.IsAdding));
    }

    [Fact]
    public async Task Inline_CreateKeepsPasswordUntilFinishedAndRefreshesSelection()
    {
        var users = new UsersService();
        var viewModel = new UsersViewModel(users, new RolesService());
        viewModel.NewUserCommand.Execute(null);
        await viewModel.FormLoadTask;
        var form = Assert.IsType<UserFormViewModel>(viewModel.ActiveForm);
        viewModel.IsEditing.Should().BeTrue();
        viewModel.NewUserCommand.CanExecute(null).Should().BeFalse();
        viewModel.SearchCommand.CanExecute(null).Should().BeFalse();
        form.Groups.Should().HaveCount(2);
        form.Username = "ana";
        form.SaveCommand.Execute(null);
        form.HasTemporaryPassword.Should().BeTrue();
        viewModel.ActiveForm.Should().BeSameAs(form);
        form.DismissCommand.Execute(null);
        await viewModel.FormCloseTask;
        viewModel.IsEditing.Should().BeFalse();
        viewModel.SelectedUser!.Username.Should().Be("ana");
        users.SearchCalls.Should().Be(1);
    }

    [Fact]
    public async Task Inline_EditSavesAndReturnsToSameSelectedUser()
    {
        var users = new UsersService();
        var viewModel = new UsersViewModel(users, new RolesService()) { SelectedUser = User() };
        viewModel.EditUserCommand.Execute(null);
        await viewModel.FormLoadTask;
        var form = Assert.IsType<UserFormViewModel>(viewModel.ActiveForm);
        viewModel.EditUserCommand.CanExecute(null).Should().BeFalse();
        viewModel.SearchCommand.CanExecute(null).Should().BeFalse();
        viewModel.ClearSearchCommand.CanExecute(null).Should().BeFalse();
        form.Username = "renamed";
        await form.SaveAsync();
        await viewModel.FormCloseTask;
        users.LastNames.Should().Be(("renamed", "renamed"));
        viewModel.IsEditing.Should().BeFalse();
        viewModel.SelectedUser!.UserID.Should().Be(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Inline_CancelDoesNotSaveOrRefresh(bool creating)
    {
        var users = new UsersService();
        var viewModel = new UsersViewModel(users, new RolesService()) { SelectedUser = User() };
        if (creating)
        {
            viewModel.NewUserCommand.Execute(null);
            await viewModel.FormLoadTask;
            Assert.IsType<UserFormViewModel>(viewModel.ActiveForm).DismissCommand.Execute(null);
        }
        else
        {
            viewModel.EditUserCommand.Execute(null);
            await viewModel.FormLoadTask;
            Assert.IsType<UserFormViewModel>(viewModel.ActiveForm).DismissCommand.Execute(null);
        }
        await viewModel.FormCloseTask;
        viewModel.IsEditing.Should().BeFalse();
        users.LastNames.Should().BeNull();
        users.SearchCalls.Should().Be(0);
        viewModel.SelectedUser!.UserID.Should().Be(2);
    }

    [Fact]
    public async Task Inline_CreateCannotCloseDuringLoadingOrSaveTwice()
    {
        var roles = new TaskCompletionSource<IReadOnlyList<AdministrativeRoleDTO>>();
        var create = new TaskCompletionSource();
        var users = new UsersService { Create = () => create.Task };
        var viewModel = new UsersViewModel(users, new RolesService { Load = () => roles.Task });
        viewModel.NewUserCommand.Execute(null);
        var form = Assert.IsType<UserFormViewModel>(viewModel.ActiveForm);
        form.IsBusy.Should().BeTrue();
        form.DismissCommand.CanExecute(null).Should().BeFalse();
        form.DismissCommand.Execute(null);
        viewModel.ActiveForm.Should().BeSameAs(form);
        viewModel.NewUserCommand.Execute(null);
        viewModel.ActiveForm.Should().BeSameAs(form);
        roles.SetResult([OperatorRole]);
        await viewModel.FormLoadTask;
        form.Username = "ana";
        form.Groups.Single().IsSelected = true;
        var save = form.SaveAsync();
        form.SaveCommand.Execute(null);
        users.CreateCalls.Should().Be(1);
        users.CreatedRoles.Should().Equal(2);
        form.DismissCommand.Execute(null);
        viewModel.ActiveForm.Should().BeSameAs(form);
        create.SetResult();
        await save;
        form.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Inline_EditFailureStaysOpenAndAllowsRetry()
    {
        var users = new UsersService { Update = () => Task.FromException(new InvalidOperationException("Falha de gravação")) };
        var viewModel = new UsersViewModel(users, new RolesService()) { SelectedUser = User() };
        viewModel.EditUserCommand.Execute(null);
        await viewModel.FormLoadTask;
        var form = Assert.IsType<UserFormViewModel>(viewModel.ActiveForm);
        form.Username = "renamed";
        await form.SaveAsync();
        viewModel.ActiveForm.Should().BeSameAs(form);
        form.ErrorMessage.Should().Be("Falha de gravação");
        form.WasSaved.Should().BeFalse();
        form.SaveCommand.CanExecute(null).Should().BeTrue();
        users.SearchCalls.Should().Be(0);
        users.Update = () => Task.CompletedTask;
        await form.SaveAsync();
        await viewModel.FormCloseTask;
        viewModel.IsEditing.Should().BeFalse();
    }

    [Fact]
    public async Task Inline_CreateFailureStaysOpenWithoutTemporaryPassword()
    {
        var users = new UsersService { Create = () => Task.FromException(new InvalidOperationException()) };
        var viewModel = new UsersViewModel(users, new RolesService());
        viewModel.NewUserCommand.Execute(null);
        await viewModel.FormLoadTask;
        var form = Assert.IsType<UserFormViewModel>(viewModel.ActiveForm);
        form.Username = "ana";
        form.SaveCommand.Execute(null);
        form.HasError.Should().BeTrue();
        form.HasTemporaryPassword.Should().BeFalse();
        form.IsCompleted.Should().BeFalse();
        viewModel.ActiveForm.Should().BeSameAs(form);
        users.SearchCalls.Should().Be(0);
    }

    [Fact]
    public void Inline_DeniedCommandsAndProtectedAdministratorNeverOpenForm()
    {
        var viewModel = new UsersViewModel(new UsersService(), new RolesService(), new Authorization(false)) { SelectedUser = User() };
        viewModel.NewUserCommand.Execute(null);
        viewModel.EditUserCommand.Execute(null);
        viewModel.ActiveForm.Should().BeNull();
        var administrator = new UsersViewModel(new UsersService(), new RolesService()) { SelectedUser = User(1, "admin") };
        administrator.EditUserCommand.Execute(null);
        administrator.ActiveForm.Should().BeNull();
    }

    [Fact]
    public async Task Inline_SelfChangeSignOutDoesNotReloadUsers()
    {
        var context = Context();
        var users = new UsersService { Update = () => { context.SignOut(); return Task.CompletedTask; } };
        var viewModel = new UsersViewModel(users, new RolesService(), new Authorization(), context) { SelectedUser = User() };
        viewModel.EditUserCommand.Execute(null);
        await viewModel.FormLoadTask;
        var form = Assert.IsType<UserFormViewModel>(viewModel.ActiveForm);
        form.Username = "renamed";
        await form.SaveAsync();
        await viewModel.FormCloseTask;
        viewModel.IsEditing.Should().BeFalse();
        users.SearchCalls.Should().Be(0);
    }

    private static AuthenticatedUserContext Context()
    {
        var context = new AuthenticatedUserContext();
        context.SignIn(new ApplicationUser { UserID = 10, Username = "admin", DisplayName = "legacy", IsActive = true, Roles = [], Permissions = [] });
        return context;
    }

    private static UserAdministrationService Service(Repository repository, IAuthorizationService? authorization = null) =>
        new(repository, new Hasher(), new Policy(), Context(), authorization ?? new Authorization());

    private sealed class Authorization(bool allowed = true) : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => allowed;
        public bool CanView(string resource) => allowed;
        public bool CanCreate(string resource) => allowed;
        public bool CanEdit(string resource) => allowed;
        public bool CanDelete(string resource) => allowed;
    }

    private sealed class SelectiveAuthorization(string deniedPermission) : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => permissionCode != deniedPermission;
        public bool CanView(string resource) => true;
        public bool CanCreate(string resource) => true;
        public bool CanEdit(string resource) => true;
        public bool CanDelete(string resource) => true;
    }

    private sealed class Hasher : IPasswordHasher
    {
        public string Hash(string password) => password;
        public bool Verify(string password, string passwordHash) => true;
    }

    private sealed class Policy : IPasswordPolicy
    {
        public bool IsValid(string? password) => true;
    }

    private sealed class Repository : IUserAdministrationRepository
    {
        public AdministrativeUserDTO Target { get; set; } = User();
        public int Writes { get; private set; }
        public int SearchCalls { get; private set; }
        public (string Username, string DisplayName)? LastNames { get; private set; }
        public Task<IReadOnlyList<AdministrativeUserDTO>> SearchAsync(string? search, CancellationToken cancellationToken = default)
        { SearchCalls++; return Task.FromResult<IReadOnlyList<AdministrativeUserDTO>>([Target]); }
        public Task<AdministrativeUserDTO?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult<AdministrativeUserDTO?>(Target);
        public Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdministrativeRoleDTO>>([AdministratorRole, OperatorRole]);
        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CreateWithRolesAsync(string username, string displayName, string passwordHash, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default)
        { Writes++; LastNames = (username, displayName); return Task.FromResult(3); }
        public Task<bool> UpdateAsync(int userId, string username, string displayName, CancellationToken cancellationToken = default)
        { Writes++; LastNames = (username, displayName); return Task.FromResult(true); }
        public Task<bool> SetActivePreservingLastAdministratorAsync(int userId, bool isActive, int administratorRoleId, CancellationToken cancellationToken = default)
        { Writes++; return Task.FromResult(true); }
        public Task<bool> UpdateTemporaryPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default)
        { Writes++; return Task.FromResult(true); }
        public Task ReplaceRolesPreservingLastAdministratorAsync(int userId, IReadOnlyCollection<int> roleIds, int administratorRoleId, CancellationToken cancellationToken = default)
        { Writes++; return Task.CompletedTask; }
        public Task ReplacePermissionOverridesAsync(int userId, IReadOnlyCollection<PermissionOverrideDTO> overrides, CancellationToken cancellationToken = default)
        { Writes++; return Task.CompletedTask; }
    }

    private sealed class RolesService : IRoleAdministrationService
    {
        public int LoadCalls { get; private set; }
        public Func<Task<IReadOnlyList<AdministrativeRoleDTO>>> Load { get; set; } = () => Task.FromResult<IReadOnlyList<AdministrativeRoleDTO>>([AdministratorRole, OperatorRole]);
        public Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default) { LoadCalls++; return Load(); }
        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetAllPermissionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplacePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RestoreDefaultsAsync(int roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UsersService : IUserAdministrationService
    {
        public Func<Task> Create { get; set; } = () => Task.CompletedTask;
        public Func<Task> Update { get; set; } = () => Task.CompletedTask;
        public int CreateCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int[] CreatedRoles { get; private set; } = [];
        public bool? LastActive { get; private set; }
        public Func<Task> Save { get; set; } = () => Task.CompletedTask;
        public Func<Task<IReadOnlyList<AdministrativeUserDTO>>>? Search { get; set; }
        public int? SavedUserId { get; private set; }
        public int[] SavedRoles { get; private set; } = [];
        public int? CreatedEmployeeId { get; private set; }
        public int? AssignedEmployeeId { get; private set; }
        public int? AssignedEmployeeUserId { get; private set; }
        public int SaveCalls { get; private set; }
        public int SearchCalls { get; private set; }
        public (string Username, string DisplayName)? LastNames { get; private set; }
        public Task<IReadOnlyList<AdministrativeUserDTO>> SearchAsync(string? search, CancellationToken cancellationToken = default)
        { SearchCalls++; return Search?.Invoke() ?? Task.FromResult<IReadOnlyList<AdministrativeUserDTO>>([User() with { Roles = SavedRoles.Select(id => id == 1 ? AdministratorRole : OperatorRole).ToArray() }]); }
        public Task<AdministrativeUserDTO?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult<AdministrativeUserDTO?>(User());
        public async Task<TemporaryPasswordResult> CreateAsync(string username, string displayName, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default)
        { CreateCalls++; CreatedRoles = roleIds.ToArray(); LastNames = (username, displayName); await Create(); return new TemporaryPasswordResult("Password123!"); }
        public async Task<TemporaryPasswordResult> CreateWithEmployeeAsync(string username, string displayName, IReadOnlyCollection<int> roleIds,
            int? employeeId, CancellationToken cancellationToken = default)
        {
            CreatedEmployeeId = employeeId;
            return await CreateAsync(username, displayName, roleIds, cancellationToken);
        }
        public Task AssignEmployeeAsync(int userId, int? employeeId, CancellationToken cancellationToken = default)
        { AssignedEmployeeUserId = userId; AssignedEmployeeId = employeeId; return Task.CompletedTask; }
        public Task UpdateAsync(int userId, string username, string displayName, CancellationToken cancellationToken = default)
        { UpdateCalls++; LastNames = (username, displayName); return Update(); }
        public Task SetActiveAsync(int userId, bool active, CancellationToken cancellationToken = default)
        { LastActive = active; return Task.CompletedTask; }
        public Task ReplaceRolesAsync(int userId, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default)
        { SavedUserId = userId; SavedRoles = roleIds.ToArray(); SaveCalls++; return Save(); }
        public Task ReplacePermissionOverridesAsync(int userId, IReadOnlyCollection<PermissionOverrideDTO> overrides, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RestorePermissionsAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TemporaryPasswordResult> ResetPasswordAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}