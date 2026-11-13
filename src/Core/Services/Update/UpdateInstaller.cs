using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;

namespace WwTool.Core.Services.Update;

public enum UpdateFailure
{
    /// <summary>不知道自己是哪种发布包（调试版或自行编译），或发布里没有对应的压缩包。</summary>
    NoMatchingPackage,

    /// <summary>程序目录不可写（如装在 Program Files 下）。</summary>
    NotWritable,

    /// <summary>发布没有提供校验值，不敢安装。</summary>
    ChecksumMissing,

    /// <summary>下载的文件与校验值不符。</summary>
    ChecksumMismatch,

    /// <summary>压缩包内容不符合发布包的结构。</summary>
    InvalidPackage,

    /// <summary>下载失败（网络）。</summary>
    Download,

    /// <summary>替换文件失败（已还原）。</summary>
    Apply,
}

public sealed class UpdateInstallException(UpdateFailure failure, string message, Exception? inner = null) : Exception(message, inner)
{
    public UpdateFailure Failure { get; } = failure;
}

/// <summary>
/// 程序内更新：下载与当前同类型的发布包（单文件 / 多文件 × 带不带运行时，是否 Lite），按 GitHub 提供的 SHA256 校验，
/// 解压到暂存目录，再逐个替换程序目录里的同名文件——旧文件先改名为 <c>.old</c>（Windows 允许重命名正在运行的 exe / dll），
/// 任何一步失败都还原。改名留下的 <c>.old</c> 记在清单里，下次启动时删除。
/// 只动压缩包里有的文件；设置、数据库、缓存、日志一律不碰。
/// </summary>
public sealed class UpdateInstaller(IHttpClientFactory factory, IAppPaths paths, ILoggerService logger)
{
    /// <summary>发布包类型（与 publish.ps1 的包名一致）。</summary>
    public static readonly IReadOnlyList<string> KnownPackages = ["single", "single-runtime", "multi", "multi-runtime", "single-lite", "multi-lite"];

    private const string RootFolder = "WwTool/";
    private const long MaxPackageBytes = 400L * 1024 * 1024;
    private const string CleanupList = "cleanup.txt";

    /// <summary>下载的读超时：这么久没有收到数据就放弃。</summary>
    public TimeSpan StallTimeout { get; init; } = TimeSpan.FromSeconds(60);

    public string WorkDirectory => Path.Combine(paths.CacheDirectory, "Update");

    /// <summary>
    /// 当前程序的发布包名：编译时写入的类型（single / single-runtime / multi / multi-runtime），
    /// 不带运行时的包在程序目录没有 <c>Local/Icons</c> 时为 Lite。未知时返回 null。
    /// </summary>
    public string? ResolvePackage(string? buildPackage)
    {
        if (string.IsNullOrWhiteSpace(buildPackage) || !KnownPackages.Contains(buildPackage)) return null;
        if (buildPackage is "single" or "multi" && !Directory.Exists(Path.Combine(paths.AppDirectory, "Local", "Icons")))
            return buildPackage + "-lite";
        return buildPackage;
    }

    public static string AssetName(AppVersion version, string package) => $"WwTool-{version.Number}-win-x64-{package}.zip";

    public static ReleaseAsset? FindAsset(ReleaseInfo release, string? package) =>
        package is null ? null : release.Assets.FirstOrDefault(a => string.Equals(a.Name, AssetName(release.Version, package), StringComparison.OrdinalIgnoreCase));

