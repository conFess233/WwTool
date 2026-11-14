using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using WwTool.Web.Data;

namespace WwTool.Web.Auth;

/// <summary>
/// 站点账号：用户名 + 密码，不收集邮箱；与游戏账号完全无关。
/// <list type="bullet">
/// <item>密码只保存 PBKDF2 哈希；忘记密码时用注册时给出的一次性恢复码重设。</item>
/// <item>连续输错 <see cref="MaxFailures"/> 次锁定 <see cref="LockoutDuration"/>，另有按 IP 的限流。</item>
/// <item>会话为 HttpOnly + SameSite=Strict 的 Cookie，带安全戳：改密码、重设密码后其他设备上的会话立即失效。</item>
/// </list>
/// </summary>
public static class AuthEndpoints
{
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string StampClaim = "wwtool:stamp";
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public sealed record CredentialsRequest(string? UserName, string? Password);
    public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
    public sealed record RecoverRequest(string? UserName, string? RecoveryCode, string? NewPassword);
    public sealed record PasswordRequest(string? Password);

    public static Guid? UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out Guid id) ? id : null;

    public static void MapAuth(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth");

        // 注册：成功后直接登录，并返回只显示一次的恢复码
        auth.MapPost("/register", async (CredentialsRequest body, HttpContext http, WebDbContext db) =>
        {
            if (ValidateUserName(body.UserName) is { } nameError) return Problem(nameError);
            if (ValidatePassword(body.Password) is { } passwordError) return Problem(passwordError);
            string userName = body.UserName!.Trim();
            string normalized = Normalize(userName);
            if (await db.Users.AnyAsync(u => u.NormalizedUserName == normalized)) return Problem("这个用户名已被使用。", StatusCodes.Status409Conflict);

            string recoveryCode = PasswordHasher.NewRecoveryCode();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var user = new SiteUser
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                NormalizedUserName = normalized,
                DisplayName = userName,
                PasswordHash = PasswordHasher.Hash(body.Password!),
                RecoveryCodeHash = PasswordHasher.HashRecoveryCode(recoveryCode),
                SecurityStamp = NewStamp(),
                CreatedAt = now,
                LastSeenAt = now,
            };
            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // 并发注册同名：唯一索引兜底
                return Problem("这个用户名已被使用。", StatusCodes.Status409Conflict);
            }
            await SignInAsync(http, user);
            return Results.Ok(new { recoveryCode });
        });

        auth.MapPost("/login", async (CredentialsRequest body, HttpContext http, WebDbContext db) =>
        {
            string normalized = Normalize(body.UserName ?? string.Empty);
            string password = body.Password ?? string.Empty;
            SiteUser? user = normalized.Length == 0 ? null : await db.Users.FirstOrDefaultAsync(u => u.NormalizedUserName == normalized);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (user?.LockoutUntil is { } until && until > now)
                return Problem($"密码错误次数过多，请在 {Math.Ceiling((until - now).TotalMinutes)} 分钟后再试。", StatusCodes.Status429TooManyRequests);

            // 用户不存在时也做一次哈希计算，响应时间不泄露用户名是否存在
            if (!PasswordHasher.Verify(password, user?.PasswordHash) || user is null)
            {
                if (user is not null)
                {
                    user.FailedLogins++;
                    if (user.FailedLogins >= MaxFailures)
                    {
                        user.FailedLogins = 0;
                        user.LockoutUntil = now + LockoutDuration;
                    }
                    await db.SaveChangesAsync();
                }
                return Problem("用户名或密码不正确。", StatusCodes.Status401Unauthorized);
            }

            user.FailedLogins = 0;
            user.LockoutUntil = null;
            user.LastSeenAt = now;
            await db.SaveChangesAsync();
            await SignInAsync(http, user);
            return Results.Ok();
        });

        auth.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(Scheme);
            return Results.Ok();
        });

        // 用恢复码重设密码：成功后旧恢复码作废并发放新的，其他设备上的会话全部失效
        auth.MapPost("/recover", async (RecoverRequest body, HttpContext http, WebDbContext db) =>
        {
            if (ValidatePassword(body.NewPassword) is { } passwordError) return Problem(passwordError);
            string normalized = Normalize(body.UserName ?? string.Empty);
            SiteUser? user = normalized.Length == 0 ? null : await db.Users.FirstOrDefaultAsync(u => u.NormalizedUserName == normalized);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (user?.LockoutUntil is { } until && until > now)
                return Problem($"尝试次数过多，请在 {Math.Ceiling((until - now).TotalMinutes)} 分钟后再试。", StatusCodes.Status429TooManyRequests);
            if (user is null || !PasswordHasher.VerifyRecoveryCode(body.RecoveryCode ?? string.Empty, user.RecoveryCodeHash))
            {
                if (user is not null)
                {
                    user.FailedLogins++;
                    if (user.FailedLogins >= MaxFailures)
                    {
                        user.FailedLogins = 0;
                        user.LockoutUntil = now + LockoutDuration;
                    }
                    await db.SaveChangesAsync();
                }
                return Problem("用户名或恢复码不正确。", StatusCodes.Status401Unauthorized);
            }

            string recoveryCode = PasswordHasher.NewRecoveryCode();
            user.PasswordHash = PasswordHasher.Hash(body.NewPassword!);
            user.RecoveryCodeHash = PasswordHasher.HashRecoveryCode(recoveryCode);
            user.SecurityStamp = NewStamp();
            user.FailedLogins = 0;
            user.LockoutUntil = null;
            user.LastSeenAt = now;
            await db.SaveChangesAsync();
            await SignInAsync(http, user);
            return Results.Ok(new { recoveryCode });
        });

        // 修改密码：需要当前密码；其他设备上的会话失效，当前设备保持登录
        auth.MapPost("/password", async (ChangePasswordRequest body, HttpContext http, WebDbContext db) =>
        {
            if (await db.Users.FindAsync(http.User.UserId()!.Value) is not { } user) return Results.Unauthorized();
            if (!PasswordHasher.Verify(body.CurrentPassword ?? string.Empty, user.PasswordHash)) return Problem("当前密码不正确。");
            if (ValidatePassword(body.NewPassword) is { } passwordError) return Problem(passwordError);
            user.PasswordHash = PasswordHasher.Hash(body.NewPassword!);
            user.SecurityStamp = NewStamp();
            await db.SaveChangesAsync();
            await SignInAsync(http, user);
            return Results.Ok();
        }).RequireAuthorization();

        // 重新生成恢复码（旧的立即作废），需要当前密码
        auth.MapPost("/recovery-code", async (PasswordRequest body, HttpContext http, WebDbContext db) =>
        {
            if (await db.Users.FindAsync(http.User.UserId()!.Value) is not { } user) return Results.Unauthorized();
            if (!PasswordHasher.Verify(body.Password ?? string.Empty, user.PasswordHash)) return Problem("当前密码不正确。");
            string recoveryCode = PasswordHasher.NewRecoveryCode();
            user.RecoveryCodeHash = PasswordHasher.HashRecoveryCode(recoveryCode);
            await db.SaveChangesAsync();
            return Results.Ok(new { recoveryCode });
        }).RequireAuthorization();
    }

    /// <summary>每个请求核对一次安全戳：账号已删除或密码已在别处修改时，旧 Cookie 立即失效。</summary>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        Guid? userId = context.Principal?.UserId();
        string? stamp = context.Principal?.FindFirstValue(StampClaim);
        WebDbContext db = context.HttpContext.RequestServices.GetRequiredService<WebDbContext>();
        string? current = userId is { } id
            ? await db.Users.Where(u => u.Id == id).Select(u => u.SecurityStamp).FirstOrDefaultAsync()
            : null;
        if (current is null || current != stamp)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(Scheme);
        }
    }

    public static async Task SignInAsync(HttpContext http, SiteUser user)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(StampClaim, user.SecurityStamp)], Scheme);
        await http.SignInAsync(Scheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
    }

    public static string NewStamp() => Guid.NewGuid().ToString("N");

    public static string Normalize(string userName) => userName.Trim().ToUpperInvariant();

    /// <summary>3–32 个字符：字母（含中日文）、数字、下划线、连字符和点。</summary>
    private static string? ValidateUserName(string? userName)
    {
        string name = (userName ?? string.Empty).Trim();
        if (name.Length is < 3 or > 32) return "用户名长度需在 3–32 个字符之间。";
        if (!name.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.')) return "用户名只能包含文字、数字、下划线、连字符和点。";
        return null;
    }

    private static string? ValidatePassword(string? password)
    {
        if (password is null || password.Length < 8) return "密码至少需要 8 个字符。";
        if (password.Length > 128) return "密码不能超过 128 个字符。";
        return null;
    }

    public static IResult Problem(string detail, int status = StatusCodes.Status400BadRequest) =>
        Results.Problem(detail: detail, statusCode: status);
}
