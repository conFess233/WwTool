using Microsoft.EntityFrameworkCore;

namespace WwTool.Common.Context;

public sealed class AppDbContextFactory(DatabaseOptions databaseOptions) : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options = CreateOptions(databaseOptions);

    public AppDbContext CreateDbContext() => new(_options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDbContext());
    }

    private static DbContextOptions<AppDbContext> CreateOptions(DatabaseOptions databaseOptions)
    {
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = databaseOptions.DatabasePath,
            ForeignKeys = true,
            DefaultTimeout = 5,
            Pooling = true
        }.ToString();

        return new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;
    }
}
