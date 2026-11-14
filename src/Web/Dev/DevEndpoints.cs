using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using WwTool.Core.Abstractions;
using WwTool.Core.Data.Repositories;
using WwTool.Core.Enums;
using WwTool.Core.Models.Domain;
using WwTool.Web.Auth;
using WwTool.Web.Data;

namespace WwTool.Web.Dev;

/// <summary>
/// 本地预览用的开发接口：免密码登录一个开发账号，并写入一个虚构 UID 的样例抽卡记录与账号资料。
/// 只有同时满足以下条件才会注册：Development 环境、配置 <c>WwTool:EnableDevTools=true</c>；
/// 每个请求还必须来自本机回环地址。正式部署不会出现这些路由。
/// </summary>
public static class DevEndpoints
{
    public const string SampleUid = "100000001";

    public static void MapDevTools(this IEndpointRouteBuilder api, IWebHostEnvironment env, IConfiguration config)
    {
        if (!env.IsDevelopment() || !config.GetValue<bool>("WwTool:EnableDevTools")) return;

        var dev = api.MapGroup("/dev").AddEndpointFilter(async (ctx, next) =>
            ctx.HttpContext.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip) ? await next(ctx) : Results.NotFound());

        dev.MapPost("/session", async (HttpContext http, WebDbContext db) =>
        {
            SiteUser? user = await db.Users.FirstOrDefaultAsync(u => u.DisplayName == "开发预览");
            if (user is null)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                // 密码哈希为空：这个账号不能用密码登录，只能经由本接口
                user = new SiteUser
                {
                    Id = Guid.NewGuid(), UserName = "dev-preview", NormalizedUserName = "DEV-PREVIEW", DisplayName = "开发预览",
                    SecurityStamp = AuthEndpoints.NewStamp(), CreatedAt = now, LastSeenAt = now,
                };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }
            await AuthEndpoints.SignInAsync(http, user);
            return Results.Ok();
        });

        dev.MapPost("/seed", async (HttpContext http, WebDbContext db, IGachaRepository gacha, IPlayerInfoRepository players, IGuideRepository guide, CancellationToken token) =>
        {
            if (http.User.UserId() is not { } userId) return Results.Unauthorized();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (await db.UidBindings.FindAsync([SampleUid], token) is not { } binding)
                db.UidBindings.Add(new UidBinding { Uid = SampleUid, UserId = userId, Region = "global", BoundAt = now, LastVerifiedAt = now });
            else binding.UserId = userId;
            foreach (string kind in new[] { "gacha", "profile" })
            {
                if (await db.DataSources.FindAsync([SampleUid, kind], token) is { } row) row.UpdatedAt = now;
                else db.DataSources.Add(new UidDataSource { Uid = SampleUid, Kind = kind, Source = "dev", UpdatedAt = now });
            }
            await db.SaveChangesAsync(token);

            await gacha.DeleteByUidAsync(SampleUid, token);
            foreach (var (pool, records) in SampleGacha())
                await gacha.SyncGachaDataAsync(SampleUid, (int)pool, records, "dev", token);

            var region = new PlayerRegionSummary { RoleId = SampleUid, RoleName = "漂泊者", Level = 80, Sex = 0, HeadPhoto = 1102 };
            await players.SavePlayerRegionInfoAsync(region, "Asia", string.Empty, token);
            await players.SavePlayerRoleDataAsync(SampleUid, SampleProfile(), "Asia", region, token);

            // 样例角色快照（结构与攻略站 /introduction/info 相同的最小子集）
            await guide.SavePlayersAsync("dev", [new GuidePlayerIdentity(long.Parse(SampleUid, CultureInfo.InvariantCulture), "dev-server")], token);
            var (roles, weapons) = SampleRoles();
            await guide.ReplaceSnapshotAsync(SampleUid, roles, weapons, DateTimeOffset.UtcNow, token);
            return Results.Ok(new { uid = SampleUid });
        });
    }

    /// <summary>虚构的抽卡记录：响应顺序为从新到旧（与官方接口一致），由仓储按规则反转编号。</summary>
    private static IEnumerable<(CardPoolType, List<GachaPull>)> SampleGacha()
    {
        var rng = new Random(42);
        (int Id, string Name)[] limited = [(1506, "菲比"), (1507, "赞妮"), (1409, "卡提希娅"), (1208, "嘉贝莉娜")];
        (int Id, string Name)[] standard = [(1405, "鉴心"), (1104, "凌阳"), (1203, "安可"), (1503, "维里奈"), (1301, "卡卡罗")];
        (int Id, string Name)[] four = [(1102, "散华"), (1204, "莫特斐"), (1402, "秧秧"), (1601, "桃祈"), (1303, "渊武")];
        foreach (CardPoolType pool in new[] { CardPoolType.CharacterEvent, CardPoolType.WeaponEvent, CardPoolType.CharacterStandard })
        {
            var list = new List<GachaPull>();
            DateTime time = DateTime.Today.AddDays(-160);
            int since = 0;
            bool guaranteed = false;
            int total = pool == CardPoolType.CharacterEvent ? 420 : pool == CardPoolType.WeaponEvent ? 160 : 90;
            for (int i = 0; i < total; i++)
            {
                since++;
                if (i % 10 == 0) time = time.AddHours(rng.Next(6, 40));
                bool five = since >= 80 || (since > 60 && rng.NextDouble() < 0.12) || rng.NextDouble() < 0.008;
                GachaPull pull;
                if (five)
                {
                    since = 0;
                    bool win = pool != CardPoolType.CharacterEvent || guaranteed || rng.NextDouble() < 0.5;
                    var item = pool == CardPoolType.WeaponEvent ? (21010036, "裁春") : win && pool == CardPoolType.CharacterEvent ? limited[rng.Next(limited.Length)] : standard[rng.Next(standard.Length)];
                    guaranteed = pool == CardPoolType.CharacterEvent && !win;
                    pull = Pull(pool, item.Item1, item.Item2, pool == CardPoolType.WeaponEvent ? "武器" : "角色", 5, time);
                }
                else if (i % 10 == 9 || rng.NextDouble() < 0.06)
                {
                    var item = four[rng.Next(four.Length)];
                    pull = Pull(pool, item.Id, item.Name, "角色", 4, time);
                }
                else
                {
                    pull = Pull(pool, 21010013, "暗夜长刃·玄明", "武器", 3, time);
                }
                list.Add(pull);
            }
            list.Reverse();
            yield return (pool, list);
        }
    }

    private static GachaPull Pull(CardPoolType pool, int id, string name, string type, int quality, DateTime time) => new()
    {
        CardPoolType = ((int)pool).ToString(CultureInfo.InvariantCulture), ResourceId = id, Name = name, ResourceType = type,
        QualityLevel = quality, Count = 1, Time = time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
    };

    private static (List<GuideRoleData>, List<GuideWeaponData>) SampleRoles()
    {
        (string Id, int Star, int Seq, string? Weapon)[] list =
        [
            ("1506", 5, 2, "21050036"), ("1507", 5, 0, null), ("1405", 5, 1, null), ("1104", 5, 0, null),
            ("1102", 4, 6, "21010013"), ("1204", 4, 3, null), ("1402", 4, 5, null), ("1601", 4, 0, null),
        ];
        var roles = new List<GuideRoleData>();
        var weapons = new List<GuideWeaponData>();
        for (int i = 0; i < list.Length; i++)
        {
            var (id, star, seq, weapon) = list[i];
            string chain = string.Join(",", Enumerable.Range(1, 6).Select(n => $"{{\"resonanceSequence\":{n},\"isAcquired\":{(n <= seq ? "true" : "false")}}}"));
            string weaponJson = weapon is null ? "null" : $"{{\"gbId\":\"{weapon}\",\"star\":{star},\"currentLevel\":\"90\",\"currentResonanceRank\":\"1\"}}";
            string detail = $$$$"""
                {"roleAttribute":{"items":[
                  {"gbId":"2-1","currentAmount":"16,850"},{"gbId":"7-1","currentAmount":"2,140"},{"gbId":"10-1","currentAmount":"1,190"},
                  {"gbId":"8-2","currentAmount":"68.5%"},{"gbId":"9-2","currentAmount":"236.4%"},{"gbId":"11-2","currentAmount":"124.0%"}]},
                 "weapon":{"current":{{{{weaponJson}}}}},
                 "roleResonance":{"items":[{{{{chain}}}}]},
                 "roleSkill":{"addPointTarget":[
                  {"currentLevel":10,"texts":[{"language":"zh-Hans","name":"常态攻击"}]},{"currentLevel":10,"texts":[{"language":"zh-Hans","name":"共鸣技能"}]},
                  {"currentLevel":9,"texts":[{"language":"zh-Hans","name":"共鸣解放"}]},{"currentLevel":10,"texts":[{"language":"zh-Hans","name":"变奏技能"}]},
                  {"currentLevel":8,"texts":[{"language":"zh-Hans","name":"共鸣回路"}]}]},
                 "echo":{"current":{"echoProps":{"texts":[{"language":"zh-Hans","name":"无归的谬误"}]},
                  "echoSetEffects":[{"texts":[{"language":"zh-Hans","name":"流云逝尽之空"}]}],
                  "echoAttributes":[
                   {"cost":4,"currentLevel":25,"attribute":{"texts":[{"language":"zh-Hans","name":"暴击"}]},"attribute2":{"texts":[{"language":"zh-Hans","name":"攻击"}]}},
                   {"cost":3,"currentLevel":25,"attribute":{"texts":[{"language":"zh-Hans","name":"气动伤害加成"}]}},
                   {"cost":1,"currentLevel":25,"attribute":{"texts":[{"language":"zh-Hans","name":"攻击"}]}}]}}}
                """;
            roles.Add(new GuideRoleData { Uid = SampleUid, RoleGbId = id, SourceOrder = i, Star = star, Sequence = seq, IsAcquired = true, DetailJson = detail });
            if (weapon is not null)
                weapons.Add(new GuideWeaponData { Uid = SampleUid, OwnerRoleGbId = id, WeaponGbId = weapon, Star = star, SourceOrder = i });
        }
        return (roles, weapons);
    }

    private static PlayerSnapshot SampleProfile()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new PlayerSnapshot
        {
            Base = new PlayerBaseSnapshot
            {
                Name = "漂泊者", Id = long.Parse(SampleUid, CultureInfo.InvariantCulture), CreatTime = now - 86400 * 500, ActiveDays = 412, Level = 80, WorldLevel = 8,
                RoleNum = 46, SoundBox = 1180, Energy = 186, MaxEnergy = 240, StoreEnergy = 312, MaxStoreEnergy = 480, EnergyRecoverTime = now + 54 * 6 * 60,
                Liveness = 80, LivenessMaxCount = 100, LivenessUnlock = true, WeeklyInstCount = 2,
                Boxes = new() { ["1"] = 1210, ["2"] = 640, ["3"] = 288, ["4"] = 96 }, BasicBoxes = new() { ["1"] = 320 },
                PhantomBoxes = new() { ["1"] = 52, ["2"] = 31, ["3"] = 14 },
            },
            MotorData = new MotorSnapshot
            {
                Level = 12, Exp = 340, NextExp = 600,
                Skins = [new MotorSkinSnapshot { SkinId = 0, Quality = 3 }],
                Frames = [new MotorFrameSnapshot { Id = 89400001, Quality = 0 }, new MotorFrameSnapshot { Id = 89400002, Quality = 5 }],
                Stickers = [], Decorations = [],
            },
            MusicData = [new MusicSnapshot { Id = 1, Count = 3, TotalCount = 6 }, new MusicSnapshot { Id = 2, Count = 6, TotalCount = 6 }],
            BattlePass = new BattlePassSnapshot { Level = 47, WeekExp = 6400, WeekMaxExp = 10000, IsUnlock = true, IsOpen = true, Exp = 600, ExpLimit = 1000 },
        };
    }
}
