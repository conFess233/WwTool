using WwTool.Core.Data;

namespace WwTool.Core.Abstractions;

public interface IDatabaseWriteCoordinator
{
    Task ExecuteAsync(
        Func<AppDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteAsync<TResult>(
        Func<AppDbContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
