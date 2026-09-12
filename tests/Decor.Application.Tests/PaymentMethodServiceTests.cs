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

public sealed class PaymentMethodServiceTests
{
    [Fact]
    public async Task SavePaymentMethod_WithoutPermission_ThrowsUnauthorizedAccessException()
    {
        var repo = new TrackingPaymentMethodRepository();
        var service = CreateService(repo);

        var dto = new PaymentMethodDTO(0, "Boleto", PaymentTiming.Term, true);
        var act = () => service.SavePaymentMethodAsync(dto);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SavePaymentMethod_ValidData_SavesSuccessfully()
    {
        var repo = new TrackingPaymentMethodRepository();
        var service = CreateService(repo, DecorPermissions.PaymentMethodsCreate);

        var dto = new PaymentMethodDTO(0, "Boleto Bancário", PaymentTiming.Term, true);
        await service.SavePaymentMethodAsync(dto);

        repo.PaymentMethods.Should().HaveCount(1);
        repo.PaymentMethods[0].Name.Should().Be("Boleto Bancário");
        repo.PaymentMethods[0].Timing.Should().Be(PaymentTiming.Term);
    }

    [Fact]
    public async Task SavePaymentMethod_DuplicateName_ThrowsValidationException()
    {
        var repo = new TrackingPaymentMethodRepository();
        repo.PaymentMethods.Add(new PaymentMethod { PaymentMethodID = 1, Name = "PIX", Timing = PaymentTiming.Cash, IsActive = true });

        var service = CreateService(repo, DecorPermissions.PaymentMethodsCreate);

        var dto = new PaymentMethodDTO(0, "PIX", PaymentTiming.Cash, true);
        var act = () => service.SavePaymentMethodAsync(dto);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*PIX*");
    }

    [Fact]
    public async Task DeletePaymentMethod_InUse_ThrowsValidationException()
    {
        var repo = new TrackingPaymentMethodRepository();
        repo.PaymentMethods.Add(new PaymentMethod { PaymentMethodID = 1, Name = "PIX", Timing = PaymentTiming.Cash, IsActive = true });
        repo.InUseIds.Add(1);

        var service = CreateService(repo, DecorPermissions.PaymentMethodsDelete);

        var act = () => service.DeletePaymentMethodAsync(1);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*uso*");
    }

    [Fact]
    public async Task DeletePaymentMethod_NotInUse_DeletesSuccessfully()
    {
        var repo = new TrackingPaymentMethodRepository();
        repo.PaymentMethods.Add(new PaymentMethod { PaymentMethodID = 1, Name = "PIX", Timing = PaymentTiming.Cash, IsActive = true });

        var service = CreateService(repo, DecorPermissions.PaymentMethodsDelete);

        await service.DeletePaymentMethodAsync(1);

        repo.PaymentMethods.Should().BeEmpty();
    }

    private static PaymentMethodService CreateService(TrackingPaymentMethodRepository repo, params string[] permissions)
    {
        return new PaymentMethodService(
            repo,
            new PaymentMethodDTOValidator(),
            new PaymentMethodRepositoryValidator(repo),
            new FixedAuthorizationService(permissions));
    }

    private sealed class TrackingPaymentMethodRepository : IPaymentMethodRepository
    {
        public List<PaymentMethod> PaymentMethods { get; } = [];
        public HashSet<int> InUseIds { get; } = [];
        private int _nextId = 1;

        public int Save(PaymentMethod entity)
        {
            if (entity.PaymentMethodID == 0) entity.PaymentMethodID = _nextId++;
            PaymentMethods.RemoveAll(p => p.PaymentMethodID == entity.PaymentMethodID);
            PaymentMethods.Add(entity);
            return 1;
        }

        public Task<int> SaveAsync(PaymentMethod entity, CancellationToken cancellationToken = default)
        {
            Save(entity);
            return Task.FromResult(1);
        }

        public int Delete(int id)
        {
            PaymentMethods.RemoveAll(p => p.PaymentMethodID == id);
            return 1;
        }

        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            Delete(id);
            return Task.FromResult(1);
        }

        public IEnumerable<PaymentMethod> SearchGetBy(string? arg = null) => PaymentMethods;

        public Task<IReadOnlyList<PaymentMethod>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(arg) && int.TryParse(arg, out int id))
            {
                var filtered = PaymentMethods.Where(p => p.PaymentMethodID == id).ToList();
                return Task.FromResult<IReadOnlyList<PaymentMethod>>(filtered);
            }
            return Task.FromResult<IReadOnlyList<PaymentMethod>>(PaymentMethods);
        }

        public bool IsInUse(int paymentMethodId) => InUseIds.Contains(paymentMethodId);

        public Task<bool> IsInUseAsync(int paymentMethodId, CancellationToken cancellationToken = default)
            => Task.FromResult(IsInUse(paymentMethodId));

        public bool NameExists(string name, int currentPaymentMethodId)
            => PaymentMethods.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.PaymentMethodID != currentPaymentMethodId);

        public Task<bool> NameExistsAsync(string name, int currentPaymentMethodId, CancellationToken cancellationToken = default)
            => Task.FromResult(NameExists(name, currentPaymentMethodId));
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
