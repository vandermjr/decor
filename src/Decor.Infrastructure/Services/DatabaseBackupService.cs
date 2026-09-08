using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Services;
using Dapper;

namespace Decor.Infrastructure.Services;

public sealed class DatabaseBackupService(IDatabaseConnection databaseConnection) : IDatabaseBackupService
{
    public async Task<DatabaseBackupResult> CreateBackupAsync(IReadOnlyCollection<string> backupFiles, CancellationToken cancellationToken = default)
    {
        if (backupFiles.Count == 0)
            throw new ArgumentException("Informe pelo menos um arquivo de destino para o backup.", nameof(backupFiles));

        var createdAt = DateTime.Now;
        var sql = await CreateSqlDumpAsync(cancellationToken);
        var createdFiles = new List<string>();

        foreach (var backupFile in backupFiles.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = Path.GetDirectoryName(backupFile);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException($"Destino inválido para backup: {backupFile}");

            Directory.CreateDirectory(directory);
            var sqlEntryName = Path.GetFileNameWithoutExtension(backupFile) + ".sql";

            await using var fileStream = File.Create(backupFile);
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(sqlEntryName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
                await writer.WriteAsync(sql.AsMemory(), cancellationToken);
            }

            createdFiles.Add(backupFile);
        }

        return new DatabaseBackupResult(createdAt, createdFiles);
    }

    private async Task<string> CreateSqlDumpAsync(CancellationToken cancellationToken)
    {
        using var connection = databaseConnection.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        var databaseName = await connection.QuerySingleAsync<string>(new CommandDefinition("SELECT DATABASE();", cancellationToken: cancellationToken));
        var schema = await connection.QuerySingleAsync<DatabaseSchemaInfo>(new CommandDefinition(
            """
            SELECT DEFAULT_CHARACTER_SET_NAME AS CharacterSetName,
                   DEFAULT_COLLATION_NAME AS CollationName
            FROM information_schema.SCHEMATA
            WHERE SCHEMA_NAME = DATABASE();
            """,
            cancellationToken: cancellationToken));
        var tables = (await connection.QueryAsync<string>(new CommandDefinition("SHOW FULL TABLES WHERE Table_type = 'BASE TABLE';", cancellationToken: cancellationToken))).ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("-- Decor database backup");
        builder.AppendLine($"-- Database: {QuoteIdentifier(databaseName)}");
        builder.AppendLine($"-- Created at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"CREATE DATABASE IF NOT EXISTS {QuoteIdentifier(databaseName)} DEFAULT CHARACTER SET {schema.CharacterSetName} COLLATE {schema.CollationName};");
        builder.AppendLine($"USE {QuoteIdentifier(databaseName)};");
        builder.AppendLine("SET FOREIGN_KEY_CHECKS=0;");
        builder.AppendLine();

        foreach (var table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AppendTableAsync(connection, builder, table, cancellationToken);
        }

        builder.AppendLine("SET FOREIGN_KEY_CHECKS=1;");
        return builder.ToString();
    }

    private static async Task AppendTableAsync(IDbConnection connection, StringBuilder builder, string table, CancellationToken cancellationToken)
    {
        var quotedTable = QuoteIdentifier(table);
        var createRows = await connection.QueryAsync(new CommandDefinition($"SHOW CREATE TABLE {quotedTable};", cancellationToken: cancellationToken));
        var createRow = (IDictionary<string, object>)createRows.Single();
        var createSql = createRow.Values.ElementAt(1)?.ToString() ?? throw new InvalidOperationException($"Não foi possível obter a estrutura da tabela {table}.");

        builder.AppendLine($"DROP TABLE IF EXISTS {quotedTable};");
        builder.AppendLine(createSql + ";");
        builder.AppendLine();

        var rows = (await connection.QueryAsync(new CommandDefinition($"SELECT * FROM {quotedTable};", cancellationToken: cancellationToken))).ToArray();
        if (rows.Length == 0)
        {
            builder.AppendLine();
            return;
        }

        var firstRow = (IDictionary<string, object>)rows[0];
        var columns = firstRow.Keys.Select(QuoteIdentifier).ToArray();
        builder.AppendLine($"INSERT INTO {quotedTable} ({string.Join(", ", columns)}) VALUES");

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = (IDictionary<string, object>)rows[rowIndex];
            var values = row.Values.Select(ToSqlLiteral);
            var suffix = rowIndex == rows.Length - 1 ? ";" : ",";
            builder.AppendLine($"({string.Join(", ", values)}){suffix}");
        }

        builder.AppendLine();
    }

    private static string QuoteIdentifier(string identifier) =>
        "`" + identifier.Replace("`", "``", StringComparison.Ordinal) + "`";

    private sealed record DatabaseSchemaInfo(string CharacterSetName, string CollationName);

    private static string ToSqlLiteral(object? value)
    {
        if (value is null or DBNull)
            return "NULL";

        return value switch
        {
            string text => QuoteString(text),
            char character => QuoteString(character.ToString()),
            bool boolean => boolean ? "1" : "0",
            DateTime dateTime => QuoteString(dateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture)),
            DateTimeOffset dateTimeOffset => QuoteString(dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture)),
            TimeSpan timeSpan => QuoteString(timeSpan.ToString(@"hh\:mm\:ss\.ffffff", CultureInfo.InvariantCulture)),
            byte[] bytes => "0x" + Convert.ToHexString(bytes),
            sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "NULL",
            _ => QuoteString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
        };
    }

    private static string QuoteString(string value) =>
        "'" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "''", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "'";
}
