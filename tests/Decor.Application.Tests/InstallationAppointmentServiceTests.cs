using System.ComponentModel.DataAnnotations;
using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Tests;

public sealed class InstallationAppointmentServiceTests
{
    [Fact]
    public async Task CreateAppointment_RequiresExactlyOneExecutor()
    {
        var repository = new FakeRepository { ServiceOrderItemExistsResult = true };
        var service = CreateService(repository, DecorPermissions.InstallationAppointmentsCreate);

        var action = () => service.CreateAppointmentAsync(1, new DateTime(2026, 9, 15), null, 10, 20, 30);

        await action.Should().ThrowAsync<ValidationException>().WithMessage("*exatamente um executor*");
        repository.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task CreateAppointment_RejectsNonServiceOrderItem()
    {
        var repository = new FakeRepository { ServiceOrderItemExistsResult = false };
        var service = CreateService(repository, DecorPermissions.InstallationAppointmentsCreate);

        var action = () => service.CreateAppointmentAsync(1, new DateTime(2026, 9, 15), null, 10, null, 30);

        await action.Should().ThrowAsync<ValidationException>().WithMessage("*item de serviço*");
        repository.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task CreateAppointment_RejectsActiveExecutorConflict()
    {
        var repository = new FakeRepository { ServiceOrderItemExistsResult = true, ConflictResult = true };
        var service = CreateService(repository, DecorPermissions.InstallationAppointmentsCreate);

        var action = () => service.CreateAppointmentAsync(1, new DateTime(2026, 9, 15), null, 10, null, 30);

        await action.Should().ThrowAsync<ValidationException>().WithMessage("*agendamento ativo*");
        repository.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Reschedule_UpdatesStatusAndStoresHistory()
    {
        var repository = new FakeRepository
        {
            Appointment = new InstallationAppointment { AppointmentID = 7, OrderItemID = 1, ScheduledDate = new DateTime(2026, 9, 15), ExecutorEmployeeID = 10, Status = InstallationAppointmentStatus.Scheduled, CreatedByEmployeeID = 30 }
        };
        var service = CreateService(repository, DecorPermissions.InstallationAppointmentsReschedule);

        var result = await service.RescheduleAsync(7, new DateTime(2026, 9, 18), "Cliente solicitou", 30);

        result.Status.Should().Be(InstallationAppointmentStatus.Rescheduled);
        repository.RescheduleCalls.Should().Be(1);
        repository.LastReschedule!.PreviousDate.Should().Be(new DateTime(2026, 9, 15));
        repository.LastReschedule.NewDate.Should().Be(new DateTime(2026, 9, 18));
    }

    [Fact]
    public async Task Cancel_RejectsCompletedAppointment()
    {
        var repository = new FakeRepository { Appointment = new InstallationAppointment { AppointmentID = 7, Status = InstallationAppointmentStatus.Completed } };
        var service = CreateService(repository, DecorPermissions.InstallationAppointmentsCancel);

        var action = () => service.CancelAsync(7);

        await action.Should().ThrowAsync<ValidationException>();
        repository.SaveCalls.Should().Be(0);
    }

    private static InstallationAppointmentService CreateService(FakeRepository repository, string permission) => new(repository, new InstallationAppointmentDTOValidator(), new ValidRepositoryValidator(), new FixedAuthorizationService(permission));

    private sealed class ValidRepositoryValidator : IRepositoryValidator<InstallationAppointment>
    {
        public IEnumerable<string> Validate(InstallationAppointment entity) => [];
    }

    private sealed class FixedAuthorizationService(string permission) : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => permissionCode == permission;
        public bool CanView(string resource) => HasPermission($"{resource}.View");
        public bool CanCreate(string resource) => HasPermission($"{resource}.Create");
        public bool CanEdit(string resource) => false;
        public bool CanDelete(string resource) => HasPermission($"{resource}.Delete");
    }

    private sealed class FakeRepository : IInstallationAppointmentRepository
    {
        public bool ServiceOrderItemExistsResult { get; init; }
        public bool ConflictResult { get; init; }
        public InstallationAppointment? Appointment { get; init; }
        public int SaveCalls { get; private set; }
        public int RescheduleCalls { get; private set; }
        public AppointmentReschedule? LastReschedule { get; private set; }
        public int Save(InstallationAppointment entity) => 1;
        public int Delete(int id) => throw new NotSupportedException();
        public IEnumerable<InstallationAppointment> SearchGetBy(string? arg = null) => [];
        public Task<int> SaveAsync(InstallationAppointment entity, CancellationToken cancellationToken = default) { SaveCalls++; return Task.FromResult(1); }
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<InstallationAppointment>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InstallationAppointment>>([]);
        public Task<InstallationAppointment?> GetByIdAsync(int appointmentId, CancellationToken cancellationToken = default) => Task.FromResult(Appointment);
        public Task<bool> ServiceOrderItemExistsAsync(int orderItemId, CancellationToken cancellationToken = default) => Task.FromResult(ServiceOrderItemExistsResult);
        public Task<bool> HasActiveConflictAsync(int? executorEmployeeId, int? executorPartnerId, DateTime scheduledDate, int? excludingAppointmentId = null, CancellationToken cancellationToken = default) => Task.FromResult(ConflictResult);
        public Task<IReadOnlyList<InstallationAppointment>> GetScheduleForExecutorAsync(int? executorEmployeeId, int? executorPartnerId, DateTime from, DateTime to, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InstallationAppointment>>([]);
        public Task<IReadOnlyList<AppointmentReschedule>> GetReschedulesAsync(int appointmentId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AppointmentReschedule>>([]);
        public Task<int> RescheduleAsync(InstallationAppointment appointment, AppointmentReschedule reschedule, CancellationToken cancellationToken = default) { RescheduleCalls++; LastReschedule = reschedule; return Task.FromResult(1); }
    }
}