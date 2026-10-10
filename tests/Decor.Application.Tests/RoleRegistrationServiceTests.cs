using Decor.Application.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using FluentAssertions;

namespace Decor.Application.Tests;

public sealed class RoleRegistrationServiceTests
{
    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(11, false)]
    public async Task Create_RequiresStrictlyLowerAuthority(int level, bool allowed)
    {
        var (service, repository, _) = Create();
        var action = () => service.SaveAsync(0, "Equipe", null, level);
        if (allowed) await action.Should().NotThrowAsync();
        else await action.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.Writes.Should().Be(allowed ? 1 : 0);
    }

    [Theory]
    [InlineData(10, 9)]
    [InlineData(9, 10)]
    public async Task Update_ChecksExistingAndProposedLevels(int existing, int proposed)
    {
        var (service, repository, _) = Create(target: new(2, "Equipe", null, existing, false));
        var action = () => service.SaveAsync(2, "Equipe", null, proposed);
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.Writes.Should().Be(0);
    }

    [Theory]
    [InlineData("Administradores", false)]
    [InlineData("Administrador", false)]
    [InlineData(" administradores ", true)]
    public async Task ProtectedGroups_CannotBeEditedEvenByAdministrator(string name, bool flag)
    {
        var (service, repository, _) = Create(target: new(2, name, null, 30, flag), administrator: true);
        var action = () => service.SaveAsync(2, "Equipe", null, 40);
        await action.Should().ThrowAsync<InvalidOperationException>();
        repository.Writes.Should().Be(0);
        (await service.GetRolesAsync()).Should().Contain(role => role.RoleID == 2);
        (await service.GetEditableRoleIdsAsync()).Should().NotContain(2);
    }

    [Theory]
    [InlineData("Supervisores", true)]
    [InlineData("Supervisores", false)]
    [InlineData("Supervisor", true)]
    [InlineData("Equipe", true)]
    public async Task Administrator_CanEditSubordinateGroupsDespiteLegacyProtection(string name, bool flag)
    {
        var (service, repository, _) = Create(target: new(2, name, null, 30, flag), administrator: true);
        (await service.GetEditableRoleIdsAsync()).Should().Contain(2);
        (await service.GetRolesAsync()).Single(role => role.RoleID == 2).IsSystemProtected.Should().BeFalse();

        var saved = await service.SaveAsync(2, name, "Atualizado", 40);

        saved.Should().Be(new AdministrativeRoleDTO(2, name, "Atualizado", 40, false));
        repository.Writes.Should().Be(1);
    }

    [Fact]
    public async Task Save_NormalizesFieldsAndPreservesId()
    {
        var (service, repository, _) = Create(target: new(2, "Equipe", null, 8, false));
        (await service.GetEditableRoleIdsAsync()).Should().Contain(2).And.NotContain(1);
        var result = await service.SaveAsync(2, "  Equipe  ", "  Descrição  ", 9);
        result.Should().Be(new AdministrativeRoleDTO(2, "Equipe", "Descrição", 9, false));
        repository.Writes.Should().Be(1);
    }

    [Fact]
    public async Task Save_RejectsDuplicateNameIgnoringCaseAndWhitespace()
    {
        var (service, repository, _) = Create(target: new(2, "Equipe", null, 20, false));
        var action = () => service.SaveAsync(0, " equipe ", null, 9);
        await action.Should().ThrowAsync<InvalidOperationException>();
        repository.Writes.Should().Be(0);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("  ", 0)]
    [InlineData("Equipe", 256)]
    public async Task Save_ValidatesSchemaLimits(string name, int descriptionLength)
    {
        var (service, repository, _) = Create();
        var action = () => service.SaveAsync(0, name, new string('d', descriptionLength), 30);
        await action.Should().ThrowAsync<ArgumentException>();
        repository.Writes.Should().Be(0);
    }

    [Fact]
    public async Task Save_RejectsNameLongerThan50()
    {
        var (service, _, _) = Create();
        var action = () => service.SaveAsync(0, new string('n', 51), null, 30);
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Operations_RequireAuthenticationAndTheirPermission(bool authenticated, bool permitted)
    {
        var (service, repository, context) = Create(permitted: permitted);
        if (!authenticated) context.SignOut();
        var list = () => service.GetRolesAsync();
        var save = () => service.SaveAsync(0, "Equipe", null, 30);
        await list.Should().ThrowAsync<UnauthorizedAccessException>();
        await save.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.Writes.Should().Be(0);
    }

    [Fact]
    public async Task ReservedAdministrator_BypassesHierarchyWithoutRoleClaims()
    {
        var (service, _, context) = Create(target: new(2, "Equipe", null, int.MaxValue, false), administrator: true);
        context.SignIn(new ApplicationUser { UserID = 1, Username = SystemAccountDefaults.AdministratorUsername });
        (await service.SaveAsync(0, "Nova equipe", null, 0)).HierarchyLevel.Should().Be(0);
        (await service.SaveAsync(2, "Equipe", null, int.MaxValue)).HierarchyLevel.Should().Be(int.MaxValue);
        (await service.GetEditableRoleIdsAsync()).Should().Contain(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AdministratorRole_BypassesOnlyWhenSystemProtected(bool protectedRole)
    {
        var (service, _, context) = Create(target: new(2, SystemRoleDefaults.Administrators, null, 200, protectedRole));
        context.SignIn(new ApplicationUser { UserID = 1, Username = "gestor", Roles = [SystemRoleDefaults.Administrators] });
        var action = () => service.SaveAsync(0, "Equipe", null, 200);
        if (protectedRole) await action.Should().NotThrowAsync();
        else await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task MultipleRoles_UseHighestLevelFromRepository()
    {
        var (service, _, context) = Create(target: new(2, "Operadores", null, 50, false));
        context.SignIn(new ApplicationUser { UserID = 1, Username = "gestor", Roles = ["gestores", "Operadores"] });
        (await service.SaveAsync(0, "Equipe", null, 49)).RoleID.Should().Be(3);
        var equalLevel = () => service.SaveAsync(0, "Equipe", null, 50);
        await equalLevel.Should().ThrowAsync<UnauthorizedAccessException>();
        (await service.GetEditableRoleIdsAsync()).Should().Contain(1).And.NotContain(2);
    }

    [Fact]
    public async Task UnknownRoleClaims_DoNotGrantHierarchyAuthority()
    {
        var (service, repository, context) = Create();
        context.SignIn(new ApplicationUser { UserID = 1, Username = "gestor", Roles = ["Inexistente"] });
        foreach (var level in new[] { 0, int.MaxValue })
        {
            var action = () => service.SaveAsync(0, "Equipe", null, level);
            await action.Should().ThrowAsync<UnauthorizedAccessException>();
        }
        (await service.GetEditableRoleIdsAsync()).Should().BeEmpty();
        repository.Writes.Should().Be(0);
    }

    [Theory]
    [InlineData("administradores")]
    [InlineData("Administrador")]
    [InlineData(" ADMINISTRADOR ")]
    public async Task ReservedNames_CannotBeCreated(string name)
    {
        var (service, repository, _) = Create(administrator: true);
        var action = () => service.SaveAsync(0, name, null, 30);
        await action.Should().ThrowAsync<InvalidOperationException>();
        repository.Writes.Should().Be(0);
    }

    [Fact]
    public async Task Save_AcceptsSchemaBoundariesAndNormalizesBlankDescription()
    {
        var (service, _, _) = Create(administrator: true);
        var result = await service.SaveAsync(0, new string('n', 50), new string('d', 255), int.MaxValue);
        result.RoleName.Length.Should().Be(50);
        result.Description!.Length.Should().Be(255);
        (await service.SaveAsync(0, "Equipe", "  ", 30)).Description.Should().BeNull();
    }

    [Theory]
    [InlineData(-1, 30)]
    [InlineData(0, -1)]
    public async Task Save_RejectsNegativeIdentifiersAndLevels(int id, int level)
    {
        var (service, repository, _) = Create();
        var action = () => service.SaveAsync(id, "Equipe", null, level);
        await action.Should().ThrowAsync<ArgumentException>();
        repository.Writes.Should().Be(0);
    }

    [Fact]
    public async Task Update_RejectsMissingTarget()
    {
        var (service, repository, _) = Create();
        var action = () => service.SaveAsync(99, "Equipe", null, 30);
        await action.Should().ThrowAsync<InvalidOperationException>();
        repository.Writes.Should().Be(0);
    }

    [Fact]
    public async Task Update_ReportsRepositoryRefusal()
    {
        var (service, repository, _) = Create(target: new(2, "Equipe", null, 8, false));
        repository.UpdateResult = false;
        var action = () => service.SaveAsync(2, "Equipe", null, 9);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Permissions_AreSpecificToListAndSave()
    {
        var (_, repository, context) = Create();
        var lookup = new LookupRepository([new(1, "Gestores", null, 10, false)]);
        var reader = new RoleRegistrationService(lookup, repository, context, new Authorization(true, DecorPermissions.RolesView));
        (await reader.GetRolesAsync()).Should().HaveCount(1);
        (await reader.GetEditableRoleIdsAsync()).Should().BeEmpty();
        var save = () => reader.SaveAsync(0, "Equipe", null, 30);
        await save.Should().ThrowAsync<UnauthorizedAccessException>();
        var writer = new RoleRegistrationService(lookup, repository, context, new Authorization(true, DecorPermissions.RolesEdit));
        var list = () => writer.GetRolesAsync();
        await list.Should().ThrowAsync<UnauthorizedAccessException>();
        await writer.SaveAsync(0, "Equipe", null, 9);
    }

    [Fact]
    public async Task ViewModel_ShowsProtectedGroupsWithoutEnablingEditor()
    {
        var (service, _, context) = Create(target: new(2, SystemRoleDefaults.Administrators, null, 100, true), administrator: true);
        var viewModel = new GroupRegistrationViewModel(service, context, new Authorization(true));
        await viewModel.InitializeAsync();
        viewModel.Roles.Should().HaveCount(2);
        viewModel.SelectedRole = viewModel.Roles.Single(role => role.RoleID == 2);
        viewModel.CanEdit.Should().BeFalse();
        viewModel.SaveCommand.CanExecute(null).Should().BeFalse();
        viewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task ViewModel_SearchFiltersByCodeOrAllTextTermsAndClearEmptiesListing()
    {
        var (service, _, context) = Create(target: new(2, "Equipe de vendas", "Atendimento comercial", 8, false));
        var viewModel = new GroupRegistrationViewModel(service, context, new Authorization(true));
        await viewModel.InitializeAsync();
        viewModel.Listing.Clear();

        viewModel.SearchText = "vendas comercial";
        viewModel.SearchCommand.Execute(null);
        viewModel.Roles.Should().ContainSingle(role => role.RoleID == 2);

        viewModel.SearchText = "2";
        viewModel.SearchCommand.Execute(null);
        viewModel.Roles.Should().ContainSingle(role => role.RoleID == 2);

        viewModel.ClearSearchCommand.Execute(null);
        viewModel.SearchText.Should().BeEmpty();
        viewModel.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task ViewModel_AdministratorCanEditAndSaveLegacyProtectedSupervisor()
    {
        var (service, repository, context) = Create(target: new(2, SystemRoleDefaults.Supervisors, null, 30, true), administrator: true);
        var viewModel = new GroupRegistrationViewModel(service, context, new Authorization(true));
        await viewModel.InitializeAsync();
        viewModel.SelectedRole = viewModel.Roles.Single(role => role.RoleID == 2);
        viewModel.SelectedRole.IsSystemProtected.Should().BeFalse();
        viewModel.EditCommand.CanExecute(null).Should().BeTrue();
        viewModel.EditCommand.Execute(null);
        viewModel.Description = "Atualizado";
        viewModel.CanSave.Should().BeTrue();

        await viewModel.SaveAsync();

        viewModel.HasError.Should().BeFalse();
        viewModel.IsEditing.Should().BeFalse();
        viewModel.SelectedRole!.Description.Should().Be("Atualizado");
        viewModel.CanEdit.Should().BeTrue();
        repository.Writes.Should().Be(1);
    }

    [Theory]
    [InlineData(9, 9, true)]
    [InlineData(10, 9, false)]
    [InlineData(11, 9, false)]
    [InlineData(9, 10, false)]
    [InlineData(9, 11, false)]
    public async Task LegacyProtection_DoesNotBypassNonAdministratorHierarchy(int existing, int proposed, bool allowed)
    {
        var (service, repository, _) = Create(target: new(2, SystemRoleDefaults.Supervisors, null, existing, true));
        (await service.GetEditableRoleIdsAsync()).Contains(2).Should().Be(existing < 10);
        var action = () => service.SaveAsync(2, SystemRoleDefaults.Supervisors, null, proposed);
        if (allowed) await action.Should().NotThrowAsync();
        else await action.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.Writes.Should().Be(allowed ? 1 : 0);
    }

    [Theory]
    [InlineData(" supervisores ", "supervisores")]
    [InlineData("Supervisor", "Supervisor")]
    public async Task SubordinateNames_CanBeCreated(string name, string expected)
    {
        var (service, repository, _) = Create(administrator: true);
        (await service.SaveAsync(0, name, null, 30)).RoleName.Should().Be(expected);
        repository.Writes.Should().Be(1);
    }

    [Fact]
    public async Task ViewModel_NewSaveAndErrorMaintainEditorState()
    {
        var (service, repository, context) = Create();
        var viewModel = new GroupRegistrationViewModel(service, context, new Authorization(true));
        await viewModel.InitializeAsync();
        viewModel.NewCommand.Execute(null);
        viewModel.IsAdding.Should().BeTrue();
        viewModel.StatusMessage.Should().Be("Cadastrando um grupo.");
        viewModel.CanSave.Should().BeTrue();
        viewModel.Name = "Equipe";
        viewModel.HierarchyLevel = 10;
        await viewModel.SaveAsync();
        viewModel.HasError.Should().BeTrue();
        viewModel.ErrorMessage.Should().Contain("valor estritamente menor").And.Contain("Valores maiores representam maior autoridade");
        viewModel.Name.Should().Be("Equipe");
        viewModel.HierarchyLevel = 9;
        await viewModel.SaveAsync();
        viewModel.HasError.Should().BeFalse();
        viewModel.SelectedRole!.RoleID.Should().Be(3);
        repository.Writes.Should().Be(1);
        viewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task ViewModel_EditAndCancelRestoreSelectedFields()
    {
        var (service, repository, context) = Create(target: new(2, "Equipe", "Original", 8, false));
        var viewModel = new GroupRegistrationViewModel(service, context, new Authorization(true));
        await viewModel.InitializeAsync();
        viewModel.SelectedRole = viewModel.Roles.Single(role => role.RoleID == 2);
        viewModel.IsEditing.Should().BeFalse();
        viewModel.SaveCommand.CanExecute(null).Should().BeFalse();
        viewModel.EditCommand.Execute(null);
        viewModel.IsEditing.Should().BeTrue();
        viewModel.IsAdding.Should().BeFalse();
        viewModel.StatusMessage.Should().Be("Editando o grupo 2.");
        viewModel.Name = "Alterado";
        viewModel.Description = "Alterada";
        viewModel.CancelCommand.Execute(null);
        viewModel.IsEditing.Should().BeFalse();
        viewModel.StatusMessage.Should().BeEmpty();
        viewModel.Name.Should().Be("Equipe");
        viewModel.Description.Should().Be("Original");
        viewModel.HierarchyLevel.Should().Be(8);
        repository.Writes.Should().Be(0);
    }

    private static (RoleRegistrationService Service, TrackingRepository Repository, AuthenticatedUserContext Context) Create(
        AdministrativeRoleDTO? target = null, bool administrator = false, bool permitted = true)
    {
        var context = new AuthenticatedUserContext();
        context.SignIn(new ApplicationUser { UserID = 1, Username = administrator ? SystemAccountDefaults.AdministratorUsername : "operador", Roles = ["Gestores"] });
        var roles = new List<AdministrativeRoleDTO> { new(1, "Gestores", null, 10, false) };
        if (target is not null) roles.Add(target);
        var repository = new TrackingRepository();
        return (new RoleRegistrationService(new LookupRepository(roles), repository, context, new Authorization(permitted)), repository, context);
    }

    private sealed class TrackingRepository : IRoleRegistrationRepository
    {
        public int Writes { get; private set; }
        public bool UpdateResult { get; set; } = true;
        public Task<int> CreateAsync(string name, string? description, int hierarchyLevel, CancellationToken cancellationToken = default) { Writes++; return Task.FromResult(3); }
        public Task<bool> UpdateAsync(int roleId, string name, string? description, int hierarchyLevel, CancellationToken cancellationToken = default) { Writes++; return Task.FromResult(UpdateResult); }
    }

    private sealed class Authorization(bool permitted, string? permission = null) : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => permitted && (permission is null || permission == permissionCode);
        public bool CanView(string module) => permitted;
        public bool CanCreate(string module) => permitted;
        public bool CanEdit(string module) => permitted;
        public bool CanDelete(string module) => permitted;
    }

    private sealed class LookupRepository(IReadOnlyList<AdministrativeRoleDTO> roles) : IUserAdministrationRepository
    {
        public Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default) => Task.FromResult(roles);
        public Task<IReadOnlyList<AdministrativeUserDTO>> SearchAsync(string? search, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdministrativeUserDTO?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CreateWithRolesAsync(string username, string displayName, string passwordHash, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(int userId, string username, string displayName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> SetActivePreservingLastAdministratorAsync(int userId, bool isActive, int administratorRoleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateTemporaryPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplaceRolesPreservingLastAdministratorAsync(int userId, IReadOnlyCollection<int> roleIds, int administratorRoleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplacePermissionOverridesAsync(int userId, IReadOnlyCollection<PermissionOverrideDTO> overrides, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}