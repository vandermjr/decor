using System.ComponentModel.DataAnnotations;
using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Tests;

public sealed class ServiceExecutionRecordServiceTests
{
    [Fact]
    public async Task RegisterExecution_RequiresSignedConfirmationWhenCustomerIsPresent()
    {
        var context = CreateContext();

        var action = () => context.Service.RegisterExecutionAsync(7, DateTime.UtcNow, null, true, null, null);

        await action.Should().ThrowAsync<ValidationException>().WithMessage("*assinou a confirmação*");
        context.RecordRepository.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task RegisterExecution_RequiresAuthorizationWhenCustomerIsAbsent()
    {
        var context = CreateContext();

        var action = () => context.Service.RegisterExecutionAsync(7, DateTime.UtcNow, null, false, null, " ");

        await action.Should().ThrowAsync<ValidationException>().WithMessage("*autorização de ausência*");
        context.RecordRepository.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task RegisterExecution_CompletesAppointmentAndStoresRecord()
    {
        var context = CreateContext();

        var result = await context.Service.RegisterExecutionAsync(7, DateTime.UtcNow, "  concluído  ", true, false, null);

        result.CustomerSignedConfirmation.Should().BeFalse();
        result.Observations.Should().Be("concluído");
        context.RecordRepository.SaveCalls.Should().Be(1);
        context.AppointmentService.CompletedAppointmentId.Should().Be(7);
    }

    [Fact]
    public async Task RegisterExecution_RejectsCompletedAppointment()
    {
        var context = CreateContext(InstallationAppointmentStatus.Completed);

        var action = () => context.Service.RegisterExecutionAsync(7, DateTime.UtcNow, null, false, null, "Autorizado");

        await action.Should().ThrowAsync<ValidationException>().WithMessage("*programados*");
    }

    [Fact]
    public async Task RegisterExecution_RejectsDuplicateExecution()
    {
        var context = CreateContext();
        context.RecordRepository.Record = new ServiceExecutionRecord { ExecutionID = 1, AppointmentID = 7 };

        var action = () => context.Service.RegisterExecutionAsync(7, DateTime.UtcNow, null, false, null, "Autorizado");

        await action.Should().ThrowAsync<ValidationException>().WithMessage("*já possui*");
    }

    private static TestContext CreateContext(InstallationAppointmentStatus status = InstallationAppointmentStatus.Scheduled)
    {
        var appointmentRepository = new FakeAppointmentRepository
        {
            Appointment = new InstallationAppointment { AppointmentID = 7, Status = status }
        };
        var recordRepository = new FakeRecordRepository();
        var appointmentService = new FakeAppointmentService();
        var service = new ServiceExecutionRecordService(recordRepository, appointmentRepository, appointmentService, new ServiceExecutionRecordDTOValidator(), new ValidRepositoryValidator(), new FixedAuthorizationService());
        return new TestContext(service, recordRepository, appointmentService);
    }

    private sealed record TestContext(ServiceExecutionRecordService Service, FakeRecordRepository RecordRepository, FakeAppointmentService AppointmentService);
    private sealed class ValidRepositoryValidator : IRepositoryValidator<ServiceExecutionRecord> { public IEnumerable<string> Validate(ServiceExecutionRecord entity) => []; }
    private sealed class FixedAuthorizationService : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => permissionCode is DecorPermissions.ServiceExecutionRecordsCreate or DecorPermissions.ServiceExecutionRecordsView;
        public bool CanView(string resource) => true;
        public bool CanCreate(string resource) => true;
        public bool CanEdit(string resource) => false;
        public bool CanDelete(string resource) => false;
    }

    private sealed class FakeRecordRepository : IServiceExecutionRecordRepository
    {
        public ServiceExecutionRecord? Record { get; set; }
        public int SaveCalls { get; private set; }
        public int Save(ServiceExecutionRecord entity) { SaveCalls++; entity.ExecutionID = 1; Record = entity; return 1; }
        public Task<int> SaveAsync(ServiceExecutionRecord entity, CancellationToken cancellationToken = default) { Save(entity); return Task.FromResult(1); }
        public int Delete(int id) => throw new NotSupportedException();
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IEnumerable<ServiceExecutionRecord> SearchGetBy(string? arg = null) => [];
        public Task<IReadOnlyList<ServiceExecutionRecord>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ServiceExecutionRecord>>([]);
        public Task<ServiceExecutionRecord?> GetByIdAsync(int executionId, CancellationToken cancellationToken = default) => Task.FromResult(Record);
        public Task<ServiceExecutionRecord?> GetByAppointmentIdAsync(int appointmentId, CancellationToken cancellationToken = default) => Task.FromResult(Record);
    }

    private sealed class FakeAppointmentRepository : IInstallationAppointmentRepository
    {
        public InstallationAppointment? Appointment { get; init; }
        public int Save(InstallationAppointment entity) => 1;
        public Task<int> SaveAsync(InstallationAppointment entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public int Delete(int id) => throw new NotSupportedException();
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IEnumerable<InstallationAppointment> SearchGetBy(string? arg = null) => [];
        public Task<IReadOnlyList<InstallationAppointment>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InstallationAppointment>>([]);
        public Task<InstallationAppointment?> GetByIdAsync(int appointmentId, CancellationToken cancellationToken = default) => Task.FromResult(Appointment);
        public Task<bool> ServiceOrderItemExistsAsync(int orderItemId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> HasActiveConflictAsync(int? executorEmployeeId, int? executorPartnerId, DateTime scheduledDate, int? excludingAppointmentId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<InstallationAppointment>> GetScheduleForExecutorAsync(int? executorEmployeeId, int? executorPartnerId, DateTime from, DateTime to, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InstallationAppointment>>([]);
        public Task<IReadOnlyList<AppointmentReschedule>> GetReschedulesAsync(int appointmentId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AppointmentReschedule>>([]);
        public Task<int> RescheduleAsync(InstallationAppointment appointment, AppointmentReschedule reschedule, CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeAppointmentService : IInstallationAppointmentService
    {
        public int? CompletedAppointmentId { get; private set; }
        public Task<InstallationAppointmentDTO> CreateAppointmentAsync(int orderItemId, DateTime scheduledDate, TimeSpan? scheduledTime, int? executorEmployeeId, int? executorPartnerId, int createdByEmployeeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InstallationAppointmentDTO> RescheduleAsync(int appointmentId, DateTime newDate, string reason, int registeredByEmployeeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelAsync(int appointmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CompleteAsync(int appointmentId, CancellationToken cancellationToken = default) { CompletedAppointmentId = appointmentId; return Task.CompletedTask; }
        public Task<IReadOnlyList<InstallationAppointmentDTO>> GetScheduleForExecutorAsync(int? executorEmployeeId, int? executorPartnerId, DateTime from, DateTime to, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AppointmentRescheduleDTO>> GetRescheduleHistoryAsync(int appointmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
