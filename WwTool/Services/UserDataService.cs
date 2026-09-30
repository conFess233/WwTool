using WwTool.Common.Models;
using WwTool.Common.Models.Entities;
using WwTool.Common.Models.Domain;
using WwTool.Common.Models.ApiResponse;
using WwTool.Services.Interfaces;
using WwTool.Services.Repositories;

namespace WwTool.Services;

public sealed class UserDataService(
    IUserRepository userRepository,
    IPlayerInfoRepository playerInfoRepository,
    IGachaRepository gachaRepository,
    IConfigService configService, ILoginService loginService) : IUserDataService
{
    public event EventHandler<AccountDeletedEventArgs>? AccountDeleted;

    public async Task<IReadOnlyList<AccountSummary>> ListAccountsAsync(CancellationToken cancellationToken = default) =>
        await userRepository.GetAllUserAccountAsync(cancellationToken);

    public Task<AcquisitionTimes> ReadAcquisitionTimesAsync(string uid, int[] pools, CancellationToken cancellationToken = default) =>
        gachaRepository.ReadAcquisitionTimesAsync(uid, pools, cancellationToken);

    public Task<string?> GetCredentialAsync(string uid, CancellationToken cancellationToken = default) =>
        userRepository.GetOauthCodeAsync(uid, cancellationToken);

    public async Task DeleteAccountAsync(string uid, CancellationToken cancellationToken = default)
    {
        await userRepository.DeleteUserAccountAsync(uid, cancellationToken);
        loginService.RemoveUserContext(uid);
        AccountDeleted?.Invoke(this, new AccountDeletedEventArgs(uid));
        if (configService.User.LastUserId == uid)
        {
            configService.User.LastUserId = string.Empty;
            await configService.SaveAllAsync();
        }
    }

    public Task<PlayerSnapshot?> LoadRoleSnapshotAsync(string uid, CancellationToken cancellationToken = default) =>
        playerInfoRepository.LoadPlayerRoleDataAsync(uid, cancellationToken);

    public Task<IReadOnlyList<GachaPull>> ReadGachaInSourceOrderAsync(string uid, int poolType, CancellationToken cancellationToken = default) =>
        gachaRepository.GetPoolRecordsByUidAsync(uid, poolType, cancellationToken);

    public Task<int> ImportGachaAsync(string uid, int poolType, IEnumerable<GachaPull> records, string source, CancellationToken cancellationToken = default) =>
        gachaRepository.SyncGachaDataAsync(uid, poolType, records, source, cancellationToken);

}
