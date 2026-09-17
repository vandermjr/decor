using System.ComponentModel.DataAnnotations;
using Decor.Application.Services;
using Decor.Application.Validation;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;
using FluentAssertions;
using Xunit;

namespace Decor.Application.Tests;

public sealed class UnitOfMeasureServiceTests
{
    [Fact]
    public async Task SaveUnitOfMeasure_WithoutPermission_ThrowsUnauthorizedAccessException()
    {
        var repo = new TrackingUnitOfMeasureRepository();
        var service = CreateService(repo);

        var dto = new UnitOfMeasureDTO(0, "UN", "Unidade", false, true);
        var act = () => service.SaveUnitOfMeasureAsync(dto);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SaveUnitOfMeasure_ValidData_SavesSuccessfully()
    {
        var repo = new TrackingUnitOfMeasureRepository();
        var service = CreateService(repo, DecorPermissions.UnitsOfMeasureCreate);

        var dto = new UnitOfMeasureDTO(0, "UN", "Unidade", false, true);
        await service.SaveUnitOfMeasureAsync(dto);

        repo.Units.Should().HaveCount(1);
        repo.Units[0].Code.Should().Be("UN");
        repo.Units[0].Description.Should().Be("Unidade");
        repo.Units[0].AllowsFraction.Should().BeFalse();
    }

    [Fact]
    public async Task SaveUnitOfMeasure_DuplicateCode_ThrowsValidationException()
    {
        var repo = new TrackingUnitOfMeasureRepository();
        repo.Units.Add(new UnitOfMeasure { UnitOfMeasureID = 1, Code = "UN", Description = "Unidade", AllowsFraction = false, IsActive = true });

        var service = CreateService(repo, DecorPermissions.UnitsOfMeasureCreate);

        var dto = new UnitOfMeasureDTO(0, "un", "Outra", true, true);
        var act = () => service.SaveUnitOfMeasureAsync(dto);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*UN*");
    }

    [Fact]
    public async Task SaveUnitOfMeasure_Update_PreservesCurrentInactiveState_WhenIsActiveIsNotExplicitlyTrue()
    {
        var repo = new TrackingUnitOfMeasureRepository();
        repo.Units.Add(new UnitOfMeasure { UnitOfMeasureID = 7, Code = "UN", Description = "Unidade", AllowsFraction = false, IsActive = false });

        var service = CreateService(repo, DecorPermissions.UnitsOfMeasureEdit);

        var dto = new UnitOfMeasureDTO(7, "UN", "Unidade atualizada", true, false);
        await service.SaveUnitOfMeasureAsync(dto);

        repo.Units[0].Description.Should().Be("Unidade atualizada");
        repo.Units[0].AllowsFraction.Should().BeTrue();
        repo.Units[0].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateUnitOfMeasure_Existing_UpdatesToInactive()
    {
        var repo = new TrackingUnitOfMeasureRepository();
        repo.Units.Add(new UnitOfMeasure { UnitOfMeasureID = 3, Code = "M", Description = "Metro", AllowsFraction = true, IsActive = true });

        var service = CreateService(repo, DecorPermissions.UnitsOfMeasureDeactivate);

        await service.DeactivateAsync(3);

        repo.Units[0].IsActive.Should().BeFalse();
    }

    private static UnitOfMeasureService CreateService(TrackingUnitOfMeasureRepository repo, params string[] permissions)
    {
        return new UnitOfMeasureService(
            repo,
            new UnitOfMeasureDTOValidator(),
            new UnitOfMeasureRepositoryValidator(repo),
            new FixedAuthorizationService(permissions));
    }

    private sealed class TrackingUnitOfMeasureRepository : IUnitOfMeasureRepository
    {
        public List<UnitOfMeasure> Units { get; } = [];
        private int _nextId = 1;

        public int Save(UnitOfMeasure entity)
        {
            if (entity.UnitOfMeasureID == 0) entity.UnitOfMeasureID = _nextId++;
            Units.RemoveAll(u => u.UnitOfMeasureID == entity.UnitOfMeasureID);
            Units.Add(entity);
            return 1;
        }

        public int Delete(int id)
        {
            Units.RemoveAll(u => u.UnitOfMeasureID == id);
            return 1;
        }

        public IEnumerable<UnitOfMeasure> SearchGetBy(string? arg = null) => Units;

        public Task<int> SaveAsync(UnitOfMeasure entity, CancellationToken cancellationToken = default)
        {
            Save(entity);
            return Task.FromResult(1);
        }

        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            Delete(id);
            return Task.FromResult(1);
        }

        public Task<IReadOnlyList<UnitOfMeasure>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(arg) && int.TryParse(arg, out var id))
            {
                return Task.FromResult<IReadOnlyList<UnitOfMeasure>>(Units.Where(u => u.UnitOfMeasureID == id).ToList());
            }
            return Task.FromResult<IReadOnlyList<UnitOfMeasure>>(Units.ToList());
        }

        public Task<UnitOfMeasure?> GetByIdAsync(int unitOfMeasureId, CancellationToken cancellationToken = default)
            => Task.FromResult(Units.FirstOrDefault(u => u.UnitOfMeasureID == unitOfMeasureId));

        public bool CodeExists(string code, int currentUnitOfMeasureId)
            => Units.Any(u => u.Code.Equals(code, StringComparison.OrdinalIgnoreCase) && u.UnitOfMeasureID != currentUnitOfMeasureId);

        public Task<bool> CodeExistsAsync(string code, int currentUnitOfMeasureId, CancellationToken cancellationToken = default)
            => Task.FromResult(CodeExists(code, currentUnitOfMeasureId));

        public int Deactivate(int unitOfMeasureId)
        {
            var unit = Units.FirstOrDefault(u => u.UnitOfMeasureID == unitOfMeasureId);
            if (unit == null) return 0;
            unit.IsActive = false;
            return 1;
        }

        public Task<int> DeactivateAsync(int unitOfMeasureId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Deactivate(unitOfMeasureId));
        }

        public int Activate(int unitOfMeasureId)
        {
            var unit = Units.FirstOrDefault(u => u.UnitOfMeasureID == unitOfMeasureId);
            if (unit == null) return 0;
            unit.IsActive = true;
            return 1;
        }

        public Task<int> ActivateAsync(int unitOfMeasureId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Activate(unitOfMeasureId));
        }
    }

    private sealed class FixedAuthorizationService(params string[] permissions) : IAuthorizationService
    {
        private readonly HashSet<string> _permissions = new(permissions);
        public bool HasPermission(string permission) => _permissions.Contains(permission);
        public bool CanCreate(string resource) => HasPermission($"{resource}.Create");
        public bool CanEdit(string resource) => HasPermission($"{resource}.Edit");
        public bool CanDelete(string resource) => HasPermission($"{resource}.Delete");
        public bool CanView(string resource) => HasPermission($"{resource}.View");
    }
}
