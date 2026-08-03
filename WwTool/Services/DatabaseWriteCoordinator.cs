using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WwTool.Common.Context;
using WwTool.Services.Interfaces;

namespace WwTool.Services;

public sealed class DatabaseWriteCoordinator(
    IDbContextFactory<AppDbContext> contextFactory,
    ILoggerService logger) : IDatabaseWriteCoordinator
{
    private const int MaxAttempts = 3;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public Task ExecuteAsync(
        Func<AppDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async (db, token) =>
        {
            await operation(db, token);
            return true;
        }, cancellationToken);

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<AppDbContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            for (int attempt = 1; ; attempt++)
            {
                await using AppDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    TResult result = await operation(db, cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch (SqliteException ex) when (IsBusy(ex) && attempt < MaxAttempts)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    int delayMs = 80 * attempt;
                    logger.Debug($"SQLite 写入繁忙，将在 {delayMs}ms 后进行第 {attempt + 1} 次尝试。");
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private static bool IsBusy(SqliteException exception) =>
        exception.SqliteErrorCode is 5 or 6;
}
