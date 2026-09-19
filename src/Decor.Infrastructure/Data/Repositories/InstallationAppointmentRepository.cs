using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class InstallationAppointmentRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IInstallationAppointmentRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(InstallationAppointment entity)
    {
        using var connection = _dbConnection.CreateConnection();
        var (sql, parameters) = entity.AppointmentID == 0
            ? _createCommandBuilder().Insert(i => i.Entity(entity)).ReturningGeneratedId().Build()
            : _createCommandBuilder().Update(u => u.Entity(entity)).Where(w => w.Equals<InstallationAppointment>(a => a.AppointmentID, entity.AppointmentID)).Build();
        if (entity.AppointmentID == 0)
        {
            entity.AppointmentID = connection.QuerySingle<int>(sql, parameters);
            return 1;
        }
        return connection.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(InstallationAppointment entity, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        var (sql, parameters) = entity.AppointmentID == 0
            ? _createCommandBuilder().Insert(i => i.Entity(entity)).ReturningGeneratedId().Build()
            : _createCommandBuilder().Update(u => u.Entity(entity)).Where(w => w.Equals<InstallationAppointment>(a => a.AppointmentID, entity.AppointmentID)).Build();
        if (entity.AppointmentID == 0)
        {
            entity.AppointmentID = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            return 1;
        }
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int id) => throw new NotSupportedException("Agendamentos não podem ser excluídos.");
    public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException("Agendamentos não podem ser excluídos.");
    public IEnumerable<InstallationAppointment> SearchGetBy(string? arg = null) => throw new NotSupportedException();
    public Task<IReadOnlyList<InstallationAppointment>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => GetScheduleForExecutorAsync(null, null, DateTime.MinValue, DateTime.MaxValue, cancellationToken);

    public async Task<InstallationAppointment?> GetByIdAsync(int appointmentId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder().Select(s => s.AllColumns<InstallationAppointment>()).From<InstallationAppointment>().Where(w => w.Equals<InstallationAppointment>(a => a.AppointmentID, appointmentId)).Build();
        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<InstallationAppointment>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<bool> ServiceOrderItemExistsAsync(int orderItemId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM order_items oi INNER JOIN products p ON p.ProductID = oi.ProductID WHERE oi.OrderItemID = @OrderItemID AND p.ProductType = @ProductType";
        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { OrderItemID = orderItemId, ProductType = ProductType.Service }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> HasActiveConflictAsync(int? executorEmployeeId, int? executorPartnerId, DateTime scheduledDate, int? excludingAppointmentId = null, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM installation_appointments WHERE ScheduledDate = @ScheduledDate AND Status IN (@Scheduled, @Rescheduled) AND ((@EmployeeID IS NOT NULL AND ExecutorEmployeeID = @EmployeeID) OR (@PartnerID IS NOT NULL AND ExecutorPartnerID = @PartnerID)) AND (@ExcludedID IS NULL OR AppointmentID <> @ExcludedID)";
        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { ScheduledDate = scheduledDate.Date, EmployeeID = executorEmployeeId, PartnerID = executorPartnerId, ExcludedID = excludingAppointmentId, Scheduled = InstallationAppointmentStatus.Scheduled, Rescheduled = InstallationAppointmentStatus.Rescheduled }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<IReadOnlyList<InstallationAppointment>> GetScheduleForExecutorAsync(int? executorEmployeeId, int? executorPartnerId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT ia.* FROM installation_appointments ia WHERE ia.ScheduledDate BETWEEN @FromDate AND @ToDate AND ((@EmployeeID IS NOT NULL AND ia.ExecutorEmployeeID = @EmployeeID) OR (@PartnerID IS NOT NULL AND ia.ExecutorPartnerID = @PartnerID)) ORDER BY ia.ScheduledDate ASC, ia.ScheduledTime ASC";
        using var connection = _dbConnection.CreateConnection();
        return (await connection.QueryAsync<InstallationAppointment>(new CommandDefinition(sql, new { FromDate = from.Date, ToDate = to.Date, EmployeeID = executorEmployeeId, PartnerID = executorPartnerId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<AppointmentReschedule>> GetReschedulesAsync(int appointmentId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder().Select(s => s.AllColumns<AppointmentReschedule>()).From<AppointmentReschedule>().Where(w => w.Equals<AppointmentReschedule>(r => r.AppointmentID, appointmentId)).OrderBy("ar.RegisteredAt ASC").Build();
        using var connection = _dbConnection.CreateConnection();
        return (await connection.QueryAsync<AppointmentReschedule>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<int> RescheduleAsync(InstallationAppointment appointment, AppointmentReschedule reschedule, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var update = _createCommandBuilder().Update(u => u.Entity(appointment)).Where(w => w.Equals<InstallationAppointment>(a => a.AppointmentID, appointment.AppointmentID)).Build();
            var insert = _createCommandBuilder().Insert(i => i.Entity(reschedule)).ReturningGeneratedId().Build();
            var updated = await connection.ExecuteAsync(new CommandDefinition(update.Sql, update.Parameters, transaction, cancellationToken: cancellationToken));
            if (updated != 1) throw new InvalidOperationException("O agendamento não foi encontrado ou não pôde ser atualizado.");
            reschedule.RescheduleID = await connection.QuerySingleAsync<int>(new CommandDefinition(insert.Sql, insert.Parameters, transaction, cancellationToken: cancellationToken));
            transaction.Commit();
            return 1;
        }
        catch { transaction.Rollback(); throw; }
    }
}