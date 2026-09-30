using WwTool.Common.Models.Domain;
using WwTool.Common.Models.Entities;

namespace WwTool.Services.Repositories;

internal static class GuideSnapshotMapper
{
    internal static GuideWeaponData ToData(GuideEquippedWeaponSnapshot value) => new()
    {
        Uid = value.Uid,
        OwnerRoleGbId = value.OwnerRoleGbId,
        WeaponGbId = value.WeaponGbId,
        PictureUrl = value.PictureUrl,
        Star = value.Star,
        SourceOrder = value.SourceOrder,
    };

    internal static GuideEquippedWeaponSnapshot ToEntity(GuideWeaponData value) => new()
    {
        Uid = value.Uid,
        OwnerRoleGbId = value.OwnerRoleGbId,
        WeaponGbId = value.WeaponGbId,
        PictureUrl = value.PictureUrl,
        Star = value.Star,
        SourceOrder = value.SourceOrder,
    };

    internal static GuideRoleData ToData(GuideRoleSnapshot value) => new()
    {
        Uid = value.Uid,
        RoleGbId = value.RoleGbId,
        SourceOrder = value.SourceOrder,
        CardPictureUrl = value.CardPictureUrl,
        IllustrationPictureUrl = value.IllustrationPictureUrl,
        Star = value.Star,
        RoleStatus = value.RoleStatus,
        Sequence = value.Sequence,
        IsAcquired = value.IsAcquired,
        MayRoleGbId = value.MayRoleGbId,
        StrategyId = value.StrategyId,
        StrategyModifiedAt = value.StrategyModifiedAt,
        DetailJson = value.DetailJson,
    };

    internal static GuideRoleSnapshot ToEntity(GuideRoleData value) => new()
    {
        Uid = value.Uid,
        RoleGbId = value.RoleGbId,
        SourceOrder = value.SourceOrder,
        CardPictureUrl = value.CardPictureUrl,
        IllustrationPictureUrl = value.IllustrationPictureUrl,
        Star = value.Star,
        RoleStatus = value.RoleStatus,
        Sequence = value.Sequence,
        IsAcquired = value.IsAcquired,
        MayRoleGbId = value.MayRoleGbId,
        StrategyId = value.StrategyId,
        StrategyModifiedAt = value.StrategyModifiedAt,
        DetailJson = value.DetailJson,
    };

}
