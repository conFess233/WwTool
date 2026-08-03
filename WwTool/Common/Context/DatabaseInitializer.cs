using System.Data.Common;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace WwTool.Common.Context
{
    public static class DatabaseInitializer
    {
        internal const string InitialMigrationId = "20260622113232_InitialCreate";
        private const string EfProductVersion = "10.0.9";

        private static readonly string[] ExpectedLegacyTables =
        [
            "GachaRecords",
            "PlayerBaseInfos",
            "PlayerBattlePasses",
            "PlayerMotorData",
            "PlayerMusicData",
            "UserAccounts"
        ];

        public static async Task InitializeAsync(
            AppDbContext db,
            CancellationToken cancellationToken = default)
        {
            await BaselineLegacyDatabaseAsync(db, cancellationToken);
            await CreateMigrationBackupIfNeededAsync(db, cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", cancellationToken);
        }

        private static async Task CreateMigrationBackupIfNeededAsync(
            AppDbContext db,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<string> pendingMigrations = (await db.Database
                .GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pendingMigrations.Count == 0 || db.Database.GetDbConnection() is not SqliteConnection source)
            {
                return;
            }

            string databasePath = source.DataSource;
            if (!File.Exists(databasePath) || new FileInfo(databasePath).Length == 0)
            {
                return;
            }

            string localDirectory = Directory.GetParent(Path.GetDirectoryName(databasePath)!)?.FullName
                ?? Path.GetDirectoryName(databasePath)!;
            string backupDirectory = Path.Combine(localDirectory, "Backups");
            Directory.CreateDirectory(backupDirectory);
            string backupPath = Path.Combine(
                backupDirectory,
                $"LocalData-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{pendingMigrations[^1]}.db");

            bool shouldClose = source.State != System.Data.ConnectionState.Open;
            if (shouldClose)
            {
                await source.OpenAsync(cancellationToken);
            }

            try
            {
                await using var destination = new SqliteConnection($"Data Source={backupPath}");
                await destination.OpenAsync(cancellationToken);
                source.BackupDatabase(destination);
            }
            finally
            {
                if (shouldClose)
                {
                    await source.CloseAsync();
                }
            }

            foreach (FileInfo oldBackup in new DirectoryInfo(backupDirectory)
                .EnumerateFiles("LocalData-*.db")
                .OrderByDescending(x => x.CreationTimeUtc)
                .Skip(3))
            {
                oldBackup.Delete();
            }
        }

        private static async Task BaselineLegacyDatabaseAsync(
            AppDbContext db,
            CancellationToken cancellationToken)
        {
            DbConnection connection = db.Database.GetDbConnection();
            bool shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                HashSet<string> tables = await ReadTableNamesAsync(connection, cancellationToken);
                if (tables.Contains("__EFMigrationsHistory"))
                {
                    return;
                }

                string[] existingAppTables = ExpectedLegacyTables.Where(tables.Contains).ToArray();
                if (existingAppTables.Length == 0)
                {
                    return;
                }

                string[] missingTables = ExpectedLegacyTables
                    .Except(tables, StringComparer.Ordinal)
                    .ToArray();
                if (missingTables.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Legacy database schema is incomplete. Missing tables: {string.Join(", ", missingTables)}.");
                }

                await using DbTransaction transaction =
                    await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    await ExecuteAsync(
                        connection,
                        transaction,
                        """
                        CREATE TABLE "__EFMigrationsHistory" (
                            "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                            "ProductVersion" TEXT NOT NULL
                        );
                        """,
                        cancellationToken);

                    await using DbCommand insert = connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText =
                        """
                        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                        VALUES (@migrationId, @productVersion);
                        """;
                    AddParameter(insert, "@migrationId", InitialMigrationId);
                    AddParameter(insert, "@productVersion", EfProductVersion);
                    await insert.ExecuteNonQueryAsync(cancellationToken);

                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private static async Task<HashSet<string>> ReadTableNamesAsync(
            DbConnection connection,
            CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";

            var tables = new HashSet<string>(StringComparer.Ordinal);
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }

        private static async Task ExecuteAsync(
            DbConnection connection,
            DbTransaction transaction,
            string sql,
            CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static void AddParameter(DbCommand command, string name, string value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
    }
}
