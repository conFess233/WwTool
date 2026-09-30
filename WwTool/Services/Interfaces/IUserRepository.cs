using WwTool.Common.Models;
using WwTool.Common.Models.Domain;

namespace WwTool.Services.Repositories;

public interface IUserRepository
{
    Task<AccountSummary?> GetUserAccountAsync(string uid, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountSummary>> GetAllUserAccountAsync(CancellationToken cancellationToken = default);
    Task DeleteUserAccountAsync(string uid, CancellationToken cancellationToken = default);
    Task SaveOauthCodeAsync(string uid, string oauthCode, CancellationToken cancellationToken = default);
    Task<string?> GetOauthCodeAsync(string uid, CancellationToken cancellationToken = default);
}
