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
    IConfigService configService) : IUserDataService
{
    public async Task<IReadOnlyList<AccountSummary>> ListAccountsAsync(CancellationToken cancellationToken = default) =>
        (await userRepository.GetAllUserAccountAsync(cancellationToken)).Select(MapAccount).ToList();

    public Task<string?> GetCredentialAsync(string uid, CancellationToken cancellationToken = default) =>
        userRepository.GetOauthCodeAsync(uid, cancellationToken);

    public async Task DeleteAccountAsync(string uid, CancellationToken cancellationToken = default)
    {
        await userRepository.DeleteUserAccountAsync(uid, cancellationToken);
        if (configService.User.LastUserId == uid)
        {
            configService.User.LastUserId = string.Empty;
            await configService.SaveAllAsync();
        }
    }

    public Task<RoleDetailInfo?> LoadRoleSnapshotAsync(string uid, CancellationToken cancellationToken = default) =>
        playerInfoRepository.LoadPlayerRoleDataAsync(uid, cancellationToken);

    public Task<IReadOnlyList<GachaData>> ReadGachaInSourceOrderAsync(string uid, int poolType, CancellationToken cancellationToken = default) =>
        gachaRepository.GetPoolRecordsByUidAsync(uid, poolType, cancellationToken);

    public Task<int> ImportGachaAsync(string uid, int poolType, IEnumerable<GachaData> records, string source, CancellationToken cancellationToken = default) =>
        gachaRepository.SyncGachaDataAsync(uid, poolType, records, source, cancellationToken);

    private static AccountSummary MapAccount(UserAccount account) => new()
    {
        Uid = account.Uid,
        Region = account.Region,
        Name = account.Name,
        Level = account.Level,
        Sex = account.Sex,
        HeadPhoto = account.HeadPhoto,
        LastSyncedAtUtc = account.LastSyncedAtUtc
    };
}
