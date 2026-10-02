using Decor.AvaloniaUI.Presentation;
using Decor.Core.Common;

namespace Decor.Application.Tests;

public sealed class RoleNamePresentationTests
{
    [Theory]
    [InlineData(SystemRoleDefaults.Administrator, "Administradores")]
    [InlineData(SystemRoleDefaults.Supervisor, "Supervisores")]
    public void GetGroupDisplayName_PresentsProtectedRolesInPlural(string canonicalName, string expected)
    {
        RoleNamePresentation.GetGroupDisplayName(canonicalName).Should().Be(expected);
    }

    [Theory]
    [InlineData("Administrador", "Administradores")]
    [InlineData("Supervisor", "Supervisores")]
    public void GetGroupDisplayName_DoesNotChangeCanonicalRoleName(string canonicalName, string expectedDisplayName)
    {
        var role = new Decor.Core.DTOs.AdministrativeRoleDTO(1, canonicalName, null, 10, true);

        RoleNamePresentation.GetGroupDisplayName(role.RoleName).Should().Be(expectedDisplayName);
        role.RoleName.Should().Be(canonicalName);
        SystemRoleDefaults.Permissions.ContainsKey(role.RoleName).Should().BeTrue();
    }

    [Fact]
    public void GetGroupDisplayName_LeavesCustomRoleNamesUnchanged()
    {
        RoleNamePresentation.GetGroupDisplayName("Vendedor").Should().Be("Vendedor");
    }
}