    /// <summary>程序目录能否写入（试着建一个临时文件）。</summary>
    public bool CanWriteAppDirectory()
    {
        string probe = Path.Combine(paths.AppDirectory, ".wwtool-write-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>下载并校验；返回本地压缩包路径。<paramref name="progress"/> 报告 0–1。</summary>
    public async Task<string> DownloadAsync(ReleaseAsset asset, IProgress<double>? progress, CancellationToken token)
    {
        if (asset.Sha256 is not { Length: 64 } expected)
            throw new UpdateInstallException(UpdateFailure.ChecksumMissing, $"{asset.Name} 没有校验值");
        if (asset.Size > MaxPackageBytes)
            throw new UpdateInstallException(UpdateFailure.InvalidPackage, $"{asset.Name} 过大（{asset.Size} 字节）");

        Directory.CreateDirectory(WorkDirectory);
        string target = Path.Combine(WorkDirectory, Path.GetFileName(asset.Name));
        if (File.Exists(target) && await HashFileAsync(target, token).ConfigureAwait(false) == expected)
        {
            progress?.Report(1);
            return target;
        }

        string part = target + ".part";
        try
        {
            HttpClient client = factory.CreateClient(UpdateService.ClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
            using var connect = CancellationTokenSource.CreateLinkedTokenSource(token);
            connect.CancelAfter(StallTimeout);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, connect.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? asset.Size;
            if (total > MaxPackageBytes) throw new UpdateInstallException(UpdateFailure.InvalidPackage, "压缩包过大");

            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var file = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long received = 0;
                while (true)
                {
                    using var stall = CancellationTokenSource.CreateLinkedTokenSource(token);
                    stall.CancelAfter(StallTimeout);
                    int read = await source.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
                    if (read == 0) break;
                    received += read;
                    if (received > MaxPackageBytes) throw new UpdateInstallException(UpdateFailure.InvalidPackage, "压缩包过大");
                    sha.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    if (total > 0) progress?.Report(Math.Min(1, (double)received / total));
                }
            }
            string actual = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
            if (actual != expected)
                throw new UpdateInstallException(UpdateFailure.ChecksumMismatch, $"{asset.Name} 校验不符：{actual}");
            File.Move(part, target, overwrite: true);
            progress?.Report(1);
            return target;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException || ex is OperationCanceledException && !token.IsCancellationRequested)
        {
            throw new UpdateInstallException(UpdateFailure.Download, $"下载失败：{ex.GetType().Name} {ex.Message}", ex);
        }
        finally
        {
            TryDelete(part);
        }
    }

    /// <summary>解压到暂存目录（去掉压缩包里的 WwTool/ 外层），检查结构；返回暂存目录。</summary>
    public string Extract(string zipPath, AppVersion version)
    {
        string staging = Path.Combine(WorkDirectory, "staging-" + version.Number);
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);
        string root = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string name = entry.FullName.Replace('\\', '/');
                if (!name.StartsWith(RootFolder, StringComparison.Ordinal))
                    throw new UpdateInstallException(UpdateFailure.InvalidPackage, $"压缩包条目不在 WwTool/ 下：{name}");
                string relative = name[RootFolder.Length..];
                if (relative.Length == 0 || name.EndsWith('/')) continue;
                if (IsUserData(relative))
                    throw new UpdateInstallException(UpdateFailure.InvalidPackage, $"压缩包包含用户数据：{relative}");
                string destination = Path.GetFullPath(Path.Combine(staging, relative));
                if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new UpdateInstallException(UpdateFailure.InvalidPackage, $"压缩包条目路径非法：{name}");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, overwrite: true);
            }
        }
        catch (InvalidDataException ex)
        {
            throw new UpdateInstallException(UpdateFailure.InvalidPackage, "压缩包损坏", ex);
        }
        if (!File.Exists(Path.Combine(staging, "WwTool.exe")))
            throw new UpdateInstallException(UpdateFailure.InvalidPackage, "压缩包里没有 WwTool.exe");
        return staging;
    }

    /// <summary>
    /// 用暂存目录里的文件替换程序目录中的同名文件；失败时还原并抛出 <see cref="UpdateFailure.Apply"/>。
    /// </summary>
    public void Apply(string staging)
    {
        string appRoot = Path.GetFullPath(paths.AppDirectory);
        var replaced = new List<(string Target, string? Backup)>();
        try
        {
            foreach (string source in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(staging, source);
                string target = Path.Combine(appRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                string? backup = null;
                if (File.Exists(target))
                {
                    backup = target + ".old";
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(target, backup);
                }
                replaced.Add((target, backup));
                File.Copy(source, target, overwrite: false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Error("更新替换文件失败，正在还原", ex);
            Rollback(replaced);
            throw new UpdateInstallException(UpdateFailure.Apply, $"替换文件失败：{ex.Message}", ex);
        }

        // 记下 .old，下次启动删除（正在运行的旧 exe / dll 现在删不掉）。
        try
        {
            Directory.CreateDirectory(WorkDirectory);
            File.AppendAllLines(Path.Combine(WorkDirectory, CleanupList), replaced.Where(r => r.Backup is not null).Select(r => r.Backup!));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Warn($"更新清理清单写入失败：{ex.Message}");
        }
        logger.Info($"更新文件已替换：{replaced.Count} 个");
    }

    private void Rollback(List<(string Target, string? Backup)> replaced)
    {
        for (int i = replaced.Count - 1; i >= 0; i--)
        {
            (string target, string? backup) = replaced[i];
            try
            {
                if (File.Exists(target)) File.Delete(target);
                if (backup is not null && File.Exists(backup)) File.Move(backup, target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.Error($"更新还原失败：{target}", ex);
            }
        }
    }

    /// <summary>启动时调用：删除上次更新留下的 .old 文件、暂存目录与下载的压缩包。只删清单里记录的和本目录下的东西。</summary>
    public void CleanupPreviousUpdate()
    {
        string list = Path.Combine(WorkDirectory, CleanupList);
        try
        {
            if (File.Exists(list))
            {
                string appRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.AppDirectory)) + Path.DirectorySeparatorChar;
                var remaining = new List<string>();
                foreach (string path in File.ReadAllLines(list).Where(l => l.Length > 0).Distinct())
                {
                    string full = Path.GetFullPath(path);
                    if (!full.EndsWith(".old", StringComparison.OrdinalIgnoreCase) || !full.StartsWith(appRoot, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!TryDelete(full)) remaining.Add(full);
                }
                if (remaining.Count == 0) File.Delete(list);
                else File.WriteAllLines(list, remaining);
            }
            if (Directory.Exists(WorkDirectory))
            {
                foreach (string dir in Directory.EnumerateDirectories(WorkDirectory, "staging-*")) Directory.Delete(dir, recursive: true);
                foreach (string zip in Directory.EnumerateFiles(WorkDirectory, "WwTool-*.zip*")) TryDelete(zip);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Debug($"清理上次更新的文件失败：{ex.GetType().Name}");
        }
    }

    /// <summary>不属于发布包、永远不该被覆盖的用户数据。</summary>
    private static bool IsUserData(string relative)
    {
        string r = relative.Replace('\\', '/');
        return r.StartsWith("Config/", StringComparison.OrdinalIgnoreCase)
            || r.StartsWith("Local/Cache/", StringComparison.OrdinalIgnoreCase)
            || r.StartsWith("Local/Logs/", StringComparison.OrdinalIgnoreCase)
            || r.StartsWith("Local/Backups/", StringComparison.OrdinalIgnoreCase)
            || (r.StartsWith("Local/Data/", StringComparison.OrdinalIgnoreCase) && r.Contains(".db", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false)).ToLowerInvariant();
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
