using WwTool.Common.Models;
using WwTool.Common.Models.Entities;

namespace WwTool.Services.Repositories;

public interface IUserRepository
{
    Task<UserAccount?> GetUserAccountAsync(string uid, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserAccount>> GetAllUserAccountAsync(CancellationToken cancellationToken = default);
    Task DeleteUserAccountAsync(string uid, CancellationToken cancellationToken = default);
    Task SaveOauthCodeAsync(string uid, string oauthCode, CancellationToken cancellationToken = default);
    Task<string?> GetOauthCodeAsync(string uid, CancellationToken cancellationToken = default);
}
