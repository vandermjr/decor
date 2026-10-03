using Decor.Core.DTOs;

namespace Decor.Application.Tests;

public sealed class AdministrativeUserPresentationTests
{
    [Fact]
    public void AdministratorAccount_UsesReservedPresentationWithoutEmployee()
    {
        var user = new AdministrativeUserDTO(1, "admin", "legacy display name", true, [], []);

        user.PresentationName.Should().Be("Administrador");
        user.IsSystemAdministrator.Should().BeTrue();
    }

    [Fact]
    public void EmployeeName_IsPrimaryPresentationForRegularUsers()
    {
        var user = new AdministrativeUserDTO(2, "ana", "legacy display name", true, [], [], "Ana Silva");

        user.PresentationName.Should().Be("Ana Silva");
        user.IsSystemAdministrator.Should().BeFalse();
    }

    [Fact]
    public void Username_IsPresentationFallbackWhenEmployeeIsMissing()
    {
        var user = new AdministrativeUserDTO(3, "unlinked", "legacy display name", true, [], []);

        user.PresentationName.Should().Be("unlinked");
    }
}