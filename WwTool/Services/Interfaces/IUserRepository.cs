using System.Collections.Generic;
using System.Threading.Tasks;
using WwTool.Common.Models;

namespace WwTool.Services.Repositories
{
    public interface IUserRepository
    {
        Task<UserAccount?> GetUserAccountAsync(string uid);
        Task<List<UserAccount>> GetAllUserAccountAsync();
        Task DeleteUserAccountAsync(string uid);
        Task SaveOauthCodeAsync(string uid, string oauthCode);
        Task<string?> GetOauthCodeAsync(string uid);
    }
}
