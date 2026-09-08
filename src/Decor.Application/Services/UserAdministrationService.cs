using System.Security.Cryptography;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
namespace Decor.Application.Services;

public sealed class UserAdministrationService(IUserAdministrationRepository repository, IPasswordHasher hasher, IPasswordPolicy policy, IAuthenticatedUserContext context, IAuthorizationService authorization) : IUserAdministrationService
{
    public async Task<IReadOnlyList<AdministrativeUserDTO>> SearchAsync(string? search, CancellationToken cancellationToken = default) { Require(DecorPermissions.UsersView); var users=await repository.SearchAsync(search,cancellationToken); var authority=OperatorAuthority(await repository.GetRolesAsync(cancellationToken)); return authority.IsAdministrator?users:users.Where(u=>u.Roles.Count==0||u.Roles.Max(r=>r.HierarchyLevel)<authority.Level).ToArray(); }
    public async Task<AdministrativeUserDTO?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) { Require(DecorPermissions.UsersView); var user=await repository.GetByIdAsync(userId,cancellationToken); if(user is not null) await EnsureTarget(user,cancellationToken); return user; }
    public async Task<TemporaryPasswordResult> CreateAsync(string username, string displayName, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.UsersCreate); ValidateNames(username, displayName); await EnsureRolesAssignable(roleIds,cancellationToken);
        var password=GenerateTemporaryPassword(); await repository.CreateWithRolesAsync(username.Trim(),displayName.Trim(),hasher.Hash(password),roleIds,cancellationToken); return new(password);
    }
    public async Task UpdateAsync(int userId,string username,string displayName,CancellationToken cancellationToken=default) { Require(DecorPermissions.UsersEdit); ValidateNames(username,displayName); var target=await Target(userId,cancellationToken); if(!await repository.UpdateAsync(target.UserID,username.Trim(),displayName.Trim(),cancellationToken)) throw new InvalidOperationException("Usuário não encontrado."); if(context.User?.UserID==userId) context.SignOut(); }
    public async Task SetActiveAsync(int userId,bool active,CancellationToken cancellationToken=default) { Require(active?DecorPermissions.UsersActivate:DecorPermissions.UsersDeactivate); await Target(userId,cancellationToken); var admin=await AdministratorRole(cancellationToken); await repository.SetActivePreservingLastAdministratorAsync(userId,active,admin.RoleID,cancellationToken); SignOutIfCurrentUser(userId); }
    public async Task ReplaceRolesAsync(int userId,IReadOnlyCollection<int> roleIds,CancellationToken cancellationToken=default)
    {
        Require(DecorPermissions.UsersAssignRoles); await Target(userId,cancellationToken); await EnsureRolesAssignable(roleIds,cancellationToken);
        var admin=await AdministratorRole(cancellationToken); await repository.ReplaceRolesPreservingLastAdministratorAsync(userId,roleIds,admin.RoleID,cancellationToken); SignOutIfCurrentUser(userId);
    }
    public async Task ReplacePermissionOverridesAsync(int userId,IReadOnlyCollection<PermissionOverrideDTO> overrides,CancellationToken cancellationToken=default) { Require(DecorPermissions.UsersManagePermissions); await Target(userId,cancellationToken); foreach(var item in overrides.Where(x=>x.IsGranted)) Require(item.PermissionCode); await repository.ReplacePermissionOverridesAsync(userId,overrides,cancellationToken); SignOutIfCurrentUser(userId); }
    public async Task RestorePermissionsAsync(int userId,CancellationToken cancellationToken=default) { Require(DecorPermissions.UsersRestorePermissions); await Target(userId,cancellationToken); await repository.ReplacePermissionOverridesAsync(userId,[],cancellationToken); SignOutIfCurrentUser(userId); }
    public async Task<TemporaryPasswordResult> ResetPasswordAsync(int userId,CancellationToken cancellationToken=default) { Require(DecorPermissions.UsersResetPassword); await Target(userId,cancellationToken); var password=GenerateTemporaryPassword(); if(!await repository.UpdateTemporaryPasswordAsync(userId,hasher.Hash(password),cancellationToken)) throw new InvalidOperationException("Não foi possível redefinir a senha de usuário inativo."); if(context.User?.UserID==userId)context.SignOut(); return new(password); }
    private async Task<AdministrativeUserDTO> Target(int id,CancellationToken ct) { var user=await repository.GetByIdAsync(id,ct) ?? throw new InvalidOperationException("Usuário não encontrado."); await EnsureTarget(user,ct); return user; }
    private void Require(string p) { if(!context.IsAuthenticated || !authorization.HasPermission(p)) throw new UnauthorizedAccessException("Você não possui permissão para esta operação administrativa."); }
    private async Task EnsureTarget(AdministrativeUserDTO target,CancellationToken ct) { var roles=await repository.GetRolesAsync(ct); var (level,isAdmin)=OperatorAuthority(roles); if(!isAdmin&&target.Roles.Count>0&&target.Roles.Max(r=>r.HierarchyLevel)>=level) throw new UnauthorizedAccessException("A hierarquia não permite administrar este usuário."); }
    private async Task EnsureRolesAssignable(IReadOnlyCollection<int> ids,CancellationToken ct) { var roles=await repository.GetRolesAsync(ct); var (level,isAdmin)=OperatorAuthority(roles); if(ids.Any(id=>roles.All(r=>r.RoleID!=id)||(!isAdmin&&roles.Single(r=>r.RoleID==id).HierarchyLevel>=level))) throw new UnauthorizedAccessException("A hierarquia não permite atribuir uma ou mais roles."); }
    private (int Level,bool IsAdministrator) OperatorAuthority(IReadOnlyCollection<AdministrativeRoleDTO> roles) { var assigned=roles.Where(r=>context.User!.Roles.Contains(r.RoleName,StringComparer.OrdinalIgnoreCase)).ToArray(); return (assigned.Select(r=>r.HierarchyLevel).DefaultIfEmpty(0).Max(),assigned.Any(r=>r.IsSystemProtected&&r.RoleName.Equals(SystemRoleDefaults.Administrator,StringComparison.OrdinalIgnoreCase))); }
    private async Task<AdministrativeRoleDTO> AdministratorRole(CancellationToken ct)=>(await repository.GetRolesAsync(ct)).Single(r=>r.IsSystemProtected&&r.RoleName.Equals(SystemRoleDefaults.Administrator,StringComparison.OrdinalIgnoreCase));
    private void SignOutIfCurrentUser(int userId) { if (context.User?.UserID == userId) context.SignOut(); }
    private static void ValidateNames(string username,string displayName) { if(string.IsNullOrWhiteSpace(username)||username.Trim().Length>50||string.IsNullOrWhiteSpace(displayName)||displayName.Trim().Length>100) throw new ArgumentException("Nome de usuário ou nome de exibição inválido."); }
    private string GenerateTemporaryPassword() { const string alphabet="ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%"; var chars=new char[16]; chars[0]='A'; chars[1]='a'; chars[2]='2'; chars[3]='!'; for(var i=4;i<chars.Length;i++) chars[i]=alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]; var result=new string(chars.OrderBy(_=>RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray()); if(!policy.IsValid(result)) throw new InvalidOperationException("A senha temporária não atende à política."); return result; }
}
